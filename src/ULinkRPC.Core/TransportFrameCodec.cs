using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace ULinkRPC.Core
{
    public sealed class TransportFrameCodec
    {
        private const byte FlagCompressed = 1;
        private const int IvSize = 16;
        private const int HmacSize = 32;
        private const int CopyBufferSize = 8192;

        private readonly bool _compressEnabled;
        private readonly int _compressThreshold;
        private readonly int _maxDecompressedFrameBytes;
        private readonly bool _encryptEnabled;
        private readonly byte[]? _encKey;
        private readonly byte[]? _macKey;

        public TransportFrameCodec(TransportSecurityConfig config)
        {
            if (config is null) throw new ArgumentNullException(nameof(config));

            _compressEnabled = config.EnableCompression;
            _compressThreshold = Math.Max(0, config.CompressionThresholdBytes);
            _maxDecompressedFrameBytes = config.MaxDecompressedFrameBytes > 0
                ? config.MaxDecompressedFrameBytes
                : throw new ArgumentOutOfRangeException(nameof(config), "MaxDecompressedFrameBytes must be positive.");
            _encryptEnabled = config.EnableEncryption;

            if (_encryptEnabled)
            {
                var master = config.ResolveKey();
                if (master is null || master.Length == 0)
                    throw new InvalidOperationException("Encryption enabled but no key provided.");

                _encKey = DeriveKey(master, "enc");
                _macKey = DeriveKey(master, "mac");
            }
        }

        public ReadOnlyMemory<byte> Encode(ReadOnlyMemory<byte> frame)
        {
            if (!_compressEnabled && !_encryptEnabled)
                return frame.ToArray();

            var payload = frame.ToArray();
            var flags = (byte)0;

            if (_compressEnabled && payload.Length >= _compressThreshold)
            {
                var compressed = Compress(payload);
                if (compressed.Length < payload.Length)
                {
                    payload = compressed;
                    flags |= FlagCompressed;
                }
            }

            var header = new byte[1 + payload.Length];
            header[0] = flags;
            Buffer.BlockCopy(payload, 0, header, 1, payload.Length);

            if (_encryptEnabled)
                return Encrypt(header);

            return header;
        }

        public ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> frame)
        {
            if (!_compressEnabled && !_encryptEnabled)
                return frame.ToArray();

            var payload = _encryptEnabled ? Decrypt(frame) : frame.ToArray();

            if (payload.Length < 1)
                throw new InvalidOperationException("Security header missing.");

            var flags = payload[0];
            var body = new byte[payload.Length - 1];
            Buffer.BlockCopy(payload, 1, body, 0, body.Length);

            if (_compressEnabled && (flags & FlagCompressed) != 0)
                return Decompress(body, _maxDecompressedFrameBytes);

            return body;
        }

        private static byte[] Compress(byte[] data)
        {
            using var ms = new MemoryStream();
            using (var gz = new GZipStream(ms, CompressionLevel.Fastest, leaveOpen: true))
            {
                gz.Write(data, 0, data.Length);
            }
            return ms.ToArray();
        }

        private static byte[] Decompress(byte[] data, int maxOutputBytes)
        {
            using var input = new MemoryStream(data);
            using var gz = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[CopyBufferSize];
            var total = 0;

            while (true)
            {
                var read = gz.Read(buffer, 0, buffer.Length);
                if (read == 0)
                    break;

                total += read;
                if (total > maxOutputBytes)
                    throw new InvalidOperationException(
                        $"Decompressed frame exceeds configured limit of {maxOutputBytes} bytes.");

                output.Write(buffer, 0, read);
            }

            return output.ToArray();
        }

        private byte[] Encrypt(byte[] plaintext)
        {
            var iv = new byte[IvSize];
            RandomNumberGenerator.Fill(iv);

            byte[] ciphertext;
            using (var aes = Aes.Create())
            {
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = _encKey!;
                aes.IV = iv;

                using var enc = aes.CreateEncryptor();
                ciphertext = enc.TransformFinalBlock(plaintext, 0, plaintext.Length);
            }

            var tag = ComputeHmac(iv, ciphertext);

            var output = new byte[iv.Length + ciphertext.Length + tag.Length];
            Buffer.BlockCopy(iv, 0, output, 0, iv.Length);
            Buffer.BlockCopy(ciphertext, 0, output, iv.Length, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, output, iv.Length + ciphertext.Length, tag.Length);
            return output;
        }

        private byte[] Decrypt(ReadOnlyMemory<byte> data)
        {
            if (data.Length < IvSize + HmacSize)
                throw new InvalidOperationException("Encrypted frame too small.");

            var iv = data.Slice(0, IvSize).ToArray();
            var tag = data.Slice(data.Length - HmacSize, HmacSize).ToArray();
            var ciphertext = data.Slice(IvSize, data.Length - IvSize - HmacSize).ToArray();

            var expected = ComputeHmac(iv, ciphertext);
            if (!FixedTimeEquals(expected, tag))
                throw new InvalidOperationException("Encrypted frame authentication failed.");

            using var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = _encKey!;
            aes.IV = iv;
            using var dec = aes.CreateDecryptor();
            return dec.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
        }

        private byte[] ComputeHmac(byte[] iv, byte[] ciphertext)
        {
            using var hmac = new HMACSHA256(_macKey!);
            var input = new byte[iv.Length + ciphertext.Length];
            Buffer.BlockCopy(iv, 0, input, 0, iv.Length);
            Buffer.BlockCopy(ciphertext, 0, input, iv.Length, ciphertext.Length);
            return hmac.ComputeHash(input);
        }

        private static byte[] DeriveKey(byte[] master, string purpose)
        {
            using var sha = SHA256.Create();
            var purposeBytes = Encoding.ASCII.GetBytes(purpose);
            var input = new byte[master.Length + purposeBytes.Length];
            Buffer.BlockCopy(master, 0, input, 0, master.Length);
            Buffer.BlockCopy(purposeBytes, 0, input, master.Length, purposeBytes.Length);
            return sha.ComputeHash(input);
        }

        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
                return false;

            var diff = 0;
            for (var i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
