namespace Darkages.Types.Buffs;

public class Soulbound : BuffBase
{
    public override byte Icon => 152;
    public override int Length => 300;
    public override string Name => "soulbound";

    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Buffs.TryAdd(Name, this))
            return;

        affected.BonusMr += 10;
        affected.BonusRegen += 20;
        affected.Animate(36);
        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "True love radiates from within!");

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        affected.BonusMr -= 10;
        affected.BonusRegen -= 20;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "True love's radiance fades.");

        base.OnEnded(affected);
    }
}