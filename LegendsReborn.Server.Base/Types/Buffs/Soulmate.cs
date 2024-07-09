namespace Darkages.Types.Buffs;

public class Soulmate : BuffBase
{
    public override byte Icon => 152;
    public override int Length => 300;
    public override string Name => "soulmate";

    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Buffs.TryAdd(Name, this))
            return;

        affected.BonusMr += 10;
        affected.Animate(35);
        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Your body feels more resilient.");

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        affected.BonusMr -= 10;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Your body's resilience fades.");

        base.OnEnded(affected);
    }
}