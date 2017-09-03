using System;
using System.Collections;
using System.Configuration.Install;

using Force.AutoTunnel.Logging;

namespace Force.AutoTunnel.Service
{
	public static class MainServiceInstallerHelper
	{
		public static void Process(bool install, string[] args)
		{
			var log = Logging.LogHelper.Log;

			try
			{
				log.WriteLine("Started service installation process");

				using (var installer = new AssemblyInstaller(typeof(Program).Assembly, args))
				{
					var state = new Hashtable();

					try
					{
						if (install)
						{
							log.WriteLine("Installing service");
							installer.Install(state);

							log.WriteLine("Commiting installation");
							installer.Commit(state);

							log.WriteLine("Disabling server header");
						}
						else
						{
							log.WriteLine("Uninstalling service");
							installer.Uninstall(state);
						}

						log.WriteLine("Installation process completed successfully");
					}
					catch (Exception ex)
					{
						log.WriteLine("Exception during installation process");
						log.WriteLine(ex);
						log.WriteLine("Rolling back installation process");

						try
						{
							installer.Rollback(state);
						}
						catch (Exception rex)
						{
							log.WriteLine("Exception in rollback");
							log.WriteLine(rex);
						}
					}
				}
			}
			catch (Exception ex)
			{
				log.WriteLine("Exception in installation process: ");
				log.WriteLine(ex);
			}
		}
	}
}
