using System.Collections.Generic;

namespace Legends.Server.Base.Types.Debuffs;

public class Silence : DebuffBase
{
    public override byte Icon => 111;
    public override int Length => 16;
    public override string Name => "silence";
    public override ICollection<string> Aliases { get; } = new List<string>
    {
        "siolaidh"
    };
    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Debuffs.TryAdd(Name, this))
            return;

        if (affected is Aisling aisling)
        {
            aisling.Client.SendStats(StatusFlags.All);
            aisling.Client.SendMessage(0x02, "You are unable to speak!");
        }

        affected.SendAnimation(94, affected, affected, 100);
        
        base.OnApplied(affected, timeLeft);
    }

    public override void OnDurationUpdate(Sprite affected)
    {
        affected.SendAnimation(94, affected, affected, 100);
        
        base.OnDurationUpdate(affected);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Debuffs.TryRemove(Name, out _))
            return;

        if (affected is Aisling aisling)
        {
            aisling.Client.SendStats(StatusFlags.All);
            aisling.Client.SendMessage(0x02, "You are no longer silenced.");
        }
        
        base.OnEnded(affected);
    }
}