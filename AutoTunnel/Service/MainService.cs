using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;

namespace Force.AutoTunnel.Service
{
	public class MainService : ServiceBase
	{
		protected override void OnStart(string[] args)
		{
			base.OnStart(args);
			Starter.Start();
		}

		protected override void OnStop()
		{
			Starter.Stop();
			base.OnStop();
		}
	}
}
