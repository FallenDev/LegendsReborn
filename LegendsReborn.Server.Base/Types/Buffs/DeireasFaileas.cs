namespace Darkages.Types.Buffs;

public class DeireasFaileas : BuffBase
{
    public override byte Icon => 54;
    public override int Length => 21;
    public override string Name => "deireas faileas";

    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Buffs.TryAdd(Name, this))
            return;

        affected.SpellReflect = true;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Magic begins to bounce off of you.");

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        affected.SpellReflect = false;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "You are no longer reflecting magic.");

        base.OnEnded(affected);
    }
}