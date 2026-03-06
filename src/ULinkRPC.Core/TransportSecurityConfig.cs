using System;

namespace ULinkRPC.Core
{
    public sealed class TransportSecurityConfig
    {
        public bool EnableCompression { get; set; }
        public int CompressionThresholdBytes { get; set; } = 1024;
        public bool EnableEncryption { get; set; }

        public byte[]? EncryptionKey { get; set; }
        public string? EncryptionKeyBase64 { get; set; }

        public bool IsEnabled => EnableCompression || EnableEncryption;

        public byte[]? ResolveKey()
        {
            if (EncryptionKey is { Length: > 0 })
                return EncryptionKey;

            if (!string.IsNullOrWhiteSpace(EncryptionKeyBase64))
                return Convert.FromBase64String(EncryptionKeyBase64);

            return null;
        }
    }
}
