using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Force.AutoTunnel.Logging
{
	public class FileLog : ILog
	{
		private readonly string _fileName;

		public FileLog(string fileName)
		{
			if (!Path.IsPathRooted(fileName)) 
				fileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
			_fileName = fileName;
		}

		public void WriteLine(string line)
		{
			File.AppendAllText(_fileName, DateTime.Now.ToString("[yyyy-MM-dd HH:mm:ss] ", CultureInfo.InvariantCulture) + line + Environment.NewLine);
		}
	}
}
