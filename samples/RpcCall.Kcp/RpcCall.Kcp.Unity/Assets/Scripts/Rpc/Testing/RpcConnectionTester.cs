using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Game.Rpc.Contracts;
using Rpc.Generated;
using ULinkRPC.Client;
using ULinkRPC.Serializer.MemoryPack;
using ULinkRPC.Transport.Kcp;
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

        public string Host = "127.0.0.1";
        public int Port = 20000;
        [Header("Login")] public string Account = "a";

        public string Password = "b";

        [Header("Multi Connection")] public int ConnectionCount = 3;
        public float RequestIntervalSeconds = 1f;

        [Header("Debug UI")] public bool ShowDebugPanel = true;

        public bool AutoConnect = true;

        private const int MaxLogEntries = 24;
        private readonly Dictionary<int, SessionSnapshot> _sessionSnapshots = new();
        private readonly List<ConnectionSession> _sessions = new();
        private readonly List<string> _logEntries = new();
        private ConnectionState _state = ConnectionState.Idle;
        private Vector2 _logScroll;

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
            AppendLog($"Server push: {message}");
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
                AppendLog($"Session[{session.Index}] disconnected.");
            else
                AppendLog($"Session[{session.Index}] disconnected: {ex.Message}");

            UpdateSession(session.Index, snapshot =>
            {
                snapshot.State = ex is null ? "Disconnected" : "Error";
                if (ex is not null)
                    snapshot.LastMessage = ex.Message;
            });

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
            if (!string.IsNullOrWhiteSpace(message))
                AppendLog($"State => {state}: {message}");
        }

        private ULinkRPC.Core.ITransport CreateTransport()
        {
            return new KcpTransport(Host, Port);
        }

        private void OnGUI()
        {
            if (!ShowDebugPanel)
                return;

            var area = new Rect(16f, 16f, Mathf.Min(Screen.width - 32f, 720f), Mathf.Min(Screen.height - 32f, 520f));
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("RpcCall.Kcp Runtime");
            GUILayout.Label($"State: {_state}");
            GUILayout.Label("Transport: KCP");
            GUILayout.Label($"Endpoint: {Host}:{Port}");
            GUILayout.Label($"Connections: {_sessions.Count}/{Mathf.Max(1, ConnectionCount)}");
            GUILayout.Label($"Interval: {RequestIntervalSeconds:0.##}s");

            if ((_state == ConnectionState.Idle || _state == ConnectionState.Disconnected || _state == ConnectionState.Error) &&
                GUILayout.Button("Connect"))
            {
                ConnectFromUi();
            }

            GUILayout.Space(8f);
            GUILayout.Label("Sessions");
            foreach (var pair in _sessionSnapshots)
            {
                var snapshot = pair.Value;
                var line = new StringBuilder()
                    .Append('[').Append(snapshot.Index).Append("] ")
                    .Append(snapshot.Account ?? "?")
                    .Append(" | ").Append(snapshot.State ?? "Idle")
                    .Append(" | step=").Append(snapshot.LastStep);

                if (!string.IsNullOrWhiteSpace(snapshot.LastMessage))
                    line.Append(" | ").Append(snapshot.LastMessage);

                GUILayout.Label(line.ToString());
            }

            GUILayout.Space(8f);
            GUILayout.Label("Recent Logs");
            _logScroll = GUILayout.BeginScrollView(_logScroll, GUILayout.Height(220f));
            foreach (var entry in _logEntries)
                GUILayout.Label(entry);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void UpdateSession(int index, Action<SessionSnapshot> update)
        {
            if (!_sessionSnapshots.TryGetValue(index, out var snapshot))
            {
                snapshot = new SessionSnapshot { Index = index };
                _sessionSnapshots[index] = snapshot;
            }

            update(snapshot);
        }

        private void AppendLog(string message)
        {
            Debug.Log(message);
            _logEntries.Add(message);
            if (_logEntries.Count > MaxLogEntries)
                _logEntries.RemoveAt(0);
        }

        private sealed class SessionSnapshot
        {
            public string? Account;
            public int Index;
            public string? LastMessage;
            public int LastStep;
            public string? State;
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

                _owner.UpdateSession(Index, snapshot =>
                {
                    snapshot.Account = account;
                    snapshot.State = "Connected";
                    snapshot.LastMessage = $"token={reply.Token}";
                });
                _owner.AppendLog($"Session[{Index}] login ok: code={reply.Code}, token={reply.Token}");
                _pollingTask = RunPollingAsync(account);
            }

            public void OnNotify(string message)
            {
                _owner.UpdateSession(Index, snapshot => snapshot.LastMessage = message);
                _owner.AppendLog($"Session[{Index}] server push: {message}");
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
                    _owner.UpdateSession(Index, snapshot =>
                    {
                        snapshot.Account = account;
                        snapshot.LastStep = step;
                        snapshot.State = "Polling";
                    });
                    _owner.AppendLog($"Session[{Index}] {account} step={step}");
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
