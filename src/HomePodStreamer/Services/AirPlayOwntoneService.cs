using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using HomePodStreamer.Models;
using HomePodStreamer.Utils;

namespace HomePodStreamer.Services
{
    public class AirPlayOwntoneService : IAirPlayService
    {
        private readonly IOwntoneApiClient _apiClient;
        private readonly object _lock = new();
        private readonly HashSet<string> _enabledOutputIds = new();

        private TcpClient? _tcpClient;
        private NetworkStream? _networkStream;
        private bool _disposed;
        private int _audioChunkCount;

        private const string TcpHost = "127.0.0.1";
        private const int TcpPort = 5555;

        public AirPlayOwntoneService(IOwntoneApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        public async Task ConnectToDeviceAsync(HomePodDevice device)
        {
            try
            {
                if (string.IsNullOrEmpty(device.OwntoneOutputId))
                {
                    throw new InvalidOperationException(
                        $"Device {device.Name} has no owntone output ID. Refresh devices first.");
                }

                Logger.Info($"Enabling owntone output for device: {device.Name} (output ID: {device.OwntoneOutputId})");
                device.State = ConnectionState.Connecting;

                // Enable the output in owntone
                await _apiClient.SetOutputEnabledAsync(device.OwntoneOutputId, true);

                // Start playback (ensures queue has pcm_input and player is playing)
                await _apiClient.StartPlaybackAsync();

                // Ensure TCP connection to socat/FIFO is open
                EnsureTcpConnection();

                lock (_lock)
                {
                    _enabledOutputIds.Add(device.OwntoneOutputId);
                }

                device.State = ConnectionState.Connected;
                device.IsConnected = true;

                Logger.Info($"Successfully enabled device: {device.Name}");
            }
            catch (Exception ex)
            {
                device.State = ConnectionState.Error;
                device.ErrorMessage = ex.Message;
                Logger.Error($"Failed to connect to device: {device.Name}", ex);
                throw;
            }
        }

        public async Task DisconnectFromDeviceAsync(HomePodDevice device)
        {
            try
            {
                Logger.Info($"Disabling owntone output for device: {device.Name}");

                if (!string.IsNullOrEmpty(device.OwntoneOutputId))
                {
                    try
                    {
                        await _apiClient.SetOutputEnabledAsync(device.OwntoneOutputId, false);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Failed to disable output via API for {device.Name}: {ex.Message}");
                    }

                    lock (_lock)
                    {
                        _enabledOutputIds.Remove(device.OwntoneOutputId);
                    }
                }

                device.State = ConnectionState.Disconnected;
                device.IsConnected = false;

                // Close TCP if no more enabled outputs
                bool anyEnabled;
                lock (_lock)
                {
                    anyEnabled = _enabledOutputIds.Count > 0;
                }
                if (!anyEnabled)
                {
                    CloseTcpConnection();
                }

                Logger.Info($"Disconnected from device: {device.Name}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error disconnecting from device: {device.Name}", ex);
            }
        }

        public async Task StartPlaybackAsync()
        {
            await _apiClient.StartPlaybackAsync();
        }

        public void SendAudioToAll(byte[] encodedBuffer)
        {
            if (_disposed) return;

            try
            {
                bool anyEnabled;
                lock (_lock)
                {
                    anyEnabled = _enabledOutputIds.Count > 0;
                }
                if (!anyEnabled) return;

                if (_networkStream == null || !(_tcpClient?.Connected ?? false))
                {
                    EnsureTcpConnection();
                }

                _networkStream?.Write(encodedBuffer, 0, encodedBuffer.Length);
                _networkStream?.Flush();

                _audioChunkCount++;
                if (_audioChunkCount <= 5)
                {
                    Logger.Info($"Sent audio chunk #{_audioChunkCount} via TCP: {encodedBuffer.Length} bytes");
                }
                else if (_audioChunkCount == 6)
                {
                    Logger.Info("Audio streaming via TCP normally (further chunk logs suppressed)");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error sending audio via TCP to owntone", ex);
                // Try to reconnect on next call
                CloseTcpConnection();
            }
        }

        public void SetVolume(int volume)
        {
            volume = Math.Max(0, Math.Min(100, volume));

            // Fire-and-forget volume change via API (real-time, no reconnection needed)
            _ = Task.Run(async () =>
            {
                try
                {
                    await _apiClient.SetMasterVolumeAsync(volume);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to set volume to {volume}", ex);
                }
            });
        }

        public void StopAll()
        {
            Logger.Info("Stopping all owntone outputs");

            List<string> outputIds;
            lock (_lock)
            {
                outputIds = _enabledOutputIds.ToList();
                _enabledOutputIds.Clear();
            }

            // Close TCP first to unblock any pending writes
            CloseTcpConnection();
            _audioChunkCount = 0;

            // Disable outputs on background thread to avoid UI deadlock
            _ = Task.Run(async () =>
            {
                foreach (var outputId in outputIds)
                {
                    try
                    {
                        await _apiClient.SetOutputEnabledAsync(outputId, false);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Failed to disable output {outputId}: {ex.Message}");
                    }
                }
            });
        }

        private void EnsureTcpConnection()
        {
            if (_tcpClient?.Connected == true && _networkStream != null)
                return;

            CloseTcpConnection();

            Logger.Info($"Opening TCP connection to {TcpHost}:{TcpPort}");

            _tcpClient = new TcpClient();
            _tcpClient.Connect(TcpHost, TcpPort);
            _tcpClient.NoDelay = true;
            _networkStream = _tcpClient.GetStream();

            Logger.Info("TCP connection established");
        }

        private void CloseTcpConnection()
        {
            try
            {
                _networkStream?.Close();
                _networkStream?.Dispose();
                _tcpClient?.Close();
                _tcpClient?.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error closing TCP connection: {ex.Message}");
            }
            finally
            {
                _networkStream = null;
                _tcpClient = null;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            StopAll();
        }
    }
}
