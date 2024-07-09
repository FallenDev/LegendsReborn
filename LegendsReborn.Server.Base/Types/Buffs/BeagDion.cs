namespace Darkages.Types.Buffs;

public class BeagDion : BuffBase
{
    public override byte Icon => 53;
    public override int Length => 3;
    public override string Name => "beag dion";
    public override ICollection<string> Aliases { get; set; } = new List<string>
    {
        "sgiath"
    };
    
    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Buffs.TryAdd(Name, this))
            return;

        affected.Immunity = true;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Your body has been galvanized.");

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        affected.Immunity = false;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "You are no longer galvanized.");

        base.OnEnded(affected);
    }
}