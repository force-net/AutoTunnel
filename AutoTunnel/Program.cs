using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;

using Force.AutoTunnel.Config;
using Force.AutoTunnel.Logging;

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
				if (config.Keys == null)
					config.Keys = new string[0];
				if (config.Keys.Length == 0) 
					config.EnableListening = false;

				var log = new AggregateLog();
				if (Environment.UserInteractive) log.AddLog(new ConsoleLog());
				if (!string.IsNullOrEmpty(config.LogFileName)) log.AddLog(new FileLog(config.LogFileName));
				LogHelper.SetLog(log);
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine("Error in parsing config: " + ex.Message);
				return;
			}

			if (!NativeHelper.IsNativeAvailable)
			{
				Console.Error.WriteLine("Cannot load WinDivert library");
				return;
			}

			var ts = new TunnelStorage();

			if (config.EnableListening)
			{
				if (config.AddFirewallRule)
					FirewallHelper.AddOpenFirewallRule(config.Port.ToString(CultureInfo.InvariantCulture));

				var l = new Listener(ts, config);
				l.Start();
			}

			var clientSenders = new List<ClientSender>();
			foreach (var rs in config.RemoteServers ?? new RemoteServerConfig[0])
			{
				clientSenders.Add(new ClientSender(rs, ts));
			}

			Thread.Sleep(-1);
		}
	}
}