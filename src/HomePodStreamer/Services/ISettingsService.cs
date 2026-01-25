using System.Threading.Tasks;
using HomePodStreamer.Models;

namespace HomePodStreamer.Services
{
    public interface ISettingsService
    {
        Task<AppSettings> LoadSettingsAsync();
        Task SaveSettingsAsync(AppSettings settings);
    }
}
