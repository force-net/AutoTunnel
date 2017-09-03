using System;
using System.Diagnostics;
using System.IO;

namespace Force.AutoTunnel
{
	public static class FirewallHelper
	{
		public static void AddOpenFirewallRule(string port)
		{
			if (Environment.OSVersion.Version.Major < 6)
				ProcessRunner.RunProcess("netsh", "firewall add portopening TCP " + port + " AutoTunnel ENABLE all");
			else
			{
				ProcessRunner.RunProcess("netsh", "advfirewall firewall delete rule name=\"AutoTunnel\" protocol=UDP dir=in localport=" + port);
				ProcessRunner.RunProcess("netsh", "advfirewall firewall add rule name=\"AutoTunnel\" protocol=UDP dir=in localport=" + port + " action=allow");
			}
		}

		public static void DeleteFirewallRule(string port)
		{
			if (Environment.OSVersion.Version.Major < 6)
			{
				ProcessRunner.RunProcess("netsh", "firewall delete portopening TCP " + port);
			}
			else
			{
				ProcessRunner.RunProcess("netsh", "advfirewall firewall delete rule name=\"AutoTunnel\" protocol=UDP dir=in");
			}
		}

		public static class ProcessRunner
		{
			public static string RunProcess(string fileName, string args)
			{
				using (var process =
						Process.Start(
							new ProcessStartInfo
							{
								FileName = fileName,
								Arguments = args,
								WorkingDirectory = Path.GetDirectoryName(fileName) ?? Environment.CurrentDirectory,
								UseShellExecute = false,
								RedirectStandardError = true,
								RedirectStandardInput = true, // don't remove or change these 2 lines, 
								RedirectStandardOutput = true,
								// "The handle is invalid" message will flood error output otherwise
								CreateNoWindow = true
							}))
				{
					string errors = process.StandardError.ReadToEnd();
					process.WaitForExit();
					return string.IsNullOrEmpty(errors) ? null : errors;
				}
			}
		}
	}
}
