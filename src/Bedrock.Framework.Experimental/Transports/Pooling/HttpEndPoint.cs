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
            : base(IPAddress.Parse(host), port)
        {
            Kind = kind;
            Host = host;
            SslHostName = sslHostName;
            ProxyUri = proxyUri;
            MaxConnections = maxConnections;
        }

        public HttpConnectionKind Kind {get;}
        public string Host {get;}
        public string SslHostName {get;}
        public Uri ProxyUri {get;}
        public int MaxConnections {get;}

        public override bool Equals(object comparand)
        {
            return comparand is HttpEndPoint other 
                && base.Equals(comparand) 
                && other.Kind.Equals(Kind) 
                && other.Host.Equals(Host) 
                && other.SslHostName.Equals(SslHostName) 
                && other.ProxyUri.Equals(ProxyUri) 
                && other.MaxConnections == MaxConnections;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode() 
                ^ Kind.GetHashCode()
                ^ Host.GetHashCode()
                ^ SslHostName.GetHashCode()
                ^ ProxyUri.GetHashCode()
                ^ MaxConnections.GetHashCode();
        }
    }
}
