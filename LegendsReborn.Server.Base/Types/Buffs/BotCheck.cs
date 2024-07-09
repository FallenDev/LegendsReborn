namespace Darkages.Types.Buffs;

public class BotCheck : BuffBase
{
    public override byte Icon => 203;
    public override int Length => 180;
    public override string Name => "bot check";
    public override ICollection<string> Aliases { get; set; } = new List<string>
    {
    };
    
    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Buffs.TryAdd(Name, this))
            return;

        affected.Immunity = true;

        if (affected is Aisling aisling)
        {
            aisling.Client.SystemMessage("A member of your party is currently being analyzed.");
            aisling.Client.SystemMessage("Please wait.");
        }

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        affected.Immunity = false;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "You are free to go.");

        base.OnEnded(affected);
    }
}