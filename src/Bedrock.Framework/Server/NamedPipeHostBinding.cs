using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework
{
    public class NamedPipeHostBinding : ServerBinding
    {
        private readonly ConnectionDelegate _application;

        public NamedPipeHostBinding(EndPoint endPoint, ConnectionDelegate application, IConnectionListenerFactory connectionListenerFactory)
        {
            if (endPoint is not NamedPipeEndPoint) throw new ArgumentException("EndPoint must be of type NamedPipeEndPoint", nameof(endPoint));

            this.EndPoint = endPoint;
            _application = application;
            ConnectionListenerFactory = connectionListenerFactory;
        }

        private EndPoint EndPoint { get; }
        private IConnectionListenerFactory ConnectionListenerFactory { get; }

        public override ConnectionDelegate Application => _application;

        public override async IAsyncEnumerable<IConnectionListener> BindAsync([EnumeratorCancellation]CancellationToken cancellationToken = default)
        {
            var exceptions = new List<Exception>();

            IConnectionListener? namedPipeListener = null;

            try
            {
                namedPipeListener = await ConnectionListenerFactory.BindAsync(EndPoint, cancellationToken);
            }
            catch (Exception ex) when (!(ex is IOException))
            {
                exceptions.Add(ex);
            }

            if (namedPipeListener != null)
            {
                yield return namedPipeListener;
            }
        }

        public override string ToString()
        {
            return EndPoint.ToString();
        }
    }
}
