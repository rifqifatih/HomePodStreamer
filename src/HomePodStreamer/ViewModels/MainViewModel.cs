using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomePodStreamer.Models;
using HomePodStreamer.Services;
using HomePodStreamer.Utils;
using NAudio.Wave;

namespace HomePodStreamer.ViewModels
{
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private readonly IAudioCaptureService _audioCaptureService;
        private readonly IAudioEncoderService _audioEncoderService;
        private readonly IAirPlayService _airPlayService;
        private readonly IDeviceDiscoveryService _deviceDiscoveryService;
        private readonly ISettingsService _settingsService;

        private readonly AudioBuffer _audioBuffer;
        private CancellationTokenSource? _streamingCts;
        private Task? _processingTask;

        [ObservableProperty]
        private bool _isStreaming;

        [ObservableProperty]
        private int _volume = 75;

        [ObservableProperty]
        private ObservableCollection<HomePodDevice> _devices;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private string _streamingButtonText = "Start Streaming";

        public MainViewModel(
            IAudioCaptureService audioCaptureService,
            IAudioEncoderService audioEncoderService,
            IAirPlayService airPlayService,
            IDeviceDiscoveryService deviceDiscoveryService,
            ISettingsService settingsService)
        {
            _audioCaptureService = audioCaptureService;
            _audioEncoderService = audioEncoderService;
            _airPlayService = airPlayService;
            _deviceDiscoveryService = deviceDiscoveryService;
            _settingsService = settingsService;

            _audioBuffer = new AudioBuffer();
            _devices = new ObservableCollection<HomePodDevice>();

            // Subscribe to audio capture events
            _audioCaptureService.DataAvailable += OnAudioDataAvailable;

            // Load settings
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                var settings = await _settingsService.LoadSettingsAsync();
                Volume = settings.GlobalVolume;
                Logger.Info("ViewModel initialized");
            }
            catch (Exception ex)
            {
                Logger.Error("Error initializing ViewModel", ex);
            }
        }

        partial void OnVolumeChanged(int value)
        {
            _airPlayService.SetVolume(value);
            SaveSettingsAsync().ConfigureAwait(false);
        }

        [RelayCommand]
        private async Task ToggleStreamingAsync()
        {
            if (IsStreaming)
            {
                await StopStreamingAsync();
            }
            else
            {
                await StartStreamingAsync();
            }
        }

        [RelayCommand]
        private async Task RefreshDevicesAsync()
        {
            try
            {
                StatusMessage = "Scanning for HomePods...";
                await _deviceDiscoveryService.StartDiscoveryAsync();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Devices.Clear();
                    foreach (var device in _deviceDiscoveryService.DiscoveredDevices)
                    {
                        Devices.Add(device);
                    }
                });

                StatusMessage = $"Found {Devices.Count} device(s)";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                Logger.Error("Device discovery failed", ex);
            }
        }

        [RelayCommand]
        private async Task ToggleDeviceAsync(HomePodDevice device)
        {
            try
            {
                device.IsEnabled = !device.IsEnabled;

                if (device.IsEnabled && IsStreaming)
                {
                    await _airPlayService.ConnectToDeviceAsync(device);
                }
                else if (!device.IsEnabled && device.IsConnected)
                {
                    await _airPlayService.DisconnectFromDeviceAsync(device);
                }

                await SaveSettingsAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error toggling device: {ex.Message}";
                Logger.Error($"Error toggling device {device.Name}", ex);
            }
        }

        private async Task StartStreamingAsync()
        {
            try
            {
                Logger.Info("Starting streaming");
                StatusMessage = "Starting streaming...";

                var enabledDevices = Devices.Where(d => d.IsEnabled).ToList();
                if (enabledDevices.Count == 0)
                {
                    StatusMessage = "No devices enabled. Please enable at least one device.";
                    return;
                }

                // Connect to all enabled devices
                foreach (var device in enabledDevices)
                {
                    await _airPlayService.ConnectToDeviceAsync(device);
                }

                // Initialize encoder with capture format
                await _audioCaptureService.StartCaptureAsync();
                if (_audioCaptureService.CaptureFormat != null)
                {
                    _audioEncoderService.Initialize(_audioCaptureService.CaptureFormat);
                }

                // Start audio processing task
                _streamingCts = new CancellationTokenSource();
                _processingTask = Task.Run(() => ProcessAudioAsync(_streamingCts.Token));

                IsStreaming = true;
                StreamingButtonText = "Stop Streaming";
                StatusMessage = $"Streaming to {enabledDevices.Count} device(s)";

                Logger.Info("Streaming started successfully");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to start streaming: {ex.Message}";
                Logger.Error("Failed to start streaming", ex);
                await StopStreamingAsync();
            }
        }

        private async Task StopStreamingAsync()
        {
            try
            {
                Logger.Info("Stopping streaming");
                StatusMessage = "Stopping streaming...";

                // Stop audio capture
                _audioCaptureService.StopCapture();

                // Cancel processing task
                _streamingCts?.Cancel();
                if (_processingTask != null)
                {
                    await _processingTask;
                }

                // Disconnect from all devices
                _airPlayService.StopAll();

                // Clear buffer
                _audioBuffer.Clear();

                IsStreaming = false;
                StreamingButtonText = "Start Streaming";
                StatusMessage = "Streaming stopped";

                Logger.Info("Streaming stopped successfully");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error stopping streaming: {ex.Message}";
                Logger.Error("Error stopping streaming", ex);
            }
        }

        private void OnAudioDataAvailable(object? sender, WaveInEventArgs e)
        {
            try
            {
                if (e.BytesRecorded > 0)
                {
                    // Copy audio data to buffer
                    var audioData = new byte[e.BytesRecorded];
                    Buffer.BlockCopy(e.Buffer, 0, audioData, 0, e.BytesRecorded);

                    // Encode and enqueue
                    var encoded = _audioEncoderService.Encode(audioData);
                    _audioBuffer.Enqueue(encoded);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error processing audio data", ex);
            }
        }

        private async Task ProcessAudioAsync(CancellationToken cancellationToken)
        {
            try
            {
                Logger.Info("Audio processing task started");

                while (!cancellationToken.IsCancellationRequested)
                {
                    var audioData = await _audioBuffer.DequeueAsync(cancellationToken);
                    if (audioData != null)
                    {
                        _airPlayService.SendAudioToAll(audioData);
                    }
                }

                Logger.Info("Audio processing task stopped");
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Audio processing task cancelled");
            }
            catch (Exception ex)
            {
                Logger.Error("Error in audio processing task", ex);
            }
        }

        private async Task SaveSettingsAsync()
        {
            try
            {
                var settings = new AppSettings
                {
                    GlobalVolume = Volume,
                    SavedDevices = Devices.Select(d => new SavedDevice
                    {
                        Id = d.Id,
                        Name = d.Name,
                        IsEnabled = d.IsEnabled
                    }).ToList()
                };

                await _settingsService.SaveSettingsAsync(settings);
            }
            catch (Exception ex)
            {
                Logger.Error("Error saving settings", ex);
            }
        }

        public void Dispose()
        {
            _streamingCts?.Cancel();
            _audioCaptureService.DataAvailable -= OnAudioDataAvailable;
            _audioBuffer.Dispose();
            _audioCaptureService.Dispose();
            _airPlayService.Dispose();
        }
    }
}
