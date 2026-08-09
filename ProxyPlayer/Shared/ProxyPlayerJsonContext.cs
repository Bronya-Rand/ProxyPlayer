using System.Text.Json.Serialization;

namespace ProxyPlayer.Shared
{
    [JsonSerializable(typeof(MediaState))]
    [JsonSerializable(typeof(MediaCommandMessage))]
    internal partial class ProxyPlayerJsonContext : JsonSerializerContext
    {
    }
}
