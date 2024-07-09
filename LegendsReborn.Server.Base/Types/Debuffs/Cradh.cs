namespace Legends.Server.Base.Types.Debuffs;

public class Cradh : DebuffBase
{
    public override byte Icon => 82;
    public override int Length => 600;
    public override string Name => "cradh";

    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Debuffs.TryAdd(Name, this))
            return;
        
        affected.BonusAc += 35;
        affected.BonusMr -= 10;
        
        base.OnApplied(affected, timeLeft);
    }
    
    public override void OnEnded(Sprite affected)
    {
        if (!affected.Debuffs.TryRemove(Name, out _))
            return;
        
        affected.BonusAc -= 35;
        affected.BonusMr += 10;
        
        base.OnEnded(affected);
    }
}