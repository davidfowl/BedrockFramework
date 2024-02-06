
// Based on: https://github.com/dotnet/aspnetcore/blob/main/src/Servers/Kestrel/Transport.Sockets/src/SocketTransportFactory.cs
//           https://github.com/dotnet/aspnetcore/blob/main/src/Servers/Kestrel/Transport.NamedPipes/src/Internal/NamedPipeTransportFactory.cs

using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Bedrock.Framework;

/// <summary>
/// A factory for named pipe based connections.
/// </summary>
#if (NET8_0_OR_GREATER)
public sealed class NamedPipeTransportFactory : IConnectionListenerFactory, IConnectionListenerFactorySelector
#else
public sealed class NamedPipeTransportFactory : IConnectionListenerFactory
#endif
{
    private readonly NamedPipeTransportOptions _options;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="SocketTransportFactory"/> class.
    /// </summary>
    /// <param name="options">The transport options.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public NamedPipeTransportFactory(
        IOptions<NamedPipeTransportOptions> options,
        ILoggerFactory loggerFactory)
    {
#if (NETFRAMEWORK || NETSTANDARD2_0 || NETCOREAPP2_0 || NETCOREAPP3_1)
        ArgumentNullExceptionEx.ThrowIfNull(options);
        ArgumentNullExceptionEx.ThrowIfNull(loggerFactory);
#else
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(loggerFactory);
#endif

        _loggerFactory = loggerFactory;
        _options = options.Value;
    }

    /// <inheritdoc />
    public ValueTask<IConnectionListener> BindAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
    {
#if (NETFRAMEWORK || NETSTANDARD2_0 || NETCOREAPP2_0 || NETCOREAPP3_1)
        ArgumentNullExceptionEx.ThrowIfNull(endpoint);
#else
        ArgumentNullException.ThrowIfNull(endpoint);
#endif

        if (endpoint is not NamedPipeEndPoint namedPipeEndPoint)
        {
            throw new NotSupportedException($"{endpoint.GetType()} is not supported.");
        }

        return new ValueTask<IConnectionListener>(new NamedPipeConnectionListener(namedPipeEndPoint, _options, _loggerFactory));
    }

    /// <inheritdoc />
    public bool CanBind(EndPoint endpoint)
    {
        return endpoint switch
        {
            IPEndPoint _ => false,
            //UnixDomainSocketEndPoint _ => true,
            FileHandleEndPoint _ => false,
            NamedPipeEndPoint => true,
            _ => false
        };
    }
}