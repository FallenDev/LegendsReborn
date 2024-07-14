namespace Darkages.Types.Debuffs;

public class Blind : DebuffBase
{
    public override byte Icon => 3;
    public override int Length => 16;
    public override string Name => "blind";
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
            Length = 300;
            aisling.Client.SendStats(StatusFlags.All);
            aisling.Client.SendMessage(0x02, "Your accuracy has been reduced!");
        }

        affected.SendAnimation(391, affected, affected);
        
        base.OnApplied(affected, timeLeft);
    }

    public override void OnDurationUpdate(Sprite affected)
    {
        if (affected is Monster)
            affected.SendAnimation(391, affected, affected);
        
        base.OnDurationUpdate(affected);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Debuffs.TryRemove(Name, out _))
            return;

        if (affected is Aisling aisling)
        {
            aisling.Client.SendStats(StatusFlags.All);
            aisling.Client.SendMessage(0x02, "Your accuracy returns to normal.");
        }
        
        base.OnEnded(affected);
    }
}