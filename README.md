# HomePod Streamer

A Windows desktop application that streams all system audio to Apple HomePod speakers via AirPlay, using [owntone-server](https://github.com/owntone/owntone-server) running in WSL2.

## Features

- Stream all system audio (Spotify, YouTube, games, etc.) to HomePod devices
- Support for multiple HomePods simultaneously
- Volume normalization: HomePod volume is independent of Windows volume, so you can mute local speakers without affecting HomePod
- Real-time volume control via owntone API
- Automatic device discovery (scans every 10 seconds)
- System tray integration for background operation
- Persistent settings (remembers enabled devices and volume)

## Prerequisites

- **Windows 11** (required for WSL2 mirrored networking)
- **.NET 9 SDK** ([download](https://dotnet.microsoft.com/download/dotnet/9.0))
- **WSL2 with Ubuntu** installed (`wsl --install -d Ubuntu`)
- **owntone-server** built from source inside WSL2 Ubuntu (see setup below)
- **Windows Firewall rule** allowing inbound connections from your HomePod's IP (for AirPlay handshake)

### WSL2 Setup

#### 1. Enable Mirrored Networking

owntone uses mDNS/Avahi to discover HomePod on the LAN. This requires WSL2 mirrored networking.

Create or edit `%UserProfile%\.wslconfig`:

```ini
[wsl2]
networkingMode=mirrored
```

Then restart WSL:
```
wsl --shutdown
```

#### 2. Install owntone in WSL2 Ubuntu

Run the included setup script from PowerShell:

```
wsl -d Ubuntu -u root -- bash $(wsl wslpath "scripts/setup-wsl-owntone.sh")
```

Or manually in a WSL terminal:
```bash
cd /mnt/c/path/to/HomePodStreamer
sudo bash scripts/setup-wsl-owntone.sh
```

This clones owntone-server from GitHub, builds from source, and installs it.

#### 3. Add Firewall Rule for HomePod

AirPlay requires bidirectional connections. The HomePod needs to connect back to your PC on random ports during the RTSP handshake. Add a firewall rule for your HomePod's IP:

```powershell
# Replace with your HomePod's IP address
New-NetFirewallRule -DisplayName "HomePod AirPlay" -Direction Inbound -RemoteAddress 192.168.x.x -Action Allow
```

You can find your HomePod's IP in the Apple Home app under device settings.

## Build & Run

```
dotnet restore
dotnet build src/HomePodStreamer/HomePodStreamer.csproj
dotnet run --project src/HomePodStreamer/HomePodStreamer.csproj
```

Or open `HomePodStreamer.sln` in Visual Studio and press F5.

The app automatically starts owntone in WSL2 on launch and stops it on exit.

## Usage

1. HomePods on your network are discovered automatically
2. Check the HomePods you want to stream to
3. Click **"Start Streaming"**
4. Play any audio on your PC

### System Tray

- The app minimizes to the system tray when the window is closed
- **Double-click** the tray icon to show/hide the window
- **Right-click** for quick actions (Show, Toggle Streaming, Quit)

## Architecture

```
Windows (WPF App)
  WASAPI Capture -> Encode (S16LE 44.1kHz stereo) -> TCP localhost:5555
  UI <-> owntone JSON API (HTTP localhost:3689) for device control + volume

WSL2 Ubuntu (owntone-server)
  socat TCP:5555 -> FIFO pipe -> owntone (pipe input) -> AirPlay -> HomePod
```

### Audio Pipeline

1. **AudioCaptureService** - Captures all system audio via WASAPI loopback
2. **AudioEncoderService** - Converts float32 48kHz to int16 44.1kHz PCM
3. **AudioBuffer** - Thread-safe async queue between capture and streaming
4. **AirPlayOwntoneService** - Sends PCM via TCP to owntone's FIFO pipe
5. **OwntoneApiClient** - Controls outputs and playback via owntone's HTTP API
6. **WslOwntoneService** - Manages owntone lifecycle in WSL2

### Project Structure

```
HomePodStreamer/
+-- src/HomePodStreamer/
|   +-- Models/              # Data models (HomePodDevice, OwntoneModels, etc.)
|   +-- Services/            # Business logic services
|   +-- ViewModels/          # MVVM ViewModels
|   +-- Views/               # WPF UI (MainWindow)
|   +-- Utils/               # AudioBuffer, Logger
|   +-- Resources/Icons/     # App icon
+-- scripts/
|   +-- owntone.conf         # owntone configuration
|   +-- start-owntone.sh     # WSL startup script (FIFO + socat + owntone)
|   +-- setup-wsl-owntone.sh # One-time owntone build/install script
+-- README.md
+-- IMPLEMENTATION_STATUS.md
```

## Known Limitations

### AirPlay Latency (~2 seconds)

AirPlay 1 (RAOP) has a mandatory ~2-second audio buffer at the receiver. This is built into the protocol for clock synchronization and cannot be reduced. Every audio sample arrives at the HomePod approximately 2 seconds after being captured.

This means:
- **Music/podcasts**: Works great, the delay is not noticeable
- **Video**: Audio will be ~2s behind video (no sync compensation)
- **Gaming**: Not suitable for latency-sensitive use

This is the same latency you get with any AirPlay sender. Apple TV compensates by delaying the video to match the audio. owntone uses AirPlay 1 because AirPlay 2 is proprietary and not reverse-engineered for open-source use.

### Audio Source

WASAPI loopback captures the default audio output device. Applications that output to a different device (e.g., Spotify in exclusive mode) may not be captured. Ensure your audio plays through the default Windows output device.

### Windows 11

WSL2 mirrored networking requires Windows 11. The WASAPI capture itself works on Windows 10+, but owntone in WSL2 needs mirrored networking for mDNS discovery.

## Troubleshooting

### owntone won't start

- Ensure WSL2 Ubuntu is installed: `wsl -l -v` should show "Ubuntu" with version 2
- Ensure owntone is installed: `wsl -d Ubuntu -- owntone --version`
- Check logs in `%APPDATA%\HomePodStreamer\Logs\`

### No devices found

- Verify mirrored networking: `wsl -d Ubuntu -- ip addr` should show a `192.168.x.x` address
- Ensure HomePod is powered on and on the same network
- Check owntone API directly: `Invoke-RestMethod http://127.0.0.1:3689/api/outputs`
- Restart Avahi in WSL: `wsl -d Ubuntu -u root -- avahi-daemon -D`

### No sound after starting stream

- Check Windows Firewall: HomePod needs to connect back to your PC
- Verify the firewall rule covers your HomePod's IP
- In Apple Home app, ensure "Allow Speaker Access" is set to "Everyone"
- Check logs for "evrtsp_read: read timeout" (indicates firewall blocking)

### Audio stuttering

- Check Wi-Fi signal strength
- Use wired Ethernet if possible
- Check CPU usage (resampling + encoding runs continuously)

### App logs

```
%APPDATA%\HomePodStreamer\Logs\log_YYYYMMDD.txt
```

Settings are stored in:
```
%APPDATA%\HomePodStreamer\settings.json
```

## Technology Stack

- **Language**: C# / .NET 9
- **UI**: WPF with MVVM (CommunityToolkit.Mvvm)
- **Audio Capture**: NAudio (WASAPI loopback)
- **AirPlay Backend**: owntone-server in WSL2
- **Device Discovery**: owntone HTTP API
- **System Tray**: Hardcodet.NotifyIcon.Wpf
- **DI**: Microsoft.Extensions.DependencyInjection

## License

This project uses the following open-source libraries:

- [NAudio](https://github.com/naudio/NAudio) - MIT License
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) - MIT License
- [owntone-server](https://github.com/owntone/owntone-server) - GPL-2.0 License
- [Hardcodet.NotifyIcon.Wpf](https://github.com/hardcodet/wpf-notifyicon) - CPOL License

---

**Disclaimer**: This project is not affiliated with or endorsed by Apple Inc. AirPlay and HomePod are trademarks of Apple Inc.
