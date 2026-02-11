using System.Threading.Tasks;

namespace HomePodStreamer.Services
{
    public interface IOwntoneHostService
    {
        bool IsRunning { get; }
        Task StartAsync();
        Task StopAsync();
        Task WaitForHealthyAsync(int timeoutSeconds = 60);
    }
}
