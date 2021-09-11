﻿#nullable enable
using System;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework.Middleware.Tls
{
    public delegate bool RemoteCertificateValidator(X509Certificate2 certificate, X509Chain chain, SslPolicyErrors policyErrors);

    /// <summary>
    /// Settings for how TLS connections are handled.
    /// </summary>
    public class TlsOptions
    {
        private TimeSpan _handshakeTimeout;

        /// <summary>
        /// Initializes a new instance of <see cref="TlsOptions"/>.
        /// </summary>
        public TlsOptions()
        {
            RemoteCertificateMode = RemoteCertificateMode.RequireCertificate;
            HandshakeTimeout = TimeSpan.FromSeconds(10);
        }

        /// <summary>
        /// <para>
        /// Specifies the local certificate used to authenticate TLS connections. This is ignored if LocalCertificateSelector is set.
        /// </para>
        /// <para>
        /// If the certificate has an Extended Key Usage extension, the usages must include Server Authentication (OID 1.3.6.1.5.5.7.3.1).
        /// </para>
        /// </summary>
        public X509Certificate2 LocalCertificate { get; set; }

        /// <summary>
        /// <para>
        /// A callback that will be invoked to dynamically select a local server certificate. This is higher priority than LocalCertificate.
        /// If SNI is not available then the name parameter will be null.
        /// </para>
        /// <para>
        /// If the certificate has an Extended Key Usage extension, the usages must include Server Authentication (OID 1.3.6.1.5.5.7.3.1).
        /// </para>
        /// </summary>
        public Func<ConnectionContext, string, X509Certificate2> LocalServerCertificateSelector { get; set; }

        /// <summary>
        /// Specifies the remote endpoint certificate requirements for a TLS connection. Defaults to <see cref="RemoteCertificateMode.RequireCertificate"/>.
        /// </summary>
        public RemoteCertificateMode RemoteCertificateMode { get; set; }

        /// <summary>
        /// Specifies a callback for additional remote certificate validation that will be invoked during authentication. This will be ignored
        /// if <see cref="AllowAnyRemoteCertificate"/> is called after this callback is set.
        /// </summary>
        public RemoteCertificateValidator RemoteCertificateValidation { get; set; }

        /// <summary>
        /// Specifies allowable SSL protocols. Defaults to <see cref="System.Security.Authentication.SslProtocols.Tls12" /> and <see cref="System.Security.Authentication.SslProtocols.Tls11"/>.
        /// </summary>
        public SslProtocols SslProtocols { get; set; }

        /// <summary>
        /// Specifies whether the certificate revocation list is checked during authentication.
        /// </summary>
        public bool CheckCertificateRevocation { get; set; }

        /// <summary>
        /// Specifies the cipher suites allowed for TLS. When set to null, the operating system default is used.
        /// </summary>
        public CipherSuitesPolicy? CipherSuitesPolicy { get; set; }

        /// <summary>
        /// Overrides the current <see cref="RemoteCertificateValidation"/> callback and allows any client certificate.
        /// </summary>
        public void AllowAnyRemoteCertificate()
        {
            RemoteCertificateValidation = (_, __, ___) => true;
        }

        /// <summary>
        /// Provides direct configuration of the <see cref="SslServerAuthenticationOptions"/> on a per-connection basis.
        /// This is called after all of the other settings have already been applied.
        /// </summary>
        public Action<ConnectionContext, SslServerAuthenticationOptions> OnAuthenticateAsServer { get; set; }

        /// <summary>
        /// Provides direct configuration of the <see cref="SslClientAuthenticationOptions"/> on a per-connection basis.
        /// This is called after all of the other settings have already been applied.
        /// </summary>
        public Action<ConnectionContext, SslClientAuthenticationOptions> OnAuthenticateAsClient { get; set; }

        /// <summary>
        /// Specifies the maximum amount of time allowed for the TLS/SSL handshake. This must be positive and finite.
        /// </summary>
        public TimeSpan HandshakeTimeout
        {
            get => _handshakeTimeout;
            set
            {
                if (value <= TimeSpan.Zero && value != Timeout.InfiniteTimeSpan)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), nameof(HandshakeTimeout) + " must be positive");
                }
                _handshakeTimeout = value != Timeout.InfiniteTimeSpan ? value : TimeSpan.MaxValue;
            }
        }
    }
}
