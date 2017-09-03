using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace Force.AutoTunnel.Service
{
	[RunInstaller(true)]
	public class MainServiceInstaller : Installer
	{
		public MainServiceInstaller()
		{
			Installers.Add(new ServiceProcessInstaller
			{
				Account = ServiceAccount.LocalSystem
			});

			Installers.Add(new ServiceInstaller
			{
				StartType = ServiceStartMode.Automatic,
				ServiceName = "AutoTunnel",
				DisplayName = "AutoTunnel",
			});
		}
	}
}
