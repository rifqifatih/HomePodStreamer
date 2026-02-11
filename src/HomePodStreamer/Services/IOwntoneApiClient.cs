using System.Collections.Generic;
using System.Threading.Tasks;
using HomePodStreamer.Models;

namespace HomePodStreamer.Services
{
    public interface IOwntoneApiClient
    {
        Task<List<OwntoneOutput>> GetOutputsAsync();
        Task SetOutputEnabledAsync(string outputId, bool enabled);
        Task SetOutputVolumeAsync(string outputId, int volume);
        Task SetMasterVolumeAsync(int volume);
        Task StartPlaybackAsync();
        Task StopPlaybackAsync();
        Task<bool> IsAvailableAsync();
    }
}
