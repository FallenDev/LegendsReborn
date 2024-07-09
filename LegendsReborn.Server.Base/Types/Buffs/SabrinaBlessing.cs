namespace Darkages.Types.Buffs;

public class SabrinaBlessing : BuffBase
{
    public override byte Icon => 124;
    public override int Length => 7200;
    public override string Name => "sabrina's blessing";
    
    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Buffs.TryAdd(Name, this))
            return;

        affected.BonusRegen += 10;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Your body feels warm.");

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        affected.BonusRegen -= 10;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Your inner warmth subsides.");

        base.OnEnded(affected);
    }
}