using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;

using Force.AutoTunnel.Encryption;
using Force.AutoTunnel.Service;

namespace Force.AutoTunnel
{
	public class TunnelStorage
	{
		private readonly ConcurrentDictionary<long, BaseSender> _clients = new ConcurrentDictionary<long, BaseSender>();

		public readonly HashSet<IPAddress> OutgoingConnectionAdresses = new HashSet<IPAddress>();

		public BaseSender GetOrAddSender(IPAddress dstAddr, Func<BaseSender> creatorFunc)
		{
#pragma warning disable 612,618
			return _clients.GetOrAdd(dstAddr.Address, s => creatorFunc());
#pragma warning restore 612,618
		}

		public IPEndPoint[] GetOldSessions(TimeSpan killTime)
		{
			var dt = DateTime.UtcNow;
			return _sessions.Where(x => !x.Value.IsClientSession && dt.Subtract(x.Value.LastActivity) >= killTime)
				.Select(x => x.Key)
				.Select(x => new IPEndPoint((long)(x >> 16), (int)(x & 0xffff)))
				.ToArray();
		}

		public void RemoveAllSessions()
		{
			_sessions.Keys
				.Select(x => new IPEndPoint((long)(x >> 16), (int)(x & 0xffff)))
				.ToList()
				.ForEach(RemoveSession);
		}

		public void RemoveSession(IPEndPoint endPoint)
		{
			var hostKey = GetHostKey(endPoint);
			Session session;
			if (_sessions.TryRemove(hostKey, out session))
			{
				if (session.IsClientSession)
					return;
				_clients.Where(x => x.Value.Session == session).Select(x => x.Key).ToList().ForEach(
					x =>
						{
							BaseSender value;
							if (_clients.TryRemove(x, out value)) value.Dispose();
						});
			}

			SetIcon();
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

			public bool IsClientSession { get; set; }

			public void UpdateLastActivity()
			{
				LastActivity = DateTime.UtcNow;
			}
		}

		private readonly ConcurrentDictionary<ulong, Session> _sessions = new ConcurrentDictionary<ulong, Session>();

		public Session AddSession(byte[] sessionKey, IPEndPoint remoteEP)
		{
			var hostKey = GetHostKey(remoteEP);
			var s = new Session(remoteEP)
				{
					Key = sessionKey,
					Decryptor = new DecryptHelper(sessionKey)
				};
			_sessions[hostKey] = s;
			SetIcon();
			return s;
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

		private int _estabilishingCount;

		public void IncrementEstabilishing()
		{
			Interlocked.Increment(ref _estabilishingCount);
			SetIcon();
		}

		public void DecrementEstabilishing()
		{
			Interlocked.Decrement(ref _estabilishingCount);
			SetIcon();
		}

		private void SetIcon()
		{
			if (_estabilishingCount > 0)
			{
				ConsoleHelper.SetActiveIcon(ConsoleHelper.IconStatus.Estabilishing);
			}
			else
			{
				ConsoleHelper.SetActiveIcon(_sessions.Count > 0 ? ConsoleHelper.IconStatus.Active : ConsoleHelper.IconStatus.Default);
			}
		}
	}
}
