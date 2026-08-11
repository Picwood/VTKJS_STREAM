// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace PrePoMax
{
    internal static class PmxFileProtection
    {
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("PMXPROT1");
        private const int FormatVersion = 1;
        private const int SaltLength = 16;
        private const int IvLength = 16;
        private const int HmacLength = 32;
        private const int KeyLength = 32;
        private const int DefaultIterations = 200000;

        private static int HeaderLength
        {
            get { return Magic.Length + sizeof(int) + sizeof(int) + SaltLength + IvLength; }
        }

        public static bool IsProtected(string fileName)
        {
            if (!File.Exists(fileName)) return false;
            //
            byte[] buffer = new byte[Magic.Length];
            using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (fs.Read(buffer, 0, buffer.Length) != buffer.Length) return false;
            }
            //
            return ConstantTimeEquals(buffer, Magic);
        }

        public static void ProtectFile(string sourceFileName, string targetFileName, string hashKey)
        {
            if (string.IsNullOrEmpty(hashKey)) throw new ArgumentException("The hash key must not be empty.", nameof(hashKey));
            //
            byte[] salt = new byte[SaltLength];
            byte[] iv = new byte[IvLength];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
                rng.GetBytes(iv);
            }
            //
            byte[] encryptionKey;
            byte[] authenticationKey;
            DeriveKeys(hashKey, salt, DefaultIterations, out encryptionKey, out authenticationKey);
            //
            try
            {
                using (Aes aes = Aes.Create())
                {
                    aes.KeySize = 256;
                    aes.BlockSize = 128;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.Key = encryptionKey;
                    aes.IV = iv;
                    //
                    using (FileStream input = new FileStream(sourceFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (FileStream output = new FileStream(targetFileName, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                    {
                        WriteHeader(output, DefaultIterations, salt, iv);
                        using (CryptoStream cryptoStream = new CryptoStream(output, aes.CreateEncryptor(),
                                                                            CryptoStreamMode.Write))
                        {
                            CopyStream(input, cryptoStream);
                            cryptoStream.FlushFinalBlock();
                        }
                    }
                }
                //
                byte[] hmac = ComputeFileHmac(targetFileName, authenticationKey, 0, new FileInfo(targetFileName).Length);
                using (FileStream output = new FileStream(targetFileName, FileMode.Append, FileAccess.Write, FileShare.None))
                {
                    output.Write(hmac, 0, hmac.Length);
                }
            }
            finally
            {
                Clear(encryptionKey);
                Clear(authenticationKey);
            }
        }

        public static void UnprotectFile(string sourceFileName, string targetFileName, string hashKey)
        {
            if (string.IsNullOrEmpty(hashKey)) throw new CryptographicException("The hash key must not be empty.");
            //
            int iterations;
            byte[] salt;
            byte[] iv;
            ReadHeader(sourceFileName, out iterations, out salt, out iv);
            //
            byte[] encryptionKey;
            byte[] authenticationKey;
            DeriveKeys(hashKey, salt, iterations, out encryptionKey, out authenticationKey);
            //
            try
            {
                FileInfo fileInfo = new FileInfo(sourceFileName);
                if (fileInfo.Length < HeaderLength + HmacLength) throw new CryptographicException("The protected file is incomplete.");
                //
                long authenticatedLength = fileInfo.Length - HmacLength;
                byte[] expectedHmac = ReadTrailingHmac(sourceFileName);
                byte[] actualHmac = ComputeFileHmac(sourceFileName, authenticationKey, 0, authenticatedLength);
                if (!ConstantTimeEquals(expectedHmac, actualHmac))
                    throw new CryptographicException("The hash key is incorrect or the file has been modified.");
                //
                using (Aes aes = Aes.Create())
                {
                    aes.KeySize = 256;
                    aes.BlockSize = 128;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.Key = encryptionKey;
                    aes.IV = iv;
                    //
                    using (FileStream input = new FileStream(sourceFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (FileStream output = new FileStream(targetFileName, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        input.Position = HeaderLength;
                        using (LimitedReadStream limitedInput = new LimitedReadStream(input, authenticatedLength - HeaderLength))
                        using (CryptoStream cryptoStream = new CryptoStream(limitedInput, aes.CreateDecryptor(),
                                                                            CryptoStreamMode.Read))
                        {
                            CopyStream(cryptoStream, output);
                        }
                    }
                }
            }
            finally
            {
                Clear(encryptionKey);
                Clear(authenticationKey);
                Clear(salt);
                Clear(iv);
            }
        }

        private static void WriteHeader(Stream stream, int iterations, byte[] salt, byte[] iv)
        {
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.ASCII, true))
            {
                writer.Write(Magic);
                writer.Write(FormatVersion);
                writer.Write(iterations);
                writer.Write(salt);
                writer.Write(iv);
            }
        }

        private static void ReadHeader(string fileName, out int iterations, out byte[] salt, out byte[] iv)
        {
            using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (BinaryReader reader = new BinaryReader(fs, Encoding.ASCII))
            {
                byte[] magic = reader.ReadBytes(Magic.Length);
                if (!ConstantTimeEquals(magic, Magic)) throw new CryptographicException("The file is not protected.");
                //
                int formatVersion = reader.ReadInt32();
                if (formatVersion != FormatVersion)
                    throw new CryptographicException("The protected .pmx format version is not supported.");
                //
                iterations = reader.ReadInt32();
                salt = reader.ReadBytes(SaltLength);
                iv = reader.ReadBytes(IvLength);
                if (salt.Length != SaltLength || iv.Length != IvLength)
                    throw new CryptographicException("The protected file header is incomplete.");
            }
        }

        private static void DeriveKeys(string hashKey, byte[] salt, int iterations,
                                       out byte[] encryptionKey, out byte[] authenticationKey)
        {
            using (Rfc2898DeriveBytes kdf = new Rfc2898DeriveBytes(hashKey, salt, iterations,
                                                                   HashAlgorithmName.SHA256))
            {
                encryptionKey = kdf.GetBytes(KeyLength);
                authenticationKey = kdf.GetBytes(KeyLength);
            }
        }

        private static byte[] ComputeFileHmac(string fileName, byte[] key, long offset, long length)
        {
            using (HMACSHA256 hmac = new HMACSHA256(key))
            using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (LimitedReadStream limitedStream = new LimitedReadStream(fs, length))
            {
                fs.Position = offset;
                return hmac.ComputeHash(limitedStream);
            }
        }

        private static byte[] ReadTrailingHmac(string fileName)
        {
            byte[] hmac = new byte[HmacLength];
            using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                fs.Position = fs.Length - HmacLength;
                if (fs.Read(hmac, 0, hmac.Length) != hmac.Length) throw new EndOfStreamException();
            }
            return hmac;
        }

        private static void CopyStream(Stream input, Stream output)
        {
            byte[] buffer = new byte[1024 * 1024];
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0) output.Write(buffer, 0, read);
        }

        private static void CopyStream(Stream input, Stream output, long bytesToCopy)
        {
            byte[] buffer = new byte[1024 * 1024];
            long remaining = bytesToCopy;
            while (remaining > 0)
            {
                int read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                if (read <= 0) throw new EndOfStreamException();
                output.Write(buffer, 0, read);
                remaining -= read;
            }
        }

        private static bool ConstantTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null) return false;
            int diff = a.Length ^ b.Length;
            int length = Math.Min(a.Length, b.Length);
            for (int i = 0; i < length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        private static void Clear(byte[] buffer)
        {
            if (buffer != null) Array.Clear(buffer, 0, buffer.Length);
        }

        private class LimitedReadStream : Stream
        {
            private readonly Stream _baseStream;
            private long _remaining;

            public LimitedReadStream(Stream baseStream, long length)
            {
                _baseStream = baseStream;
                _remaining = length;
            }

            public override bool CanRead { get { return _baseStream.CanRead; } }
            public override bool CanSeek { get { return false; } }
            public override bool CanWrite { get { return false; } }
            public override long Length { get { throw new NotSupportedException(); } }
            public override long Position
            {
                get { throw new NotSupportedException(); }
                set { throw new NotSupportedException(); }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_remaining <= 0) return 0;
                int read = _baseStream.Read(buffer, offset, (int)Math.Min(count, _remaining));
                _remaining -= read;
                return read;
            }

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }
    }
}
