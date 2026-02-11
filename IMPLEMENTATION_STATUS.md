# HomePod Streamer - Implementation Status

**Last Updated**: 2026-02-11
**Status**: Working end-to-end (Windows audio -> HomePod via AirPlay)

---

## Project Overview

A Windows desktop application that streams all system audio to Apple HomePod speakers via AirPlay. Uses owntone-server running in WSL2 Ubuntu as the AirPlay backend.

### Technology Stack

- **Language**: C# .NET 9
- **UI Framework**: WPF with MVVM (CommunityToolkit.Mvvm)
- **Audio Capture**: NAudio (WASAPI loopback)
- **Audio Encoding**: Float32 48kHz -> Int16 44.1kHz PCM (resampling + conversion)
- **AirPlay Backend**: owntone-server in WSL2 Ubuntu
- **Device Discovery**: owntone JSON API (`GET /api/outputs`)
- **Audio Transport**: TCP socket -> socat -> FIFO pipe -> owntone pipe input
- **Host Management**: WSL2 process management via `WslOwntoneService`
- **DI Container**: Microsoft.Extensions.DependencyInjection
- **System Tray**: Hardcodet.NotifyIcon.Wpf

### Architecture

```
Windows (WPF App)
  WASAPI Capture -> Encode (S16LE 44.1kHz stereo) -> TCP localhost:5555
  UI <-> owntone JSON API (HTTP localhost:3689) for device control + volume

WSL2 Ubuntu (owntone-server)
  socat TCP:5555 -> FIFO pipe -> owntone (pipe_autostart) -> AirPlay -> HomePod
```

---

## Completed Features

### Audio Pipeline
- WASAPI loopback capture (all system audio)
- Format conversion (IEEE Float32 -> PCM Int16 S16LE)
- Resampling to 44.1kHz (persistent WDL resampler)
- Thread-safe audio buffering with backpressure (max 50 frames)
- Background audio processing with silence injection for constant bitrate
- TCP socket transport to owntone via socat/FIFO

### AirPlay Integration
- owntone-server in WSL2 for AirPlay support
- TCP -> socat -> FIFO pipe audio transport
- owntone `pipe_autostart` for automatic playback
- Real-time volume control via owntone HTTP API
- Automatic owntone start on app launch, stop on exit
- Health check polling before device discovery

### Device Management
- Device discovery via owntone `GET /api/outputs` (filters AirPlay type)
- Enable/disable outputs via owntone `PUT /api/outputs/{id}`
- Device list with connection state indicators
- Enable/disable devices during active streaming

### User Interface
- WPF UI with MVVM pattern
- System tray with icon, context menu, double-click to show/hide
- Device list with checkboxes
- Volume slider (0-100) with real-time control
- Status messages
- Start/Stop streaming toggle

### Settings & Persistence
- Saved enabled devices
- Saved volume level
- Auto-load settings on startup
- Settings in `%APPDATA%\HomePodStreamer\`

### Lifecycle & Cleanup
- Graceful shutdown on window close, tray quit, and Ctrl+C
- owntone processes killed on exit (synchronous to ensure completion)
- Stop Streaming without UI deadlock (TCP closed before awaiting background tasks)

---

## Project Structure

```
HomePodStreamer/
+-- HomePodStreamer.sln
+-- README.md
+-- IMPLEMENTATION_STATUS.md
|
+-- scripts/
|   +-- owntone.conf              # owntone configuration (pipe input, dummy audio)
|   +-- start-owntone.sh          # WSL startup: FIFO + socat + dbus + avahi + owntone
|   +-- setup-wsl-owntone.sh      # One-time: build and install owntone from source
|
+-- src/HomePodStreamer/
    +-- HomePodStreamer.csproj
    +-- App.xaml / App.xaml.cs     # DI setup, owntone lifecycle, console handler
    |
    +-- Models/
    |   +-- HomePodDevice.cs       # Device model (OwntoneOutputId, connection state)
    |   +-- OwntoneModels.cs       # API DTOs (OwntoneOutput, OwntoneOutputList, etc.)
    |   +-- AppSettings.cs         # Settings model
    |   +-- StreamingState.cs      # Streaming state enum
    |
    +-- Services/
    |   +-- IAudioCaptureService.cs / AudioCaptureService.cs      # WASAPI loopback
    |   +-- IAudioEncoderService.cs / AudioEncoderService.cs      # Float32->Int16 + resample
    |   +-- IAirPlayService.cs / AirPlayOwntoneService.cs         # TCP streaming + owntone control
    |   +-- IOwntoneApiClient.cs / OwntoneApiClient.cs            # owntone HTTP API
    |   +-- IOwntoneHostService.cs / WslOwntoneService.cs         # WSL2 process management
    |   +-- IDeviceDiscoveryService.cs / HybridDeviceDiscoveryService.cs  # Device discovery
    |   +-- ISettingsService.cs / SettingsService.cs              # Settings persistence
    |
    +-- ViewModels/
    |   +-- MainViewModel.cs       # Core logic, streaming control, device management
    |
    +-- Views/
    |   +-- MainWindow.xaml / .cs  # UI with tray icon
    |
    +-- Utils/
    |   +-- AudioBuffer.cs         # Thread-safe async audio queue
    |   +-- Logger.cs              # File-based logging
    |
    +-- Resources/Icons/
        +-- app-icon.ico           # Application icon
```

---

## Dependency Injection

All services registered in `App.xaml.cs`:

```csharp
services.AddSingleton<HttpClient>();
services.AddSingleton<IOwntoneApiClient, OwntoneApiClient>();
services.AddSingleton<IOwntoneHostService, WslOwntoneService>();
services.AddSingleton<IAudioCaptureService, AudioCaptureService>();
services.AddSingleton<IAudioEncoderService, AudioEncoderService>();
services.AddSingleton<IAirPlayService, AirPlayOwntoneService>();
services.AddSingleton<IDeviceDiscoveryService, HybridDeviceDiscoveryService>();
services.AddSingleton<ISettingsService, SettingsService>();
```

---

## Known Limitations

### AirPlay Latency (~2 seconds)

AirPlay 1 (RAOP) has a mandatory ~2-second buffer at the receiver (HomePod). This is inherent to the protocol and cannot be reduced. owntone uses AirPlay 1 because AirPlay 2's protocol is proprietary and not reverse-engineered for open-source use.

The latency breakdown:
| Stage | Latency |
|-------|---------|
| WASAPI capture | ~10ms |
| Encode + buffer | ~5-10ms |
| TCP + socat + FIFO | ~1-5ms |
| owntone internal | ~100-500ms |
| **AirPlay protocol buffer** | **~2000ms** |

Apple TV works around this by delaying video to match audio. We cannot do this for desktop audio.

### Audio Source Limitation

WASAPI loopback captures the default audio output device. Applications using exclusive mode or a different output device (e.g., Spotify's direct audio) may not be captured.

### Windows 11 Required

WSL2 mirrored networking requires Windows 11 for mDNS to work across the WSL boundary.

---

## Key Files

**Runtime Locations**:
- Logs: `%APPDATA%\HomePodStreamer\Logs\log_YYYYMMDD.txt`
- Settings: `%APPDATA%\HomePodStreamer\settings.json`

**Scripts** (copied to build output):
- `scripts/owntone.conf` - owntone config (pipe input at `/srv/media/pcm_input`)
- `scripts/start-owntone.sh` - Startup: creates FIFO, starts socat/dbus/avahi/owntone
- `scripts/setup-wsl-owntone.sh` - One-time build/install of owntone from source
