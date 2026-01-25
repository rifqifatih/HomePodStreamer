# HomePod Streamer

A Windows desktop application that streams all system audio to Apple HomePod 2 devices via AirPlay.

## Features

- Stream all system audio (games, Spotify, browser, etc.) to HomePod devices
- Support for multiple HomePods simultaneously with automatic synchronization
- System tray integration for background operation
- Volume control
- Persistent settings (remembers enabled devices and volume)
- Simple, minimal UI

## Requirements

- Windows 10 or later
- .NET 7.0 or later
- Visual Studio 2022 (for building libraop)
- HomePod 2 or other AirPlay 2 compatible devices on the same network

## Building the Project

### Step 1: Clone and Build libraop

The application requires `raop_play.exe` from the libraop library to handle AirPlay streaming.

1. **Clone libraop repository:**
   ```bash
   git clone https://github.com/philippe44/libraop
   cd libraop
   ```

2. **Open in Visual Studio 2022:**
   - Open `vs2022/raop_play/raop_play.sln` in Visual Studio 2022

3. **Build the project:**
   - Select **Release** configuration
   - Select **x64** platform
   - Build → Build Solution (or press Ctrl+Shift+B)

4. **Copy the executable:**
   - Find `raop_play.exe` in `vs2022/raop_play/x64/Release/`
   - Copy it to `HomePodStreamer/lib/native/raop_play.exe`

   ```bash
   # From the libraop directory:
   copy vs2022\raop_play\x64\Release\raop_play.exe ..\HomePodStreamer\lib\native\
   ```

### Step 2: Build HomePod Streamer

1. **Restore NuGet packages:**
   ```bash
   cd HomePodStreamer
   dotnet restore
   ```

2. **Build the application:**
   ```bash
   dotnet build src/HomePodStreamer/HomePodStreamer.csproj -c Release
   ```

3. **Run the application:**
   ```bash
   dotnet run --project src/HomePodStreamer/HomePodStreamer.csproj
   ```

   Or open `HomePodStreamer.sln` in Visual Studio and press F5.

## Usage

### First Time Setup

1. Launch HomePodStreamer
2. Click **"Refresh Devices"** to discover HomePods on your network
3. Enable the HomePods you want to stream to by checking the boxes
4. Click **"Start Streaming"**
5. Play any audio on your PC - it will stream to the enabled HomePods

### System Tray

- The app minimizes to the system tray when closed
- **Double-click** the tray icon to show/hide the window
- **Right-click** the tray icon for quick actions:
  - Show Window
  - Toggle Streaming
  - Quit (graceful shutdown)

### Volume Control

- Use the slider at the bottom to control volume
- Volume applies to all connected devices
- Settings are saved automatically

## Project Structure

```
HomePodStreamer/
├── src/HomePodStreamer/
│   ├── Models/              # Data models
│   ├── Services/            # Business logic services
│   ├── ViewModels/          # MVVM ViewModels
│   ├── Views/               # WPF UI
│   ├── Utils/               # Utility classes
│   └── Resources/           # Icons and assets
├── lib/native/              # Native dependencies (raop_play.exe)
└── README.md
```

## Architecture

### Technology Stack

- **Language**: C# .NET 7+
- **UI Framework**: WPF with MVVM pattern
- **Audio Capture**: NAudio (WASAPI loopback)
- **Device Discovery**: Zeroconf (mDNS/Bonjour)
- **AirPlay Streaming**: libraop (process wrapper)
- **DI Container**: Microsoft.Extensions.DependencyInjection

### Audio Pipeline

```
System Audio → WASAPI Loopback → Format Conversion (float32→int16)
    → Audio Buffer → raop_play.exe → AirPlay → HomePod
```

### Key Services

- **AudioCaptureService**: Captures all system audio using WASAPI loopback
- **AudioEncoderService**: Converts audio format for AirPlay compatibility
- **DeviceDiscoveryService**: Discovers HomePods via mDNS
- **AirPlayProcessService**: Manages raop_play.exe processes and audio streaming
- **SettingsService**: Persists user preferences

## Troubleshooting

### "raop_play.exe not found"

Make sure you've built libraop and copied `raop_play.exe` to `lib/native/` directory.

### No devices found

- Ensure HomePods are powered on and connected to the same Wi-Fi network
- Check firewall settings - mDNS requires UDP port 5353
- Try restarting the HomePods

### Audio stuttering or dropouts

- Check Wi-Fi signal strength
- Close unnecessary applications to free up CPU/memory
- Try connecting your PC to the router via Ethernet cable

### Process crashes immediately

- Check the logs in `%APPDATA%\HomePodStreamer\Logs\`
- Ensure HomePod is reachable (ping the IP address)
- Try restarting both the app and the HomePod

## Logs

Application logs are stored in:
```
%APPDATA%\HomePodStreamer\Logs\log_YYYYMMDD.txt
```

Settings are stored in:
```
%APPDATA%\HomePodStreamer\settings.json
```

## Known Limitations

1. **Latency**: ~100-150ms latency may be noticeable for gaming
2. **Volume Control**: Changing volume requires restarting connections (limitation of process wrapper approach)
3. **Windows 10+**: Requires Windows 10 or later for WASAPI loopback
4. **Network**: Requires stable Wi-Fi; wired Ethernet recommended for best quality

## Future Improvements (Phase 2)

- Migrate to native P/Invoke integration with libraop.dll for better performance
- Per-application audio capture (Windows 11)
- Real-time volume control without reconnection
- Audio format selection (ALAC vs AAC)
- Auto-discovery and connection on startup

## License

This project uses the following open-source libraries:

- **NAudio**: MIT License
- **Zeroconf**: MIT License
- **CommunityToolkit.Mvvm**: MIT License
- **libraop**: Apache 2.0 License

## Contributing

This is a personal project, but suggestions and bug reports are welcome via GitHub issues.

## Credits

- **libraop** by philippe44: https://github.com/philippe44/libraop
- AirPlay protocol documentation from the open-source community

---

**Disclaimer**: This project is not affiliated with or endorsed by Apple Inc. AirPlay is a trademark of Apple Inc.
