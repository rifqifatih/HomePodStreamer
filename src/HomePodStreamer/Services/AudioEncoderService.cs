using System;
using System.Collections.Generic;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using HomePodStreamer.Utils;

namespace HomePodStreamer.Services
{
    public class AudioEncoderService : IAudioEncoderService
    {
        private WaveFormat? _inputFormat;
        private PushSampleProvider? _pushSource;
        private WdlResamplingSampleProvider? _resampler;
        private bool _initialized;
        private bool _needsResample;
        private const int TARGET_SAMPLE_RATE = 44100; // AirPlay requires 44.1kHz

        public void Initialize(WaveFormat inputFormat)
        {
            _inputFormat = inputFormat;

            Logger.Info($"AudioEncoder initialized - Input SampleRate: {inputFormat.SampleRate}, " +
                       $"Channels: {inputFormat.Channels}, " +
                       $"BitsPerSample: {inputFormat.BitsPerSample}, " +
                       $"Encoding: {inputFormat.Encoding}");

            _needsResample = inputFormat.SampleRate != TARGET_SAMPLE_RATE;

            if (_needsResample)
            {
                Logger.Info($"Will resample from {inputFormat.SampleRate}Hz to {TARGET_SAMPLE_RATE}Hz");
                // Create persistent resampler chain that maintains state between chunks
                _pushSource = new PushSampleProvider(inputFormat);
                _resampler = new WdlResamplingSampleProvider(_pushSource, TARGET_SAMPLE_RATE);
            }

            _initialized = true;
        }

        public byte[] Encode(byte[] pcmData)
        {
            if (!_initialized || _inputFormat == null)
            {
                throw new InvalidOperationException("Encoder not initialized");
            }

            try
            {
                if (_inputFormat.Encoding == WaveFormatEncoding.IeeeFloat)
                {
                    if (_needsResample && _pushSource != null && _resampler != null)
                    {
                        return ResampleAndConvert(pcmData);
                    }
                    else
                    {
                        return ConvertFloat32ToInt16(pcmData);
                    }
                }

                return pcmData;
            }
            catch (Exception ex)
            {
                Logger.Error("Error encoding audio", ex);
                throw;
            }
        }

        private byte[] ResampleAndConvert(byte[] float32Data)
        {
            // Convert byte array to float array and push to persistent source
            int sampleCount = float32Data.Length / 4;
            float[] floatSamples = new float[sampleCount];
            Buffer.BlockCopy(float32Data, 0, floatSamples, 0, float32Data.Length);

            _pushSource!.AddSamples(floatSamples);

            // Calculate expected output sample count
            int outputSampleCount = (int)(sampleCount * (TARGET_SAMPLE_RATE / (double)_inputFormat!.SampleRate)) + 64;
            float[] resampledFloat = new float[outputSampleCount];
            int samplesRead = _resampler!.Read(resampledFloat, 0, outputSampleCount);

            // Convert resampled float to int16
            byte[] output = new byte[samplesRead * 2];
            for (int i = 0; i < samplesRead; i++)
            {
                float sample = Math.Max(-1.0f, Math.Min(1.0f, resampledFloat[i]));
                short int16Sample = (short)(sample * 32767);
                output[i * 2] = (byte)(int16Sample & 0xFF);
                output[i * 2 + 1] = (byte)((int16Sample >> 8) & 0xFF);
            }

            return output;
        }

        /// <summary>
        /// Persistent push-based sample provider that the resampler reads from.
        /// Maintains continuity between chunks so the resampler keeps its filter state.
        /// </summary>
        private class PushSampleProvider : ISampleProvider
        {
            private readonly Queue<float[]> _chunks = new();
            private float[] _current = Array.Empty<float>();
            private int _position;

            public WaveFormat WaveFormat { get; }

            public PushSampleProvider(WaveFormat waveFormat)
            {
                WaveFormat = waveFormat;
            }

            public void AddSamples(float[] samples)
            {
                _chunks.Enqueue(samples);
            }

            public int Read(float[] buffer, int offset, int count)
            {
                int totalRead = 0;
                while (totalRead < count)
                {
                    if (_position >= _current.Length)
                    {
                        if (_chunks.Count == 0) break;
                        _current = _chunks.Dequeue();
                        _position = 0;
                    }

                    int available = _current.Length - _position;
                    int toRead = Math.Min(count - totalRead, available);
                    Array.Copy(_current, _position, buffer, offset + totalRead, toRead);
                    _position += toRead;
                    totalRead += toRead;
                }
                return totalRead;
            }
        }

        private byte[] ConvertFloat32ToInt16(byte[] input)
        {
            int sampleCount = input.Length / 4;
            byte[] output = new byte[sampleCount * 2];

            for (int i = 0; i < sampleCount; i++)
            {
                float sample = BitConverter.ToSingle(input, i * 4);
                sample = Math.Max(-1.0f, Math.Min(1.0f, sample));
                short int16Sample = (short)(sample * 32767);
                output[i * 2] = (byte)(int16Sample & 0xFF);
                output[i * 2 + 1] = (byte)((int16Sample >> 8) & 0xFF);
            }

            return output;
        }
    }
}
