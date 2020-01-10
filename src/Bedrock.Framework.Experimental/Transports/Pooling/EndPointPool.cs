using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework
{
    internal class EndPointPool : IEndPointPool, IAsyncDisposable
    {
        private EndPoint _endPoint;
        private List<ConnectionContext> _idleConnections;
        private IConnectionFactory _connectionFactory;
        private int _maxConnectionCount;
        private int _connectionCount;

        private object SyncObj = new object();
        private Queue<TaskCompletionSourceWithCancellation<ConnectionContext>> _waiters;

        public EndPointPool(EndPoint endPoint, IConnectionFactory connectionFactory, int maxConnections)
        {
            _endPoint = endPoint;
            _connectionFactory = connectionFactory;
            _idleConnections = new List<ConnectionContext>();
            _maxConnectionCount = maxConnections;
        }

        internal void IncrementConnectionCount()
        {
            lock (SyncObj)
            {
                IncrementConnectionCountNoLock();
            }
        }

        private void DecrementConnectionCount()
        {
            lock (SyncObj)
            {
                // if (TransferConnection(null))
                // {
                //     return;
                // }

                // There are no waiters to which the count should logically be transferred,
                // so simply decrement the count.
                _connectionCount--;
            }
        }

        public ValueTask ReturnAsync(ConnectionContext context)
        {
            // TODO handle context being invalid.
            lock (SyncObj)
            {
                if (HasWaiter())
                {
                    TransferConnection(context);
                }
                else
                {
                    _idleConnections.Add(context);
                }
            }

            return default;
        }

        private bool TransferConnection(ConnectionContext connection)
        {
            while (HasWaiter())
            {
                TaskCompletionSource<ConnectionContext> waiter = DequeueWaiter();

                // Try to complete the task. If it's been cancelled already, this will fail.
                if (waiter.TrySetResult(connection))
                {
                    return true;
                }

                // Couldn't transfer to that waiter because it was cancelled. Try again.
            }

            return false;
        }

        private void IncrementConnectionCountNoLock()
        {
            _connectionCount++;
        }

        public ValueTask<ConnectionContext> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new ValueTask<ConnectionContext>(Task.FromCanceled<ConnectionContext>(cancellationToken));
            }

            // TODO figure out how to get a connection lifetime via settings.
            var list = _idleConnections;
            List<ConnectionContext> _connectionsToDispose = null;
            async ValueTask<ConnectionContext> DisposeConnectionsAndReturn(List<ConnectionContext> toDispose, ConnectionContext context)
            {
                foreach(var disposeContext in toDispose)
                {
                    await disposeContext.DisposeAsync();
                }
                return context;
            }

            async ValueTask<ConnectionContext> GetConnectionAsync(EndPoint endpoint, CancellationToken token)
            {
                IncrementConnectionCount();

                var connectionContext = new PooledConnectionContext(await _connectionFactory.ConnectAsync(_endPoint, cancellationToken), this);
                if (connectionContext == null)
                {
                    DecrementConnectionCount();
                }

                return connectionContext;
            }

            while (true)
            {
                lock (SyncObj)
                {
                    if (list.Count > 0)
                    {
                        var cachedConnection = list[list.Count - 1];
                        list.RemoveAt(list.Count - 1);
                        
                        // TODO check if the connection is expired. 
                        if (!cachedConnection.ConnectionClosed.IsCancellationRequested)
                        {
                            if (_connectionsToDispose != null)
                            {
                                return DisposeConnectionsAndReturn(_connectionsToDispose, cachedConnection);
                            }

                            return new ValueTask<ConnectionContext>(cachedConnection);
                        }
                        else
                        {
                            if (_connectionsToDispose == null)
                            {
                                _connectionsToDispose = new List<ConnectionContext>();
                            }
                            _connectionsToDispose.Add(cachedConnection);
                        }
                    }
                    else
                    {
                        if (_connectionCount < _maxConnectionCount)
                        {
                            // TODO wrap connectionContext with a pooled version which returns to the pool.
                            return GetConnectionAsync(_endPoint, cancellationToken);
                        }
                        else
                        {
                            // Need to wait for a connection to be available
                            return EnqueueWaiter().WaitWithCancellationAsync(cancellationToken);
                        }
                    }
                }
            }
        }

        private TaskCompletionSourceWithCancellation<ConnectionContext> EnqueueWaiter()
        {
            if (_waiters == null)
            {
                _waiters = new Queue<TaskCompletionSourceWithCancellation<ConnectionContext>>();
            }

            var waiter = new TaskCompletionSourceWithCancellation<ConnectionContext>();
            _waiters.Enqueue(waiter);
            return waiter;
        }

        private bool HasWaiter()
        {
            return (_waiters != null && _waiters.Count > 0);
        }

        /// <summary>Dequeues a waiter from the waiters list.  The list must not be empty.</summary>
        /// <returns>The dequeued waiter.</returns>
        private TaskCompletionSourceWithCancellation<ConnectionContext> DequeueWaiter()
        {
            return _waiters.Dequeue();
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var connection in _idleConnections)
            {
                await connection.DisposeAsync();
            }
            // TODO should we do anything with disposed connections?
        }
    }
}