namespace Darkages.Types.Buffs;

public class MysteriousConcoction : BuffBase
{
    public override byte Icon => 145;
    public override int Length => 1200;
    public override string Name => "mysterious concoction";
    
    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Buffs.TryAdd(Name, this))
            return;

        if (affected is Aisling aisling)
            aisling.Client.SystemMessage("You no longer fear chaos.");

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        base.OnEnded(affected);
    }
}