using System;

namespace Force.AutoTunnel
{
	public class PacketWriter : IDisposable
	{
		private IntPtr _handle;

		private WinDivert.WinDivertAddress _addr;

		public PacketWriter()
		{
			_handle = WinDivert.WinDivertOpen("false", WinDivert.LAYER_NETWORK, 0, 0);
			_addr = new WinDivert.WinDivertAddress();
			_addr.IfIdx = InterfaceHelper.GetInterfaceId();
		}

		public void Dispose()
		{
			if (_handle != IntPtr.Zero && _handle != (IntPtr)(-1))
				WinDivert.WinDivertClose(_handle);
			_handle = IntPtr.Zero;
		}

		public void Write(byte[] packet, int packetLen)
		{
			int writeLen = 0;
			WinDivert.WinDivertSend(_handle, packet, packetLen, ref _addr, ref writeLen);
		}

		~PacketWriter()
		{
			Dispose();
		}
	}
}
