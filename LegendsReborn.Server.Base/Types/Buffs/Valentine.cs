namespace Darkages.Types.Buffs;

public class Valentine : BuffBase
{
    public override byte Icon => 152;
    public override int Length => 1800;
    public override string Name => "valentine";
    
    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Buffs.TryAdd(Name, this))
            return;

        affected.SendAnimation(36, affected, affected);
        base.OnApplied(affected, timeLeft);
    }

    public override void OnDurationUpdate(Sprite affected)
    {
        affected.SendAnimation(36, affected, affected);
        
        base.OnDurationUpdate(affected);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        base.OnEnded(affected);
    }
}