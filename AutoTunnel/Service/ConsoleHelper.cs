using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Force.AutoTunnel.Service
{
	public static class ConsoleHelper
	{
		[DllImport("kernel32.dll")]
		private static extern IntPtr GetConsoleWindow();
		[DllImport("user32.dll")]
		private static extern IntPtr SendMessage(IntPtr hWnd, uint message, int lParam, IntPtr wParam);

		private static IntPtr _iconMainHandle = IntPtr.Zero;
		private static IntPtr _iconParentHandle = IntPtr.Zero;

		public static void RestoreOriginalIcon()
		{
			if (!Environment.UserInteractive)
				return;
			SendMessage(GetConsoleWindow(), 0x80, 1, _iconParentHandle);
			SendMessage(GetConsoleWindow(), 0x80, 0, _iconParentHandle);
		}

		public static void SetActiveIcon()
		{
			if (!Environment.UserInteractive)
				return;
			if (_iconMainHandle == IntPtr.Zero)
			{
// ReSharper disable AssignNullToNotNullAttribute
				var icon = new Bitmap(typeof(ConsoleHelper).Assembly.GetManifestResourceStream("Force.AutoTunnel.tunnel_active.png")).GetHicon();
// ReSharper restore AssignNullToNotNullAttribute

				_iconMainHandle = icon;

				SendMessage(GetConsoleWindow(), 0x80, 1, _iconMainHandle);
				_iconParentHandle = SendMessage(GetConsoleWindow(), 0x80, 0, _iconMainHandle);
			}

			SendMessage(GetConsoleWindow(), 0x80, 0, _iconMainHandle);
		}
	}
}
