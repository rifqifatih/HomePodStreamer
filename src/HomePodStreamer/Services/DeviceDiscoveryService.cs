using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Zeroconf;
using HomePodStreamer.Models;
using HomePodStreamer.Utils;

namespace HomePodStreamer.Services
{
    public class DeviceDiscoveryService : IDeviceDiscoveryService
    {
        private const string AirPlayServiceType = "_airplay._tcp.local.";

        public ObservableCollection<HomePodDevice> DiscoveredDevices { get; }

        public DeviceDiscoveryService()
        {
            DiscoveredDevices = new ObservableCollection<HomePodDevice>();
        }

        public async Task StartDiscoveryAsync()
        {
            try
            {
                Logger.Info("Starting AirPlay device discovery");
                DiscoveredDevices.Clear();

                var responses = await ZeroconfResolver.ResolveAsync(
                    AirPlayServiceType,
                    scanTime: TimeSpan.FromSeconds(5)
                );

                foreach (var response in responses)
                {
                    try
                    {
                        var service = response.Services.FirstOrDefault().Value;
                        if (service == null) continue;

                        var device = new HomePodDevice
                        {
                            Id = response.Id,
                            Name = response.DisplayName,
                            IPAddress = response.IPAddress,
                            Port = service.Port,
                            State = ConnectionState.Disconnected
                        };

                        DiscoveredDevices.Add(device);
                        Logger.Info($"Discovered device: {device.Name} at {device.IPAddress}:{device.Port}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error parsing device response: {response.DisplayName}", ex);
                    }
                }

                Logger.Info($"Device discovery completed. Found {DiscoveredDevices.Count} device(s)");
            }
            catch (Exception ex)
            {
                Logger.Error("Device discovery failed", ex);

                // Check if it's a Bonjour/mDNS service missing error
                if (ex.Message.Contains("protocol") ||
                    ex.InnerException?.Message.Contains("protocol") == true ||
                    ex.Message.Contains("10043"))
                {
                    throw new InvalidOperationException(
                        "Bonjour service is not installed. Please install Bonjour Print Services from Apple " +
                        "(https://support.apple.com/kb/DL999) or iTunes to enable device discovery.",
                        ex);
                }

                throw;
            }
        }
    }
}
