using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Force.AutoTunnel.Logging
{
	public class AggregateLog : ILog
	{
		private List<ILog> _logs = new List<ILog>();

		public AggregateLog AddLog(ILog log)
		{
			_logs = _logs.Concat(new[] { log }).ToList();
			return this;
		}

		public void ClearLogs()
		{
			_logs = new List<ILog>();
		}

		public void WriteLine(string line)
		{
			_logs.ForEach(x => x.WriteLine(line));
		}
	}
}
