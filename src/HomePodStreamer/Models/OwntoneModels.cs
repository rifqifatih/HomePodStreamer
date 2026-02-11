using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HomePodStreamer.Models
{
    public class OwntoneOutput
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("selected")]
        public bool Selected { get; set; }

        [JsonPropertyName("has_password")]
        public bool HasPassword { get; set; }

        [JsonPropertyName("requires_auth")]
        public bool RequiresAuth { get; set; }

        [JsonPropertyName("needs_probe_scan")]
        public bool NeedsProbeScan { get; set; }

        [JsonPropertyName("volume")]
        public int Volume { get; set; }
    }

    public class OwntoneOutputsResponse
    {
        [JsonPropertyName("outputs")]
        public List<OwntoneOutput> Outputs { get; set; } = new();
    }

    public class OwntonePlayerStatus
    {
        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;

        [JsonPropertyName("volume")]
        public int Volume { get; set; }
    }

    public class OwntoneConfig
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("websocket_port")]
        public int WebsocketPort { get; set; }

        [JsonPropertyName("buildoptions")]
        public List<string> BuildOptions { get; set; } = new();
    }
}
