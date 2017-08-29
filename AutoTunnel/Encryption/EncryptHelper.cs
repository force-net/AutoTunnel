using System.Security.Cryptography;
using System.Threading;

namespace Force.AutoTunnel.Encryption
{
	public class EncryptHelper
	{
		private readonly byte[] _key;

		private readonly RandomNumberGenerator _random = RandomNumberGenerator.Create();

		private readonly byte[] _innerBuf = new byte[65536];

		private readonly byte[] _headerBuf = new byte[16];

		private int _counter;

		public byte[] InnerBuf
		{
			get
			{
				return _innerBuf;
			}
		}

		public EncryptHelper(byte[] key)
		{
			_key = key;
		}

		public int Encrypt(byte[] data, int len)
		{
			var aes = Aes.Create();
			aes.Key = _key;
			aes.IV = new byte[16];
			aes.Mode = CipherMode.CBC;
			aes.Padding = PaddingMode.None;
			var encryptor = aes.CreateEncryptor();
			_random.GetBytes(_headerBuf);
			_headerBuf[0] = (byte)(len & 0xff);
			_headerBuf[1] = (byte)((len >> 8) & 0xff);
			_headerBuf[2] = (byte)((len >> 16) & 0xff);
			_headerBuf[3] = (byte)((len >> 24) & 0xff);
			// var c = Interlocked.Increment(ref _counter);
			/*_headerBuf[4] = (byte)(c & 0xff);
			_headerBuf[5] = (byte)((c >> 8) & 0xff);
			_headerBuf[6] = (byte)((c >> 16) & 0xff);
			_headerBuf[7] = (byte)((c >> 24) & 0xff);*/
			_headerBuf[4] = 0x1;
			_headerBuf[5] = 0x0;
			_headerBuf[6] = (byte)'A';
			_headerBuf[7] = (byte)'T';

			encryptor.TransformBlock(_headerBuf, 0, 16, _innerBuf, 0 + 4);
			if (len == 0) return 16 + 4;
			return encryptor.TransformBlock(data, 0, (len + 15) & ~15, _innerBuf, 16 + 4) + 16 + 4;
		}
	}
}
