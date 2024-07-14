namespace Darkages.Types.Debuffs;

public class Analyze : DebuffBase
{
    public override byte Icon => 0;
    public override int Length => 10;
    public override string Name => "expert analysis";
    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Debuffs.TryAdd(Name, this))
            return;

        affected.SendAnimation(379, affected, affected);
        
        base.OnApplied(affected, timeLeft);
    }

    public override void OnDurationUpdate(Sprite affected)
    {
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Debuffs.TryRemove(Name, out _))
            return;
        
        base.OnEnded(affected);
    }
}