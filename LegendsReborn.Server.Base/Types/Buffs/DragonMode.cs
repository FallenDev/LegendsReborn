namespace Darkages.Types.Buffs;

public class DragonMode : BuffBase
{
    public override byte Icon => 213;
    public override int Length => 600;
    public override string Name => "dragon mode";
    private int AddedHp;

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
        affected.DragonScale = true;

        if (affected is Aisling aisling)
        {
            AddedHp = Convert.ToInt32(affected._MaximumHp * 0.1);
            //add the added values
            affected.BonusHp += AddedHp;
            affected.CurrentHp += AddedHp;
            //difference of 0.1 as a tiny buffer
            double currentHpPct = affected.HpPct;
            affected.HpPct = currentHpPct;

            aisling.Client.SendMessage(0x02, "Your body develops iron-like scales.");
        }
        base.OnApplied(affected, timeLeft);
    }
    public override void OnDurationUpdate(Sprite affected)
    {
        //recalculate incase maximum hp/mp changes (like if they level or something)
        var addedHp = Convert.ToInt32(affected._MaximumHp * 0.1);
        //If someone leveled up, it would endlessly increase their HP/MP because the basic AddedHp / AddedMp
        //variables were not adjusted in conjunction with the new values.
        if (addedHp != AddedHp)
        {
            affected.BonusHp += addedHp - AddedHp;
            AddedHp = addedHp;
        }
        base.OnDurationUpdate(affected);
    }
    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        affected.DragonScale = false;
        if (affected is Aisling aisling)
        {
            //var currentHpPct = affected.HpPct;
            affected.BonusHp -= AddedHp;
            //affected.HpPct = currentHpPct;
            if (affected.CurrentHp > affected.MaximumHp)
                affected.CurrentHp = affected.MaximumHp;

            aisling.Client.SendMessage(0x02, "Your body weakens as the scales subside.");
        }
        base.OnEnded(affected);
    }
}