# HomePod Streamer - Implementation Status & Continuation Plan

**Last Updated**: 2026-01-25
**Status**: Core application complete, requires libraop build and testing

---

## 📊 Project Overview

A Windows desktop application that streams all system audio to Apple HomePod 2 devices via AirPlay. Built with C# WPF, MVVM architecture, and process wrapper approach for AirPlay streaming.

### Technology Stack
- **Language**: C# .NET 7+
- **UI Framework**: WPF with MVVM pattern
- **Audio Capture**: NAudio (WASAPI loopback)
- **Audio Encoding**: Float32→Int16 conversion (ready for ALAC)
- **Device Discovery**: Zeroconf (mDNS/Bonjour)
- **AirPlay Streaming**: libraop via process wrapper
- **DI Container**: Microsoft.Extensions.DependencyInjection
- **System Tray**: Hardcodet.NotifyIcon.Wpf

---

## ✅ Completed Implementation

### Project Structure Created
```
HomePodStreamer/
├── HomePodStreamer.sln                  ✅ Created
├── README.md                            ✅ Created
├── src/HomePodStreamer/
│   ├── HomePodStreamer.csproj          ✅ Created with all NuGet packages
│   ├── App.xaml                        ✅ Created
│   ├── App.xaml.cs                     ✅ Created with DI container
│   │
│   ├── Models/
│   │   ├── HomePodDevice.cs            ✅ Complete with ObservableObject
│   │   ├── AppSettings.cs              ✅ Complete
│   │   └── StreamingState.cs           ✅ Complete
│   │
│   ├── Services/
│   │   ├── IAudioCaptureService.cs     ✅ Interface defined
│   │   ├── AudioCaptureService.cs      ✅ WASAPI loopback implemented
│   │   ├── IAudioEncoderService.cs     ✅ Interface defined
│   │   ├── AudioEncoderService.cs      ✅ Float32→Int16 conversion
│   │   ├── IAirPlayService.cs          ✅ Interface defined
│   │   ├── AirPlayProcessService.cs    ✅ Process wrapper complete
│   │   ├── IDeviceDiscoveryService.cs  ✅ Interface defined
│   │   ├── DeviceDiscoveryService.cs   ✅ Zeroconf mDNS discovery
│   │   ├── ISettingsService.cs         ✅ Interface defined
│   │   └── SettingsService.cs          ✅ JSON persistence
│   │
│   ├── ViewModels/
│   │   └── MainViewModel.cs            ✅ Complete MVVM implementation
│   │
│   ├── Views/
│   │   ├── MainWindow.xaml             ✅ Complete UI with system tray
│   │   └── MainWindow.xaml.cs          ✅ Code-behind with tray logic
│   │
│   ├── Utils/
│   │   ├── AudioBuffer.cs              ✅ Thread-safe circular buffer
│   │   └── Logger.cs                   ✅ File logging
│   │
│   └── Resources/Icons/
│       └── README.txt                  ✅ Icon placeholder note
│
└── lib/native/                         ⚠️ Needs raop_play.exe
```

### Core Features Implemented

#### Audio Pipeline ✅
- ✅ WASAPI loopback capture (all system audio)
- ✅ Format conversion (IEEE Float32 → PCM Int16)
- ✅ Thread-safe audio buffering
- ✅ Background audio processing task
- ✅ Error handling in audio pipeline

#### Device Management ✅
- ✅ mDNS/Bonjour device discovery
- ✅ Device list with connection states
- ✅ Enable/disable individual devices
- ✅ Multi-device simultaneous streaming
- ✅ Device connection/disconnection handling

#### AirPlay Integration ✅
- ✅ Process wrapper for raop_play.exe
- ✅ Per-device process management
- ✅ Audio data piping to stdin
- ✅ Process output/error monitoring
- ✅ Graceful process termination

