namespace Force.AutoTunnel.Config
{
	public class RemoteServerConfig
	{
		public string Address { get; set; }

		public string Key { get; set; }

		public bool KeepAlive { get; set; }

		public bool ConnectOnStart { get; set; }
	}
}
