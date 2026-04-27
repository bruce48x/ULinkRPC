using System.Net;

namespace ULinkRPC.Transport.Kcp;

public delegate ValueTask<bool> KcpHandshakeAdmission(uint conversationId, IPEndPoint remoteEndPoint, CancellationToken ct);
