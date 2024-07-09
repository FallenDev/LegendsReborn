namespace Darkages.Types.Buffs;

public class Transport : BuffBase
{
    public override byte Icon => 203;
    public override int Length => 30;
    public override string Name => "transport";

    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (affected is Aisling)
            return;

        if (!affected.Buffs.TryAdd(Name, this))
            return;

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        base.OnEnded(affected);
    }
}