namespace Darkages.Types.Buffs;

public class GoblinAlliance : BuffBase
{
    public override byte Icon => 20;
    public override int Length => 3600;
    public override string Name => "goblin alliance";
    public override ICollection<string> Aliases { get; set; } = new List<string>
    {
        "grimlok alliance"
    };
    
    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (affected is not Aisling aisling)
            return;

        if (!affected.Buffs.TryAdd(Name, this))
            return;
        aisling.Goblins = 0;
        aisling.Alliance = 1;
        affected.Client.SendMessage(0x02, "You ally yourself with the Goblin Horde.");

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        if (affected is Aisling aisling)
        {
            aisling.Alliance = 0;
            aisling.Client.SendMessage(0x02, "You are no longer allied with the Goblin Horde.");
        }

        base.OnEnded(affected);
    }
}