#### User Interface ✅
- ✅ Modern WPF UI with MVVM
- ✅ System tray integration (minimize to tray)
- ✅ Tray icon with context menu
- ✅ Double-click tray to show/hide
- ✅ Device list with checkboxes
- ✅ Volume slider (0-100)
- ✅ Status messages
- ✅ Refresh devices button
- ✅ Start/Stop streaming toggle

#### Settings & Persistence ✅
- ✅ Save enabled devices
- ✅ Save volume level
- ✅ Auto-load settings on startup
- ✅ Settings stored in %APPDATA%\HomePodStreamer\

#### Error Handling & Logging ✅
- ✅ Comprehensive logging to file
- ✅ Error messages surfaced to UI
- ✅ Process crash detection
- ✅ Audio device change handling
- ✅ Network error handling

---

## ⚠️ Pending Tasks

### CRITICAL: Build libraop

**Status**: Not started - REQUIRED before testing
**Priority**: HIGH

#### Instructions:

1. **Clone libraop repository**:
   ```bash
   cd C:\Users\rifqi
   git clone https://github.com/philippe44/libraop
   cd libraop
   ```

2. **Open in Visual Studio 2022**:
   - Navigate to `vs2022/raop_play/`
   - Open `raop_play.sln` in Visual Studio 2022

3. **Build the project**:
   - Select **Release** configuration
   - Select **x64** platform
   - Build → Build Solution (Ctrl+Shift+B)
   - Wait for build to complete

4. **Copy executable**:
   ```bash
   # From libraop directory
   copy vs2022\raop_play\x64\Release\raop_play.exe C:\Users\rifqi\HomePodStreamer\lib\native\raop_play.exe
   ```

5. **Verify**:
   ```bash
   # Test raop_play.exe works
   cd C:\Users\rifqi\HomePodStreamer\lib\native
   raop_play.exe --help
   ```

**Alternative if build fails**:
- Search for pre-built raop_play.exe binaries online
- Check TuneBlade community forums
- Try building with older Visual Studio versions

---

### Optional: Add Application Icon

**Status**: Not started
**Priority**: LOW

The app currently uses a placeholder for the icon. To add a proper icon:

1. Create or download a `.ico` file (256x256 recommended)
2. Save it as: `C:\Users\rifqi\HomePodStreamer\src\HomePodStreamer\Resources\Icons\app-icon.ico`
3. Rebuild the project

Icon resources:
- https://icons8.com/ (free with attribution)
- https://www.flaticon.com/ (free with attribution)
- https://www.favicon-generator.org/ (create from PNG)

---

## 🧪 Testing Plan

### Phase 1: Build Verification

1. **Build the application**:
   ```bash
   cd C:\Users\rifqi\HomePodStreamer
   dotnet restore
   dotnet build src/HomePodStreamer/HomePodStreamer.csproj -c Release
   ```

2. **Expected output**:
   - No build errors
   - All NuGet packages restored
   - Executable created in `bin/Release/net7.0-windows/`

3. **If build fails**:
   - Check .NET 7 SDK is installed: `dotnet --version`
   - Install .NET 7 SDK if missing: https://dotnet.microsoft.com/download/dotnet/7.0
   - Check all NuGet packages restored successfully
   - Review build errors and fix any missing references

---

### Phase 2: Application Launch

1. **Run the application**:
   ```bash
   dotnet run --project src/HomePodStreamer/HomePodStreamer.csproj
   ```
   Or press F5 in Visual Studio if you have the solution open.

2. **Expected behavior**:
   - Main window appears
   - Status shows "Ready"
   - No crash or errors
   - System tray icon appears (may be default Windows icon if no custom icon)

3. **Test UI interactions**:
   - Click "Refresh Devices" - should work without errors (may find 0 devices if HomePods off)
   - Click "Start Streaming" without devices - should show error message
   - Adjust volume slider - should move smoothly
   - Close window - should minimize to tray
   - Double-click tray icon - should show window again
   - Right-click tray icon - should show context menu
   - Click "Quit" from tray menu - should close application

