namespace Darkages.Types.Buffs;

public class ArdFasDeireas : BuffBase
{
    public override byte Icon => 52;
    public override int Length => 1800;
    public override string Name => "ard fas deireas";
    public override ICollection<string> Aliases { get; set; } = new List<string>
    {
        "spionnadh"
    };
    
    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Buffs.TryAdd(Name, this))
            return;

        affected.BonusDmg += 8;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Your attacks become more powerful.");

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        affected.BonusDmg -= 8;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Your attacks return to normal.");

        base.OnEnded(affected);
    }
}