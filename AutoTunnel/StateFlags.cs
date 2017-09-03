namespace Force.AutoTunnel
{
	public enum StateFlags : byte
	{
		Connecting = 1,

		ConnectAnswer = 2,

		ErrorFromServer = 3,

		Ping = 5,

		ProxyConnecting = 7,

		ErrorFromProxy = 8,

		Pong = 9
	}
}
