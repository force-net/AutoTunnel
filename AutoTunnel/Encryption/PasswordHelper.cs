using System;
using System.Linq;
using System.Security.Cryptography;

namespace Force.AutoTunnel
{
	public class PasswordHelper
	{
		public static byte[] GenerateKey(string password)
		{
			// we encrypt all data with random value, so, we can use static password here
			return new Rfc2898DeriveBytes(string.IsNullOrEmpty(password) ? "no_password" : password, "AutoTunnel".Select(x => (byte)x).ToArray(), 4096).GetBytes(16);
		}

		public static Tuple<string, string> CreateRsa()
		{
			var rsa = RSA.Create();
			rsa.KeySize = 2048;
			return new Tuple<string, string>(rsa.ToXmlString(false), rsa.ToXmlString(true));
		}

		public static byte[] Encrypt(string publicRsaKey, byte[] data)
		{
			var rsa = new RSACryptoServiceProvider();
			rsa.FromXmlString(publicRsaKey);
			return rsa.Encrypt(data, false);
		}

		public static byte[] Decrypt(string privateRsaKey, byte[] data)
		{
			var rsa = new RSACryptoServiceProvider();
			rsa.FromXmlString(privateRsaKey);
			return rsa.Decrypt(data, false);
		}
	}
}
