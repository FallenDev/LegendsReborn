using Darkages.Models;

namespace Darkages.Templates;

public class WorldMapTemplate : Template
{
    public List<WorldPortal> Portals = [];

    public int FieldNumber { get; set; }
    public Warp Transition { get; set; }
    public int WorldIndex { get; set; } = 1;

    public override string[] GetMetaData() =>
    [
        ""
    ];
}
