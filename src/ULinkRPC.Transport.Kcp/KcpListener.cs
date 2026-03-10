using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace ULinkRPC.Transport.Kcp
{
    public sealed class KcpListener : IAsyncDisposable
    {
        private readonly ConcurrentDictionary<string, SessionRecord> _sessions = new();
        private readonly Socket _socket;

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
        }

        public EndPoint? LocalEndPoint => _socket.LocalEndPoint;

        public async ValueTask<KcpAcceptResult> AcceptAsync(CancellationToken ct = default)
        {
            var buffer = new byte[2048];
            EndPoint any = new IPEndPoint(IPAddress.Any, 0);

            while (true)
            {
#if NET8_0_OR_GREATER
                var received = await _socket.ReceiveFromAsync(buffer, SocketFlags.None, any, ct).ConfigureAwait(false);
#else
                var received = await _socket.ReceiveFromAsync(new ArraySegment<byte>(buffer), SocketFlags.None, any).ConfigureAwait(false);
#endif
                if (received.RemoteEndPoint is not IPEndPoint remoteEndPoint)
                    continue;

                var packet = buffer.AsMemory(0, received.ReceivedBytes);
                if (!KcpHandshake.TryParseRequest(packet.Span, out var conv))
                    continue;

                var key = remoteEndPoint.ToString();
                if (_sessions.TryGetValue(key, out var existing))
                {
                    var ack = KcpHandshake.CreateAck(existing.ConversationId, existing.Port);
#if NET8_0_OR_GREATER
                    await _socket.SendToAsync(ack, SocketFlags.None, remoteEndPoint, ct).ConfigureAwait(false);
#else
                    await _socket.SendToAsync(new ArraySegment<byte>(ack), SocketFlags.None, remoteEndPoint).ConfigureAwait(false);
#endif
                    continue;
                }

                var sessionSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                sessionSocket.Bind(new IPEndPoint(IPAddress.Any, 0));
                var sessionPort = ((IPEndPoint)sessionSocket.LocalEndPoint!).Port;

                var sessionAck = KcpHandshake.CreateAck(conv, sessionPort);
#if NET8_0_OR_GREATER
                await _socket.SendToAsync(sessionAck, SocketFlags.None, remoteEndPoint, ct).ConfigureAwait(false);
#else
                await _socket.SendToAsync(new ArraySegment<byte>(sessionAck), SocketFlags.None, remoteEndPoint).ConfigureAwait(false);
#endif

                var record = new SessionRecord(conv, sessionPort);
                _sessions[key] = record;

                var transport = new KcpServerTransport(
                    sessionSocket,
                    remoteEndPoint,
                    conv,
                    ReadOnlyMemory<byte>.Empty,
                    ownsSocket: true,
                    onDispose: () => _sessions.TryRemove(key, out _));

                return new KcpAcceptResult(transport, remoteEndPoint, conv, sessionPort);
            }
        }

        public ValueTask DisposeAsync()
        {
            _socket.Dispose();
            return default;
        }

        private sealed class SessionRecord
        {
            public SessionRecord(uint conversationId, int port)
            {
                ConversationId = conversationId;
                Port = port;
            }

            public uint ConversationId { get; }
            public int Port { get; }
        }
    }
}
