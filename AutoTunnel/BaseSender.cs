using System;
using System.Net;
using System.Threading.Tasks;

namespace Force.AutoTunnel
{
	public abstract class BaseSender : IDisposable
	{
		private IntPtr _handle;

		private bool _isExiting;

		protected readonly string DstAddr;

		private WinDivert.WinDivertAddress _receiveAddr;

		public IPEndPoint RemoteEP { get; private set; }

		public DateTime LastActivity { get; private set; }

		public int SessionId { get; protected set; }

		public BaseSender(string dstAddr, IPEndPoint remoteEP)
		{
			Console.WriteLine("Sender was created for " + dstAddr);
			this.DstAddr = dstAddr;
			_receiveAddr = new WinDivert.WinDivertAddress();
			_receiveAddr.IfIdx = InterfaceHelper.GetInterfaceId();

			RemoteEP = remoteEP;
			var udpSuff = remoteEP.Address.Equals(IPAddress.Parse(this.DstAddr))
							? "(udp and udp.DstPort != " + remoteEP.Port + ")"
							: "udp";
			//  or (udp and udp.DstPort != 12017)
			_handle = WinDivert.WinDivertOpen("outbound and (tcp or icmp or " + udpSuff + ") and (ip.DstAddr == " + this.DstAddr + ")", WinDivert.LAYER_NETWORK, 0, 0);
			Task.Factory.StartNew(StartInternal);
		}

		protected abstract void Send(byte[] packet, int packetLen);

		protected void UpdateLastActivity()
		{
			LastActivity = DateTime.UtcNow;
		}

		public virtual void OnReceive(byte[] packet, int packetLen)
		{
			UpdateLastActivity();
			if (packetLen == 0)
				return;
			var writeLen = 0;
			// Console.WriteLine("< " + packetLen + " " + _receiveAddr.IfIdx + " " + _receiveAddr.SubIfIdx + " " + _receiveAddr.Direction);
			// Console.WriteLine(" " + packet[9] + " " + packet[12] + "." + packet[13] + "." + packet[14] + "." + packet[15] + "->" + packet[16] + "." + packet[17] + "." + packet[18] + "." + packet[19] + " " + packet[10].ToString("X2") + packet[11].ToString("X2"));
			// Console.WriteLine(BitConverter.ToString(inBuf, 0, cnt));
			var x = WinDivert.WinDivertSend(_handle, packet, packetLen, ref _receiveAddr, ref writeLen);
			//var s = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.IP);
			// s.SendTo(packet, packetLen, SocketFlags.None, new IPEndPoint(IPAddress.Parse("192.168.16.7"), 0));
			/*if (!x)
			{
				Console.WriteLine(Marshal.GetLastWin32Error());
			}*/
		}

		private void StartInternal()
		{
			byte[] packet = new byte[65536];
			WinDivert.WinDivertAddress addr = new WinDivert.WinDivertAddress();
			int packetLen = 0;
			while (!_isExiting && WinDivert.WinDivertRecv(_handle, packet, packet.Length, ref addr, ref packetLen))
			{
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
