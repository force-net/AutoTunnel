# AutoTunnel
Secured network tunnel for connecting two computers. Main usage: connecting travelling computer to one server.

## Features

* Works for Windows 7 or greater
* Client-Server model
* Works on IP level and tunnels ICMP, TCP and UDP data
* Uses only one UDP port (12017 by default)
* Can connect by hostnames with periodic refreshing data
* Client can be NAT'ed
* Server can be NAT'ed, but you need to use a special proxy
* Creates transparent secured tunnel between two computers without allocation of additional IP-addresses, so you do not need for separate DNS-names for computers 

## Differences from VPN

* No server needed
* No separate IP address or network interface
* Always active (if service running)

## Differences from IPSec

* No need to specify both endpoints to static addresses
* Only one UDP port for usage (insted of ESP, AH protocols and 500 and 4500 UDP ports)
* Just some settings to make tunnel works, you no need to specify lot of parameters in different configs and phases
* Currently, no IPv6 support

## Limitations

* Due usage existing IPs for computers there are can be collision with current IPs
* If network has problems with UDP fragmentation (or badly configured Jumbo Frames), there are can be strange errors and packet loss

# Description

AutoTunnel uses [WinDivert](https://reqrypt.org/windivert.html) library to catch outgoing network packets, transfers it to another computer and puts them back, as normal packets.
So, this packets looks like usual network packets, but you do not need to specify routing tables between computers. These computers are in virtual local network.
After estabilishing connection with another computer, it begin to catch packets to source computer and transfers it back to it through estabilished tunnel.

## Encryption
Tunnel is encrypted with AES256 with PFS support. Currently, for auth you can use only preshared keys (I think, it simple and good variant for authentication, because one key can be changed to another in some seconds, you do not need to regenerate certificates or private keys).
This key is used only for initial handshake and has not transferred in open form. After handshake stage, another, temporary session key is used for data encryption.

## Tunnel Handling

There are two types of computers:

* Server - listens incoming connections
* Client - connects to server

Any computer can be server for others computers and client for anothers. There are no need to use separate program.
So, despite only two computers can use one tunnel, you can create a lot of tunnels between pairs of different computers.

Also, you can estabilish connection between two computers in client mode. Two tunnels will be created, but packets will be passed correctly.


# Installation and running

You need:

* Windows 7/2008R2 or greater
* Enabled .NET Framework 4.0
* [VC Redist 2012](https://www.microsoft.com/en-us/download/details.aspx?id=30679)

Program can be runned in console or work as a service. Service can be installed in next way:
```
AutoTunnel.exe service install
sc start AutoTunnel
```
## Configuration

Before starting, you need to configure config.json file. Example config:
```
{
	"enableListening": true,
	"addFirewallRule": true,
	"port": 12017,
	"logFileName": "somelog.log",
	"autoReloadOnChange": true,
	"remoteClients": [
		{
			"key": "key1",
			"description": "my key"
		}
	],
	"remoteServers": [
		{
			"tunnelHost": "192.168.16.8",
			"connectHost": "192.168.18.1:12017",
			"proxyHost": "192.168.24.1:12018",
			"key": "key1",
			"keepAlive": true,
			"connectOnStart": true
		}
	]
}
```

Key | Default Value | Description
----|---------------|------------
enableListening | true | Is Application listen incoming connections
addFirewallRule | true | Add opening rule to Windows Firewall
port | 12017 | Listening port
logFileName | null | Name of file to log program messages
autoReloadOnChange | true | Reload config automatically if it changed
remoteClients | null | Data of remote clients for server (if multiple clients are specified - any key can be used for connection)
remoteClients.key | null | Pre-shared Key for remote client
remoteClients.description | null | Description of remote client for logging and your notes
remoteServers | null | Servers for connecting as client
remoteServers.tunnelHost | null | IP address or host name of remote computer. If any packets will be send to this computer, it will be passed through tunnel
remoteServers.connectHost | null | IP address or host with port of remote computer to connect. If skipped - tunnel host data can be used. You can specify it, it target computer has different IP addresses and you want to connect to one of them, but pass data for another
remoteServers.proxyHost | null | IP address or host with port of proxy. Proxy can be used to connect to server which is not available from outer network
remoteServers.key | null | Pre-shared key to estabilish connection with remote server
remoteServers.keepAlive | false | Send special keep alive packets to keep connection alive. If you need permament connection, you can set it to true 
remoteServers.connectOnStart | false | Connect to another computer on application start or only when first packet will be sent to it


# Proxy
If target computer is unavailabe from outer network but you have separate computer which can transfer packets from local network to outer network, you can use special proxy program.

Full scheme of connection can be similar:

```
Client computer (gray ip, NAT) <-> Router <-> (Internet) <-> Router      <-> Proxy       <-> Server (NAT)
192.168.1.42 <-> 192.168.1.1|91.10.20.42  <-> (Internet) <-> 82.40.10.1 <-> 82.40.10.54 <-> 10.0.1.15
```


Current implementation does not encrypt data, it just transfer packets from one computer to another. For better compatibility, current version of proxy is written on node.js, so it can be run at lot of hardware.

Also, when you use a proxy, just proxy resolves host name to ip. So, it can be useful for internal host names.

By default, proxy is listening on 12018 port and can filter destination ip addresses or host names.

# Licensing

AutoTunnel has [MIT](https://github.com/force-net/AutTunnel/blob/develop/LICENSE) license, but it uses [WinDivert](https://reqrypt.org/windivert.html) library with [LGPL3](https://reqrypt.org/windivert-doc-v1.2.html#license) license.
 
<div>Application Icons made by <a href="http://www.freepik.com" title="Freepik">Freepik</a> from <a href="https://www.flaticon.com/" title="Flaticon">www.flaticon.com</a> is licensed by <a href="http://creativecommons.org/licenses/by/3.0/" title="Creative Commons BY 3.0" target="_blank">CC 3.0 BY</a></div>
 

