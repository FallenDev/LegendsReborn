namespace Darkages.Types.Debuffs;

public class ArdCradh : DebuffBase
{
    public override byte Icon => 84;
    public override int Length => 600;
    public override string Name => "ard cradh";

    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Debuffs.TryAdd(Name, this))
            return;
        
        affected.BonusAc += 65;
        affected.BonusMr -= 30;
        
        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Debuffs.TryRemove(Name, out _))
            return;
        
        affected.BonusAc -= 65;
        affected.BonusMr += 30;
        
        base.OnEnded(affected);
    }
}