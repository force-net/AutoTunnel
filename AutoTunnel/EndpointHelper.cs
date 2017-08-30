using System;
using System.Linq;
using System.Net;

namespace Force.AutoTunnel
{
	public static class EndpointHelper
	{
		public static IPEndPoint ParseEndPoint(string address, int defaultPort)
		{
			var sepIdx = address.IndexOf(':');
			string host = address;
			int port = defaultPort;
			if (sepIdx >= 0)
			{
				host = address.Substring(0, sepIdx);
				port = Convert.ToInt32(address.Remove(0, sepIdx + 1));
			}

			IPAddress ipAddress;
			if (!IPAddress.TryParse(host, out ipAddress)) 
				ipAddress = Dns.GetHostAddresses(host).First();

			return new IPEndPoint(ipAddress, port);
		}
	}
}