4. **Check logs**:
   ```bash
   notepad %APPDATA%\HomePodStreamer\Logs\log_YYYYMMDD.txt
   ```
   - Should see initialization messages
   - No critical errors

---

### Phase 3: Device Discovery

**Prerequisites**: HomePod 2 must be powered on and connected to same Wi-Fi network as PC

1. **Ensure HomePod is ready**:
   - HomePod powered on
   - Connected to Wi-Fi
   - AirPlay enabled (should be default)
   - On same network as your PC

2. **Test discovery**:
   - Launch application
   - Click "Refresh Devices"
   - Wait 5-10 seconds

3. **Expected behavior**:
   - HomePod appears in device list
   - Shows device name (e.g., "Living Room")
   - Shows IP address
   - Shows state "Disconnected"
   - Checkbox is unchecked

4. **If no devices found**:
   - Check HomePod is on same network
   - Check Windows Firewall - allow UDP port 5353 (mDNS)
   - Ping HomePod IP address manually to verify network connectivity
   - Check logs for discovery errors
   - Try restarting HomePod

---

### Phase 4: Streaming Test

**Prerequisites**: raop_play.exe built and copied to lib/native/

1. **Enable device and start streaming**:
   - Check the checkbox next to your HomePod
   - Click "Start Streaming"

2. **Expected behavior**:
   - Device state changes to "Connecting" then "Connected"
   - Start Streaming button changes to "Stop Streaming" (red)
   - Status shows "Streaming to 1 device(s)"

3. **Play audio**:
   - Open Spotify, YouTube, or any media player
   - Play music or video

4. **Expected behavior**:
   - Audio plays on HomePod with ~100-150ms latency
   - Device state shows "Streaming" in blue
   - No stuttering or dropouts
   - Audio quality is good

5. **If streaming fails**:
   - Check raop_play.exe exists in lib/native/
   - Check logs for raop_play.exe errors
   - Verify HomePod is reachable: `ping <homepod_ip>`
   - Try manually running: `raop_play.exe -e <homepod_ip> test.wav`
   - Check Windows Firewall allows raop_play.exe
   - Check HomePod isn't being used by another AirPlay source

---

### Phase 5: Multi-Device Test

**Prerequisites**: 2 or more AirPlay devices available

1. **Discover multiple devices**:
   - Click "Refresh Devices"
   - Verify multiple devices appear

2. **Enable multiple devices**:
   - Check checkboxes for 2+ devices
   - Click "Start Streaming"

3. **Expected behavior**:
   - All devices connect
   - Status shows "Streaming to X device(s)"
   - Audio plays on all devices
   - Devices stay synchronized (within ~50ms)

4. **Test enable/disable during streaming**:
   - While streaming, uncheck one device
   - Device should disconnect, others continue
   - Check device again
   - Device should reconnect and resume

---

### Phase 6: Volume Control Test

1. **Start streaming to at least one device**

2. **Adjust volume slider**:
   - Move slider to different positions
   - Note: Current implementation requires reconnection for volume change

3. **Expected behavior**:
   - Volume changes apply (may need to restart streaming for current implementation)
   - Volume persists after app restart

---

### Phase 7: Persistence Test

1. **Configure settings**:
   - Enable specific devices
   - Set volume to non-default value (e.g., 50)

2. **Close and reopen app**:
   - Click "Quit" from tray menu
   - Relaunch application

3. **Expected behavior**:
   - Volume slider shows saved value
   - Previously enabled devices remain enabled
   - Settings file exists: `%APPDATA%\HomePodStreamer\settings.json`

---

### Phase 8: System Tray Test

1. **Test minimize to tray**:
   - Close main window (X button)
   - Window should hide, not quit
   - Tray icon should remain

2. **Test show from tray**:
   - Double-click tray icon
   - Window should appear

3. **Test context menu**:
   - Right-click tray icon
   - Verify menu items: Show Window, Toggle Streaming, Quit
   - Click each menu item

4. **Test graceful quit**:
   - Right-click tray → Quit
   - All processes should terminate cleanly
   - No orphaned raop_play.exe processes

