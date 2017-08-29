using System;
using System.Net;
using System.Net.Sockets;

using Force.AutoTunnel.Encryption;

namespace Force.AutoTunnel
{
	public class ReplySender : BaseSender
	{
		private readonly Socket _socket;

		private readonly EncryptHelper _encryptHelper;

		private readonly IPEndPoint _remoteEP;

		public ReplySender(IPAddress dstAddr, Socket socket, IPEndPoint remoteEP, TunnelStorage storage)
			: base(dstAddr, storage)
		{
			Console.WriteLine("Tunnel watcher was created for " + dstAddr);
			_socket = socket;
			_encryptHelper = new EncryptHelper(storage.GetSessionKey(remoteEP));
			_remoteEP = remoteEP;
		}

		protected override void Send(byte[] packet, int packetLen)
		{
			var len = _encryptHelper.Encrypt(packet, packetLen);
			_socket.SendTo(_encryptHelper.InnerBuf, len, SocketFlags.None, _remoteEP);
		}
	}
}
