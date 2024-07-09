namespace Darkages.Types.Buffs;

public class WraithMode : BuffBase
{
    public override byte Icon => 98;
    public override int Length => 600;
    public override string Name => "wraith mode";
    
    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Buffs.TryAdd(Name, this))
            return;

        affected.BonusDmg += 30;
        affected.BonusAc += 30;
        affected.WraithAssail = true;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "An unbearable bloodlust begins to rip your body apart.");

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        affected.BonusDmg -= 30;
        affected.BonusAc -= 30;
        affected.WraithAssail = false;

        if (affected is Aisling aisling)
        {
            aisling.Client.SendMessage(0x02, "Your body withers as the bloodlust subsides.");
            aisling.CurrentHp = Convert.ToInt32(aisling.CurrentHp * 0.25);
        }

        base.OnEnded(affected);
    }
}