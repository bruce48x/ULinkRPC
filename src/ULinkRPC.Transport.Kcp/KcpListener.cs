using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace ULinkRPC.Transport.Kcp
{
    public sealed class KcpListener : IAsyncDisposable
    {
        private readonly Channel<KcpAcceptResult> _accepted = Channel.CreateUnbounded<KcpAcceptResult>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = true
        });
        private readonly CancellationTokenSource _cts = new();
        private readonly ConcurrentDictionary<string, SessionRecord> _sessions = new();
        private readonly Socket _socket;
        private readonly Task _receiveLoop;

        public KcpListener(int port)
            : this(new IPEndPoint(IPAddress.Any, port))
        {
        }

        public KcpListener(IPEndPoint endPoint)
        {
            if (endPoint is null)
                throw new ArgumentNullException(nameof(endPoint));

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.Bind(endPoint);
            _receiveLoop = Task.Run(ReceiveLoopAsync);
        }

        public EndPoint? LocalEndPoint => _socket.LocalEndPoint;

        public async ValueTask<KcpAcceptResult> AcceptAsync(CancellationToken ct = default)
        {
            return await _accepted.Reader.ReadAsync(ct).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            _accepted.Writer.TryComplete();
            _socket.Dispose();

            try
            {
                await _receiveLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }

            var sessions = _sessions.Values.Select(record => record.Transport).Distinct().ToArray();
            _sessions.Clear();
            foreach (var transport in sessions)
                await transport.DisposeAsync().ConfigureAwait(false);

            _cts.Dispose();
        }

        private async Task ReceiveLoopAsync()
        {
            var buffer = new byte[2048];
            EndPoint any = new IPEndPoint(IPAddress.Any, 0);
            var localPort = ((IPEndPoint)_socket.LocalEndPoint!).Port;

            while (!_cts.IsCancellationRequested)
            {
                SocketReceiveFromResult received;
#if NET8_0_OR_GREATER
                try
                {
                    received = await _socket.ReceiveFromAsync(buffer, SocketFlags.None, any, _cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
#else
                received = await _socket.ReceiveFromAsync(new ArraySegment<byte>(buffer), SocketFlags.None, any).ConfigureAwait(false);
#endif
                if (received.RemoteEndPoint is not IPEndPoint remoteEndPoint)
                    continue;

                var packet = buffer.AsMemory(0, received.ReceivedBytes);
                var key = remoteEndPoint.ToString();
                if (!KcpHandshake.TryParseRequest(packet.Span, out var conv))
                {
                    if (_sessions.TryGetValue(key, out var existingSession))
                        existingSession.Transport.ProcessDatagram(packet.Span);

                    continue;
                }

                if (_sessions.TryGetValue(key, out var existing))
                {
                    var ack = KcpHandshake.CreateAck(existing.ConversationId, localPort);
#if NET8_0_OR_GREATER
                    await _socket.SendToAsync(ack, SocketFlags.None, remoteEndPoint, _cts.Token).ConfigureAwait(false);
#else
                    await _socket.SendToAsync(new ArraySegment<byte>(ack), SocketFlags.None, remoteEndPoint).ConfigureAwait(false);
#endif
                    continue;
                }

                var transport = new KcpServerTransport(
                    _socket,
                    remoteEndPoint,
                    conv,
                    onDispose: () => _sessions.TryRemove(key, out _));
                await transport.ConnectAsync(_cts.Token).ConfigureAwait(false);
                var sessionAck = KcpHandshake.CreateAck(conv, localPort);
#if NET8_0_OR_GREATER
                await _socket.SendToAsync(sessionAck, SocketFlags.None, remoteEndPoint, _cts.Token).ConfigureAwait(false);
#else
                await _socket.SendToAsync(new ArraySegment<byte>(sessionAck), SocketFlags.None, remoteEndPoint).ConfigureAwait(false);
#endif

                var record = new SessionRecord(conv, transport);
                _sessions[key] = record;
                _accepted.Writer.TryWrite(new KcpAcceptResult(transport, remoteEndPoint, conv, localPort));
                continue;
            }
        }

        private sealed class SessionRecord
        {
            public SessionRecord(uint conversationId, KcpServerTransport transport)
            {
                ConversationId = conversationId;
                Transport = transport;
            }

            public uint ConversationId { get; }
            public KcpServerTransport Transport { get; }
        }
    }
}
