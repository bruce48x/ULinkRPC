using System;
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
            WebSocket,
            Kcp
        }

        [Header("Transport")] public TransportMode Kind = TransportMode.Tcp;

        public string Host = "127.0.0.1";
        public int Port = 20000;
        public string WsUrl = "ws://127.0.0.1:20001/rpc";

        [Header("Login")] public string Account = "a";

        public string Password = "b";

        public bool AutoConnect = true;

        private RpcClient? _client;
        private CancellationTokenSource? _cts;
        private bool _disconnectedDuringConnect;
        private IPlayerService? _proxy;
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
            if (_client is not null)
            {
                Debug.LogWarning("RpcConnectionTester already connected.");
                return;
            }

            try
            {
                UpdateStatus(ConnectionState.Connecting, "Connecting...");
                _cts = new CancellationTokenSource();
                var transport = CreateTransport();
                var serializer = new MemoryPackRpcSerializer();
                _client = new RpcClient(transport, serializer);
                _client.Disconnected += OnClientDisconnected;
                PlayerCallbackBinder.Bind(_client, this);
                await _client.StartAsync(_cts.Token);
                UpdateStatus(ConnectionState.Connected, "Connected");

                if (_disconnectedDuringConnect)
                    throw new InvalidOperationException("Connection closed.");

                var rpcApi = _client.CreateRpcApi();
                _proxy = rpcApi.Game.Player;

                var reply = await _proxy.LoginAsync(new LoginRequest
                {
                    Account = Account,
                    Password = Password
                });

                Debug.Log($"Login ok: code={reply.Code}, token={reply.Token}");

                await _proxy.PingAsync();
                Debug.Log("Ping ok.");

                var greeting = await _proxy.ComposeGreetingAsync(Account, 10, true);
                Debug.Log($"Multi-arg rpc ok: {greeting}");

                if (_disconnectedDuringConnect)
                {
                    _disconnectedDuringConnect = false;
                    await CleanupAsync(false);
                }
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
            if (_client is null)
                return;

            try
            {
                _client.Disconnected -= OnClientDisconnected;
                await _client.DisposeAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"RPC cleanup error: {ex}");
            }
            finally
            {
                _client = null;
                _proxy = null;
                if (_cts is not null)
                {
                    _cts.Dispose();
                    _cts = null;
                }
            }

            if (notifyDisconnected)
                UpdateStatus(ConnectionState.Disconnected, "Disconnected");
        }

        private void OnClientDisconnected(Exception? ex)
        {
            var wasConnecting = _state == ConnectionState.Connecting;
            if (ex is null)
                UpdateStatus(ConnectionState.Disconnected, "Disconnected");
            else
                UpdateStatus(ConnectionState.Error, ex.Message);

            if (wasConnecting)
                _disconnectedDuringConnect = true;
            else
                _ = CleanupAsync(false);
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
                TransportMode.WebSocket => throw new NotSupportedException("WebSocket client transport not implemented yet."),
                TransportMode.Kcp => throw new NotSupportedException("KCP client transport not implemented yet."),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}
