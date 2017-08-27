using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Force.AutoTunnel
{
	public class Listener
	{
		private readonly TunnelStorage _storage;

		public Listener(TunnelStorage storage)
		{
			_storage = storage;
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
				EndPoint ep = new IPEndPoint(IPAddress.Any, 0);
				while (true)
				{
					try
					{
						int cnt = s.ReceiveFrom(inBuf, ref ep);
						if (cnt == 0) continue;
						var sourceIp = inBuf[12] + "." + inBuf[13] + "." + inBuf[14] + "." + inBuf[15];

						// 			_sender = new BaseSender(dstAddr, this);
						// _sender.Start(remoteEp);

						Func<BaseSender> creatorFunc = () => new ReplySender(sourceIp, s, (IPEndPoint)ep);
						var sender = _storage.GetOrAdd(sourceIp, creatorFunc);
						if (!sender.RemoteEP.Equals(ep))
						{
							sender.Dispose();
							_storage.Remove(sourceIp);
							sender = _storage.GetOrAdd(sourceIp, creatorFunc);
						}

						sender.OnReceive(inBuf, cnt);
					}
					catch (Exception ex)
					{
						Console.WriteLine(ex.Message);
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
