# Quick Start Guide - HomePod Streamer

**For the next agent/session continuing this project**

---

## 🎯 Where We Are

The complete Windows application has been built. All code is ready. The only thing missing is the native AirPlay library.

**Status**: ✅ Code 100% complete → ⏳ Waiting for libraop build → ⏳ Ready to test

---

## 🚀 Next Actions (In Order)

### 1. Build libraop (CRITICAL - Do This First!)

```bash
# Step 1: Clone libraop
cd C:\Users\rifqi
git clone https://github.com/philippe44/libraop
cd libraop

# Step 2: Open in Visual Studio 2022
# Navigate to: vs2022/raop_play/
# Open: raop_play.sln

# Step 3: Build
# - Configuration: Release
# - Platform: x64
# - Build → Build Solution (Ctrl+Shift+B)

# Step 4: Copy executable
copy vs2022\raop_play\x64\Release\raop_play.exe C:\Users\rifqi\HomePodStreamer\lib\native\raop_play.exe

# Step 5: Verify
C:\Users\rifqi\HomePodStreamer\lib\native\raop_play.exe --help
```

### 2. Build the Application

```bash
cd C:\Users\rifqi\HomePodStreamer
dotnet restore
dotnet build src/HomePodStreamer/HomePodStreamer.csproj -c Release
```

### 3. Run the Application

```bash
dotnet run --project src/HomePodStreamer/HomePodStreamer.csproj
```

Or open `HomePodStreamer.sln` in Visual Studio and press F5.

### 4. Test with HomePod

1. Click "Refresh Devices"
2. Check your HomePod
3. Click "Start Streaming"
4. Play audio (Spotify, YouTube, etc.)
5. Audio should play on HomePod

---

## 📁 Important Files

### Must Read
- [IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md) - Complete status, testing plan, troubleshooting
- [README.md](README.md) - User documentation

### Key Code Files
- `src/HomePodStreamer/App.xaml.cs` - DI setup
- `src/HomePodStreamer/ViewModels/MainViewModel.cs` - Core logic
- `src/HomePodStreamer/Services/AirPlayProcessService.cs` - AirPlay integration
- `src/HomePodStreamer/Views/MainWindow.xaml` - UI

### Runtime Locations
- Logs: `%APPDATA%\HomePodStreamer\Logs\`
- Settings: `%APPDATA%\HomePodStreamer\settings.json`
- Native lib: `lib/native/raop_play.exe` ← **YOU NEED TO PUT THIS HERE**

---

## ⚠️ If Something Goes Wrong

### Can't build libraop?
- Check Visual Studio 2022 is installed with C++ workload
- Try older VS versions (VS2019)
- Search for pre-built raop_play.exe online
- Check libraop GitHub issues

### Application won't build?
```bash
# Check .NET version
dotnet --version  # Should be 7.x

# If not installed, download from:
# https://dotnet.microsoft.com/download/dotnet/7.0

# Clear and restore
dotnet clean
dotnet restore
dotnet build
```

### HomePod not discovered?
- HomePod powered on?
- Same Wi-Fi network?
- Windows Firewall allow UDP 5353?
- Try: `ping <homepod_ip>`

### Check logs if anything fails:
```bash
notepad %APPDATA%\HomePodStreamer\Logs\log_*.txt
```

---

## 📋 What's Already Done

✅ Complete WPF application with MVVM
✅ Audio capture (WASAPI loopback)
✅ Format conversion (Float32 → Int16)
✅ Device discovery (mDNS/Bonjour)
✅ AirPlay process wrapper
✅ Multi-device support
✅ System tray integration
✅ Settings persistence
✅ Error handling & logging

---

## 🎯 What's Next

1. Build libraop → raop_play.exe
2. Test with HomePod 2
3. Fix any bugs found during testing
4. Optional: Add application icon
5. Optional: Migrate to native P/Invoke (Phase 2)

---

## 💡 Pro Tips

- **Test incrementally**: First just launch app, then discovery, then streaming
- **Check logs constantly**: `%APPDATA%\HomePodStreamer\Logs\`
- **Use Process Explorer**: See if raop_play.exe processes are running
- **Test with system sounds first**: Before trying Spotify/YouTube
- **One device first**: Test single HomePod before multiple

---

**Read [IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md) for complete details!**

Good luck! 🚀
