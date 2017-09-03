using System;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading;

using Force.AutoTunnel.Config;
using Force.AutoTunnel.Logging;
using Force.AutoTunnel.Service;

using Newtonsoft.Json;

namespace Force.AutoTunnel
{
	public class Program
	{
		public static MainConfig Config { get; private set; }

		public static void Main(string[] args)
		{
			if (ProcessArgs(args)) return;

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
				if (config.RemoteClients == null)
					config.RemoteClients = new RemoteClientConfig[0];
				if (config.RemoteClients.Length == 0) 
					config.EnableListening = false;
				if (config.RemoteServers == null)
					config.RemoteServers = new RemoteServerConfig[0];
				foreach (var remoteServerConfig in config.RemoteServers)
				{
					if (string.IsNullOrEmpty(remoteServerConfig.ConnectHost) && string.IsNullOrEmpty(remoteServerConfig.TunnelHost))
						throw new InvalidOperationException("Missing host info in config");

					if (string.IsNullOrEmpty(remoteServerConfig.TunnelHost))
						remoteServerConfig.TunnelHost = remoteServerConfig.ConnectHost;
					if (string.IsNullOrEmpty(remoteServerConfig.ConnectHost))
						remoteServerConfig.ConnectHost = remoteServerConfig.TunnelHost;
				}

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

			Config = config;

			if (Environment.UserInteractive)
			{
				RunInConsole();
			}
			else
			{
				LogHelper.Log.WriteLine("Starting service...");
				ServiceBase.Run(new MainService());
			}
		}

		private static bool ProcessArgs(string[] args)
		{
			if (args.Any(x => x == "service"))
			{
				bool isUninstall = args.Any(x => x == "uninstall" || x == "remove");
				MainServiceInstallerHelper.Process(!isUninstall, new string[0]);
				return true;
			}

			return false;
		}

		private static void RunInConsole()
		{
			AppDomain.CurrentDomain.DomainUnload += (sender, args) => ConsoleHelper.SetActiveIcon(ConsoleHelper.IconStatus.Default);
			Console.CancelKeyPress += (sender, args) => ConsoleHelper.SetActiveIcon(ConsoleHelper.IconStatus.Default);
			Console.WriteLine("Press Ctrl+C for exit");
			LogHelper.Log.WriteLine("Starting interactive...");
			Starter.Start();
			Thread.Sleep(-1);
		}
	}
}