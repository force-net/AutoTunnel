using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;

using Force.AutoTunnel.Config;

using Newtonsoft.Json;

namespace Force.AutoTunnel
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
			if (!File.Exists(configPath))
			{
				Console.Error.WriteLine("Missing config file");
				return;
			}

			MainConfig config;

			try
			{
				config = new JsonSerializer().Deserialize<MainConfig>(new JsonTextReader(new StreamReader(File.OpenRead(configPath))));
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine("Error in parsing config: " + ex.Message);
				return;
			}

			var serverKeys = (config.Keys ?? new string[0]).Select(PasswordHelper.GenerateKey).ToArray();
			if (!NativeHelper.IsNativeAvailable)
			{
				Console.Error.WriteLine("Cannot load WinDivert library");
				return;
			}

			var ts = new TunnelStorage();

			if (config.EnableListening)
			{
				if (config.AddFirewallRule)
					FirewallHelper.AddOpenFirewallRule("12017");

				var l = new Listener(ts, serverKeys);
				l.Start();
			}

			foreach (var rs in config.RemoteServers ?? new RemoteServerConfig[0])
			{
				var endpoint = new IPEndPoint(IPAddress.Parse(rs.Address), 12017);
				ts.AddClientSender(new ClientSender(endpoint.Address, endpoint, PasswordHelper.GenerateKey(rs.Key), ts));
			}

			Thread.Sleep(-1);
		}
	}
}