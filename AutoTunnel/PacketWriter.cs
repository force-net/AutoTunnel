using System;
using System.Runtime.InteropServices;

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
			/*if (packet[9] == 6 && (packet[20 + 13] & 2) != 0 && packet[20 + 20] == 2 && packetLen > 20 + 24)
			{
				var len = packet[20 + 22] << 8 | packet[20 + 23];
				Console.WriteLine("X: " + (packet[20 + 22] << 8 | packet[20 + 23]));
				// UDP + encryption
				len -= 28 + 32;
				packet[20 + 22] = (byte)(len >> 8);
				packet[20 + 23] = (byte)(len & 0xFF);
				WinDivert.WinDivertHelperCalcChecksums(packet, packetLen, ref _addr, 0);
			}*/

			WinDivert.WinDivertSend(_handle, packet, packetLen, ref _addr, ref writeLen);
		}

		~PacketWriter()
		{
			Dispose();
		}
	}
}
