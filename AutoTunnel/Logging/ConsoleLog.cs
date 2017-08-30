using System;
using System.Globalization;

namespace Force.AutoTunnel.Logging
{
	public class ConsoleLog : ILog
	{
		public void WriteLine(string line)
		{
			Console.WriteLine(DateTime.Now.ToString("[yyyy-MM-dd HH:mm:ss] ", CultureInfo.InvariantCulture) + line);
		}
	}
}
