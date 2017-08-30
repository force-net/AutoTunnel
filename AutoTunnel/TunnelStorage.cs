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
		private readonly ConcurrentDictionary<long, BaseSender> _clients = new ConcurrentDictionary<long, BaseSender>();

		public readonly HashSet<IPAddress> OutgoingConnectionAdresses = new HashSet<IPAddress>();

		private readonly List<ClientSender> _clientSenders = new List<ClientSender>();

		public BaseSender GetOrAddSender(IPAddress dstAddr, Func<BaseSender> creatorFunc)
		{
#pragma warning disable 612,618
			return _clients.GetOrAdd(dstAddr.Address, s => creatorFunc());
#pragma warning restore 612,618
		}

		public IPEndPoint[] GetOldSessions(TimeSpan killTime)
		{
			var dt = DateTime.UtcNow;
			return _sessions.Where(x => dt.Subtract(x.Value.LastActivity) >= killTime)
				.Select(x => x.Key)
				.Select(x => new IPEndPoint((long)(x >> 16), (int)(x & 0xffff)))
				.ToArray();
		}

		public void RemoveSession(IPEndPoint endPoint)
		{
			var hostKey = GetHostKey(endPoint);
			Session session;
			if (_sessions.TryRemove(hostKey, out session))
				_clients.Where(x => x.Value.Session == session).Select(x => x.Key).ToList().ForEach(x =>
					{
						BaseSender value;
						if (_clients.TryRemove(x, out value))
							value.Dispose();
					});
		}

		public class Session
		{
			public Session(IPEndPoint remoteEP)
			{
				RemoteEP = remoteEP;
				UpdateLastActivity();
			}

			public IPEndPoint RemoteEP { get; private set; }

			public byte[] Key { get; set; }

			public DecryptHelper Decryptor { get; set; }

			public DateTime LastActivity { get; private set; }

			public void UpdateLastActivity()
			{
				LastActivity = DateTime.UtcNow;
			}
		}

		private readonly ConcurrentDictionary<ulong, Session> _sessions = new ConcurrentDictionary<ulong, Session>();

		public void SetNewEndPoint(byte[] sessionKey, IPEndPoint remoteEP)
		{
			var hostKey = GetHostKey(remoteEP);
			_sessions[hostKey] = new Session(remoteEP)
									{
										Key = sessionKey,
										Decryptor = new DecryptHelper(sessionKey)
									};
		}

		public Session GetSession(IPEndPoint remoteHost)
		{
			Session value;
			if (_sessions.TryGetValue(GetHostKey(remoteHost), out value)) return value;
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
			return _sessions.ContainsKey(key);
		}
	}
}
