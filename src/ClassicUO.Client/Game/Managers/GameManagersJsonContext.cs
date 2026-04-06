using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClassicUO.Game.Data;

namespace ClassicUO.Game.Managers
{
    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true)]
    [JsonSerializable(typeof(Layer))]
    [JsonSerializable(typeof(PaperdollSaveData))]
    [JsonSerializable(typeof(PaperdollItem))]
    [JsonSerializable(typeof(Dictionary<string, PaperdollItem>))]
    [JsonSerializable(typeof(AutoLootManager.AutoLootItem))]
    [JsonSerializable(typeof(List<AutoLootManager.AutoLootItem>))]
    internal partial class GameManagersJsonContext : JsonSerializerContext
    {
    }
}
