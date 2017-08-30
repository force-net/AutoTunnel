using System.ComponentModel;

using Newtonsoft.Json;

namespace Force.AutoTunnel.Config
{
	public class MainConfig
	{
		[DefaultValue(true)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool EnableListening { get; set; }

		[DefaultValue(true)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool AddFirewallRule { get; set; }

		public string[] Keys { get; set; }

		[DefaultValue(12017)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int Port { get; set; }

		public RemoteServerConfig[] RemoteServers { get; set; }

		[DefaultValue(10 * 60)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int IdleSessionTime { get; set; }

		public string LogFileName { get; set; }
	}
}
