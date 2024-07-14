
namespace Darkages.Types.Debuffs;

public class DanaanCradh : DebuffBase
{
    public override byte Icon => 204;
    public override int Length => 600;
    public override string Name => "danaan cradh";

    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Debuffs.TryAdd(Name, this))
            return;
        
        affected.BonusAc += 80;
        affected.BonusMr -= 40;
        
        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Debuffs.TryRemove(Name, out _))
            return;
        
        affected.BonusAc -= 80;
        affected.BonusMr += 40;
        
        base.OnEnded(affected);
    }
}