using System.Security.Cryptography;

namespace Force.AutoTunnel.Encryption
{
	public class EncryptHelper
	{
		private readonly RandomNumberGenerator _random = RandomNumberGenerator.Create();

		private readonly byte[] _innerBuf = new byte[65536];

		private readonly byte[] _headerBuf = new byte[16];

		private readonly ICryptoTransform _encryptor;

		public byte[] InnerBuf
		{
			get
			{
				return _innerBuf;
			}
		}

		public EncryptHelper(byte[] key)
		{
			var aes = Aes.Create();
			aes.Key = key;
			aes.IV = new byte[16];
			aes.Mode = CipherMode.CBC;
			aes.Padding = PaddingMode.None;
			_encryptor = aes.CreateEncryptor();
		}

		public int Encrypt(byte[] data, int len)
		{
			var hb = _headerBuf;
			// _random.GetBytes(hb);
			hb[0] = (byte)(len & 0xff);
			hb[1] = (byte)((len >> 8) & 0xff);
			hb[2] = (byte)((len >> 16) & 0xff);
			hb[3] = (byte)((len >> 24) & 0xff);
			// var c = Interlocked.Increment(ref _counter);
			/*_headerBuf[4] = (byte)(c & 0xff);
			_headerBuf[5] = (byte)((c >> 8) & 0xff);
			_headerBuf[6] = (byte)((c >> 16) & 0xff);
			_headerBuf[7] = (byte)((c >> 24) & 0xff);*/
			hb[4] = 0x1;
			hb[5] = 0x0;
			hb[6] = (byte)'A';
			hb[7] = (byte)'T';

			_encryptor.TransformBlock(hb, 0, 16, _innerBuf, 0);
			var tl = 0;
			if (len > 0) 
				tl = _encryptor.TransformBlock(data, 0, (len + 15) & ~15, _innerBuf, 16);

			_encryptor.TransformFinalBlock(new byte[0], 0, 0);
			return tl + 16;
		}
	}
}
