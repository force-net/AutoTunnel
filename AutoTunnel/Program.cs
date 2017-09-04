using System;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;

using Force.AutoTunnel.Config;
using Force.AutoTunnel.Logging;
using Force.AutoTunnel.Service;

namespace Force.AutoTunnel
{
	public class Program
	{
		public static void Main(string[] args)
		{
			if (ProcessArgs(args)) return;

			if (!NativeHelper.IsNativeAvailable)
			{
				Console.Error.WriteLine("Cannot load WinDivert library");
				return;
			}

			if (!ConfigHelper.LoadConfig(true))
				return;

			if (Environment.UserInteractive)
			{
				RunInConsole();
			}
			else
			{
				LogHelper.Log.WriteLine("Starting service...");
				ServiceBase.Run(new MainService());
			}
		}

		private static bool ProcessArgs(string[] args)
		{
			if (args.Any(x => x == "service"))
			{
				bool isUninstall = args.Any(x => x == "uninstall" || x == "remove");
				MainServiceInstallerHelper.Process(!isUninstall, new string[0]);
				return true;
			}

			return false;
		}

		private static void RunInConsole()
		{
			Console.WriteLine("Press Ctrl+C for exit");

			var attr = typeof(Program).Assembly.GetCustomAttributes(typeof(AssemblyFileVersionAttribute), false).Cast<AssemblyFileVersionAttribute>().First();
			LogHelper.Log.WriteLine("AutoTunnel by Force. Version: " + attr.Version);
			AppDomain.CurrentDomain.DomainUnload += (sender, args) => ConsoleHelper.SetActiveIcon(ConsoleHelper.IconStatus.Default);
			Console.CancelKeyPress += (sender, args) => ConsoleHelper.SetActiveIcon(ConsoleHelper.IconStatus.Default);
			LogHelper.Log.WriteLine("Starting interactive...");
			Starter.Start();
			Thread.Sleep(-1);
		}
	}
}