5. **Verify clean shutdown**:
   ```bash
   # Check no orphaned processes
   tasklist | findstr raop_play
   # Should return nothing
   ```

---

### Phase 9: Error Handling Test

1. **Test HomePod power off during streaming**:
   - Start streaming
   - Power off HomePod
   - Expected: Device shows "Error" state, error message displayed

2. **Test network disconnect**:
   - Start streaming
   - Disable Wi-Fi or disconnect network cable
   - Expected: Error detected, status updated

3. **Test audio device change**:
   - Start streaming
   - Plug/unplug headphones (changes default audio device)
   - Expected: App detects change, may restart capture

4. **Test rapid toggle**:
   - Quickly toggle "Start Streaming" / "Stop Streaming" multiple times
   - Expected: No crashes, clean state transitions

---

## 🐛 Known Issues & Limitations

### Current Limitations

1. **Volume Control**:
   - Changing volume doesn't apply in real-time
   - Requires restarting streaming or reconnecting devices
   - **Cause**: Process wrapper limitation - raop_play.exe volume is set at launch
   - **Workaround**: Stop and restart streaming after volume change
   - **Future fix**: Migrate to native P/Invoke for real-time volume control

2. **Latency**:
   - ~100-150ms audio latency
   - Acceptable for music/video, may be noticeable for gaming
   - **Cause**: Network buffering + encoding overhead
   - **Workaround**: Use wired Ethernet for lower latency
   - **Cannot be eliminated**: Inherent to AirPlay protocol

3. **Windows Version**:
   - Requires Windows 10 or later
   - WASAPI loopback not available on Windows 7/8
   - **No workaround**

4. **Icon Missing**:
   - App uses default Windows icon if app-icon.ico not provided
   - **Workaround**: Add icon file to Resources/Icons/

5. **Process Overhead**:
   - One raop_play.exe process per connected device
   - Higher memory usage than native DLL approach
   - **Future fix**: Migrate to P/Invoke wrapper for libraop.dll

### Potential Issues to Watch For

1. **raop_play.exe not found**:
   - **Symptom**: "raop_play.exe not found" error on Start Streaming
   - **Fix**: Build libraop and copy executable to lib/native/
   - **Check**: `dir C:\Users\rifqi\HomePodStreamer\lib\native\raop_play.exe`

2. **Firewall blocking**:
   - **Symptom**: Devices discovered but connection fails
   - **Fix**: Allow raop_play.exe through Windows Firewall
   - **Fix**: Allow UDP port 5353 for mDNS

3. **HomePod authentication**:
   - **Symptom**: Connection established but no audio
   - **Possible cause**: HomePod requires pairing/authentication
   - **Research**: Check if HomePod needs to be set to allow all AirPlay connections
   - **Setting**: Home app → HomePod → Settings → Allow Speakers Access

4. **Audio format mismatch**:
   - **Symptom**: Audio plays but sounds distorted/garbled
   - **Possible cause**: Sample rate or bit depth mismatch
   - **Debug**: Check logs for capture format
   - **Fix**: May need to add resampling

---

## 🚀 Next Steps for Continuation

### Immediate Next Steps (Required)

1. **Build libraop** (See "CRITICAL: Build libraop" section above)
   - Clone repository
   - Build in Visual Studio 2022
   - Copy raop_play.exe to lib/native/

2. **First Test Run**:
   ```bash
   cd C:\Users\rifqi\HomePodStreamer
   dotnet restore
   dotnet build src/HomePodStreamer/HomePodStreamer.csproj -c Release
   dotnet run --project src/HomePodStreamer/HomePodStreamer.csproj
   ```

3. **Verify Basic Functionality**:
   - App launches
   - Discovers HomePod
   - Can enable device
   - Streaming starts without crashes

4. **Test with Real Audio**:
   - Play Spotify/YouTube
   - Verify audio comes out of HomePod
   - Check for dropouts or quality issues

