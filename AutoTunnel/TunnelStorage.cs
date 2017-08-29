using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

using Force.AutoTunnel.Encryption;

namespace Force.AutoTunnel
{
	public class TunnelStorage
	{
		private readonly ConcurrentDictionary<string, BaseSender> _clients = new ConcurrentDictionary<string, BaseSender>();

		public BaseSender GetOrAdd(string ip, Func<BaseSender> creatorFunc)
		{
			return _clients.GetOrAdd(ip, s => creatorFunc());
		}

		public bool Remove(string ip)
		{
			BaseSender value;
			if (_clients.TryRemove(ip, out value))
			{
				DecryptHelper helper;
				_sessionDecryptors.TryRemove(value.SessionId, out helper);
				byte[] dummy;
				_sessionKeys.TryRemove(_sessionId, out dummy);
				value.Dispose();
				return true;
			}

			return false;
		}

		public void RemoveOldSenders(TimeSpan killTime)
		{
			var dt = DateTime.UtcNow;
			var toRemove = (from client in _clients where client.Value is ReplySender && dt.Subtract(client.Value.LastActivity) >= killTime select client.Key).ToList();
			toRemove.ForEach(x => Remove(x));
		}

		private readonly ConcurrentDictionary<int, byte[]> _sessionKeys = new ConcurrentDictionary<int, byte[]>();

		private readonly ConcurrentDictionary<int, DecryptHelper> _sessionDecryptors = new ConcurrentDictionary<int, DecryptHelper>();

		private static int _sessionId;

		public int GetNewSessionId(byte[] sessionKey)
		{
			var currentSessionId = Interlocked.Increment(ref _sessionId);
			_sessionKeys[_sessionId] = sessionKey;
			_sessionDecryptors[_sessionId] = new DecryptHelper(sessionKey);
			return currentSessionId;
		}

		public byte[] GetSessionKey(int sessionId)
		{
			byte[] value;
			if (_sessionKeys.TryGetValue(sessionId, out value)) return value;
			return null;
		}

		public DecryptHelper GetSessionDecryptor(int sessionId)
		{
			DecryptHelper value;
			if (_sessionDecryptors.TryGetValue(sessionId, out value)) return value;
			return null;
		}
	}
}
