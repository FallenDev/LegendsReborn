namespace Darkages.Types.Buffs;

public class Hide : BuffBase
{
    public override byte Icon => 10;
    public override int Length => 90;
    public override string Name => "hide";
    public override ICollection<string> Aliases { get; set; } = new List<string>
    {
        "veil"
    };
    
    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (affected is not Aisling aisling)
            return;

        if (!affected.Buffs.TryAdd(Name, this))
            return;

        if (!aisling.Dead)
        {
            aisling.Hide();
            if (aisling.Path == Class.Rogue)
                aisling.BonusHit += 10;
            aisling.Client.SendMessage(0x02, "You blend in to the shadows.");
        }

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        if (affected is Aisling aisling)
        {
            aisling.Invisible = false;
            if (aisling.Path == Class.Rogue)
                aisling.BonusHit -= 10;
            aisling.Client.UpdateDisplay();
            aisling.Client.SendMessage(0x02, "You emerge from the shadows.");
        }

        base.OnEnded(affected);
    }
}