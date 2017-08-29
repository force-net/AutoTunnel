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

		public Listener(TunnelStorage storage, byte[] serverKey)
		{
			_storage = storage;
			_serverKey = serverKey;
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
				EndPoint ep = new IPEndPoint(IPAddress.Any, 0);
				while (true)
				{
					try
					{
						int cnt = s.ReceiveFrom(inBuf, ref ep);
						int sessionId = 0;
						if (cnt == 0) continue;
						if (inBuf[0] == 1)
						{
							var decryptHelper = new DecryptHelper(_serverKey);
							var dataLen = decryptHelper.Decrypt(inBuf);
							var serverHandshake = new ServerHandshake();
							var outPacket = serverHandshake.GetOutPacket(decryptHelper.InnerBuf, dataLen);
							var encryptHelper = new EncryptHelper(_serverKey);
							var outLen = encryptHelper.Encrypt(outPacket, outPacket.Length);
							var toSend = encryptHelper.InnerBuf;
							sessionId = _storage.GetNewSessionId(serverHandshake.SessionKey);
							toSend[0] = 0x2;
							toSend[1] = (byte)sessionId;
							toSend[2] = (byte)(sessionId >> 8);
							toSend[3] = (byte)(sessionId >> 16);
							s.SendTo(toSend, outLen, SocketFlags.None, ep);
							continue;
						}
						else
						{
							sessionId = inBuf[1] | (inBuf[2] << 8) | (inBuf[3] << 16);
							var decryptor = _storage.GetSessionDecryptor(sessionId);
							if (decryptor == null)
							{
								s.SendTo(new byte[] { 0x3, inBuf[1], inBuf[2], inBuf[3] }, 4, SocketFlags.None, ep);
								continue;
							}

							var len = decryptor.Decrypt(inBuf);
							if (len < 0)
							{
								s.SendTo(new byte[] { 0x3, inBuf[1], inBuf[2], inBuf[3] }, 4, SocketFlags.None, ep);
								continue;
							}

							decBuf = decryptor.InnerBuf;
							cnt = len;
						}

						var sourceIp = decBuf[12] + "." + decBuf[13] + "." + decBuf[14] + "." + decBuf[15];

						// 			_sender = new BaseSender(DstAddr, this);
						// _sender.Start(remoteEp);

						var sender = _storage.GetOrAdd(sourceIp, () => new ReplySender(sourceIp, s, (IPEndPoint)ep, sessionId, _storage.GetSessionKey(sessionId)));
						// ip was changed, sending error and waiting for reconnect
						if (!sender.RemoteEP.Equals(ep))
						{
							sender.Dispose();
							_storage.Remove(sourceIp);
							s.SendTo(new byte[] { 0x3, inBuf[1], inBuf[2], inBuf[3] }, 4, SocketFlags.None, ep);
							continue;
						}

						sender.OnReceive(decBuf, cnt);
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
