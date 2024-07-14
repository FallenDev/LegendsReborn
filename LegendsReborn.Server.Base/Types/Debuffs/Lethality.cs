namespace Darkages.Types.Debuffs;

public class Lethality : DebuffBase
{
    public override byte Icon => 97;
    public override int Length => 45;
    public override string Name => "lethality";
    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Debuffs.TryAdd(Name, this))
            return;
        
        base.OnApplied(affected, timeLeft);
    }

    public override void OnDurationUpdate(Sprite affected)
    {
        affected.SendAnimation(116, affected, affected);
        base.OnDurationUpdate(affected);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Debuffs.TryRemove(Name, out _))
            return;

        base.OnEnded(affected);
    }
}