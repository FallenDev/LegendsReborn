namespace Darkages.Types.Buffs;

public class Siolaidh : BuffBase
{
    public override byte Icon => 210;
    public override int Length => 30;
    public override string Name => "siolaidh";

    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Buffs.TryAdd(Name, this))
            return;

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Your curse protection fades.");

        base.OnEnded(affected);
    }
}