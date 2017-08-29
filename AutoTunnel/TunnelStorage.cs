using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;

using Force.AutoTunnel.Encryption;

namespace Force.AutoTunnel
{
	public class TunnelStorage
	{
		private readonly ConcurrentDictionary<ulong, BaseSender> _clients = new ConcurrentDictionary<ulong, BaseSender>();

		public readonly HashSet<IPAddress> OutgoingConnectionAdresses = new HashSet<IPAddress>();

		private readonly List<ClientSender> _clientSenders = new List<ClientSender>();

		public BaseSender GetOrAddSender(IPEndPoint remoteHost, Func<BaseSender> creatorFunc)
		{
			return _clients.GetOrAdd(GetHostKey(remoteHost), s => creatorFunc());
		}

		public bool Remove(IPEndPoint remoteHost)
		{
			var hostKey = GetHostKey(remoteHost);
			return Remove(hostKey);
		}

		private bool Remove(ulong hostKey)
		{
			BaseSender value;
			
			if (_clients.TryRemove(hostKey, out value))
			{
				DecryptHelper helper;
				_sessionDecryptors.TryRemove(hostKey, out helper);
				byte[] dummy;
				_sessionKeys.TryRemove(hostKey, out dummy);
				value.Dispose();
				return true;
			}

			return false;
		}

		public IPEndPoint[] GetOldSenders(TimeSpan killTime)
		{
			var dt = DateTime.UtcNow;
			var toRemove = (from client in _clients where client.Value is ReplySender && dt.Subtract(client.Value.LastActivity) >= killTime select client.Key)
				.Select(x => new IPEndPoint((long)(x >> 16), (int)(x & 0xffff)))
				.ToArray();
			return toRemove;
		}

		private readonly ConcurrentDictionary<ulong, byte[]> _sessionKeys = new ConcurrentDictionary<ulong, byte[]>();

		private readonly ConcurrentDictionary<ulong, DecryptHelper> _sessionDecryptors = new ConcurrentDictionary<ulong, DecryptHelper>();

		public void SetNewEndPoint(byte[] sessionKey, IPEndPoint remoteHost)
		{
			var hostKey = GetHostKey(remoteHost);
			_sessionKeys[hostKey] = sessionKey;
			_sessionDecryptors[hostKey] = new DecryptHelper(sessionKey);
		}

		public byte[] GetSessionKey(IPEndPoint remoteHost)
		{
			byte[] value;
			if (_sessionKeys.TryGetValue(GetHostKey(remoteHost), out value)) return value;
			return null;
		}

		public DecryptHelper GetSessionDecryptor(IPEndPoint remoteHost)
		{
			DecryptHelper value;
			if (_sessionDecryptors.TryGetValue(GetHostKey(remoteHost), out value)) return value;
			return null;
		}

		private static ulong GetHostKey(IPEndPoint endpoint)
		{
#pragma warning disable 612,618
			return ((ulong)endpoint.Address.Address << 16) | (uint)endpoint.Port;
#pragma warning restore 612,618
		}

		public bool HasSession(ulong key)
		{
			return _sessionKeys.ContainsKey(key);
		}

		public void AddClientSender(ClientSender sender)
		{
			// adding fake tunnel info
			SetNewEndPoint(new byte[16], sender.RemoteEP);
			OutgoingConnectionAdresses.Add(sender.DstAddr);

			_clientSenders.Add(sender);
		}
	}
}
