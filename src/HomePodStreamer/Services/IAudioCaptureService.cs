using System;
using System.Threading.Tasks;
using NAudio.Wave;

namespace HomePodStreamer.Services
{
    public interface IAudioCaptureService : IDisposable
    {
        event EventHandler<WaveInEventArgs>? DataAvailable;
        event EventHandler<StoppedEventArgs>? RecordingStopped;

        WaveFormat? CaptureFormat { get; }
        bool IsCapturing { get; }

        Task StartCaptureAsync();
        void StopCapture();
    }
}
