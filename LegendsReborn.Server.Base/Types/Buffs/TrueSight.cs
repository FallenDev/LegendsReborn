namespace Darkages.Types.Buffs;

public class TrueSight : BuffBase
{
    public override byte Icon => 7;
    public override int Length => 1800;
    public override string Name => "true sight";
    public override ICollection<string> Aliases { get; set; } = new List<string>
    {
        "eisd creutair"
    };
    
    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (affected is not Aisling aisling)
            return;

        if (!affected.Buffs.TryAdd(Name, this))
            return;

        if (aisling.CurrentMapId is 70001 or 70003 or 5235)
            aisling.Client.UpdateDisplay();

        aisling.Client.SendMessage(0x02, "The shadows reveal their secrets to you.");

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