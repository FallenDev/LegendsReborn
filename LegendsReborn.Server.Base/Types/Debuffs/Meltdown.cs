namespace Legends.Server.Base.Types.Debuffs;

public class Meltdown : DebuffBase
{
    public override byte Icon => 121;
    public override int Length => 30;
    private int BaseStr;
    private int BaseInt;
    private int BaseWis;
    private int BaseCon;
    private int BaseDex;
    public override string Name => "meltdown";

    public override bool TryApply(Sprite source, Sprite affected)
    {

        return base.TryApply(source, affected);
    }
    
    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Debuffs.TryAdd(Name, this))
            return;
        BaseStr = affected.BonusStr;
        BaseInt = affected.BonusInt;
        BaseWis = affected.BonusWis;
        BaseCon = affected.BonusCon;
        BaseDex = affected.BonusDex;

        affected.BonusStr -= affected._Str;
        affected.BonusInt -= affected._Int;
        affected.BonusWis -= affected._Wis;
        affected.BonusCon -= affected._Con;
        affected.BonusDex -= affected._Dex;

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Debuffs.TryRemove(Name, out _))
            return;
        
        affected.BonusStr = BaseStr;
        affected.BonusInt = BaseInt;
        affected.BonusWis = BaseWis;
        affected.BonusCon = BaseCon;
        affected.BonusDex = BaseDex;
        
        base.OnEnded(affected);
    }
}