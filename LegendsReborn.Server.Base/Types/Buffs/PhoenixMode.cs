namespace Darkages.Types.Buffs;

public class PhoenixMode : BuffBase
{
    public override byte Icon => 214;
    public override int Length => 600;
    public override string Name => "phoenix mode";

    public override bool TryApply(Sprite source, Sprite affected)
    {
        affected.RemoveBuff("dragon mode");
        affected.RemoveBuff("phoenix mode");


        OnApplied(affected);

        return true;
    }
    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        affected.Buffs[Name] = this;

        affected.BonusDmg += 40;
        affected.PhoenixAssail = true;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Sacred fire surrounds your body.");

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        affected.BonusDmg -= 40;
        affected.PhoenixAssail = false;

        if (affected is Aisling aisling)
        {
            aisling.Client.SendMessage(0x02, "Your body weakens as the flames subside.");
        }

        base.OnEnded(affected);
    }
}