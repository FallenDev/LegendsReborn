namespace Darkages.Types.Buffs;

public class ShadowVeil : BuffBase
{
    public override byte Icon => 131;
    public override int Length => 300;
    public override string Name => "veil";
    public override ICollection<string> Aliases { get; set; } = new List<string>
    {
        "hide"
    };
    
    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (affected is not Aisling aisling)
            return;

        if (aisling.Dead)
            return;

        if (!affected.Buffs.TryAdd(Name, this))
            return;
        
        aisling.Hide();
        aisling.Client.SendMessage(0x02, "You blend in to the shadows.");

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        if (affected is Aisling aisling)
        {
            aisling.Invisible = false;
            aisling.Client.UpdateDisplay();
            aisling.Client.SendMessage(0x02, "You emerge from the shadows.");
        }

        base.OnEnded(affected);
    }
}