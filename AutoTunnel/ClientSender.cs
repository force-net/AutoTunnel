using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Force.AutoTunnel.Config;
using Force.AutoTunnel.Encryption;
using Force.AutoTunnel.Logging;

namespace Force.AutoTunnel
{
	public class ClientSender : BaseSender
	{
		private Socket _socket;

		private bool _disposed;

		private EncryptHelper _encryptHelper;

		private readonly byte[] _serverKey;

		private readonly ManualResetEvent _initingEvent = new ManualResetEvent(false);

		private readonly PacketWriter _packetWriter;

		private bool _isInited;

		private readonly RemoteServerConfig _config;

		private IPEndPoint _connectEP;

		public ClientSender(RemoteServerConfig config, TunnelStorage storage)
			: base(null, EndpointHelper.ParseEndPoint(config.TunnelHost, 1).Address, storage)
		{
			storage.OutgoingConnectionAdresses.Add(DstAddr);
			_config = config;
			_serverKey = PasswordHelper.GenerateKey(config.Key);
			_packetWriter = new PacketWriter();

			LogHelper.Log.WriteLine("Tunnel watcher was created for " + config.TunnelHost);

			Task.Factory.StartNew(ReceiveCycle);
			Task.Factory.StartNew(PingCycle);
			if (config.ConnectOnStart)
				Init();
			IPAddress dummy;
			if (!IPAddress.TryParse(config.TunnelHost, out dummy))
				Task.Factory.StartNew(CheckHostChange);
		}

		private void CheckHostChange()
		{
			// checking if target host has changed it ip address to other
			while (!_disposed)
			{
				var addresses = Dns.GetHostAddresses(_config.TunnelHost);
				if (addresses.Length > 0)
				{
					if (!addresses.Any(x => x.Equals(DstAddr)))
					{
						Storage.OutgoingConnectionAdresses.Remove(DstAddr);
						DstAddr = addresses.First();
						ReInitDivert(DstAddr);
						Storage.OutgoingConnectionAdresses.Add(DstAddr);
					}
				}

				Thread.Sleep(60 * 1000);
			}
		}

		private int _isIniting;

		private DateTime _lastInitRequest;

		private void Init()
		{
			_lastInitRequest = DateTime.UtcNow;
			if (_isIniting == 1)
				return;
			Task.Factory.StartNew(InitInternal);
		}

		private void CloseSocket()
		{
			if (_socket != null)
			{
				_socket.Dispose();
			}

			_socket = null;
		}

		private void InitInternal()
		{
			if (Interlocked.CompareExchange(ref _isIniting, 1, 0) == 1)
				return;
			try
			{
				_initingEvent.Reset();

				var cs = new ClientHandshake();
				var sendingPacketLen = cs.GetPacketForSending();
				_encryptHelper = new EncryptHelper(_serverKey);
				var decryptHelper = new DecryptHelper(_serverKey);

				var proxyEP = !string.IsNullOrEmpty(_config.ProxyHost) ? EndpointHelper.ParseEndPoint(_config.ProxyHost, 12018) : null;
				var connectEP = proxyEP == null ? EndpointHelper.ParseEndPoint(_config.ConnectHost, 12017) : null;

				var destEP = proxyEP ?? connectEP;

				if (!destEP.Equals(_connectEP) && _connectEP != null)
				{
					Storage.RemoveSession(_connectEP);
				}

				_connectEP = destEP;

				Storage.IncrementEstablishing();
				Storage.AddSession(new byte[16], destEP).IsClientSession = true;

				if (proxyEP != null)
					LogHelper.Log.WriteLine("Initializing connection to " + _config.ConnectHost + " via proxy " + proxyEP);
				else
					LogHelper.Log.WriteLine("Initializing connection to " + connectEP);

				var lenToSend = _encryptHelper.Encrypt(cs.SendingPacket, sendingPacketLen);
				var packetToSend = _encryptHelper.InnerBuf;
				var initBuf = new byte[lenToSend + 4];
				Buffer.BlockCopy(packetToSend, 0, initBuf, 4, lenToSend);
				initBuf[0] = (byte)StateFlags.Connecting;
				initBuf[1] = 1; // version
				var recLength = 0;

				// killing old socket
				CloseSocket();
				_socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
				_socket.Connect(destEP);

				Task task = null;
				var period = 2000;
				while (true)
				{
					if (proxyEP != null)
					{
						// do not encrypt data to proxy, bcs it should be simple and do not work with encryption
						// can be changed in future version
						var bytesHost = Encoding.UTF8.GetBytes(_config.ConnectHost);
						var totalLen = 4 + 4 + bytesHost.Length;
						// should not be divided to 16 (we need to separate message from usual packets
						if (totalLen % 16 == 0) totalLen++;
						var proxyBuf = new byte[totalLen];
						proxyBuf[0] = (byte)StateFlags.ProxyConnecting;
						proxyBuf[1] = 1; // version
						proxyBuf[4] = (byte)bytesHost.Length;
						proxyBuf[5] = (byte)(bytesHost.Length >> 8);
						proxyBuf[6] = (byte)(bytesHost.Length >> 16);
						proxyBuf[7] = (byte)(bytesHost.Length >> 24);
						Buffer.BlockCopy(bytesHost, 0, proxyBuf, 8, bytesHost.Length);
						_socket.Send(proxyBuf, proxyBuf.Length, SocketFlags.None);
						// just to give more chances to process this message by other parts of network
						Thread.Sleep(10);
					}

					_socket.Send(initBuf, initBuf.Length, SocketFlags.None);
					task = task ?? Task.Factory.StartNew(() => recLength = _socket.Receive(_receiveBuffer));
					/*var sw = Stopwatch.StartNew();
					recLength = _socket.Receive(_receiveBuffer);
					Console.WriteLine(sw.ElapsedMilliseconds);*/

					if (task.Wait(period))
						break;

					period = Math.Min(period + 2000, 1000 * 60);

					LogHelper.Log.WriteLine("No response from server " + destEP);
					if (!_config.ConnectOnStart && DateTime.UtcNow.Subtract(_lastInitRequest).TotalSeconds > 60)
					{
						LogHelper.Log.WriteLine("Stopping connect atteptions to " + destEP + " until another request will occur");
					}
				}

				if (recLength < 4 || _receiveBuffer[0] != (byte)StateFlags.ConnectAnswer)
				{
					LogHelper.Log.WriteLine("Invalid server response");
					Storage.RemoveSession(destEP);
					return;
				}

				var decLen = decryptHelper.Decrypt(_receiveBuffer, 4);
				if (decLen < 9)
				{
					LogHelper.Log.WriteLine("Invalid server response");
					Storage.RemoveSession(destEP);
					return;
				}

				var sessionKey = cs.GetPacketFromServer(decryptHelper.InnerBuf, decLen);
				_encryptHelper = new EncryptHelper(sessionKey);
				var session = Storage.GetSession(destEP);
				session.Decryptor = new DecryptHelper(sessionKey);
				Session = session;
				LogHelper.Log.WriteLine("Initialized connection to " + destEP);
				_isInited = true;
				_initingEvent.Set();

				// after connect - sending packet to estabilish backward connection
				using (var p = new Ping()) p.Send(DstAddr, 1);
			}
			finally
			{
				Interlocked.Exchange(ref _isIniting, 0);
				Storage.DecrementEstablishing();
			}
		}

