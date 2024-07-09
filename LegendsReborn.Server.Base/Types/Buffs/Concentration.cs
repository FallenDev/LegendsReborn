namespace Darkages.Types.Buffs;

public class Concentration : BuffBase
{
    public override byte Icon => 109;
    public override int Length => 600;
    public override string Name => "concentration";

    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Buffs.TryAdd(Name, this))
            return;

        affected.BonusHit += 40;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Your focus is unparalleled.");

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        affected.BonusHit -= 40;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Your focus returns to normal.");

        base.OnEnded(affected);
    }
}