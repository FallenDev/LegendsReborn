using System.Collections.Generic;

namespace Legends.Server.Base.Types.Debuffs;

public class Frozen : DebuffBase
{
    public override byte Icon => 50;
    public override int Length => 12;
    public override string Name => "frozen";
    public override ICollection<string> Aliases { get; } = new List<string>
    {
        "suain",
        "pramh",
        "siolaidh"
    };
    
    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Debuffs.TryAdd(Name, this))
            return;

        affected.SendAnimation(389, affected, affected);
        
        base.OnApplied(affected, timeLeft);
    }

    public override void OnDurationUpdate(Sprite affected)
    {
        affected.SendAnimation(389, affected, affected);
        
        if(affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Your body is frozen solid.");
        
        base.OnDurationUpdate(affected);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Debuffs.TryRemove(Name, out _))
            return;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Your body thaws out.");
        
        base.OnEnded(affected);
    }
}