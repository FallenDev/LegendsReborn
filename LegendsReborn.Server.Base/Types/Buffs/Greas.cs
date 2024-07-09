namespace Darkages.Types.Buffs;

public class Greas : BuffBase
{
    public override byte Icon => 142;
    public override int Length => 30;
    public override string Name => "greas";
    
    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Buffs.TryAdd(Name, this))
            return;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Your attacks become quicker.");

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Your attack speed slows.");

        base.OnEnded(affected);
    }
}