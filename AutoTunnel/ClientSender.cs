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
		private Socket _socket;

		private bool _disposed;

		private EncryptHelper _encryptHelper;

		private DecryptHelper _decryptHelper;

		private static int _sessionId;

		private int _currentSessionId;

		private readonly byte[] _serverKey;

		private ManualResetEvent _initingEvent = new ManualResetEvent(true);

		public ClientSender(string dstAddr, IPEndPoint remoteEP, byte[] serverKey)
			: base(dstAddr, remoteEP)
		{
			_socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
			_socket.Connect(remoteEP);

			_serverKey = serverKey;
			Init();

			Task.Factory.StartNew(ReceiveCycle);
			// Task.Factory.StartNew(PingCycle);
		}

		private void Init()
		{
			_initingEvent.Reset();
			_currentSessionId = Interlocked.Increment(ref _sessionId);

			var cs = new ClientHandshake();
			var sendingPacketLen = cs.GetPacketForSending();
			_encryptHelper = new EncryptHelper(_serverKey);
			_decryptHelper = new DecryptHelper(_serverKey);

			Console.WriteLine("Initializing connection to " + DstAddr);
			SendEncryptedCommand(1, cs.SendingPacket, sendingPacketLen);
			_socket.Receive(_receiveBuffer);
			var decLen = _decryptHelper.Decrypt(_receiveBuffer);
			var sessionKey = cs.GetPacketFromServer(_decryptHelper.InnerBuf, decLen);
			_encryptHelper = new EncryptHelper(sessionKey);
			_decryptHelper = new DecryptHelper(sessionKey);
			_currentSessionId = _receiveBuffer[1] | (_receiveBuffer[2] << 8) | (_receiveBuffer[3] << 16);
			Console.WriteLine("Initialized connection to " + DstAddr + ", SessionId: " + _sessionId);
			_initingEvent.Set();
		}

		private void PingCycle()
		{
			while (!_disposed)
			{
				Send(new byte[0], 0);
				Thread.Sleep(15 * 1000);
			}
		}

		protected void SendEncryptedCommand(byte commandId, byte[] packet, int packetLen)
		{
			var lenToSend = _encryptHelper.Encrypt(packet, packetLen);
			var packetToSend = _encryptHelper.InnerBuf;
			packetToSend[0] = commandId;
			packetToSend[1] = (byte)(_currentSessionId & 0xff);
			packetToSend[2] = (byte)((_currentSessionId >> 8) & 0xff);
			packetToSend[3] = (byte)((_currentSessionId >> 16) & 0xff);
			_socket.Send(packetToSend, lenToSend, SocketFlags.None);
		}

		protected override void Send(byte[] packet, int packetLen)
		{
			_initingEvent.WaitOne();
			// _socket.Send(packet, packetLen, SocketFlags.None);
			SendEncryptedCommand(0, packet, packetLen);
		}

		private readonly byte[] _receiveBuffer = new byte[65536];

		private void ReceiveCycle()
		{
			byte[] buf = _receiveBuffer;
			while (!_disposed)
			{
				_socket.Receive(buf);
				if (buf[0] == 0x3)
				{
					Console.WriteLine("Received an error flag from " + DstAddr);
					// failed data
					Init();
					continue;
				}

				var decLen = _decryptHelper.Decrypt(buf);
				OnReceive(_decryptHelper.InnerBuf, decLen);
			}
		}

		public override void Dispose()
		{
			base.Dispose();
			_disposed = true;
			_socket.Dispose();
		}
	}
}
