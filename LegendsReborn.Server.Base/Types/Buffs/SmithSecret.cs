namespace Darkages.Types.Buffs;

public class SmithSecret : BuffBase
{
    public override byte Icon => 1;
    public override int Length => 30;
    public override string Name => "smith's secret";
    
    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Buffs.TryAdd(Name, this))
            return;

        if (affected is Aisling aisling)
            aisling.Client.SystemMessage("You hear something move in the distance.");

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        base.OnEnded(affected);
    }
}