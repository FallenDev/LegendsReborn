
namespace Legends.Server.Base.Types.Debuffs;

public class Disrobe : DebuffBase
{
    public override byte Icon => 110;
    public override int Length => 3;
    public override string Name => "disrobe";
    private int OriginalAc;

    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Debuffs.TryAdd(Name, this))
            return;

        OriginalAc = affected.BonusAc;
        affected.BonusAc += (OriginalAc + (100 - affected.Level / 3));

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Debuffs.TryRemove(Name, out _))
            return;

        affected.BonusAc -= (OriginalAc + (100 - affected.Level / 3));

        base.OnEnded(affected);
    }
}