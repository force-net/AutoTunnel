namespace Force.AutoTunnel.Config
{
	public class RemoteServerConfig
	{
		public string TunnelHost { get; set; }

		public string ConnectHost { get; set; }

		public string Key { get; set; }

		public bool KeepAlive { get; set; }

		public bool ConnectOnStart { get; set; }
	}
}
