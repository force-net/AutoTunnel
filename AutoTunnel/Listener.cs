using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using Force.AutoTunnel.Encryption;

namespace Force.AutoTunnel
{
	public class Listener
	{
		private readonly TunnelStorage _storage;

		private readonly byte[][] _serverKeys;

		private readonly PacketWriter _packetWriter;

		public Listener(TunnelStorage storage, byte[][] serverKeys)
		{
			_storage = storage;
			_serverKeys = serverKeys;
			_packetWriter = new PacketWriter();
		}

		public void Start()
		{
			Console.WriteLine("Started listening for incoming connections");
			Task.Factory.StartNew(StartInternal);
			Task.Factory.StartNew(CleanupThread);
		}

		private void CleanupThread()
		{
			while (true)
			{
				var oldSenders = _storage.GetOldSenders(TimeSpan.FromMinutes(10));
				foreach (var os in oldSenders)
				{
					Console.WriteLine("Removing idle session " + os.Address + ":" + os.Port);
					_storage.Remove(os);
				}

				Thread.Sleep(10 * 60 * 1000);
			}
		}

		private void StartInternal()
		{
			Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
			try
			{
				s.Bind(new IPEndPoint(IPAddress.Any, 12017));
				byte[] inBuf = new byte[65536];
				EndPoint ep1 = new IPEndPoint(IPAddress.Any, 0);
				while (true)
				{
					try
					{
						int cnt = s.ReceiveFrom(inBuf, ref ep1);
						IPEndPoint ep = (IPEndPoint)ep1;

						if (cnt == 0) continue;
						byte[] decBuf = null;
						if (cnt % 16 != 0)
						{
							if (inBuf[0] == 1)
							{
								Console.WriteLine("Estabilishing connection from " + ep.Address + ":" + ep.Port);
								int dataLen = -1;
								DecryptHelper decryptHelper = null;
								EncryptHelper encryptHelper = null;
								foreach (var serverKey in _serverKeys)
								{
									decryptHelper = new DecryptHelper(serverKey);
									dataLen = decryptHelper.Decrypt(inBuf, 4);
									if (dataLen > 0)
									{
										encryptHelper = new EncryptHelper(serverKey);
										break;
									}
								}

								// data is invalid, do not reply
								if (dataLen < 0)
								{
									Console.WriteLine("Invalid data from " + ep.Address + ":" + ep.Port);
									continue;
								}
								
								var serverHandshake = new ServerHandshake();
								var outPacket = serverHandshake.GetOutPacket(decryptHelper.InnerBuf, dataLen);
								_storage.SetNewEndPoint(serverHandshake.SessionKey, ep);
								
								var outLen = encryptHelper.Encrypt(outPacket, outPacket.Length);
								var initBuf = new byte[outLen + 4];
								Buffer.BlockCopy(encryptHelper.InnerBuf, 0, initBuf, 4, outLen);
								initBuf[0] = 2;
								initBuf[1] = 1; // version

								s.SendTo(initBuf, initBuf.Length, SocketFlags.None, ep);
								Console.WriteLine("Estabilished connection from " + ep.Address + ":" + ep.Port);
								continue;
							}

							if (inBuf[0] == 0x5) // ping
							{
								continue;
							}
							else
							{
								// error
								Console.WriteLine("Unsupported data from " + ep.Address + ":" + ep.Port);
								s.SendTo(new byte[] { 0x3, 0, 0, 0 }, 4, SocketFlags.None, ep);
								continue;
							}
						}
						else
						{
							var decryptor = _storage.GetSessionDecryptor(ep);
							if (decryptor == null)
							{
								s.SendTo(new byte[] { 0x3, 0, 0, 0 }, 4, SocketFlags.None, ep);
								Console.WriteLine("Missing decryptor for " + ep.Address + ":" + ep.Port);
								continue;
							}

							var len = decryptor.Decrypt(inBuf, 0);
							if (len < 0)
							{
								s.SendTo(new byte[] { 0x3, 0, 0, 0 }, 4, SocketFlags.None, ep);
								Console.WriteLine("Unable to decrypt data from " + ep.Address + ":" + ep.Port);
								continue;
							}

							decBuf = decryptor.InnerBuf;
							cnt = len;
						}

						// var sourceIp = decBuf[12] + "." + decBuf[13] + "." + decBuf[14] + "." + decBuf[15];
						var sourceIp = new IPAddress(decBuf[12] | (decBuf[13] << 8) | (decBuf[14] << 16) | (decBuf[15] << 24));
						// if we already has option to estabilish connection to this ip, do not add additional sender
						if (!_storage.OutgoingConnectionAdresses.Contains(sourceIp))
						{
							var sender = _storage.GetOrAddSender(ep, () => new ReplySender(sourceIp, s, ep, _storage));
							sender.UpdateLastActivity();

							// ip was changed for client
							if (!sender.DstAddr.Equals(sourceIp))
							{
								Console.WriteLine("Remote endpoint " + ep.Address + ":" + ep.Port + " has changed ip: " + sender.DstAddr + "->" + sourceIp);
								s.SendTo(new byte[] { 0x3, 0, 0, 0 }, 4, SocketFlags.None, ep);
								sender.Dispose();
								_storage.Remove(ep);
								// _storage.GetOrAddSender(ep, () => new ReplySender(sourceIp, s, ep, _storage));
							}
						}

						_packetWriter.Write(decBuf, cnt);
					}
					catch (Exception ex)
					{
						Console.WriteLine(ex.Message);
						Console.WriteLine(ex.StackTrace);
						break;
					}
				}
			}
			catch (SocketException)
			{
				return;
			}
		}
	}
}
