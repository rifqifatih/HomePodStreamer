# Plan: Remove owntone/WSL Dependency — Native AirPlay 1 (RAOP) in C#

## Context

HomePodStreamer currently delegates all AirPlay protocol work to owntone-server running in WSL2 Ubuntu. The app captures system audio, encodes it to PCM, and sends it via TCP to owntone, which handles RAOP/RTSP/RTP streaming, mDNS discovery, encryption, and volume control. This requires WSL2 with mirrored networking, owntone built from source, socat, Avahi, D-Bus, and a FIFO pipe — a fragile multi-process chain.

The goal is to implement RAOP natively in C# so the app is fully self-contained on Windows with no external dependencies beyond .NET 9.

---

## What owntone Provides (Must Be Replaced)

| Capability | Current mechanism | Replacement |
|---|---|---|
| Device discovery | Avahi mDNS in WSL → owntone API `/api/outputs` | `Makaretu.Dns.Multicast` NuGet — browse `_raop._tcp` |
| RTSP session | owntone internal | New `RtspClient` class |
| Audio encoding | PCM pipe input → owntone re-encodes to ALAC | New `AlacEncoder` class |
| RTP streaming | owntone internal | New `RtpAudioSender` class (UDP) |
| Timing sync | owntone internal | New `RtpTimingChannel` class (NTP-like) |
| Retransmission | owntone internal | New `RtpControlChannel` class |
| Encryption | owntone internal (RSA key exchange + AES-CBC) | New `RaopCrypto` class (`System.Security.Cryptography`) |
| Volume control | owntone API `PUT /api/outputs/{id}` | RTSP `SET_PARAMETER volume:` on each session |
| Process management | WSL2 process, socat, FIFO, Avahi, D-Bus | Eliminated entirely |

---

## New Architecture

```
WASAPI Capture → AudioEncoder (S16LE 44.1kHz) → AudioBuffer
    ↓
NativeAirPlayService.SendAudioToAll()
    ↓
AlacEncoder (PCM → ALAC frames, 352 samples each)
    ↓
For each connected HomePod:
    RaopSession → RtpAudioSender (encrypt + RTP/UDP)
                → RtpControlChannel (sync packets)
                → RtpTimingChannel (NTP responses)
                → RtspClient (RTSP/TCP control)
```

---

## New Files to Create

### `src/HomePodStreamer/Raop/` (new namespace)

| File | Purpose |
|---|---|
| `RaopConstants.cs` | Apple RSA public key, RTP payload types, ALAC fmtp params, NTP epoch offset |
| `RaopCrypto.cs` | AES-128 key/IV generation, RSA-OAEP encryption of AES key, AES-CBC audio encryption (RAOP-specific: only full 16-byte blocks encrypted, remainder left in clear) |
| `SdpBuilder.cs` | Build SDP body for RTSP ANNOUNCE (codec params, encrypted AES key, IV) |
| `RtspClient.cs` | RTSP TCP client: OPTIONS, ANNOUNCE, SETUP, RECORD, SET_PARAMETER, FLUSH, TEARDOWN. Handles CSeq, Apple-Challenge, Session header, Transport header parsing |
| `AlacEncoder.cs` | ALAC encoding — wraps a native ALAC library via P/Invoke or implements minimal ALAC in C# (see risks). Chunks S16LE PCM into 352-sample frames, encodes each |
| `RtpAudioSender.cs` | Sends encrypted ALAC frames as RTP packets (payload type 96) via UDP. Manages sequence numbers, timestamps (+352 per packet), marker bit, ring buffer for retransmission |
| `RtpControlChannel.cs` | UDP channel: sends sync packets (type 84) every ~1 second, handles retransmit requests (type 85) |
| `RtpTimingChannel.cs` | UDP channel: responds to timing requests (type 82) with timing responses (type 83) containing NTP timestamps |
| `RaopSession.cs` | Orchestrates one full RAOP connection to a single device. Owns RtspClient + all 3 UDP channels + AlacEncoder. Lifecycle: connect → announce → setup → record → stream → teardown |

### `src/HomePodStreamer/Services/` (new implementations)

| File | Purpose |
|---|---|
| `NativeAirPlayService.cs` | Implements `IAirPlayService`. Manages `ConcurrentDictionary<string, RaopSession>` for multi-device. ALAC-encodes PCM once, sends to all sessions. Volume: converts 0-100 → dB scale (-144..0) |
| `MdnsDeviceDiscoveryService.cs` | Implements `IDeviceDiscoveryService`. Uses `Makaretu.Dns.Multicast` to browse `_raop._tcp`. Extracts IP/port/name from SRV+A+TXT records. Instance name format: `AABBCCDDEEFF@DeviceName` |

---

## Files to Modify

