using System.Security.Cryptography;
using System.Text;

namespace Force.AutoTunnel.Encryption
{
	public class ServerHandshake
	{
		public ServerHandshake()
		{
		}

		public byte[] SessionKey { get; private set; }

		public byte[] GetOutPacket(byte[] inPacket, int len)
		{
			if (inPacket[0] != 1 || inPacket[1] != 0 || inPacket[2] != 'A' || inPacket[3] != 'T') return null;
			var publicRsa = Encoding.UTF8.GetString(inPacket, 4, inPacket.Length - 4);
			SessionKey = new byte[16];
			using (var random = RandomNumberGenerator.Create())
				random.GetBytes(SessionKey);
			return PasswordHelper.Encrypt(publicRsa, SessionKey);
		}
	}
}
