using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Force.AutoTunnel.Logging;

using Newtonsoft.Json;

namespace Force.AutoTunnel.Config
{
	public static class ConfigHelper
	{
		public static MainConfig Config { get; private set; }

		private static FileSystemWatcher _fsw;

		public static bool LoadConfig(bool isFirstTime)
		{
			try
			{
				if (_fsw != null)
				{
					_fsw.Dispose();
					_fsw = null;
				}

				if (!isFirstTime)
					LogHelper.Log.WriteLine("Reloading config");

				var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
				if (!File.Exists(configPath))
				{
					if (isFirstTime)
						Console.Error.WriteLine("Missing config file");
					else
						LogHelper.Log.WriteLine("Missing config file");

					return false;
				}

				MainConfig config;
				using (var f = File.OpenRead(configPath))
					config = new JsonSerializer().Deserialize<MainConfig>(new JsonTextReader(new StreamReader(f)));

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

				if (!isFirstTime)
					Starter.Stop();

				var log = new AggregateLog();
				if (Environment.UserInteractive) log.AddLog(new ConsoleLog());
				if (!string.IsNullOrEmpty(config.LogFileName)) log.AddLog(new FileLog(config.LogFileName));
				LogHelper.SetLog(log);

				Config = config;

				if (!isFirstTime)
					Starter.Start();

				if (config.AutoReloadOnChange)
				{
					_fsw = new FileSystemWatcher(Path.GetDirectoryName(configPath) ?? string.Empty, Path.GetFileName(configPath) ?? string.Empty);
					_fsw.Changed += FswOnChanged;
					_fsw.Created += FswOnChanged;
					_fsw.Deleted += FswOnChanged;
					_fsw.EnableRaisingEvents = true;
				}
			}
			catch (Exception ex)
			{
				if (isFirstTime) Console.Error.WriteLine("Error in parsing config: " + ex.Message);
				else
				{
					LogHelper.Log.WriteLine("Error in parsing config. Leaving old config " + ex.Message);
				}

				return false;
			}

			return true;
		}

		private static DateTime _reloadTime;

		private static Task _activatedTask;

		private static void FswOnChanged(object sender, FileSystemEventArgs fileSystemEventArgs)
		{
			_reloadTime = DateTime.UtcNow.AddSeconds(4);
			if (_activatedTask != null)
				return;
			_activatedTask = Task.Factory.StartNew(
				() =>
					{
						while (true)
						{
							Thread.Sleep(TimeSpan.FromSeconds(1));
							if (DateTime.UtcNow > _reloadTime)
							{
								_activatedTask = null;
								LoadConfig(false);
								break;
							}
						}
					});
		}
	}
}
