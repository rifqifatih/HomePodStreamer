using System.Collections.ObjectModel;
using System.Threading.Tasks;
using HomePodStreamer.Models;

namespace HomePodStreamer.Services
{
    public interface IDeviceDiscoveryService
    {
        ObservableCollection<HomePodDevice> DiscoveredDevices { get; }
        Task StartDiscoveryAsync();
    }
}
