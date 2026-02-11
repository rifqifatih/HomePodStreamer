using System;
using System.Threading.Tasks;
using HomePodStreamer.Models;

namespace HomePodStreamer.Services
{
    public interface IAirPlayService : IDisposable
    {
        Task ConnectToDeviceAsync(HomePodDevice device);
        Task DisconnectFromDeviceAsync(HomePodDevice device);
        void SendAudioToAll(byte[] encodedBuffer);
        void SetVolume(int volume);
        Task StartPlaybackAsync();
        void StopAll();
    }
}
