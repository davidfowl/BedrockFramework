using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Connections;
using System.Threading;
using System.Threading.Tasks;
using Bedrock.Framework.Infrastructure;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework
{
    internal class EndPointPool : IEndPointPool, IAsyncDisposable
    {
        private EndPoint _endPoint;
        private List<Connection> _idleConnections;
        private ConnectionFactory _connectionFactory;
        private int _maxConnectionCount;
        private int _connectionCount;

        private object _syncObj = new object();
        private Queue<TaskCompletionSourceWithCancellation<Connection>> _waiters;

        public EndPointPool(EndPoint endPoint, ConnectionFactory connectionFactory, int maxConnections)
        {
            _endPoint = endPoint;
            _connectionFactory = connectionFactory;
            _idleConnections = new List<Connection>();
            _maxConnectionCount = maxConnections;
        }

        internal void IncrementConnectionCount()
        {
            lock (_syncObj)
            {
                IncrementConnectionCountNoLock();
            }
        }

        private void DecrementConnectionCount()
        {
            lock (_syncObj)
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

        public ValueTask ReturnAsync(Connection context)
        {
            // TODO handle context being invalid.
            lock (_syncObj)
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

        private bool TransferConnection(Connection connection)
        {
            while (HasWaiter())
            {
                TaskCompletionSource<Connection> waiter = DequeueWaiter();

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

        public ValueTask<Connection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new ValueTask<Connection>(Task.FromCanceled<Connection>(cancellationToken));
            }

            // TODO figure out how to get a connection lifetime via settings.
            var list = _idleConnections;
            List<Connection> _connectionsToDispose = null;
            async ValueTask<Connection> DisposeConnectionsAndReturn(List<Connection> toDispose, Connection context)
            {
                foreach (var disposeContext in toDispose)
                {
                    await disposeContext.DisposeAsync();
                }
                return context;
            }

            async ValueTask<Connection> GetConnectionAsync(EndPoint endpoint, CancellationToken token)
            {
                IncrementConnectionCount();

                var Connection = new PooledConnection(await _connectionFactory.ConnectAsync(_endPoint, options: null, cancellationToken: token), this);
                if (Connection == null)
                {
                    DecrementConnectionCount();
                }

                return Connection;
            }

            while (true)
            {
                lock (_syncObj)
                {
                    if (list.Count > 0)
                    {
                        var cachedConnection = list[list.Count - 1];
                        list.RemoveAt(list.Count - 1);

                        // TODO check if the connection is expired. 
                        //if (!cachedConnection.ConnectionClosed.IsCancellationRequested)
                        //{
                        //    if (_connectionsToDispose != null)
                        //    {
                        //        return DisposeConnectionsAndReturn(_connectionsToDispose, cachedConnection);
                        //    }

                        //    return new ValueTask<Connection>(cachedConnection);
                        //}
                        //else
                        //{
                        if (_connectionsToDispose == null)
                        {
                            _connectionsToDispose = new List<Connection>();
                        }
                        _connectionsToDispose.Add(cachedConnection);
                        //}
                    }
                    else
                    {
                        if (_connectionCount < _maxConnectionCount)
                        {
                            // TODO wrap Connection with a pooled version which returns to the pool.
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

        private TaskCompletionSourceWithCancellation<Connection> EnqueueWaiter()
        {
            if (_waiters == null)
            {
                _waiters = new Queue<TaskCompletionSourceWithCancellation<Connection>>();
            }

            var waiter = new TaskCompletionSourceWithCancellation<Connection>();
            _waiters.Enqueue(waiter);
            return waiter;
        }

        private bool HasWaiter()
        {
            return (_waiters != null && _waiters.Count > 0);
        }

        /// <summary>Dequeues a waiter from the waiters list.  The list must not be empty.</summary>
        /// <returns>The dequeued waiter.</returns>
        private TaskCompletionSourceWithCancellation<Connection> DequeueWaiter()
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
