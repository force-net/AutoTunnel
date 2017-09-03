using Newtonsoft.Json;

namespace Force.AutoTunnel.Config
{
	public class RemoteClientConfig
	{
		public string Key { get; set; }

		[JsonIgnore]
		public byte[] BinaryKey { get; set; }

		public string Description { get; set; }
	}
}
