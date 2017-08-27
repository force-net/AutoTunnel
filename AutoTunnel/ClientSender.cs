using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Force.AutoTunnel
{
	public class ClientSender : BaseSender
	{
		private readonly Socket _socket;

		private bool _disposed;

		public ClientSender(string dstAddr, IPEndPoint remoteEP)
			: base(dstAddr, remoteEP)
		{
			_socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
			_socket.Connect(remoteEP);
			Task.Factory.StartNew(ReceiveCycle);
			// Task.Factory.StartNew(PingCycle);
		}

		private void PingCycle()
		{
			while (!_disposed)
			{
				Send(new byte[0], 0);
				Thread.Sleep(15 * 1000);
			}
		}

		protected override void Send(byte[] packet, int packetLen)
		{
			_socket.Send(packet, packetLen, SocketFlags.None);
		}

		private void ReceiveCycle()
		{
			byte[] buf = new byte[65536];
			while (!_disposed)
			{
				var len = _socket.Receive(buf);
				OnReceive(buf, len);
			}
		}


		public override void Dispose()
		{
			base.Dispose();
			_disposed = true;
			_socket.Dispose();
		}
	}
}
