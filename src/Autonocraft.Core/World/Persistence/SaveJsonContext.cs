using System.Text.Json;
using System.Text.Json.Serialization;
using Autonocraft.Domain.Persistence;

namespace Autonocraft.World.Persistence
{
    /// <summary>Source-generated JSON context for save/settings DTOs.</summary>
    [JsonSerializable(typeof(WorldSaveData))]
    [JsonSerializable(typeof(SaveSnapshot))]
    [JsonSerializable(typeof(PlayerSaveData))]
    [JsonSerializable(typeof(PlayerStatisticsSaveData))]
    [JsonSerializable(typeof(List<BlockModification>))]
    [JsonSerializable(typeof(List<FluidModification>))]
    [JsonSerializable(typeof(List<ContainerModification>))]
    [JsonSerializable(typeof(List<VillageSaveData>))]
    [JsonSerializable(typeof(List<VillagerSaveData>))]
    [JsonSerializable(typeof(List<string>))]
    public partial class SaveJsonContext : JsonSerializerContext
    {
    }
}
