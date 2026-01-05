using System;

namespace ChillPatcher.Module.Bilibili
{
    // 简单的线程安全环形缓冲区
    public class RingBuffer
    {
        private readonly float[] _buffer;
        private readonly int _capacity;
        private int _writePos;
        private int _readPos;
        private int _count;
        private readonly object _lock = new object();

        public RingBuffer(int capacity)
        {
            _capacity = capacity;
            _buffer = new float[capacity];
        }

        public int Count { get { lock (_lock) return _count; } }
        public int Capacity => _capacity;

        public int Write(float[] data, int offset, int length)
        {
            lock (_lock)
            {
                int writable = _capacity - _count;
                if (writable <= 0) return 0;
                int toWrite = Math.Min(writable, length);

                int firstChunk = Math.Min(toWrite, _capacity - _writePos);
                Array.Copy(data, offset, _buffer, _writePos, firstChunk);

                int secondChunk = toWrite - firstChunk;
                if (secondChunk > 0)
                    Array.Copy(data, offset + firstChunk, _buffer, 0, secondChunk);

                _writePos = (_writePos + toWrite) % _capacity;
                _count += toWrite;
                return toWrite;
            }
        }

        public int Read(float[] output, int count)
        {
            lock (_lock)
            {
                if (_count <= 0) return 0;
                int toRead = Math.Min(_count, count);

                int firstChunk = Math.Min(toRead, _capacity - _readPos);
                Array.Copy(_buffer, _readPos, output, 0, firstChunk);

                int secondChunk = toRead - firstChunk;
                if (secondChunk > 0)
                    Array.Copy(_buffer, 0, output, firstChunk, secondChunk);

                _readPos = (_readPos + toRead) % _capacity;
                _count -= toRead;
                return toRead;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _writePos = 0; _readPos = 0; _count = 0;
            }
        }
    }
}