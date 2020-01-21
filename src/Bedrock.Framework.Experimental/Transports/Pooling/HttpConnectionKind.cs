namespace Bedrock.Framework
{
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
