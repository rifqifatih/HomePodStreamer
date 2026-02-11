using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using HomePodStreamer.Models;
using HomePodStreamer.Utils;

namespace HomePodStreamer.Services
{
    public class OwntoneApiClient : IOwntoneApiClient
    {
        private readonly HttpClient _httpClient;

        public OwntoneApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("http://127.0.0.1:3689");
            _httpClient.Timeout = TimeSpan.FromSeconds(5);
        }

        public async Task<List<OwntoneOutput>> GetOutputsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/outputs");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                Logger.Info($"GetOutputsAsync raw response: {json}");
                var result = JsonSerializer.Deserialize<OwntoneOutputsResponse>(json);
                return result?.Outputs ?? new List<OwntoneOutput>();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get owntone outputs", ex);
                throw;
            }
        }

        public async Task SetOutputEnabledAsync(string outputId, bool enabled)
        {
            try
            {
                var payload = JsonSerializer.Serialize(new { selected = enabled });
                var content = new StringContent(payload, Encoding.UTF8, "application/json");
                var url = $"/api/outputs/{outputId}";

                Logger.Info($"PUT {url} body={payload}");
                var response = await _httpClient.PutAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    Logger.Error($"PUT {url} failed: {response.StatusCode} body={body}");
                }

                response.EnsureSuccessStatusCode();
                Logger.Info($"Output {outputId} {(enabled ? "enabled" : "disabled")}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to set output {outputId} enabled={enabled}", ex);
                throw;
            }
        }

        public async Task SetOutputVolumeAsync(string outputId, int volume)
        {
            try
            {
                volume = Math.Max(0, Math.Min(100, volume));
                var payload = JsonSerializer.Serialize(new { volume });
                var content = new StringContent(payload, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync($"/api/outputs/{outputId}", content);
                response.EnsureSuccessStatusCode();

                Logger.Info($"Output {outputId} volume set to {volume}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to set volume for output {outputId}", ex);
                throw;
            }
        }

        public async Task SetMasterVolumeAsync(int volume)
        {
            try
            {
                volume = Math.Max(0, Math.Min(100, volume));
                var response = await _httpClient.PutAsync($"/api/player/volume?volume={volume}", null);
                response.EnsureSuccessStatusCode();

                Logger.Info($"Master volume set to {volume}");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to set master volume", ex);
                throw;
            }
        }

        public async Task StartPlaybackAsync()
        {
            try
            {
                // Ensure the pipe track is in the queue
                var queueResponse = await _httpClient.GetAsync("/api/queue");
                var queueJson = await queueResponse.Content.ReadAsStringAsync();

                if (!queueJson.Contains("pcm_input"))
                {
                    Logger.Info("Pipe track not in queue, adding it");
                    // Clear queue and add the pipe track
                    await _httpClient.PutAsync("/api/queue/clear", null);
                    var addResponse = await _httpClient.PostAsync(
                        "/api/queue/items/add?uris=library:track:1", null);
                    if (!addResponse.IsSuccessStatusCode)
                    {
                        Logger.Warning($"Failed to add pipe track to queue: {addResponse.StatusCode}");
                    }
                }

                var response = await _httpClient.PutAsync("/api/player/play", null);
                response.EnsureSuccessStatusCode();
                Logger.Info("Playback started");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to start playback", ex);
                throw;
            }
        }

        public async Task StopPlaybackAsync()
        {
            try
            {
                var response = await _httpClient.PutAsync("/api/player/stop", null);
                response.EnsureSuccessStatusCode();
                Logger.Info("Playback stopped");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to stop playback", ex);
                throw;
            }
        }

        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/outputs");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Logger.Info($"Health check failed: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }
    }
}
