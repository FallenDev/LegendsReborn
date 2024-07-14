using Darkages.Sprites;
using Darkages.Types;

namespace Darkages.Templates;

public class NationTemplate : Template
{
    public int AreaId { get; set; }
    public Position MapPosition { get; set; }
    public byte NationId { get; set; }

    public override string[] GetMetaData() =>
    [
        ""
    ];

    public bool PastCurfew(Aisling aisling) => (DateTime.UtcNow - aisling.LastLogged).TotalHours > ServerSetup.Instance.Config.NationReturnHours;
}