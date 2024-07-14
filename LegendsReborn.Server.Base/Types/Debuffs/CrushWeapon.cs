namespace Darkages.Types.Debuffs;

public class CrushWeapon : DebuffBase
{
    public override byte Icon => 1;
    public override int Length => 16;
    private int BaseStr;
    private int BaseCon;
    private int BaseDex;
    public override string Name => "crush weapon";

    public override bool TryApply(Sprite source, Sprite affected)
    {

        return base.TryApply(source, affected);
    }
    
    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Debuffs.TryAdd(Name, this))
            return;
        BaseStr = affected._Str;
        BaseCon = affected._Con;
        BaseDex = affected._Dex;

        affected._Str = Convert.ToInt32(affected._Str * 0.75);
        affected._Con = Convert.ToInt32(affected._Con * 0.75);
        affected._Dex = Convert.ToInt32(affected._Dex * 0.75);

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Debuffs.TryRemove(Name, out _))
            return;
        
        affected._Str = BaseStr;
        affected._Con = BaseCon;
        affected._Dex = BaseDex;
        
        base.OnEnded(affected);
    }
}