| File | Changes |
|---|---|
| `App.xaml.cs` | Remove owntone DI registrations (`IOwntoneApiClient`, `IOwntoneHostService`, `HttpClient`). Register `NativeAirPlayService` and `MdnsDeviceDiscoveryService`. Remove owntone startup/shutdown from `OnStartup`/`OnExit` |
| `MainViewModel.cs` | Remove `IOwntoneHostService` constructor param and field. Remove `_owntoneHostService.IsRunning` gate in `AutoScanDevicesAsync()` |
| `HomePodDevice.cs` | Remove `OwntoneOutputId`. Existing `IPAddress`, `Port`, `RaopTxtRecords` fields become primary |
| `HomePodStreamer.csproj` | Add NuGet packages. Remove `scripts/**` ItemGroup |
| `README.md` | Remove WSL2/owntone prerequisites. Update architecture diagram |
| `IMPLEMENTATION_STATUS.md` | Update to reflect native RAOP |

---

## Files to Delete

```
src/HomePodStreamer/Services/IOwntoneHostService.cs
src/HomePodStreamer/Services/WslOwntoneService.cs
src/HomePodStreamer/Services/IOwntoneApiClient.cs
src/HomePodStreamer/Services/OwntoneApiClient.cs
src/HomePodStreamer/Services/AirPlayOwntoneService.cs
src/HomePodStreamer/Services/HybridDeviceDiscoveryService.cs
src/HomePodStreamer/Models/OwntoneModels.cs
scripts/owntone.conf
scripts/start-owntone.sh
scripts/setup-wsl-owntone.sh
```

---

## NuGet Packages to Add

| Package | Purpose |
|---|---|
| `Makaretu.Dns.Multicast.New` (~0.38.0) | mDNS service discovery — replaces Avahi |

ALAC encoding approach TBD (see risks section) — may be a NuGet package, P/Invoke to native lib, or pure C# implementation.

---

## Implementation Phases

### Phase 1: Protocol Foundation (no UI changes)
- `RaopConstants.cs` — Apple RSA public key, RTP types, ALAC params
- `RaopCrypto.cs` — AES key gen, RSA encryption, AES-CBC audio encryption
- `SdpBuilder.cs` — SDP body builder
- `RtspClient.cs` — RTSP TCP client with full request/response handling

### Phase 2: Audio & Transport
- `AlacEncoder.cs` — ALAC encoding (resolve approach first — see risks)
- `RtpAudioSender.cs` — RTP packet construction and UDP send
- `RtpControlChannel.cs` — sync packets + retransmit handling
- `RtpTimingChannel.cs` — NTP timing responses

### Phase 3: Session & Services
- `RaopSession.cs` — orchestrates single-device RAOP connection
- `NativeAirPlayService.cs` — multi-device management, implements `IAirPlayService`
- `MdnsDeviceDiscoveryService.cs` — mDNS browse, implements `IDeviceDiscoveryService`

### Phase 4: Integration & Cleanup
- Modify `App.xaml.cs`, `MainViewModel.cs`, `HomePodDevice.cs`, `.csproj`
- Delete all owntone files and `scripts/` directory
- Update documentation

### Suggested First Milestone
Hardcode a HomePod IP+port, get a single device to produce sound. This validates the entire RTSP→RTP→ALAC→encryption chain before tackling multi-device and mDNS.

---

## Key Risks

### 1. HomePod Authentication (HIGH RISK)
Modern HomePod firmware may require **FairPlay SAP v2.5** or **HAP pairing** instead of simple RSA key exchange. If the HomePod rejects the legacy RTSP ANNOUNCE, the entire approach needs a much more complex authentication layer. owntone handles this via its `pair_ap` module; pyatv also implements it.
- **Mitigation**: Test with a real HomePod early (Phase 1). If rejected, pyatv's pairing code is the best reference for implementing the newer auth flow.

### 2. ALAC Encoder Availability (MEDIUM RISK)
No well-established .NET ALAC encoder NuGet exists. Options:
- (a) Compile Apple's open-source C reference encoder (`alac_encoder.c`) as a Windows DLL + P/Invoke
- (b) Port the ALAC encoder to pure C# (~500-1000 lines, LPC prediction + Rice coding)
- (c) Send raw PCM instead of ALAC (higher bandwidth, may not be accepted by HomePod)
- **Recommendation**: Start with option (a) since Apple's code is proven and compact.

### 3. RTSP Protocol Correctness (MEDIUM RISK)
Subtle header/format mismatches can cause silent connection failures. Use Wireshark to capture a working owntone→HomePod session as a byte-level reference.

### 4. RTP Timing Precision (MEDIUM RISK)
Incorrect NTP timestamps or inconsistent sync packet intervals cause audio glitches. Use `Stopwatch.GetTimestamp()` for high-resolution timing.

---

## Verification

1. **Phase 1**: Run `RtspClient` against a HomePod IP — verify OPTIONS returns 200 and ANNOUNCE is accepted (not rejected with 403/authentication error)
2. **Phase 2**: Encode a known PCM buffer with `AlacEncoder`, decrypt with a reference decoder, verify roundtrip fidelity
3. **Phase 3**: `RaopSession.ConnectAsync()` to a real HomePod with hardcoded IP — verify audio plays
4. **Phase 4**: Full app — verify mDNS discovers HomePods, connect/disconnect works, volume slider works, multi-device works, app exit cleans up properly
5. **Regression**: All existing UI features still work (device list, enable/disable checkboxes, start/stop streaming, system tray, settings persistence)
