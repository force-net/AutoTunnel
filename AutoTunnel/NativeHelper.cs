using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Force.AutoTunnel
{
	public static class NativeHelper
	{
		private static readonly bool _isNativePossible;

		[DllImport("Kernel32.dll")]
		private static extern IntPtr LoadLibrary(string path);

		public static bool IsNativeAvailable { get; private set; }

		static NativeHelper()
		{
			_isNativePossible = Init();
			IsNativeAvailable = _isNativePossible;
		}

		private static bool Init()
		{
			try
			{
				InitInternal();
				return true;
			}
			catch (Exception) // will use software realization
			{
				return false;
			}
		}

		private static void InitInternal()
		{
			var architectureSuffix = IntPtr.Size == 8 ? "x64" : "x86";
			var libraryName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, architectureSuffix);
			libraryName = Path.Combine(libraryName, "WinDivert.dll");

			if (LoadLibrary(libraryName) == IntPtr.Zero)
				throw new InvalidOperationException("Unexpected error in dll loading");
		}
	}
}
