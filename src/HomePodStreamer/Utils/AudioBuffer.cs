using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace HomePodStreamer.Utils
{
    public class AudioBuffer : IDisposable
    {
        private readonly ConcurrentQueue<byte[]> _queue;
        private readonly SemaphoreSlim _semaphore;
        private const int MaxBufferCount = 50; // ~500ms at 44.1kHz
        private bool _disposed;

        public AudioBuffer()
        {
            _queue = new ConcurrentQueue<byte[]>();
            _semaphore = new SemaphoreSlim(0);
        }

        public int Count => _queue.Count;

        public void Enqueue(byte[] data)
        {
            if (_disposed) return;

            // Drop oldest frame if buffer is full
            if (_queue.Count >= MaxBufferCount)
            {
                _queue.TryDequeue(out _);
            }

            var copy = new byte[data.Length];
            Buffer.BlockCopy(data, 0, copy, 0, data.Length);
            _queue.Enqueue(copy);
            _semaphore.Release();
        }

        public async Task<byte[]?> DequeueAsync(CancellationToken cancellationToken)
        {
            if (_disposed) return null;

            try
            {
                await _semaphore.WaitAsync(cancellationToken);
                if (_queue.TryDequeue(out var data))
                {
                    return data;
                }
            }
            catch (OperationCanceledException)
            {
                return null;
            }

            return null;
        }

        public void Clear()
        {
            while (_queue.TryDequeue(out _)) { }

            // Drain semaphore
            while (_semaphore.CurrentCount > 0)
            {
                _semaphore.Wait(0);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _semaphore?.Dispose();
        }
    }
}
