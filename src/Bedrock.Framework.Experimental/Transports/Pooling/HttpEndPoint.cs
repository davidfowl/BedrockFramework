using System;
using System.Net;

namespace Bedrock.Framework
{
    // A endpoint to route http traffic to.
    // Used to pool based on 
    public class HttpEndPoint : IPEndPoint
    {
        // Same options that are provided to the HttpConnectionPool.
        public HttpEndPoint(HttpConnectionKind kind, string host, int port, string sslHostName, Uri proxyUri, int maxConnections)
            : base(IPAddress.Parse(host), port) // TODO may need dns name.
        {
            Kind = kind;
            MaxConnections = maxConnections;
        }

        public HttpConnectionKind Kind {get;}
        public int MaxConnections {get;}
    }


    public enum HttpConnectionKind : byte
    {
        Http,               // Non-secure connection with no proxy.
        Https,              // Secure connection with no proxy.
        Proxy,              // HTTP proxy usage for non-secure (HTTP) requests.
        ProxyTunnel,        // Non-secure websocket (WS) connection using CONNECT tunneling through proxy.
        SslProxyTunnel,     // HTTP proxy usage for secure (HTTPS/WSS) requests using SSL and proxy CONNECT.
        ProxyConnect        // Connection used for proxy CONNECT. Tunnel will be established on top of this.
    }
}