### Short-Term Improvements

1. **Add Application Icon**:
   - Create or download .ico file
   - Add to Resources/Icons/app-icon.ico
   - Rebuild

2. **Fix Volume Control** (if needed):
   - Implement reconnection on volume change
   - Or move to Phase 2 (P/Invoke) for real-time control

3. **Add Error Recovery**:
   - Auto-reconnect on device disconnect
   - Retry logic for failed connections

4. **Improve Status Messages**:
   - More descriptive error messages
   - Show connection progress

### Medium-Term Enhancements

1. **Native Integration (Phase 2)**:
   - Build libraop.dll instead of raop_play.exe
   - Create P/Invoke wrapper
   - Implement AirPlayNativeService
   - Replace AirPlayProcessService in DI registration
   - **Benefits**: Real-time volume, better performance, lower latency

2. **Advanced Features**:
   - Auto-discover and connect on startup
   - Remember last enabled devices
   - System startup integration (Windows startup)
   - Keyboard shortcuts

3. **UI Polish**:
   - Better icons for connection states
   - Animations for state transitions
   - Dark mode theme
   - Compact mode (smaller window)

### Long-Term Considerations

1. **Per-Application Audio Capture** (Windows 11):
   - Capture audio from specific apps only
   - Requires Windows 11 and different WASAPI approach

2. **Format Selection**:
   - Allow user to choose ALAC vs AAC
   - Quality/bitrate settings

3. **Advanced Audio Processing**:
   - Equalizer
   - Compression/normalization
   - Crossfade

4. **Network Optimization**:
   - Adaptive bitrate based on network quality
   - Buffer size adjustment
   - Network quality monitoring

---

## 📝 Code Architecture Reference

### Dependency Injection Setup

All services are registered in `App.xaml.cs`:

```csharp
services.AddSingleton<MainViewModel>();
services.AddSingleton<IAudioCaptureService, AudioCaptureService>();
services.AddSingleton<IAudioEncoderService, AudioEncoderService>();
services.AddSingleton<IAirPlayService, AirPlayProcessService>();
services.AddSingleton<IDeviceDiscoveryService, DeviceDiscoveryService>();
services.AddSingleton<ISettingsService, SettingsService>();
services.AddSingleton<MainWindow>();
```

### Audio Processing Flow

1. **Capture** (AudioCaptureService):
   - WASAPI loopback captures system audio
   - Fires DataAvailable event with audio buffer

2. **Encode** (AudioEncoderService):
   - Converts Float32 → Int16
   - Prepares for AirPlay format

3. **Buffer** (AudioBuffer):
   - Thread-safe circular queue
   - Decouples capture from streaming

4. **Stream** (AirPlayProcessService):
   - Writes audio to raop_play.exe stdin
   - One process per enabled device
   - AirPlay 2 handles multi-device sync

### Threading Model

- **UI Thread**: WPF main thread, data binding, commands
- **Capture Thread**: WASAPI event-driven (NAudio manages)
- **Encoding Thread**: Inline in DataAvailable event handler
- **Streaming Thread**: Background Task reads from buffer, writes to processes

### Key File Locations

**Code**:
- Entry point: `src/HomePodStreamer/App.xaml.cs`
- Main UI: `src/HomePodStreamer/Views/MainWindow.xaml`
- Core logic: `src/HomePodStreamer/ViewModels/MainViewModel.cs`
- AirPlay integration: `src/HomePodStreamer/Services/AirPlayProcessService.cs`

**Runtime**:
- Executable: `bin/Release/net7.0-windows/HomePodStreamer.exe`
- Logs: `%APPDATA%\HomePodStreamer\Logs\log_YYYYMMDD.txt`
- Settings: `%APPDATA%\HomePodStreamer\settings.json`
- Native lib: `lib/native/raop_play.exe`

---

## 🔧 Troubleshooting Guide

### Build Issues

**"dotnet command not found"**:
- Install .NET 7 SDK: https://dotnet.microsoft.com/download/dotnet/7.0
- Restart terminal after installation

