using System.Security.Cryptography;
using ULinkRPC.Core;

namespace ULinkRPC.Tests;

public class TransportFrameCodecTests
{
    private static byte[] GenerateKey() => RandomNumberGenerator.GetBytes(32);

    private static TransportSecurityConfig NoSecurityConfig() => new();

    private static TransportSecurityConfig CompressionOnlyConfig(int threshold = 0) => new()
    {
        EnableCompression = true,
        CompressionThresholdBytes = threshold
    };

    private static TransportSecurityConfig EncryptionOnlyConfig() => new()
    {
        EnableEncryption = true,
        EncryptionKey = GenerateKey()
    };

    private static TransportSecurityConfig FullSecurityConfig(int threshold = 0)
    {
        return new TransportSecurityConfig
        {
            EnableCompression = true,
            CompressionThresholdBytes = threshold,
            EnableEncryption = true,
            EncryptionKey = GenerateKey()
        };
    }

    [Fact]
    public void Decode_NoSecurity_ReturnsCopy_NotOriginalReference()
    {
        var codec = new TransportFrameCodec(NoSecurityConfig());
        var original = new byte[] { 1, 2, 3, 4, 5 };
        ReadOnlyMemory<byte> input = original;

        var decoded = codec.Decode(input);

        Assert.Equal(original, decoded.ToArray());
        Assert.False(decoded.Span == input.Span,
            "Decode should return a copy, not the same memory");
    }

    [Fact]
    public void Encode_NoSecurity_ReturnsCopy()
    {
        var codec = new TransportFrameCodec(NoSecurityConfig());
        var original = new byte[] { 1, 2, 3 };

        var encoded = codec.Encode(original);
        Assert.Equal(original, encoded.ToArray());
    }

    [Fact]
    public void RoundTrip_NoSecurity()
    {
        var codec = new TransportFrameCodec(NoSecurityConfig());
        var data = new byte[] { 10, 20, 30 };

        var encoded = codec.Encode(data);
        var decoded = codec.Decode(encoded);

        Assert.Equal(data, decoded.ToArray());
    }

    [Fact]
    public void RoundTrip_CompressionOnly_SmallData_UnderThreshold()
    {
        var codec = new TransportFrameCodec(CompressionOnlyConfig(threshold: 1024));
        var data = new byte[] { 1, 2, 3, 4, 5 };

        var encoded = codec.Encode(data);
        var decoded = codec.Decode(encoded);

        Assert.Equal(data, decoded.ToArray());
    }

    [Fact]
    public void RoundTrip_CompressionOnly_LargeData()
    {
        var codec = new TransportFrameCodec(CompressionOnlyConfig(threshold: 0));
        var data = new byte[4096];
        Array.Fill(data, (byte)0xAB);

        var encoded = codec.Encode(data);
        var decoded = codec.Decode(encoded);

        Assert.Equal(data, decoded.ToArray());
        Assert.True(encoded.Length < data.Length,
            "Highly compressible data should result in smaller encoded output");
    }

    [Fact]
    public void RoundTrip_EncryptionOnly()
    {
        var config = EncryptionOnlyConfig();
        var codec = new TransportFrameCodec(config);
        var data = new byte[] { 100, 200, 255, 0, 1 };

        var encoded = codec.Encode(data);
        var decoded = codec.Decode(encoded);

        Assert.Equal(data, decoded.ToArray());
        Assert.NotEqual(data, encoded.ToArray());
    }

    [Fact]
    public void RoundTrip_FullSecurity()
    {
        var config = FullSecurityConfig(threshold: 0);
        var codec = new TransportFrameCodec(config);
        var data = new byte[2048];
        new Random(42).NextBytes(data);

        var encoded = codec.Encode(data);
        var decoded = codec.Decode(encoded);

        Assert.Equal(data, decoded.ToArray());
    }

    [Fact]
    public void Decrypt_TamperedData_ThrowsAuthFailure()
    {
        var config = EncryptionOnlyConfig();
        var codec = new TransportFrameCodec(config);
        var data = new byte[] { 1, 2, 3 };

        var encoded = codec.Encode(data).ToArray();
        encoded[20] ^= 0xFF;

        Assert.Throws<InvalidOperationException>(() => codec.Decode(encoded));
    }

    [Fact]
    public void Decrypt_TooSmallFrame_Throws()
    {
        var config = EncryptionOnlyConfig();
        var codec = new TransportFrameCodec(config);

        Assert.Throws<InvalidOperationException>(() =>
            codec.Decode(new byte[10]));
    }

    [Fact]
    public void Constructor_EncryptionEnabledWithoutKey_Throws()
    {
        var config = new TransportSecurityConfig
        {
            EnableEncryption = true
        };

        Assert.Throws<InvalidOperationException>(() => new TransportFrameCodec(config));
    }

    [Fact]
    public void Constructor_NullConfig_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new TransportFrameCodec(null!));
    }

    [Fact]
    public void RoundTrip_EmptyPayload()
    {
        var codec = new TransportFrameCodec(NoSecurityConfig());
        var data = Array.Empty<byte>();

        var encoded = codec.Encode(data);
        var decoded = codec.Decode(encoded);

        Assert.Empty(decoded.ToArray());
    }

    [Fact]
    public void DifferentKeys_CannotDecrypt()
    {
        var codec1 = new TransportFrameCodec(new TransportSecurityConfig
        {
            EnableEncryption = true,
            EncryptionKey = GenerateKey()
        });
        var codec2 = new TransportFrameCodec(new TransportSecurityConfig
        {
            EnableEncryption = true,
            EncryptionKey = GenerateKey()
        });

        var data = new byte[] { 1, 2, 3, 4 };
        var encoded = codec1.Encode(data);

        Assert.ThrowsAny<Exception>(() => codec2.Decode(encoded));
    }
}
