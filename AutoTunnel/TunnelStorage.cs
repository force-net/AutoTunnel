using System;
using System.Collections.Concurrent;
using System.Linq;

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
			return _clients.TryRemove(ip, out value);
		}

		public void RemoveOldSenders(TimeSpan killTime)
		{
			var dt = DateTime.UtcNow;
			var toRemove = (from client in _clients where client.Value is ReplySender && dt.Subtract(client.Value.LastActivity) >= killTime select client.Key).ToList();
			toRemove.ForEach(x => Remove(x));
		}
	}
}
