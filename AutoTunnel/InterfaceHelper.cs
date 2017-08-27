using System.Net.NetworkInformation;
using System.Reflection;

namespace Force.AutoTunnel
{
	public static class InterfaceHelper
	{
		public static uint GetInterfaceId()
		{
			NetworkInterface[] allNetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
			foreach (NetworkInterface interface2 in allNetworkInterfaces)
			{
				if ((((interface2.OperationalStatus == OperationalStatus.Up) && (interface2.Speed > 0L)) && (interface2.NetworkInterfaceType != NetworkInterfaceType.Loopback)) && (interface2.NetworkInterfaceType != NetworkInterfaceType.Tunnel))
				{
					var prop = interface2.GetType().GetField("index", BindingFlags.Instance | BindingFlags.NonPublic);
					return (uint)prop.GetValue(interface2);
					// Console.WriteLine(interface2.Id + " " + interface2.Name + " " + prop.GetValue(interface2));
				}
			}

			return 0;
		}
	}
}
