namespace Darkages.Types.Buffs;

public class PreventBlind : BuffBase
{
    public override byte Icon => 25;
    public override int Length => 1800;
    public override string Name => "prevent blind";
    public override ICollection<string> Aliases { get; set; } = new List<string>
    {
        "blind"
    };
    
    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Buffs.TryAdd(Name, this))
            return;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Your eyes shine with clarity.");

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Your eyes aren't as clear as before.");

        base.OnEnded(affected);
    }
}