using NAudio.Wave;

namespace HomePodStreamer.Services
{
    public interface IAudioEncoderService
    {
        void Initialize(WaveFormat inputFormat);
        byte[] Encode(byte[] pcmData);
    }
}
