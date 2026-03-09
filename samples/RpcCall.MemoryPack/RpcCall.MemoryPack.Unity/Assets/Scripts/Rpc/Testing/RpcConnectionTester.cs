using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Rpc.Contracts;
using Rpc.Generated;
using ULinkRPC.Client;
using ULinkRPC.Serializer.MemoryPack;
using ULinkRPC.Transport.Tcp;
using UnityEngine;

namespace Rpc.Testing
{
    public sealed class RpcConnectionTester : MonoBehaviour, IPlayerCallback
    {
        public enum ConnectionState
        {
            Idle,
            Connecting,
            Connected,
            Disconnected,
            Error
        }

        public enum TransportMode
        {
            Tcp,
            Kcp
        }

        [Header("Transport")] public TransportMode Kind = TransportMode.Tcp;

        public string Host = "127.0.0.1";
        public int Port = 20000;
        [Header("Login")] public string Account = "a";

        public string Password = "b";

        [Header("Multi Connection")] public int ConnectionCount = 3;
        public float RequestIntervalSeconds = 1f;

        public bool AutoConnect = true;

        private readonly List<ConnectionSession> _sessions = new();
        private ConnectionState _state = ConnectionState.Idle;

        private async void Start()
        {
            if (!AutoConnect)
                return;

            await ConnectAndTestAsync();
        }

        private async void OnDestroy()
        {
            await CleanupAsync(true);
        }

        public event Action<ConnectionState, string?>? StatusChanged;

        public async Task ConnectAndTestAsync()
        {
            if (_sessions.Count > 0)
            {
                Debug.LogWarning("RpcConnectionTester already connected.");
                return;
            }

            try
            {
                UpdateStatus(ConnectionState.Connecting, "Connecting...");
                var targetCount = Mathf.Max(1, ConnectionCount);

                for (var i = 0; i < targetCount; i++)
                {
                    var session = new ConnectionSession(this, i);
                    _sessions.Add(session);
                    await session.StartAsync();
                }

                UpdateStatus(ConnectionState.Connected, $"Connected x{_sessions.Count}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"RPC test failed: {ex}");
                UpdateStatus(ConnectionState.Error, ex.Message);
                await CleanupAsync(false);
            }
        }

        public void ConnectFromUi()
        {
            _ = ConnectAndTestAsync();
        }

        public void OnNotify(string message)
        {
            Debug.Log($"Server push: {message}");
        }

        private async Task CleanupAsync(bool notifyDisconnected)
        {
            var sessions = _sessions.ToArray();
            _sessions.Clear();

            foreach (var session in sessions)
            {
                try
                {
                    await session.DisposeAsync();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"RPC cleanup error: {ex}");
                }
            }

            if (notifyDisconnected)
                UpdateStatus(ConnectionState.Disconnected, "Disconnected");
        }

        private void OnSessionDisconnected(ConnectionSession session, Exception? ex)
        {
            _sessions.Remove(session);

            if (ex is null)
                Debug.LogWarning($"Session[{session.Index}] disconnected.");
            else
                Debug.LogError($"Session[{session.Index}] disconnected: {ex.Message}");

            if (_state == ConnectionState.Connecting)
            {
                UpdateStatus(ConnectionState.Error, "Connection closed during startup.");
                return;
            }

            if (_sessions.Count == 0)
                UpdateStatus(ex is null ? ConnectionState.Disconnected : ConnectionState.Error, ex?.Message ?? "Disconnected");
        }

        private void UpdateStatus(ConnectionState state, string? message)
        {
            _state = state;
            StatusChanged?.Invoke(state, message);
        }

        private ULinkRPC.Core.ITransport CreateTransport()
        {
            return Kind switch
            {
                TransportMode.Tcp => new TcpTransport(Host, Port),
                TransportMode.Kcp => throw new NotSupportedException("KCP client transport not implemented yet."),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private sealed class ConnectionSession : IPlayerCallback, IAsyncDisposable
        {
            private readonly RpcConnectionTester _owner;
            private readonly CancellationTokenSource _cts = new();
            private bool _disposed;
            private bool _stopped;
            private RpcClient? _client;
            private IPlayerService? _proxy;
            private Task? _pollingTask;

            public ConnectionSession(RpcConnectionTester owner, int index)
            {
                _owner = owner;
                Index = index;
            }

            public int Index { get; }

            public async Task StartAsync()
            {
                var transport = _owner.CreateTransport();
                var serializer = new MemoryPackRpcSerializer();
                _client = new RpcClient(transport, serializer);
                _client.Disconnected += OnDisconnected;
                PlayerCallbackBinder.Bind(_client, this);

                await _client.StartAsync(_cts.Token);
                var rpcApi = _client.CreateRpcApi();
                _proxy = rpcApi.Game.Player;

                var account = $"{_owner.Account}-{Index + 1}";
                var reply = await _proxy.LoginAsync(new LoginRequest
                {
                    Account = account,
                    Password = _owner.Password
                });

                Debug.Log($"Session[{Index}] login ok: code={reply.Code}, token={reply.Token}");
                _pollingTask = RunPollingAsync(account);
            }

            public void OnNotify(string message)
            {
                Debug.Log($"Session[{Index}] server push: {message}");
            }

            public async ValueTask DisposeAsync()
            {
                if (_disposed)
                    return;

                _disposed = true;
                _cts.Cancel();
                _stopped = true;

                if (_client is not null)
                    _client.Disconnected -= OnDisconnected;

                if (_pollingTask is not null)
                {
                    try
                    {
                        await _pollingTask;
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }

                if (_client is not null)
                    await _client.DisposeAsync();

                _cts.Dispose();
            }

            private async Task RunPollingAsync(string account)
            {
                var interval = Mathf.Max(0.1f, _owner.RequestIntervalSeconds);

                while (!_cts.IsCancellationRequested)
                {
                    var step = await _proxy!.IncrStep();
                    Debug.Log($"Session[{Index}] {account} step={step}");
                    await Task.Delay(TimeSpan.FromSeconds(interval), _cts.Token);
                }
            }

            private void OnDisconnected(Exception? ex)
            {
                if (_stopped)
                    return;

                _stopped = true;
                _owner.OnSessionDisconnected(this, ex);
            }
        }
    }
}
