using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
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
        private readonly IOwntoneHostService _owntoneHostService;

        private readonly AudioBuffer _audioBuffer;
        private CancellationTokenSource? _streamingCts;
        private Task? _processingTask;
        private DispatcherTimer? _scanTimer;
        private bool _isScanning;
        private List<SavedDevice>? _savedDevices;

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
            ISettingsService settingsService,
            IOwntoneHostService owntoneHostService)
        {
            _audioCaptureService = audioCaptureService;
            _audioEncoderService = audioEncoderService;
            _airPlayService = airPlayService;
            _deviceDiscoveryService = deviceDiscoveryService;
            _settingsService = settingsService;
            _owntoneHostService = owntoneHostService;

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
                _savedDevices = settings.SavedDevices;
                Logger.Info("ViewModel initialized");

                // Start automatic device scanning every 10 seconds
                _scanTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(10)
                };
                _scanTimer.Tick += async (s, e) => await AutoScanDevicesAsync();
                _scanTimer.Start();

                // Run initial scan
                await AutoScanDevicesAsync();
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

        private async Task AutoScanDevicesAsync()
        {
            if (_isScanning) return;
            if (!_owntoneHostService.IsRunning) return;

            _isScanning = true;
            try
            {
                await _deviceDiscoveryService.StartDiscoveryAsync();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var freshDevices = _deviceDiscoveryService.DiscoveredDevices.ToList();
                    var freshIds = new HashSet<string>(freshDevices.Select(d => d.Id));
                    var existingIds = new HashSet<string>(Devices.Select(d => d.Id));

                    // Remove devices that disappeared
                    var toRemove = Devices.Where(d => !freshIds.Contains(d.Id)).ToList();
                    foreach (var device in toRemove)
                    {
                        Devices.Remove(device);
                    }

                    // Add newly discovered devices
                    foreach (var device in freshDevices)
                    {
                        if (!existingIds.Contains(device.Id))
                        {
                            // Restore saved IsEnabled state
                            var saved = _savedDevices?.FirstOrDefault(
                                s => s.Id == device.Id || s.Name == device.Name);
                            if (saved != null)
                            {
                                device.IsEnabled = saved.IsEnabled;
                            }

                            Devices.Add(device);
                        }
                    }

                    StatusMessage = $"Found {Devices.Count} device(s)";
                });
            }
            catch (Exception ex)
            {
                Logger.Error("Auto-scan failed", ex);
            }
            finally
            {
                _isScanning = false;
            }
        }

        [RelayCommand]
        private async Task ToggleDeviceAsync(HomePodDevice device)
        {
            try
            {
                // Note: IsEnabled is already toggled by the CheckBox TwoWay binding
                // We just need to handle the logic based on the new state

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

                // Stop audio capture first (no more data coming in)
                _audioCaptureService.StopCapture();

                // Cancel processing task
                _streamingCts?.Cancel();

                // Stop AirPlay (closes TCP connection, unblocks any pending writes)
                _airPlayService.StopAll();

                // Now await the processing task (should exit quickly)
                if (_processingTask != null)
                {
                    var completed = await Task.WhenAny(_processingTask, Task.Delay(3000));
                    if (completed != _processingTask)
                    {
                        Logger.Warning("Processing task did not stop within 3 seconds");
                    }
                }

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

        private int _captureEventCount = 0;

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

                    // Log first few captures
                    _captureEventCount++;
                    if (_captureEventCount <= 3)
                    {
                        Logger.Info($"Audio captured #{_captureEventCount}: {e.BytesRecorded} bytes -> encoded to {encoded.Length} bytes, queued to buffer");
                    }
                    else if (_captureEventCount == 4)
                    {
                        Logger.Info("Audio capture working normally (further logs suppressed)");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error processing audio data", ex);
            }
        }

        private int _processedChunkCount = 0;

        private async Task ProcessAudioAsync(CancellationToken cancellationToken)
        {
            // owntone expects a constant stream: 44100Hz * 16-bit * 2ch = 176,400 bytes/sec
            const int BYTES_PER_SECOND = 44100 * 2 * 2; // 176,400
            var stopwatch = Stopwatch.StartNew();
            long totalBytesSent = 0;
            int silenceCount = 0;

            try
            {
                Logger.Info("Audio processing task started");

                while (!cancellationToken.IsCancellationRequested)
                {
                    var audioData = await _audioBuffer.DequeueAsync(
                        TimeSpan.FromMilliseconds(5), cancellationToken);

                    if (audioData != null)
                    {
                        _processedChunkCount++;
                        if (_processedChunkCount <= 3)
                        {
                            Logger.Info($"ProcessAudioAsync: Dequeued chunk #{_processedChunkCount}, {audioData.Length} bytes, calling SendAudioToAll");
                        }
                        else if (_processedChunkCount == 4)
                        {
                            if (silenceCount > 0)
                                Logger.Info($"ProcessAudioAsync: Sent {silenceCount} silence fills so far");
                            Logger.Info("ProcessAudioAsync working normally (further logs suppressed)");
                        }

                        _airPlayService.SendAudioToAll(audioData);
                        totalBytesSent += audioData.Length;
                    }

                    {
                        long expectedBytes = stopwatch.ElapsedMilliseconds * BYTES_PER_SECOND / 1000;
                        long deficit = expectedBytes - totalBytesSent;

                        if (deficit > 0)
                        {
                            int silenceSize = (int)(deficit / 4) * 4;
                            if (silenceSize > 0)
                            {
                                var silence = new byte[silenceSize];
                                _airPlayService.SendAudioToAll(silence);
                                totalBytesSent += silenceSize;
                                silenceCount++;
                            }
                        }
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
            _scanTimer?.Stop();
            _streamingCts?.Cancel();
            _audioCaptureService.DataAvailable -= OnAudioDataAvailable;
            _audioBuffer.Dispose();
            _audioCaptureService.Dispose();
            _airPlayService.Dispose();
        }
    }
}
