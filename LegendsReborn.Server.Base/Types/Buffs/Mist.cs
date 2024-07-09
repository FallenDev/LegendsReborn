namespace Darkages.Types.Buffs;

public class Mist : BuffBase
{
    public override byte Icon => 55;
    public override int Length => 600;
    public override string Name => "Mist";

    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Buffs.TryAdd(Name, this))
            return;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Your reflexes quicken.");

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Your reflexes return to normal.");

        base.OnEnded(affected);
    }
}