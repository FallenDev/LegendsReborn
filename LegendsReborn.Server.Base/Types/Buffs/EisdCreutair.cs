namespace Darkages.Types.Buffs;

public class EisdCreutair : BuffBase
{
    public override byte Icon => 7;
    public override int Length => 600;
    public override string Name => "eisd creutair";
    public override ICollection<string> Aliases { get; set; } = new List<string>
    {
        "true sight"
    };
    
    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Buffs.TryAdd(Name, this))
            return;

        if (affected is Aisling aisling)
        {
            aisling.Client.UpdateDisplay();
            aisling.Client.SendMessage(0x02, "The shadows reveal their secrets to you.");
        }

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        if (affected is Aisling aisling)
        {
            aisling.Client.UpdateDisplay();
            aisling.Client.SendMessage(0x02, "Your eyesight returns to normal.");
        }

        base.OnEnded(affected);
    }
}