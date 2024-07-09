namespace Darkages.Types.Buffs;

public class FasBeothail : BuffBase
{
    private int AddedHp;
    private int AddedMp;
    public override byte Icon => 115;
    public override int Length => 1800;
    public override string Name => "fas beothail";

    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Buffs.TryAdd(Name, this))
            return;
        
        AddedHp = Convert.ToInt32(affected._MaximumHp * 0.2);
        AddedMp = Convert.ToInt32(affected._MaximumMp * 0.2);
        
        //add the added values
        affected.BonusHp += AddedHp;
        affected.BonusMp += AddedMp;

        double currentHpPct = affected.HpPct;
        double currentMpPct = affected.MpPct;
        //difference of 0.1 as a tiny buffer
        affected.HpPct = currentHpPct;
        affected.MpPct = currentMpPct;
        
        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "You feel energized.");

        base.OnApplied(affected, timeLeft);
    }

    public override void OnDurationUpdate(Sprite affected)
    {
        //recalculate incase maximum hp/mp changes (like if they level or something)
        var addedHp = Convert.ToInt32(affected._MaximumHp * 0.2);
        var addedMp = Convert.ToInt32(affected._MaximumMp * 0.2);
        //If someone leveled up, it would endlessly increase their HP/MP because the basic AddedHp / AddedMp
        //variables were not adjusted in conjunction with the new values.
        if (addedHp != AddedHp)
        {
            affected.BonusHp += addedHp - AddedHp;
            AddedHp = addedHp;
        }

        if (addedMp != AddedMp)
        {
            affected.BonusMp += addedMp - AddedMp;
            AddedMp = addedMp;
        }

        base.OnDurationUpdate(affected);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        //remove the saved/updated amounts
        //previously, if someone leveled while this spell was active
        //more hp/mp would be removed than was originally added
        var currentHpPct = affected.HpPct;
        var currentMpPct = affected.MpPct;
        
        affected.BonusHp -= AddedHp;
        affected.BonusMp -= AddedMp;
        
        //difference of 0.1 as a tiny buffer
        affected.HpPct = currentHpPct;
        affected.MpPct = currentMpPct;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "You no longer feel energized.");

        base.OnEnded(affected);
    }
}