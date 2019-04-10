using System;
using System.Security.Cryptography;

namespace Force.AutoTunnel.Encryption
{
	public class DecryptHelper : IDisposable
	{
		private readonly byte[] _innerBuf = new byte[65536];

		private readonly Aes _aes;

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
			Aes aes;
			aes = Aes.Create();
			aes.Key = key;
			aes.IV = new byte[16];
			aes.Mode = CipherMode.CBC;
			aes.Padding = PaddingMode.None;
			_aes = aes;
			// _decryptor = aes.CreateDecryptor();
		}

		public int Decrypt(byte[] data, int offset)
		{
			var hb = _headerBuf;
			// strange situation, decryptor cannot be used multiple times, problem with resetting cbc data, or my fault...
			// but encrypting is work with extracted encryptor
			using (var decryptor = _aes.CreateDecryptor())
			{
				decryptor.TransformBlock(data, offset, 16, hb, 0);
				var len = hb[0] | (hb[1] << 8) | (hb[2] << 16) | (hb[3] << 24);
				if (len > data.Length) return -1;
				if (hb[4] != 1 || hb[5] != 0 || hb[6] != 'A' || hb[7] != 'T') return -1;
				var len16 = (len + 15) & ~15;
				if (len < 0 || len > _innerBuf.Length) return -1;
				decryptor.TransformBlock(data, offset + 16, len16, _innerBuf, 0);
				// decryptor.TransformFinalBlock(new byte[0], 0, 0);
				return len;
			}
		}

		public void Dispose()
		{
			_aes.Dispose();
		}
	}
}
