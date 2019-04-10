using System.Net;
using System.Net.Sockets;

using Force.AutoTunnel.Encryption;
using Force.AutoTunnel.Logging;

namespace Force.AutoTunnel
{
	public class ReplySender : BaseSender
	{
		private readonly Socket _socket;

		private readonly EncryptHelper _encryptHelper;

		public ReplySender(TunnelSession session, IPAddress watchAddr, Socket socket, TunnelStorage storage)
			: base(session, watchAddr, storage, session.ClampMss)
		{
			LogHelper.Log.WriteLine("Tunnel watcher was created for " + watchAddr);
			_socket = socket;
			_encryptHelper = new EncryptHelper(session.Key);
		}

		protected override void Send(byte[] packet, int packetLen)
		{
			Session.UpdateSendActivity();
			var len = _encryptHelper.Encrypt(packet, packetLen);
			_socket.SendTo(_encryptHelper.InnerBuf, len, SocketFlags.None, Session.RemoteEP);
		}
	}
}
