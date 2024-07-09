namespace Darkages.Types.Buffs;

public class GrimlokAlliance : BuffBase
{
    public override byte Icon => 20;
    public override int Length => 3600;
    public override string Name => "grimlok alliance";
    public override ICollection<string> Aliases { get; set; } = new List<string>
    {
        "goblin alliance"
    };
    
    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (affected is not Aisling aisling)
            return;

        if (!affected.Buffs.TryAdd(Name, this))
            return;

        aisling.Grimloks = 0;
        aisling.Alliance = 2;
        affected.Client.SendMessage(0x02, "You ally yourself with the Grimlok Tribe.");

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        if (affected is Aisling aisling)
        {
            aisling.Alliance = 0;
            aisling.Client.SendMessage(0x02, "You are no longer allied with the Grimlok Tribe.");
        }

        base.OnEnded(affected);
    }
}