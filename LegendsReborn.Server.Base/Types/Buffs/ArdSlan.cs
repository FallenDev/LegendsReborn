namespace Darkages.Types.Buffs;

public class ArdSlan : BuffBase
{
    public override byte Icon => 26;
    public override int Length => 1800;
    public override string Name => "ard slan";

    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Buffs.TryAdd(Name, this))
            return;

        affected.BonusRegen += 20;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Your body recovers more rapidly.");

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        affected.BonusRegen -= 20;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Your body's rapid recovery ends.");

        base.OnEnded(affected);
    }
}