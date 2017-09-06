using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Force.AutoTunnel.Logging;

namespace Force.AutoTunnel
{
	public abstract class BaseSender : IDisposable
	{
		private IntPtr _handle;

		private bool _isExiting;

		protected IPAddress DstAddr { get; set; }

		public DateTime LastActivity { get; private set; }

		protected readonly TunnelStorage Storage;

		public TunnelSession Session { get; set; }

		protected BaseSender(TunnelSession session, IPAddress watchAddr, TunnelStorage storage)
		{
			Storage = storage;
			Session = session;

			ReInitDivert(watchAddr);
			Task.Factory.StartNew(StartInternal);
		}

		protected void ReInitDivert(IPAddress newDstAddr)
		{
			DstAddr = newDstAddr;
			if (_handle != IntPtr.Zero && _handle != (IntPtr)(-1))
				WinDivert.WinDivertClose(_handle);

			//  or (udp and udp.DstPort != 12017)
			_handle = WinDivert.WinDivertOpen("outbound and ip and (ip.DstAddr == " + newDstAddr + ")", WinDivert.LAYER_NETWORK, 0, 0);
		}

		protected abstract void Send(byte[] packet, int packetLen);

		public void UpdateLastActivity()
		{
			LastActivity = DateTime.UtcNow;
		}

		private void StartInternal()
		{
			byte[] packet = new byte[65536];
			WinDivert.WinDivertAddress addr = new WinDivert.WinDivertAddress();
			int packetLen = 0;
			while (!_isExiting)
			{
				var oldHandle = _handle;
				if (!WinDivert.WinDivertRecv(_handle, packet, packet.Length, ref addr, ref packetLen))
				{
					// showing error only if handle is not removed and not changed
					if (_handle != IntPtr.Zero && oldHandle == _handle)
					{
						LogHelper.Log.WriteLine("Cannot receive network data: " + Marshal.GetLastWin32Error());
						Thread.Sleep(1000);
					}

					continue;
				}
				// we cannot handle such packets,
				// todo: think about writing to log
				if (packetLen >= ((65507 / 16) * 16) - 16)
				{
					continue;
				}
				// Console.WriteLine("Recv: " + packet[16] + "." + packet[17] + "." + packet[18] + "." + packet[19] + ":" + (packet[23] | ((uint)packet[22] << 8)));
				if (packet[9] == 17)
				{
					var key = ((ulong)(packet[16] | ((uint)packet[17] << 8) | ((uint)packet[18] << 16) | (((uint)packet[19]) << 24)) << 16) | (packet[23] | ((uint)packet[22] << 8));
					// do not catch this packet, it is our tunnel to other computer
					if (Storage.HasSession(key))
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
					LogHelper.Log.WriteLine(ex);
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
