namespace Darkages.Types.Buffs;

public class Armachd : BuffBase
{
    public override byte Icon => 94;
    public override int Length => 600;
    public override string Name => "armachd";
    public override ICollection<string> Aliases { get; set; } = new List<string>
    {
        "spionnadh"
    };
    
    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Buffs.TryAdd(Name, this))
            return;

        affected.BonusAc -= 10;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Your armor has been strengthened.");

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        affected.BonusAc += 10;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Your armor returns to normal.");

        base.OnEnded(affected);
    }
}