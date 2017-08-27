using System.Net;
using System.Net.Sockets;

namespace Force.AutoTunnel
{
	public class ReplySender : BaseSender
	{
		private readonly Socket _socket;

		public ReplySender(string dstAddr, Socket socket, IPEndPoint remoteEP)
			: base(dstAddr, remoteEP)
		{
			_socket = socket;
		}

		protected override void Send(byte[] packet, int packetLen)
		{
			_socket.SendTo(packet, packetLen, SocketFlags.None, RemoteEP);
		}
	}
}
