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

		private static IntPtr _iconActiveHandle = IntPtr.Zero;
		private static IntPtr _iconEstabilishingHandle = IntPtr.Zero;
		private static IntPtr _iconParentHandle = IntPtr.Zero;

		private static void RestoreOriginalIcon()
		{
			if (!Environment.UserInteractive)
				return;
			SendMessage(GetConsoleWindow(), 0x80, 1, _iconParentHandle);
			SendMessage(GetConsoleWindow(), 0x80, 0, _iconParentHandle);
		}

		public enum IconStatus
		{
			Active,
			Estabilishing,
			Default
		}

		public static void SetActiveIcon(IconStatus status)
		{
			if (!Environment.UserInteractive)
				return;
			if (_iconActiveHandle == IntPtr.Zero)
			{
// ReSharper disable AssignNullToNotNullAttribute
				_iconActiveHandle = new Bitmap(typeof(ConsoleHelper).Assembly.GetManifestResourceStream("Force.AutoTunnel.tunnel_active.png")).GetHicon();
				_iconEstabilishingHandle = new Bitmap(typeof(ConsoleHelper).Assembly.GetManifestResourceStream("Force.AutoTunnel.tunnel_estabilishing.png")).GetHicon();
// ReSharper restore AssignNullToNotNullAttribute

				_iconParentHandle = SendMessage(GetConsoleWindow(), 0x80, 0, _iconActiveHandle);
			}

			var icon = _iconParentHandle;
			if (status == IconStatus.Active) icon = _iconActiveHandle;
			else if (status == IconStatus.Estabilishing) icon = _iconEstabilishingHandle;
			SendMessage(GetConsoleWindow(), 0x80, 0, icon);
			SendMessage(GetConsoleWindow(), 0x80, 1, icon);
		}
	}
}
