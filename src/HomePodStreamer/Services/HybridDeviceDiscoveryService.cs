using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using HomePodStreamer.Models;
using HomePodStreamer.Utils;

namespace HomePodStreamer.Services
{
    /// <summary>
    /// Discovers AirPlay devices by querying owntone's outputs API.
    /// owntone in WSL2 discovers HomePods directly via Avahi on the LAN.
    /// </summary>
    public class HybridDeviceDiscoveryService : IDeviceDiscoveryService
    {
        private readonly IOwntoneApiClient _apiClient;

        public ObservableCollection<HomePodDevice> DiscoveredDevices { get; }

        public HybridDeviceDiscoveryService(IOwntoneApiClient apiClient)
        {
            _apiClient = apiClient;
            DiscoveredDevices = new ObservableCollection<HomePodDevice>();
        }

        public async Task StartDiscoveryAsync()
        {
            try
            {
                Logger.Info("Starting device discovery via owntone outputs API");
                DiscoveredDevices.Clear();

                var outputs = await _apiClient.GetOutputsAsync();
                var airplayOutputs = outputs
                    .Where(o => o.Type.StartsWith("AirPlay", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                Logger.Info($"Owntone reports {airplayOutputs.Count} AirPlay output(s)");

                foreach (var output in airplayOutputs)
                {
                    var device = new HomePodDevice
                    {
                        Id = output.Id,
                        Name = output.Name,
                        OwntoneOutputId = output.Id,
                        State = ConnectionState.Disconnected
                    };

                    DiscoveredDevices.Add(device);
                    Logger.Info($"Discovered: {device.Name} (owntone output ID: {device.OwntoneOutputId})");
                }

                Logger.Info($"Discovery completed. {DiscoveredDevices.Count} device(s) available");
            }
            catch (Exception ex)
            {
                Logger.Error("Device discovery failed", ex);
                throw;
            }
        }
    }
}
