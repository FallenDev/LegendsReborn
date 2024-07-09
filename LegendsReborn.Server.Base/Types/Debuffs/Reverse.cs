namespace Legends.Server.Base.Types.Debuffs;

public class Reverse : DebuffBase
{
    public override byte Icon => 147;
    public override int Length => 30;
    public override string Name => "reverse";

    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Debuffs.TryAdd(Name, this))
            return;

        if (affected is Aisling aisling)
        {
            aisling.Client.SendStats(StatusFlags.All);
            aisling.Client.SendMessage(0x02, "A distorted aura surrounds you.");
        }

        affected.SendAnimation(94, affected, affected, 100);
        
        base.OnApplied(affected, timeLeft);
    }

    public override void OnDurationUpdate(Sprite affected)
    {
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Debuffs.TryRemove(Name, out _))
            return;

        if (affected is Aisling aisling)
        {
            aisling.Client.SendStats(StatusFlags.All);
            aisling.Client.SendMessage(0x02, "The strange aura dissipates.");
        }
        
        base.OnEnded(affected);
    }
}