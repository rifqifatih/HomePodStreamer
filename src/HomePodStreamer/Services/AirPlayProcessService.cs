using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HomePodStreamer.Models;
using HomePodStreamer.Utils;

namespace HomePodStreamer.Services
{
    public class AirPlayProcessService : IAirPlayService
    {
        private readonly Dictionary<string, DeviceProcess> _deviceProcesses = new();
        private readonly object _lock = new();
        private int _currentVolume = 75;
        private bool _disposed;

        private class DeviceProcess
        {
            public HomePodDevice Device { get; set; } = null!;
            public Process? Process { get; set; }
            public DateTime LastSendTime { get; set; }
        }

        public async Task ConnectToDeviceAsync(HomePodDevice device)
        {
            try
            {
                lock (_lock)
                {
                    if (_deviceProcesses.ContainsKey(device.Id))
                    {
                        Logger.Warning($"Device {device.Name} is already connected");
                        return;
                    }
                }

                Logger.Info($"Connecting to device: {device.Name} at {device.IPAddress}");

                device.State = ConnectionState.Connecting;

                // Find raop_play.exe
                var exePath = FindRaopPlayExecutable();
                if (exePath == null)
                {
                    throw new FileNotFoundException("raop_play.exe not found. Please build libraop first.");
                }

                // Start raop_play process
                // Command: raop_play.exe -v <volume> -e <device_ip> -
                // The dash (-) at the end means read audio from stdin
                var processInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = $"-v {_currentVolume} -e {device.IPAddress} -",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var process = new Process { StartInfo = processInfo };

                // Handle output for debugging
                process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Logger.Info($"[{device.Name}] {e.Data}");
                    }
                };

                process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Logger.Warning($"[{device.Name}] {e.Data}");
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Give the process a moment to start
                await Task.Delay(500);

                if (process.HasExited)
                {
                    device.State = ConnectionState.Error;
                    device.ErrorMessage = "Process exited immediately";
                    throw new Exception("raop_play process exited immediately after start");
                }

                lock (_lock)
                {
                    _deviceProcesses[device.Id] = new DeviceProcess
                    {
                        Device = device,
                        Process = process,
                        LastSendTime = DateTime.UtcNow
                    };
                }

                device.State = ConnectionState.Connected;
                device.IsConnected = true;

                Logger.Info($"Successfully connected to device: {device.Name}");
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
                Logger.Info($"Disconnecting from device: {device.Name}");

                DeviceProcess? deviceProcess;
                lock (_lock)
                {
                    if (!_deviceProcesses.TryGetValue(device.Id, out deviceProcess))
                    {
                        Logger.Warning($"Device {device.Name} not found in active connections");
                        return;
                    }

                    _deviceProcesses.Remove(device.Id);
                }

                if (deviceProcess.Process != null && !deviceProcess.Process.HasExited)
                {
                    try
                    {
                        deviceProcess.Process.StandardInput.Close();
                        deviceProcess.Process.Kill();
                        await deviceProcess.Process.WaitForExitAsync();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error killing process for {device.Name}", ex);
                    }
                    finally
                    {
                        deviceProcess.Process.Dispose();
                    }
                }

                device.State = ConnectionState.Disconnected;
                device.IsConnected = false;

                Logger.Info($"Disconnected from device: {device.Name}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error disconnecting from device: {device.Name}", ex);
            }
        }

        public void SendAudioToAll(byte[] encodedBuffer)
        {
            if (_disposed) return;

            lock (_lock)
            {
                foreach (var deviceProcess in _deviceProcesses.Values.ToList())
                {
                    if (!deviceProcess.Device.IsEnabled) continue;

                    try
                    {
                        var process = deviceProcess.Process;
                        if (process == null || process.HasExited)
                        {
                            Logger.Warning($"Process for {deviceProcess.Device.Name} has exited");
                            deviceProcess.Device.State = ConnectionState.Error;
                            deviceProcess.Device.ErrorMessage = "Process terminated unexpectedly";
                            continue;
                        }

                        // Write audio data to process stdin
                        process.StandardInput.BaseStream.Write(encodedBuffer, 0, encodedBuffer.Length);
                        process.StandardInput.BaseStream.Flush();

                        deviceProcess.LastSendTime = DateTime.UtcNow;
                        deviceProcess.Device.State = ConnectionState.Streaming;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error sending audio to {deviceProcess.Device.Name}", ex);
                        deviceProcess.Device.State = ConnectionState.Error;
                        deviceProcess.Device.ErrorMessage = ex.Message;
                    }
                }
            }
        }

        public void SetVolume(int volume)
        {
            _currentVolume = Math.Max(0, Math.Min(100, volume));
            Logger.Info($"Volume set to {_currentVolume}");

            // Note: For process-based approach, volume change requires restarting processes
            // This is a limitation of the process wrapper approach
            // For now, we'll just update the internal volume value
            // New connections will use this volume
        }

        public void StopAll()
        {
            Logger.Info("Stopping all devices");

            List<HomePodDevice> devices;
            lock (_lock)
            {
                devices = _deviceProcesses.Values.Select(dp => dp.Device).ToList();
            }

            foreach (var device in devices)
            {
                DisconnectFromDeviceAsync(device).Wait();
            }
        }

        private string? FindRaopPlayExecutable()
        {
            // Look in several locations
            var searchPaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib", "native", "raop_play.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "lib", "native", "raop_play.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "raop_play.exe"),
                "raop_play.exe" // Try PATH
            };

            foreach (var path in searchPaths)
            {
                if (File.Exists(path))
                {
                    Logger.Info($"Found raop_play.exe at: {path}");
                    return path;
                }
            }

            Logger.Error("raop_play.exe not found in any expected location");
            return null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            StopAll();
        }
    }
}
