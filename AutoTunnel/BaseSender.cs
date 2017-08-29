using System;
using System.Net;
using System.Threading.Tasks;

namespace Force.AutoTunnel
{
	public abstract class BaseSender : IDisposable
	{
		private IntPtr _handle;

		private bool _isExiting;

		public readonly string DstAddr;

		public DateTime LastActivity { get; private set; }

		private readonly TunnelStorage _storage;

		protected BaseSender(string dstAddr, TunnelStorage storage)
		{
			_storage = storage;
			DstAddr = dstAddr;

			//  or (udp and udp.DstPort != 12017)
			_handle = WinDivert.WinDivertOpen("outbound and ip and (ip.DstAddr == " + DstAddr + ")", WinDivert.LAYER_NETWORK, 0, 0);
			Task.Factory.StartNew(StartInternal);
		}

		protected abstract void Send(byte[] packet, int packetLen);

		protected void UpdateLastActivity()
		{
			LastActivity = DateTime.UtcNow;
		}

		private void StartInternal()
		{
			byte[] packet = new byte[65536];
			WinDivert.WinDivertAddress addr = new WinDivert.WinDivertAddress();
			int packetLen = 0;
			while (!_isExiting && WinDivert.WinDivertRecv(_handle, packet, packet.Length, ref addr, ref packetLen))
			{
				// Console.WriteLine("Recv: " + packet[16] + "." + packet[17] + "." + packet[18] + "." + packet[19] + ":" + (packet[23] | ((uint)packet[22] << 8)));
				if (packet[9] == 17)
				{
					var key = ((ulong)(packet[16] | ((uint)packet[17] << 8) | ((uint)packet[18] << 16) | (((uint)packet[19]) << 24)) << 16) | (packet[23] | ((uint)packet[22] << 8));
					// do not catch this packet, it is our tunnel to other computer
					if (_storage.HasSession(key))
					{
						var writeLen = 0;
						WinDivert.WinDivertSend(_handle, packet, packetLen, ref addr, ref writeLen);
						continue;
					}
				}
				// Console.WriteLine("> " + packetLen + " " + addr.IfIdx + " " + addr.SubIfIdx + " " + addr.Direction);
				try
				{
					Send(packet, packetLen);
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.Message);
				}
			}
		}

		public virtual void Dispose()
		{
			_isExiting = true;
			if (_handle != IntPtr.Zero && _handle != (IntPtr)(-1))
				WinDivert.WinDivertClose(_handle);
			_handle = IntPtr.Zero;
		}

		~BaseSender()
		{
			Dispose();
		}
	}
}
