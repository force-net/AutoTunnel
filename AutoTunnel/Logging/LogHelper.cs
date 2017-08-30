using System;

namespace Force.AutoTunnel.Logging
{
	public static class LogHelper
	{
		private static ILog _log = new ConsoleLog();

		public static void SetLog(ILog log)
		{
			_log = log;
		}

		public static ILog Log
		{
			get
			{
				return _log;
			}
		}

		public static void WriteLine(this ILog log, Exception ex)
		{
			log.WriteLine(ex.ToString());
		}
	}
}
