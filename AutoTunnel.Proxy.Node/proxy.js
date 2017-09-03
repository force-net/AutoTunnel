const dgram = require('dgram');

var logFunc = console.log;

var start = function (portNumber) {
	var server = dgram.createSocket('udp4');
	
	var proxyPairs = {};
	var failPairs = {};
	
	server.bind(portNumber, function () {
		address = server.address();
		logFunc('Server listening ' + address.address + ':' + address.port);
	});
	server.on('error', function (err) {
		logFunc('Server error: ', err);
	});
	
	server.on('message', function (msg, ep) {
		// try {
			// proxy init
			if (ep.size % 16 !== 0 && msg[0] === 0x7) {
				// invalid data
				if (ep.size <= 9)
					return;
				var len = msg.readInt32LE(4);
				var dstHostAndPort = msg.toString('utf8', 8, 8 + len);
				var spl = dstHostAndPort.split(':');
				var dstHost = spl[0];
				var dstPort = (spl[1] || 12017) * 1;
				var socket = dgram.createSocket('udp4');
				socket.on('message', function (bmsg, bep) {
					var pair = proxyPairs[ep.address + ':' + ep.port];
					if (pair) {
						pair.lastActivity = new Date().getTime();
						server.send(bmsg, 0, bep.size, ep.port, ep.address);
					}
				});
				proxyPairs[ep.address + ':' + ep.port] = {
					sourceHost: ep.address,
					sourcePort: ep.port,
					host: dstHost,
					port: dstPort,
					targetSocket: socket, 
					lastActivity: new Date().getTime()
				};
				logFunc('Estabilished tunnel ', ep.address + ':' + ep.port + '->' + dstHost + ':' + dstPort);
				
				// TODO: think about answer
			} else {
				var pair = proxyPairs[ep.address + ':' + ep.port];

				if (!pair) {
					var failPair = failPairs[ep.address + ':' + ep.port];
					// one-time error sending, to add info to sender about 'we do not know who are you'
					// sender should reconnect to proxy
					if (!failPair) {
						logFunc('Missing binding for ', ep.address + ':' + ep.port);
						failPairs[ep.address + ':' + ep.port] = { lastActivity: new Date().getTime() };
						var buffer = Buffer.alloc ? Buffer.alloc(4) : new Buffer(4);
						buffer.writeUInt32LE(8);
						server.send(buffer, 0, 4, ep.port, ep.address);
					}
				} else {
					pair.lastActivity = new Date().getTime();
					pair.targetSocket.send(msg, 0, ep.size, pair.port, pair.host);
				}
			}
		// } catch (e) {
		//	logFunc('Error in proxy message processing', e);
		// }
	});
	
	setInterval(function () {
		var now = new Date().getTime();
		Object.keys(proxyPairs).forEach(function (p) {
			var proxyPair = proxyPairs[p];
			if (now - proxyPair.lastActivity > 10 * 60 * 1000) {
				proxyPair.targetSocket.close();
				delete proxyPairs[p];
				logFunc('Removing idle session ' + proxyPair.sourceHost + ':' + proxyPair.sourcePort);
			}
		});

		Object.keys(failPairs).forEach(function (p) {
			var proxyPair = failPairs[p];
			if (now - proxyPair.lastActivity > 10 * 60 * 1000) {
				delete failPairs[p];
			}
		});
	}, 10 * 60 * 1000);
};

module.exports = {
	start: function (portNumber) {
		return start(portNumber);
	},
	setLogger: function (loggerFunc) {
		logFunc = loggerFunc;
	}
};