		private void DropInit()
		{
			_isInited = false;
			if (_config.KeepAlive)
				Init();
		}

		private void PingCycle()
		{
			DateTime lastPingDate = DateTime.MinValue;
			var pingSpan = TimeSpan.FromSeconds(_config.PingInterval);

			while (!_disposed)
			{
				if (_isInited)
				{
					// problem with server? no answers, dropping connection
					if (Session.SendReceiveDifference > TimeSpan.FromSeconds(_config.PingInterval * 2))
					{
						DropInit();
					}

					if ((_config.KeepAlive && DateTime.UtcNow.Subtract(lastPingDate) > pingSpan) || Session.SendReceiveDifference > pingSpan)
					{
						_socket.Send(new byte[] { (byte)StateFlags.Ping, 0, 0, 0 }, 4, SocketFlags.None);
						lastPingDate = DateTime.UtcNow;
					}
				}
				else
				{
					// force renew connection attempt
					if (_config.KeepAlive)
						_lastInitRequest = DateTime.UtcNow;
				}

				Thread.Sleep(1000);
			}
		}

		protected override void Send(byte[] packet, int packetLen)
		{
			if (!_isInited)
			{
				Init();
				// if (!_initingEvent.WaitOne(4000))
				//	return;
			}

			if (_isInited)
			{
				var lenToSend = _encryptHelper.Encrypt(packet, packetLen);
				var packetToSend = _encryptHelper.InnerBuf;
				Session.UpdateReceiveActivity();
				_socket.Send(packetToSend, lenToSend, SocketFlags.None);
			}
		}

		private readonly byte[] _receiveBuffer = new byte[65536];

		private void ReceiveCycle()
		{
			byte[] buf = _receiveBuffer;
			while (!_disposed)
			{
				if (!_isInited)
					_initingEvent.WaitOne();
				if (_disposed)
					return;

				int len;

				try
				{
					len = _socket.Receive(buf);
				}
				catch (Exception/* ex*/)
				{
					if (_isIniting == 1 && _isInited)
					{
						LogHelper.Log.WriteLine("Receive data error");
						_isInited = false;
						// LogHelper.Log.WriteLine(ex);
						CloseSocket();

						if (_config.KeepAlive) Init();
					}

					Thread.Sleep(1000);
					continue;
				}

				// just drop data, assume that it is invalid
				if (len % 16 != 0)
				{
					// in any case, this is error
					if (buf[0] != (byte)StateFlags.Pong)
					{
						LogHelper.Log.WriteLine("Received an error flag from " + _socket.RemoteEndPoint);
						DropInit();
						continue;
					}
				}

				Session.UpdateReceiveActivity();

				var decryptHelper = Session.Decryptor;
				var decLen = decryptHelper.Decrypt(buf, 0);
				_packetWriter.Write(decryptHelper.InnerBuf, decLen);
			}
		}

		public override void Dispose()
		{
			base.Dispose();
			_disposed = true;
			_initingEvent.Set();
			CloseSocket();
		}
	}
}
