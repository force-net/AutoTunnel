using System.Security.Cryptography;

namespace Force.AutoTunnel.Encryption
{
	public class DecryptHelper
	{
		private readonly byte[] _innerBuf = new byte[65536];

		private readonly byte[] _key;

		private readonly byte[] _headerBuf = new byte[16];

		public byte[] InnerBuf
		{
			get
			{
				return _innerBuf;
			}
		}

		public DecryptHelper(byte[] key)
		{
			_key = key;
		}

		public int Decrypt(byte[] data, int offset)
		{
			var aes = Aes.Create();
			aes.Key = _key;
			aes.IV = new byte[16];
			aes.Mode = CipherMode.CBC;
			aes.Padding = PaddingMode.None;
			var decryptor = aes.CreateDecryptor();
			decryptor.TransformBlock(data, offset, 16, _headerBuf, 0);
			var len = _headerBuf[0] | (_headerBuf[1] << 8) | (_headerBuf[2] << 16) | (_headerBuf[3] << 24);
			if (len > data.Length) return -1;
			if (_headerBuf[4] != 1 || _headerBuf[5] != 0 || _headerBuf[6] != 'A' || _headerBuf[7] != 'T') return -1;
			var len16 = (len + 15) & ~15;
			if (len < 0 || len > _innerBuf.Length) return -1;
			decryptor.TransformBlock(data, offset + 16, len16, _innerBuf, 0);
			return len;
		}
	}
}
