namespace Legends.Server.Base.Types.Debuffs;

public class MorCradh : DebuffBase
{

    public override byte Icon => 83;
    public override int Length => 600;
    public override string Name => "mor cradh";

    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Debuffs.TryAdd(Name, this))
            return;
        
        affected.BonusAc += 50;
        affected.BonusMr -= 20;
        
        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Debuffs.TryRemove(Name, out _))
            return;
        
        affected.BonusAc -= 50;
        affected.BonusMr += 20;

        base.OnEnded(affected);
    }
}