using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PrePoMax.Rendering
{
    internal sealed class VtkJsMjpegAviWriter : IDisposable
    {
        private const long MaximumRiffFileLength = (long)uint.MaxValue + 8;
        private readonly FileStream _stream;
        private readonly BinaryWriter _writer;
        private readonly int _width;
        private readonly int _height;
        private readonly int _framesPerSecond;
        private readonly int _expectedFrameCount;
        private readonly List<AviIndexEntry> _index = new List<AviIndexEntry>();
        private readonly long _riffSizeOffset;
        private readonly long _maximumBytesPerSecondOffset;
        private readonly long _totalFramesOffset;
        private readonly long _mainSuggestedBufferSizeOffset;
        private readonly long _streamLengthOffset;
        private readonly long _streamSuggestedBufferSizeOffset;
        private readonly long _bitmapSizeImageOffset;
        private readonly long _moviSizeOffset;
        private readonly long _moviTypeOffset;
        private uint _maximumFrameSize;
        private bool _completed;
        private bool _disposed;

        public VtkJsMjpegAviWriter(string fileName, int width, int height, int framesPerSecond,
                                   int expectedFrameCount)
        {
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("An AVI file name is required.");
            if (width < 1 || width > short.MaxValue || height < 1 || height > short.MaxValue)
                throw new ArgumentOutOfRangeException("AVI dimensions must be between 1 and 32767 pixels.");
            if (framesPerSecond < 1 || framesPerSecond > 1000)
                throw new ArgumentOutOfRangeException("AVI frame rate must be between 1 and 1000 frames per second.");
            if (expectedFrameCount < 1 || expectedFrameCount > 100000)
                throw new ArgumentOutOfRangeException("AVI frame count must be between 1 and 100000.");

            _width = width;
            _height = height;
            _framesPerSecond = framesPerSecond;
            _expectedFrameCount = expectedFrameCount;
            _stream = new FileStream(fileName, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920);
            _writer = new BinaryWriter(_stream, Encoding.ASCII, true);

            WriteFourCc("RIFF");
            _riffSizeOffset = _stream.Position;
            _writer.Write(0U);
            WriteFourCc("AVI ");

            WriteFourCc("LIST");
            long headerListSizeOffset = _stream.Position;
            _writer.Write(0U);
            WriteFourCc("hdrl");

            WriteFourCc("avih");
            _writer.Write(56U);
            _writer.Write((uint)Math.Max(1, Math.Round(1000000.0 / framesPerSecond)));
            _maximumBytesPerSecondOffset = _stream.Position;
            _writer.Write(0U);
            _writer.Write(0U);
            _writer.Write(0x10U); // AVIF_HASINDEX
            _totalFramesOffset = _stream.Position;
            _writer.Write((uint)expectedFrameCount);
            _writer.Write(0U);
            _writer.Write(1U);
            _mainSuggestedBufferSizeOffset = _stream.Position;
            _writer.Write(0U);
            _writer.Write((uint)width);
            _writer.Write((uint)height);
            for (int i = 0; i < 4; i++) _writer.Write(0U);

            WriteFourCc("LIST");
            long streamListSizeOffset = _stream.Position;
            _writer.Write(0U);
            WriteFourCc("strl");

            WriteFourCc("strh");
            _writer.Write(56U);
            WriteFourCc("vids");
            WriteFourCc("MJPG");
            _writer.Write(0U);
            _writer.Write(0U); // priority and language
            _writer.Write(0U);
            _writer.Write(1U);
            _writer.Write((uint)framesPerSecond);
            _writer.Write(0U);
            _streamLengthOffset = _stream.Position;
            _writer.Write((uint)expectedFrameCount);
            _streamSuggestedBufferSizeOffset = _stream.Position;
            _writer.Write(0U);
            _writer.Write(uint.MaxValue);
            _writer.Write(0U);
            _writer.Write((short)0);
            _writer.Write((short)0);
            _writer.Write((short)width);
            _writer.Write((short)height);

            WriteFourCc("strf");
            _writer.Write(40U);
            _writer.Write(40U);
            _writer.Write(width);
            _writer.Write(height);
            _writer.Write((ushort)1);
            _writer.Write((ushort)24);
            WriteFourCc("MJPG");
            _bitmapSizeImageOffset = _stream.Position;
            _writer.Write(0U);
            _writer.Write(0);
            _writer.Write(0);
            _writer.Write(0U);
            _writer.Write(0U);

            PatchUInt32(streamListSizeOffset, CheckedUInt32(_stream.Position - streamListSizeOffset - 4));
            PatchUInt32(headerListSizeOffset, CheckedUInt32(_stream.Position - headerListSizeOffset - 4));

            WriteFourCc("LIST");
            _moviSizeOffset = _stream.Position;
            _writer.Write(0U);
            _moviTypeOffset = _stream.Position;
            WriteFourCc("movi");
        }

        public void WriteFrame(byte[] jpeg, int width, int height)
        {
            ThrowIfUnavailable();
            if (jpeg == null || jpeg.Length < 4 || jpeg[0] != 0xFF || jpeg[1] != 0xD8 ||
                jpeg[jpeg.Length - 2] != 0xFF || jpeg[jpeg.Length - 1] != 0xD9)
                throw new InvalidDataException("The AVI frame is not a complete JPEG image.");
            if (width != _width || height != _height)
                throw new InvalidOperationException("The browser viewport changed size during AVI capture.");
            if (_index.Count >= _expectedFrameCount)
                throw new InvalidOperationException("More AVI frames were supplied than declared.");

            long projectedLength = _stream.Position + 8L + jpeg.Length + (jpeg.Length & 1) +
                                   8L + 16L * _expectedFrameCount;
            if (projectedLength > MaximumRiffFileLength)
                throw new IOException("The AVI would exceed the 4 GiB AVI 1.0 size limit.");

            long chunkOffset = _stream.Position - _moviTypeOffset;
            WriteFourCc("00dc");
            _writer.Write((uint)jpeg.Length);
            _writer.Write(jpeg);
            if ((jpeg.Length & 1) != 0) _writer.Write((byte)0);
            _index.Add(new AviIndexEntry(CheckedUInt32(chunkOffset), (uint)jpeg.Length));
            _maximumFrameSize = Math.Max(_maximumFrameSize, (uint)jpeg.Length);
        }

        public void Complete()
        {
            ThrowIfUnavailable();
            if (_index.Count != _expectedFrameCount)
                throw new InvalidOperationException("The AVI frame count does not match the declared sequence.");

            PatchUInt32(_moviSizeOffset, CheckedUInt32(_stream.Position - _moviSizeOffset - 4));
            WriteFourCc("idx1");
            _writer.Write(CheckedUInt32(16L * _index.Count));
            foreach (AviIndexEntry entry in _index)
            {
                WriteFourCc("00dc");
                _writer.Write(0x10U); // AVIIF_KEYFRAME
                _writer.Write(entry.Offset);
                _writer.Write(entry.Size);
            }

            PatchUInt32(_maximumBytesPerSecondOffset,
                (uint)Math.Min(uint.MaxValue, (long)_maximumFrameSize * _framesPerSecond));
            PatchUInt32(_totalFramesOffset, (uint)_index.Count);
            PatchUInt32(_mainSuggestedBufferSizeOffset, _maximumFrameSize);
            PatchUInt32(_streamLengthOffset, (uint)_index.Count);
            PatchUInt32(_streamSuggestedBufferSizeOffset, _maximumFrameSize);
            PatchUInt32(_bitmapSizeImageOffset, _maximumFrameSize);
            PatchUInt32(_riffSizeOffset, CheckedUInt32(_stream.Length - 8));
            _writer.Flush();
            _stream.Flush(true);
            _completed = true;
        }

        private void WriteFourCc(string value)
        {
            if (value == null || value.Length != 4) throw new ArgumentException("A FourCC must have four characters.");
            _writer.Write(Encoding.ASCII.GetBytes(value));
        }

        private void PatchUInt32(long offset, uint value)
        {
            long position = _stream.Position;
            _stream.Position = offset;
            _writer.Write(value);
            _stream.Position = position;
        }

        private static uint CheckedUInt32(long value)
        {
            if (value < 0 || value > uint.MaxValue) throw new IOException("The AVI 1.0 size limit was exceeded.");
            return (uint)value;
        }

        private void ThrowIfUnavailable()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(VtkJsMjpegAviWriter));
            if (_completed) throw new InvalidOperationException("The AVI stream is already complete.");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _writer.Dispose();
            _stream.Dispose();
        }

        private struct AviIndexEntry
        {
            public uint Offset { get; }
            public uint Size { get; }

            public AviIndexEntry(uint offset, uint size)
            {
                Offset = offset;
                Size = size;
            }
        }
    }
}
