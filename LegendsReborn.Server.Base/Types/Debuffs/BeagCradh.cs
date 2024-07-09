namespace Legends.Server.Base.Types.Debuffs;

public class BeagCradh : DebuffBase
{
    public override byte Icon => 5;
    public override int Length => 600;
    public override string Name => "beag cradh";

    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Debuffs.TryAdd(Name, this))
            return;

        affected.BonusAc += 20;
        
        base.OnApplied(affected, timeLeft);
    }
    
    public override void OnEnded(Sprite affected)
    {
        if (!affected.Debuffs.TryRemove(Name, out _))
            return;
        
        affected.BonusAc -= 20;
        
        base.OnEnded(affected);
    }
}