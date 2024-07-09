namespace Darkages.Types.Buffs;

public class BeagAiseagBeatha : BuffBase
{
    public override byte Icon => 212;
    public override int Length => 180;
    public override string Name => "beag aiseag beatha";
    
    private void ApplyRegen(Sprite affected)
    {
        const double MODIFIER = 0.01;

        //get the added amount of hp (MaximumHp * 0.04), max of 1k
        var added = Convert.ToInt32(Math.Min(1000, affected.MaximumHp * MODIFIER));
        //add that health to the affected's current Hp (don't exceed max Hp)
        affected.CurrentHp = Math.Min(affected.MaximumHp, affected.CurrentHp + added);
    }
    
    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Buffs.TryAdd(Name, this))
            return;

        affected.SendAnimation(187, affected, affected);

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Your wounds are slowly closing.");

        base.OnApplied(affected, timeLeft);
    }

    public override void OnDurationUpdate(Sprite affected)
    {
        affected.SendAnimation(187, affected, affected);
        
        if (affected.CurrentHp <= 0)
            return;

        ApplyRegen(affected);
        base.OnDurationUpdate(affected);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Your wounds are no longer closing.");

        base.OnEnded(affected);
    }
}