namespace Darkages.Types.Buffs;

public class DiaNaomhAite : BuffBase
{
    public override byte Icon => 11;
    public override int Length => 3600;
    public override string Name => "dia naomh aite";
    public override ICollection<string> Aliases { get; set; } = new List<string>
    {
        "spionnadh"
    };
    
    
    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Buffs.TryAdd(Name, this))
            return;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "You have found sanctuary.");

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "You feel vulnerable again.");

        base.OnEnded(affected);
    }
}