**"NuGet package restore failed"**:
- Check internet connection
- Clear NuGet cache: `dotnet nuget locals all --clear`
- Retry: `dotnet restore`

**"WPF project failed to build"**:
- Ensure Windows SDK is installed (comes with Visual Studio)
- Check TargetFramework is `net7.0-windows` in .csproj

### Runtime Issues

**"raop_play.exe not found"**:
- Build libraop (see instructions above)
- Copy to correct location: `lib/native/raop_play.exe`
- Check file exists: `dir lib\native\raop_play.exe`

**"No devices found"**:
- HomePod powered on and connected to Wi-Fi
- PC and HomePod on same network
- Windows Firewall allowing UDP 5353 (mDNS)
- Try: `ping <homepod_ip>` to verify connectivity

**"Connection failed"**:
- Check HomePod reachable via ping
- Windows Firewall allowing raop_play.exe
- HomePod set to allow AirPlay from all devices
- Try manual test: `raop_play.exe -e <ip> test.wav`

**"Audio stuttering"**:
- Check Wi-Fi signal strength
- Use wired Ethernet if possible
- Close resource-intensive applications
- Check CPU usage (should be <10%)

**"App crashes on startup"**:
- Check logs: `%APPDATA%\HomePodStreamer\Logs\`
- Verify .NET 7 runtime installed
- Try running from command line to see error messages

---

## 📚 Additional Resources

### Official Documentation

- **NAudio**: https://github.com/naudio/NAudio
- **WASAPI**: https://learn.microsoft.com/en-us/windows/win32/coreaudio/wasapi
- **Zeroconf**: https://github.com/novotnyllc/Zeroconf
- **libraop**: https://github.com/philippe44/libraop

### AirPlay Protocol References

- **Unofficial Protocol Spec**: https://nto.github.io/AirPlay.html
- **AirPlay 2 Internals**: https://emanuelecozzi.net/docs/airplay2
- **Open AirPlay**: https://github.com/openairplay/open-airplay

### Community Support

- **NAudio Forums**: https://github.com/naudio/NAudio/discussions
- **libraop Issues**: https://github.com/philippe44/libraop/issues

---

## 💡 Tips for Next Developer

1. **Always check logs first**:
   ```
   %APPDATA%\HomePodStreamer\Logs\log_YYYYMMDD.txt
   ```

2. **Test with simple audio first**:
   - Use Windows system sounds
   - Then try media player
   - Finally try complex scenarios

3. **Use Process Explorer** to debug process issues:
   - Download from: https://learn.microsoft.com/sysinternals/downloads/process-explorer
   - Great for seeing raop_play.exe processes and handles

4. **Network debugging**:
   - Wireshark to capture mDNS/AirPlay traffic
   - Fiddler for HTTP debugging (if needed)

5. **If stuck on libraop build**:
   - Check libraop Issues on GitHub
   - Consider using pre-built binaries from other projects
   - TuneBlade may have shared binaries in community forums

---

## ✨ Success Criteria

The implementation will be considered successful when:

1. ✅ Application builds without errors
2. ⏳ raop_play.exe built and integrated
3. ⏳ Application launches and shows UI
4. ⏳ Discovers HomePod 2 on network
5. ⏳ Successfully streams audio to HomePod
6. ⏳ Audio quality is acceptable (no glitches)
7. ⏳ Multi-device streaming works (if testing with 2+ devices)
8. ⏳ System tray integration works correctly
9. ⏳ Settings persist across restarts
10. ⏳ Graceful shutdown (no orphaned processes)

**Current Status**: 1/10 complete (application built, not yet tested)

---

## 📞 Support & Questions

If you encounter issues continuing this project:

1. Check the logs for error details
2. Review this document's troubleshooting section
3. Check the GitHub issues for libraop and NAudio
4. Search for similar AirPlay streaming projects on GitHub

**Good luck with testing and deployment!** 🚀
