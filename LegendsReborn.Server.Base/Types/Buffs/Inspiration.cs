namespace Darkages.Types.Buffs;

public class Inspiration : BuffBase
{
    public override byte Icon => 148;
    public override int Length => 3600;
    public override string Name => "inspiration";

    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (affected is not Aisling)
            return;
        if (!affected.Buffs.TryAdd(Name, this))
            return;

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