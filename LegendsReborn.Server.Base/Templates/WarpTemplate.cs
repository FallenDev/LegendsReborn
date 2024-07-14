using Darkages.Enums;
using Darkages.Models;

using Newtonsoft.Json.Converters;
using Newtonsoft.Json;

namespace Darkages.Templates;

public class WarpTemplate : Template
{
    public WarpTemplate() => Activations = [];

    [JsonProperty] public int ActivationMapId { get; set; }
    public List<Warp> Activations { get; set; }
    [JsonProperty] public byte LevelRequired { get; set; }

    public Warp To { get; set; }
    [JsonProperty] public int WarpRadius { get; set; }

    [JsonConverter(typeof(StringEnumConverter))]
    public WarpType WarpType { get; set; }

    public int WorldResetWarpId { get; set; }
    public int WorldTransionWarpId { get; set; }

    public override string[] GetMetaData() =>
    [
        ""
    ];
}