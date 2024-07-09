namespace Darkages.Types.Buffs;

public class Industry : BuffBase
{
    public override byte Icon => 203;
    public override int Length => 900;
    public override string Name => "industry";

    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (affected is not Aisling)
            return;

        if (!affected.Buffs.TryAdd(Name, this))
            return;

        affected.Client.SendMessage(0x02, "You feel inspired to create new things.");

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        if (affected is Aisling)
            affected.Client.SendMessage(0x02, "Your inspiration fades.");

        base.OnEnded(affected);
    }
}