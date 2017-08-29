using System;
using System.Net;
using System.Threading;

namespace Force.AutoTunnel
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var serverKey = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8 };
			if (!NativeHelper.IsNativeAvailable)
			{
				Console.Error.WriteLine("Cannot load WinDivert library");
				return;
			}

			var ts = new TunnelStorage();
			var l = new Listener(ts, serverKey);
			l.Start();

			var targetIp = args.Length > 0 ? args[0] : null;

			if (targetIp != null)
			{
				var endpoint = new IPEndPoint(IPAddress.Parse(targetIp), 12017);
				var sender = ts.GetOrAdd(targetIp, () => new ClientSender(targetIp, endpoint, serverKey));
			}

			Thread.Sleep(-1);
		}
	}
}