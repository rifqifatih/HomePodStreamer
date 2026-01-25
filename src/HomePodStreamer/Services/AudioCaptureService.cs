using System;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using HomePodStreamer.Utils;

namespace HomePodStreamer.Services
{
    public class AudioCaptureService : IAudioCaptureService
    {
        private WasapiLoopbackCapture? _capture;
        private bool _disposed;

        public event EventHandler<WaveInEventArgs>? DataAvailable;
        public event EventHandler<StoppedEventArgs>? RecordingStopped;

        public WaveFormat? CaptureFormat => _capture?.WaveFormat;
        public bool IsCapturing => _capture != null && _capture.CaptureState == CaptureState.Capturing;

        public Task StartCaptureAsync()
        {
            try
            {
                if (_capture != null)
                {
                    Logger.Warning("Capture already started");
                    return Task.CompletedTask;
                }

                // Get default playback device
                var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

                Logger.Info($"Starting audio capture from device: {device.FriendlyName}");

                _capture = new WasapiLoopbackCapture(device);

                _capture.DataAvailable += OnDataAvailable;
                _capture.RecordingStopped += OnRecordingStopped;

                _capture.StartRecording();

                Logger.Info($"Audio capture started - Format: {_capture.WaveFormat}");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to start audio capture", ex);
                throw;
            }
        }

        public void StopCapture()
        {
            try
            {
                if (_capture == null) return;

                Logger.Info("Stopping audio capture");
                _capture.StopRecording();
                _capture.DataAvailable -= OnDataAvailable;
                _capture.RecordingStopped -= OnRecordingStopped;
                _capture.Dispose();
                _capture = null;
                Logger.Info("Audio capture stopped");
            }
            catch (Exception ex)
            {
                Logger.Error("Error stopping audio capture", ex);
            }
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            try
            {
                DataAvailable?.Invoke(this, e);
            }
            catch (Exception ex)
            {
                Logger.Error("Error in DataAvailable handler", ex);
            }
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            try
            {
                if (e.Exception != null)
                {
                    Logger.Error("Recording stopped with error", e.Exception);
                }
                RecordingStopped?.Invoke(this, e);
            }
            catch (Exception ex)
            {
                Logger.Error("Error in RecordingStopped handler", ex);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            StopCapture();
        }
    }
}
