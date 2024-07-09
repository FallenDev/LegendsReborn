namespace Darkages.Types.Buffs;

public class Arrest : BuffBase
{
    public override byte Icon => 126;
    public override int Length => 31536000;
    public override string Name => "arrest";
    public override ICollection<string> Aliases { get; set; } = new List<string>
    {
    };
    
    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Buffs.TryAdd(Name, this))
            return;

        if (affected is Aisling aisling)
        {
            aisling.Client.SystemMessage("You have been arrested.");
        }

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        if (affected is Aisling aisling)
            aisling.Client.SystemMessage("You are free to go");
        affected.Client.Aisling.GoHome();

        base.OnEnded(affected);
    }
}