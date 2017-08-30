using System;
using System.Net.Sockets;
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

		private DecryptHelper _decryptHelper;

		private readonly byte[] _serverKey;

		private readonly ManualResetEvent _initingEvent = new ManualResetEvent(false);

		private readonly PacketWriter _packetWriter;

		private bool _isInited;

		private readonly RemoteServerConfig _config;

		public ClientSender(RemoteServerConfig config, TunnelStorage storage)
			: base(null, EndpointHelper.ParseEndPoint(config.Address, 1).Address, storage)
		{
			storage.OutgoingConnectionAdresses.Add(DstAddr);
			_config = config;
			_serverKey = PasswordHelper.GenerateKey(config.Key);
			_packetWriter = new PacketWriter();

			LogHelper.Log.WriteLine("Tunnel watcher was created for " + config.Address);

			Task.Factory.StartNew(ReceiveCycle);
			if (config.KeepAlive)
				Task.Factory.StartNew(PingCycle);
			if (config.ConnectOnStart)
				Init();
		}

		private int _isIniting;

		private void Init()
		{
			if (_isIniting == 1)
				return;
			Task.Factory.StartNew(InitInternal);
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
				_decryptHelper = new DecryptHelper(_serverKey);

				var ep = EndpointHelper.ParseEndPoint(_config.Address, 12017);
				if (!ep.Address.Equals(DstAddr))
				{
					Storage.OutgoingConnectionAdresses.Remove(DstAddr);
					ReInitDivert(ep.Address);
					Storage.OutgoingConnectionAdresses.Add(DstAddr);
				}

				Storage.SetNewEndPoint(new byte[16], ep);

				LogHelper.Log.WriteLine("Initializing connection to " + ep);

				var lenToSend = _encryptHelper.Encrypt(cs.SendingPacket, sendingPacketLen);
				var packetToSend = _encryptHelper.InnerBuf;
				var initBuf = new byte[lenToSend + 4];
				Buffer.BlockCopy(packetToSend, 0, initBuf, 4, lenToSend);
				initBuf[0] = 1;
				initBuf[1] = 1; // version
				var recLength = 0;

				// killing old socket
				if (_socket != null)
					_socket.Dispose();
				_socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
				_socket.Connect(ep);

				Task task = null;
				var period = 2000;
				while (true)
				{
					_socket.Send(initBuf, initBuf.Length, SocketFlags.None);
					task = task ?? Task.Factory.StartNew(() => recLength = _socket.Receive(_receiveBuffer));
					/*var sw = Stopwatch.StartNew();
					recLength = _socket.Receive(_receiveBuffer);
					Console.WriteLine(sw.ElapsedMilliseconds);*/

					if (task.Wait(period))
						break;

					period = Math.Min(period + 2000, 1000 * 60);

					LogHelper.Log.WriteLine("No response from server " + ep);
				}

				if (recLength < 4 || _receiveBuffer[0] != 0x2)
				{
					Console.Error.WriteLine("Invalid server response");
					return;
				}

				var decLen = _decryptHelper.Decrypt(_receiveBuffer, 4);
				if (decLen < 9)
				{
					Console.Error.WriteLine("Invalid server response");
					return;
				}

				var sessionKey = cs.GetPacketFromServer(_decryptHelper.InnerBuf, decLen);
				_encryptHelper = new EncryptHelper(sessionKey);
				_decryptHelper = new DecryptHelper(sessionKey);
				LogHelper.Log.WriteLine("Initialized connection to " + ep);
				_isInited = true;
				_initingEvent.Set();
			}
			finally
			{
				Interlocked.Exchange(ref _isIniting, 0);
			}
		}

		private void PingCycle()
		{
			while (!_disposed)
			{
				if (_isInited)
					_socket.Send(new byte[] { 0x5, 0, 0, 0 }, 4, SocketFlags.None);
				Thread.Sleep(15 * 1000);
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

				var len = _socket.Receive(buf);
				// just drop data, assume that it is invalid
				if (len % 16 != 0)
				{
					// in any case, this is error
					// if (buf[0] == 0x3)
					{
						LogHelper.Log.WriteLine("Received an error flag from " + _socket.RemoteEndPoint);
						_isInited = false;
						// failed data
						Init();
						continue;
					}
				}

				var decLen = _decryptHelper.Decrypt(buf, 0);
				_packetWriter.Write(_decryptHelper.InnerBuf, decLen);
			}
		}

		public override void Dispose()
		{
			base.Dispose();
			_disposed = true;
			_initingEvent.Set();
			_socket.Dispose();
		}
	}
}
