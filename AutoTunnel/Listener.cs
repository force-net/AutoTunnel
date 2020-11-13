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
			LogHelper.Log.WriteLine("Started listening for incoming connections on "
				+ (string.IsNullOrEmpty(_config.ListenAddress) ? string.Empty : (_config.ListenAddress + ":"))
				+ _config.Port);
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

				_stopEvent.WaitOne(_config.IdleSessionTime * 1000);
			}
		}

		private Socket _socket;

		private void StartInternal()
		{
			Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
			_socket = s;
			try
			{
				var addr = IPAddress.Any;
				if (!string.IsNullOrEmpty(_config.ListenAddress))
				{
					if (!IPAddress.TryParse(_config.ListenAddress, out addr))
					{
						Console.Error.WriteLine("Invalid IP Address in config: " + _config.ListenAddress);
						Environment.Exit(1);
					}
				}

				s.Bind(new IPEndPoint(addr, _config.Port));
				byte[] inBuf = new byte[65536];
				EndPoint ep1 = new IPEndPoint(IPAddress.Any, 0);
				while (!_disposed)
				{
					try
					{
						int cnt = s.ReceiveFrom(inBuf, ref ep1);
						IPEndPoint ep = (IPEndPoint)ep1;
						TunnelSession session;

						if (cnt == 0) continue;
						byte[] decBuf = null;
						if (cnt % 16 != 0)
						{
							if (inBuf[0] == (byte)StateFlags.Connecting)
							{
								LogHelper.Log.WriteLine("Establishing connection from " + ep);
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
								var newSession = _storage.AddSession(serverHandshake.SessionKey, ep);
								if (selectedRemoteClient != null)
									newSession.ClampMss = selectedRemoteClient.ClampMss;
								
								var outEncryptesPacket = encryptHelper.Encrypt(outPacket, outPacket.Length);
								var initBuf = new byte[outEncryptesPacket.Count + 4];
								Buffer.BlockCopy(outEncryptesPacket.Array, 0, initBuf, 4, outEncryptesPacket.Count);
								initBuf[0] = (byte)StateFlags.ConnectAnswer;
								initBuf[1] = 1; // version

								s.SendTo(initBuf, initBuf.Length, SocketFlags.None, ep);
								var descr = selectedRemoteClient != null ? (!string.IsNullOrEmpty(selectedRemoteClient.Description) ? " as " + selectedRemoteClient.BinaryKey : string.Empty) : string.Empty;
								LogHelper.Log.WriteLine("Established connection from " + ep + descr);
								encryptHelper.Dispose();
								decryptHelper.Dispose();
								continue;
							}

							if (inBuf[0] == (byte)StateFlags.Ping) // ping
							{
								session = _storage.GetSession(ep);
								if (session != null) session.UpdateReceiveActivity();
								s.SendTo(new byte[] { (byte)StateFlags.Pong, 0, 0, 0 }, 4, SocketFlags.None, ep);
								continue;
							}
								// it is good idea, but attacker can cause close of our connection
								// so, think in future about this
							/*else if (inBuf[0] == (byte)StateFlags.ConnectionClosing) // ping
							{
								_storage.RemoveSession(ep);
								continue;
							}*/
							else
							{
								// error
								LogHelper.Log.WriteLine("Unsupported data from " + ep);
								s.SendTo(new byte[] { (byte)StateFlags.ErrorFromServer, 0, 0, 0 }, 4, SocketFlags.None, ep);
								continue;
							}
						}
						else
						{
							session = _storage.GetSession(ep);
							if (session == null)
							{
								s.SendTo(new byte[] { (byte)StateFlags.ErrorFromServer, 0, 0, 0 }, 4, SocketFlags.None, ep);
								LogHelper.Log.WriteLine("Missing decryptor for " + ep);
								continue;
							}

							var len = session.Decryptor.Decrypt(inBuf, 0);
							if (len < 0)
							{
								s.SendTo(new byte[] { (byte)StateFlags.ErrorFromServer, 0, 0, 0 }, 4, SocketFlags.None, ep);
								LogHelper.Log.WriteLine("Unable to decrypt data from " + ep);
								continue;
							}

							decBuf = session.Decryptor.InnerBuf;
							cnt = len;
							session.UpdateReceiveActivity();
						}

						// var sourceIp = decBuf[12] + "." + decBuf[13] + "." + decBuf[14] + "." + decBuf[15];
						var sourceIp = new IPAddress(decBuf[12] | (decBuf[13] << 8) | (decBuf[14] << 16) | (((uint)decBuf[15]) << 24));
						// if we already has option to establish connection to this ip, do not add additional sender
						if (!_storage.OutgoingConnectionAdresses.Contains(sourceIp))
						{
							var sender = _storage.GetOrAddSender(sourceIp, () => new ReplySender(session, sourceIp, s, _storage));
							sender.UpdateLastActivity();

							// session was changed for client, killing it and update data
							if (!sender.Session.RemoteEP.Equals(ep))
							{
								LogHelper.Log.WriteLine("Client for " + sourceIp + " has changed endpoint from " + sender.Session.RemoteEP + " to " + ep);
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
