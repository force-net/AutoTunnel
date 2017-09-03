using System.Collections.Generic;
using System.Globalization;

using Force.AutoTunnel.Config;

namespace Force.AutoTunnel
{
	public class Starter
	{
		private static TunnelStorage _storage;

		private static List<ClientSender> _clientSenders;

		private static Listener _listener;

		public static void Start()
		{
			_storage = new TunnelStorage();
			var config = Program.Config;

			if (config.EnableListening)
			{
				if (config.AddFirewallRule)
					FirewallHelper.AddOpenFirewallRule(config.Port.ToString(CultureInfo.InvariantCulture));

				_listener = new Listener(_storage, config);
				_listener.Start();
			}

			_clientSenders = new List<ClientSender>();
			foreach (var rs in config.RemoteServers ?? new RemoteServerConfig[0])
			{
				_clientSenders.Add(new ClientSender(rs, _storage));
			}
		}

		public static void Stop()
		{
			_clientSenders.ForEach(x => x.Dispose());
			_storage.RemoveAllSessions();
			if (_listener != null)
				_listener.Dispose();
			if (Program.Config.AddFirewallRule)
				FirewallHelper.DeleteFirewallRule(Program.Config.Port.ToString(CultureInfo.InvariantCulture));
		}
	}
}
