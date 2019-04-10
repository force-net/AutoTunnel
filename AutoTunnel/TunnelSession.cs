using System;
using System.Net;

using Force.AutoTunnel.Encryption;

namespace Force.AutoTunnel
{
	public class TunnelSession
	{
		public TunnelSession(IPEndPoint remoteEP)
		{
			RemoteEP = remoteEP;
			UpdateReceiveActivity();
			UpdateSendActivity();
		}

		public IPEndPoint RemoteEP { get; private set; }

		public byte[] Key { get; set; }

		public DecryptHelper Decryptor { get; set; }

		public DateTime LastReceiveActivity { get; private set; }

		public DateTime LastSendActivity { get; private set; }

		public bool IsClientSession { get; set; }

		public void UpdateReceiveActivity()
		{
			LastReceiveActivity = DateTime.UtcNow;
		}

		public void UpdateSendActivity()
		{
			LastSendActivity = DateTime.UtcNow;
		}

		public TimeSpan SendReceiveDifference
		{
			get
			{
				return LastSendActivity.Subtract(LastReceiveActivity);
			}
		}

		public int? ClampMss { get; set; }
		
	}
}