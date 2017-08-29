using System.Net;
using System.Net.Sockets;

using Force.AutoTunnel.Encryption;

namespace Force.AutoTunnel
{
	public class ReplySender : BaseSender
	{
		private readonly Socket _socket;

		private readonly EncryptHelper _encryptHelper;

		public ReplySender(string dstAddr, Socket socket, IPEndPoint remoteEP, int sessionId, byte[] sessionKey)
			: base(dstAddr, remoteEP)
		{
			_socket = socket;
			_encryptHelper = new EncryptHelper(sessionKey);
			_encryptHelper.InnerBuf[0] = 0x0;
			_encryptHelper.InnerBuf[1] = (byte)(sessionId & 0xff);
			_encryptHelper.InnerBuf[2] = (byte)((sessionId >> 8) & 0xff);
			_encryptHelper.InnerBuf[3] = (byte)((sessionId >> 16) & 0xff);
			SessionId = sessionId;
		}

		protected override void Send(byte[] packet, int packetLen)
		{
			var len = _encryptHelper.Encrypt(packet, packetLen);
			_socket.SendTo(_encryptHelper.InnerBuf, len, SocketFlags.None, RemoteEP);
		}
	}
}
