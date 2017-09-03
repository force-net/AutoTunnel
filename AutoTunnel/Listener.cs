using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using Force.AutoTunnel.Config;
using Force.AutoTunnel.Encryption;
using Force.AutoTunnel.Logging;

namespace Force.AutoTunnel
{
	public class Listener : IDisposable
	{
		private readonly TunnelStorage _storage;

		private readonly MainConfig _config;

		private readonly PacketWriter _packetWriter;

		private bool _disposed;

		private readonly ManualResetEvent _stopEvent = new ManualResetEvent(false);

		public Listener(TunnelStorage storage, MainConfig config)
		{
			_storage = storage;
			_config = config;

			_packetWriter = new PacketWriter();
		}

		public void Start()
		{
			LogHelper.Log.WriteLine("Started listening for incoming connections on " + _config.Port);
			Task.Factory.StartNew(StartInternal);
			Task.Factory.StartNew(CleanupThread);
		}

		private void CleanupThread()
		{
			while (!_disposed)
			{
				var oldSessions = _storage.GetOldSessions(TimeSpan.FromSeconds(_config.IdleSessionTime));
				foreach (var os in oldSessions)
				{
					LogHelper.Log.WriteLine("Removing idle session: " + os);
					_storage.RemoveSession(os);
				}

				_stopEvent.WaitOne(10 * 60 * 1000);
			}
		}

		private Socket _socket;

		private void StartInternal()
		{
			Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
			_socket = s;
			try
			{
				s.Bind(new IPEndPoint(IPAddress.Any, _config.Port));
				byte[] inBuf = new byte[65536];
				EndPoint ep1 = new IPEndPoint(IPAddress.Any, 0);
				while (!_disposed)
				{
					try
					{
						int cnt = s.ReceiveFrom(inBuf, ref ep1);
						IPEndPoint ep = (IPEndPoint)ep1;
						TunnelStorage.Session session;

						if (cnt == 0) continue;
						byte[] decBuf = null;
						if (cnt % 16 != 0)
						{
							if (inBuf[0] == 1)
							{
								LogHelper.Log.WriteLine("Estabilishing connection from " + ep);
								int dataLen = -1;
								DecryptHelper decryptHelper = null;
								EncryptHelper encryptHelper = null;
								RemoteClientConfig selectedRemoteClient = null;
								foreach (var remoteClient in _config.RemoteClients)
								{
									if (remoteClient.BinaryKey == null) 
										remoteClient.BinaryKey = PasswordHelper.GenerateKey(remoteClient.Key);
									decryptHelper = new DecryptHelper(remoteClient.BinaryKey);
									dataLen = decryptHelper.Decrypt(inBuf, 4);
									if (dataLen > 0)
									{
										encryptHelper = new EncryptHelper(remoteClient.BinaryKey);
										selectedRemoteClient = remoteClient;
										break;
									}
								}

								// data is invalid, do not reply
								if (dataLen < 0)
								{
									LogHelper.Log.WriteLine("Invalid data from " + ep);
									continue;
								}
								
								var serverHandshake = new ServerHandshake();
								var outPacket = serverHandshake.GetOutPacket(decryptHelper.InnerBuf, dataLen);
								_storage.AddSession(serverHandshake.SessionKey, ep);
								
								var outLen = encryptHelper.Encrypt(outPacket, outPacket.Length);
								var initBuf = new byte[outLen + 4];
								Buffer.BlockCopy(encryptHelper.InnerBuf, 0, initBuf, 4, outLen);
								initBuf[0] = 2;
								initBuf[1] = 1; // version

								s.SendTo(initBuf, initBuf.Length, SocketFlags.None, ep);
								var descr = selectedRemoteClient != null ? (!string.IsNullOrEmpty(selectedRemoteClient.Description) ? " as " + selectedRemoteClient.BinaryKey : string.Empty) : string.Empty;
								LogHelper.Log.WriteLine("Estabilished connection from " + ep + descr);
								continue;
							}

							if (inBuf[0] == 0x5) // ping
							{
								session = _storage.GetSession(ep);
								if (session != null) session.UpdateLastActivity();
								continue;
							}
							else
							{
								// error
								LogHelper.Log.WriteLine("Unsupported data from " + ep);
								s.SendTo(new byte[] { 0x3, 0, 0, 0 }, 4, SocketFlags.None, ep);
								continue;
							}
						}
						else
						{
							session = _storage.GetSession(ep);
							if (session == null)
							{
								s.SendTo(new byte[] { 0x3, 0, 0, 0 }, 4, SocketFlags.None, ep);
								LogHelper.Log.WriteLine("Missing decryptor for " + ep);
								continue;
							}

							var len = session.Decryptor.Decrypt(inBuf, 0);
							if (len < 0)
							{
								s.SendTo(new byte[] { 0x3, 0, 0, 0 }, 4, SocketFlags.None, ep);
								LogHelper.Log.WriteLine("Unable to decrypt data from " + ep);
								continue;
							}

							decBuf = session.Decryptor.InnerBuf;
							cnt = len;
							session.UpdateLastActivity();
						}

						// var sourceIp = decBuf[12] + "." + decBuf[13] + "." + decBuf[14] + "." + decBuf[15];
						var sourceIp = new IPAddress(decBuf[12] | (decBuf[13] << 8) | (decBuf[14] << 16) | (decBuf[15] << 24));
						// if we already has option to estabilish connection to this ip, do not add additional sender
						if (!_storage.OutgoingConnectionAdresses.Contains(sourceIp))
						{
							var sender = _storage.GetOrAddSender(sourceIp, () => new ReplySender(session, sourceIp, s, _storage));
							sender.UpdateLastActivity();

							// session was changed for client, killing it and update data
							if (!sender.Session.RemoteEP.Equals(ep))
							{
								Console.WriteLine("Client for " + sourceIp + " has changed endpoint from " + sender.Session.RemoteEP + " to " + ep);
								s.SendTo(new byte[] { 0x3, 0, 0, 0 }, 4, SocketFlags.None, sender.Session.RemoteEP);
								_storage.RemoveSession(sender.Session.RemoteEP);
								sender.Session = session;
							}
						}

						_packetWriter.Write(decBuf, cnt);
					}
					catch (Exception ex)
					{
						if (!_disposed)
							LogHelper.Log.WriteLine(ex);
						break;
					}
				}
			}
			catch (SocketException)
			{
				return;
			}
		}

		public void Dispose()
		{
			_disposed = true;
			_stopEvent.Set();
			_packetWriter.Dispose();
			if (_socket != null)
			{
				_socket.Dispose();
				_socket = null;
			}
		}
	}
}
