using System;
using System.Collections.Concurrent;
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

		private readonly byte[] _serverKey;

		private readonly PacketWriter _packetWriter;

		public Listener(TunnelStorage storage, byte[] serverKey)
		{
			_storage = storage;
			_serverKey = serverKey;
			_packetWriter = new PacketWriter();
		}

		public void Start()
		{
			Task.Factory.StartNew(StartInternal);
			Task.Factory.StartNew(CleanupThread);
		}

		private void CleanupThread()
		{
			while (true)
			{
				_storage.RemoveOldSenders(TimeSpan.FromMinutes(10));
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
				byte[] decBuf = null;
				EndPoint ep1 = new IPEndPoint(IPAddress.Any, 0);
				while (true)
				{
					try
					{
						int cnt = s.ReceiveFrom(inBuf, ref ep1);
						IPEndPoint ep = (IPEndPoint)ep1;

						if (cnt == 0) continue;
						if (cnt % 16 != 0)
						{
							if (inBuf[0] == 1)
							{
								Console.WriteLine("Estabilishing connection from " + ep.Address + ":" + ep.Port);
								var decryptHelper = new DecryptHelper(_serverKey);
								var dataLen = decryptHelper.Decrypt(inBuf, 4);
								var serverHandshake = new ServerHandshake();
								var outPacket = serverHandshake.GetOutPacket(decryptHelper.InnerBuf, dataLen);
								_storage.SetNewEndPoint(serverHandshake.SessionKey, ep);
								var encryptHelper = new EncryptHelper(_serverKey);
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

						var sourceIp = decBuf[12] + "." + decBuf[13] + "." + decBuf[14] + "." + decBuf[15];
						// if we already has option to estabilish connection to this ip, do not add additional sender
						if (!_storage.OutgoingConnectionAdresses.Contains(sourceIp))
						{
							var sender = _storage.GetOrAddSender(ep, () => new ReplySender(sourceIp, s, ep, _storage));

							// ip was changed for client
							if (sender.DstAddr != sourceIp)
							{
								Console.WriteLine("Remote endpoint " + ep.Address + ":" + ep.Port + " has changed ip: " + sender.DstAddr + "->" + sourceIp);
								sender.Dispose();
								_storage.Remove(ep);
								_storage.GetOrAddSender(ep, () => new ReplySender(sourceIp, s, ep, _storage));
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
