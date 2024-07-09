namespace Darkages.Types.Buffs;

public class Beannaich : BuffBase
{
    public override byte Icon => 16;
    public override int Length => 600;
    public override string Name => "beannaich";
    public override ICollection<string> Aliases { get; set; } = new List<string>
    {
        "spionnadh"
    };
    
    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Buffs.TryAdd(Name, this))
            return;

        affected.BonusHit += 2;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Your attacks become more accurate.");

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        affected.BonusHit -= 2;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Your accuracy returns to normal.");

        base.OnEnded(affected);
    }
}