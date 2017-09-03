var p = require('./proxy.js');

p.setAllowedTargetIpCheckFunc(function (dstAddr, dstHost) {
	// just for example
	console.log(dstAddr)
	return /192\.168\.\d+\.\d+/.test(dstAddr);
});

p.start(12018);