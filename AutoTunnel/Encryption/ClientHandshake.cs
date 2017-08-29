using System;
using System.Text;

namespace Force.AutoTunnel.Encryption
{
	public class ClientHandshake
	{
		private readonly string _publicKey;

		private readonly string _privateKey;

		public ClientHandshake()
		{
			var tuple = PasswordHelper.CreateRsa();
			_publicKey = tuple.Item1;
			_privateKey = tuple.Item2;
		}

		public byte[] SendingPacket { get; private set; }

		public int GetPacketForSending()
		{
			var toSend = Encoding.UTF8.GetBytes(_publicKey);
			SendingPacket = new byte[4096];
			var outBuf = SendingPacket;
			outBuf[0] = 0x1;
			outBuf[1] = 0x0;
			outBuf[2] = (byte)'A';
			outBuf[3] = (byte)'T';
			Buffer.BlockCopy(toSend, 0, outBuf, 4, toSend.Length);

			return toSend.Length + 4;
		}

		public byte[] GetPacketFromServer(byte[] data, int dataLen)
		{
			var tb = new byte[dataLen];
			Buffer.BlockCopy(data, 0, tb, 0, dataLen);
			return PasswordHelper.Decrypt(_privateKey, tb);
		}
	}
}
