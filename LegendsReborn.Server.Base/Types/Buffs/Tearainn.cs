namespace Darkages.Types.Buffs;

public class Tearainn : BuffBase
{
    public override byte Icon => 150;
    public override int Length => 600;
    public override string Name => "tearainn";
    public override ICollection<string> Aliases { get; set; } = new List<string>
    {
    };
    
    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (affected is not Aisling aisling)
            return;

        if (aisling.Dead)
            return;

        if (!affected.Buffs.TryAdd(Name, this))
            return;

        aisling.BonusMr += 20;
        aisling.Client.SendMessage(0x02, "You no longer fear magic.");

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        if (affected is Aisling aisling)
        {
            aisling.BonusMr -= 20;
            aisling.Client.SendMessage(0x02, "Your protection from magic fades.");
        }

        base.OnEnded(affected);
    }
}