namespace Darkages.Types.Buffs;

public class StoneFist : BuffBase
{
    public override byte Icon => 99;
    public override int Length => 16;
    public override string Name => "stone fist";

    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Buffs.TryAdd(Name, this))
            return;

        affected.EmpoweredAssail = true;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Your fists become as hard as stone.");

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        affected.EmpoweredAssail = false;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Your fists are no longer made of stone.");

        base.OnEnded(affected);
    }
}