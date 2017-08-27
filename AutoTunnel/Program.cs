using System;
using System.Net;
using System.Threading;

namespace Force.AutoTunnel
{
	public class Program
	{
		public static void Main(string[] args)
		{
			if (!NativeHelper.IsNativeAvailable)
			{
				Console.Error.WriteLine("Cannot load WinDivert library");
				return;
			}

			var ts = new TunnelStorage();
			var l = new Listener(ts);
			l.Start();

			var targetIp = args.Length > 0 ? args[0] : null;

			if (targetIp != null)
			{
				var endpoint = new IPEndPoint(IPAddress.Parse(targetIp), 12017);
				var sender = ts.GetOrAdd(targetIp, () => new ClientSender(targetIp, endpoint));
			}

			Thread.Sleep(-1);
		}
	}
}