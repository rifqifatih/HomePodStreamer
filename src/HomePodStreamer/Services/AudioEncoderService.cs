using System;
using NAudio.Wave;
using HomePodStreamer.Utils;

namespace HomePodStreamer.Services
{
    public class AudioEncoderService : IAudioEncoderService
    {
        private WaveFormat? _inputFormat;
        private bool _initialized;

        public void Initialize(WaveFormat inputFormat)
        {
            _inputFormat = inputFormat;
            _initialized = true;

            Logger.Info($"AudioEncoder initialized - SampleRate: {inputFormat.SampleRate}, " +
                       $"Channels: {inputFormat.Channels}, " +
                       $"BitsPerSample: {inputFormat.BitsPerSample}, " +
                       $"Encoding: {inputFormat.Encoding}");
        }

        public byte[] Encode(byte[] pcmData)
        {
            if (!_initialized || _inputFormat == null)
            {
                throw new InvalidOperationException("Encoder not initialized");
            }

            try
            {
                // NAudio WASAPI provides IEEE Float (32-bit) by default
                // We need to convert to 16-bit PCM for AirPlay/ALAC
                if (_inputFormat.Encoding == WaveFormatEncoding.IeeeFloat)
                {
                    return ConvertFloat32ToInt16(pcmData);
                }

                // If already in correct format, return as-is
                return pcmData;
            }
            catch (Exception ex)
            {
                Logger.Error("Error encoding audio", ex);
                throw;
            }
        }

        private byte[] ConvertFloat32ToInt16(byte[] input)
        {
            int sampleCount = input.Length / 4; // 4 bytes per float32 sample
            byte[] output = new byte[sampleCount * 2]; // 2 bytes per int16 sample

            for (int i = 0; i < sampleCount; i++)
            {
                // Read float32 sample
                float sample = BitConverter.ToSingle(input, i * 4);

                // Clamp to [-1.0, 1.0]
                sample = Math.Max(-1.0f, Math.Min(1.0f, sample));

                // Convert to int16 range [-32768, 32767]
                short int16Sample = (short)(sample * 32767);

                // Write int16 sample
                output[i * 2] = (byte)(int16Sample & 0xFF);
                output[i * 2 + 1] = (byte)((int16Sample >> 8) & 0xFF);
            }

            return output;
        }
    }
}
