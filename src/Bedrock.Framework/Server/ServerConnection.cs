using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Connections;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.Extensions.Logging;

namespace Bedrock.Framework
{
    internal class ServerConnection : IConnectionProperties, IConnectionHeartbeatFeature, IConnectionCompleteFeature, IConnectionLifetimeNotificationFeature, IReadOnlyList<KeyValuePair<string, object>>
    {
        private List<(Action<object> handler, object state)> _heartbeatHandlers;
        private readonly object _heartbeatLock = new object();

        private Stack<KeyValuePair<Func<object, Task>, object>> _onCompleted;
        private bool _completed;
        private readonly CancellationTokenSource _connectionClosingCts = new CancellationTokenSource();
        private Connection _connection;

        public ServerConnection(long id, Connection connection, ILogger logger)
        {
            Id = id;
            Logger = logger;
            _connection = connection;
            TransportConnection = Connection.FromPipe(connection.Pipe, leaveOpen: false, this, connection.LocalEndPoint, connection.RemoteEndPoint);
            ConnectionClosedRequested = _connectionClosingCts.Token;
        }

        private ILogger Logger { get; }
        public long Id { get; }
        public Connection TransportConnection { get; }

        public CancellationToken ConnectionClosedRequested { get; set; }

        public void TickHeartbeat()
        {
            lock (_heartbeatLock)
            {
                if (_heartbeatHandlers == null)
                {
                    return;
                }

                foreach (var (handler, state) in _heartbeatHandlers)
                {
                    handler(state);
                }
            }
        }

        public void OnHeartbeat(Action<object> action, object state)
        {
            lock (_heartbeatLock)
            {
                if (_heartbeatHandlers == null)
                {
                    _heartbeatHandlers = new List<(Action<object> handler, object state)>();
                }

                _heartbeatHandlers.Add((action, state));
            }
        }

        void IConnectionCompleteFeature.OnCompleted(Func<object, Task> callback, object state)
        {
            if (_completed)
            {
                throw new InvalidOperationException("The connection is already complete.");
            }

            if (_onCompleted == null)
            {
                _onCompleted = new Stack<KeyValuePair<Func<object, Task>, object>>();
            }
            _onCompleted.Push(new KeyValuePair<Func<object, Task>, object>(callback, state));
        }

        public Task FireOnCompletedAsync()
        {
            if (_completed)
            {
                throw new InvalidOperationException("The connection is already complete.");
            }

            _completed = true;
            var onCompleted = _onCompleted;

            if (onCompleted == null || onCompleted.Count == 0)
            {
                return Task.CompletedTask;
            }

            return CompleteAsyncMayAwait(onCompleted);
        }

        private Task CompleteAsyncMayAwait(Stack<KeyValuePair<Func<object, Task>, object>> onCompleted)
        {
            while (onCompleted.TryPop(out var entry))
            {
                try
                {
                    var task = entry.Key.Invoke(entry.Value);
                    if (!task.IsCompletedSuccessfully)
                    {
                        return CompleteAsyncAwaited(task, onCompleted);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "An error occured running an IConnectionCompleteFeature.OnCompleted callback.");
                }
            }

            return Task.CompletedTask;
        }

        private async Task CompleteAsyncAwaited(Task currentTask, Stack<KeyValuePair<Func<object, Task>, object>> onCompleted)
        {
            try
            {
                await currentTask.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An error occured running an IConnectionCompleteFeature.OnCompleted callback.");
            }

            while (onCompleted.TryPop(out var entry))
            {
                try
                {
                    await entry.Key.Invoke(entry.Value).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "An error occured running an IConnectionCompleteFeature.OnCompleted callback.");
                }
            }
        }

        public void RequestClose()
        {
            _connectionClosingCts.Cancel();
        }

        // For logging to get the connection data
        public KeyValuePair<string, object> this[int index]
        {
            get
            {
                if (index == 0)
                {
                    return new KeyValuePair<string, object>("ConnectionId", TransportConnection.LocalEndPoint.ToString());
                }

                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        public int Count => 1;

        public EndPoint LocalEndPoint
        {
            get => TransportConnection.LocalEndPoint;
        }
        public EndPoint RemoteEndPoint
        {
            get => TransportConnection.RemoteEndPoint;
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            for (int i = 0; i < Count; ++i)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool TryGet(Type propertyKey, [NotNullWhen(true)] out object property)
        {
            if (propertyKey == typeof(IConnectionHeartbeatFeature) ||
                propertyKey == typeof(IConnectionCompleteFeature) ||
                propertyKey == typeof(IConnectionLifetimeNotificationFeature))
            {
                property = this;
                return true;
            }
            
            return _connection.ConnectionProperties.TryGet(propertyKey, out property);
        }
    }
}
