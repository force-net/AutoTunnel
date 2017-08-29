using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using Force.AutoTunnel.Encryption;

namespace Force.AutoTunnel
{
	public class ClientSender : BaseSender
	{
		private readonly Socket _socket;

		private bool _disposed;

		private EncryptHelper _encryptHelper;

		private DecryptHelper _decryptHelper;

		private readonly byte[] _serverKey;

		private readonly ManualResetEvent _initingEvent = new ManualResetEvent(false);

		private readonly PacketWriter _packetWriter;

		private bool _isInited;

		public readonly IPEndPoint RemoteEP;

		public ClientSender(IPAddress dstAddr, IPEndPoint remoteEP, byte[] serverKey, TunnelStorage storage)
			: base(dstAddr, storage)
		{
			Console.WriteLine("Tunnel watcher was created for " + dstAddr);
			RemoteEP = remoteEP;
			_packetWriter = new PacketWriter();
			_socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
			_socket.Connect(remoteEP);

			_serverKey = serverKey;

			Task.Factory.StartNew(ReceiveCycle);
			// Task.Factory.StartNew(PingCycle);
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

				Console.WriteLine("Initializing connection to " + DstAddr);

				var lenToSend = _encryptHelper.Encrypt(cs.SendingPacket, sendingPacketLen);
				var packetToSend = _encryptHelper.InnerBuf;
				var initBuf = new byte[lenToSend + 4];
				Buffer.BlockCopy(packetToSend, 0, initBuf, 4, lenToSend);
				initBuf[0] = 1;
				initBuf[1] = 1; // version
				var recLength = 0;

				Task task = null;
				while (true)
				{
					_socket.Send(initBuf, initBuf.Length, SocketFlags.None);
					task = task ?? Task.Factory.StartNew(() => recLength = _socket.Receive(_receiveBuffer));
					/*var sw = Stopwatch.StartNew();
					recLength = _socket.Receive(_receiveBuffer);
					Console.WriteLine(sw.ElapsedMilliseconds);*/

					if (task.Wait(2000))
						break;

					Console.WriteLine("No response from server " + DstAddr);
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
				Console.WriteLine("Initialized connection to " + DstAddr);
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
				Send(new byte[0], 0);
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
						Console.WriteLine("Received an error flag from " + DstAddr);
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
