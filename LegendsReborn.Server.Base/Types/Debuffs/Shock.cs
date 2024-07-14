namespace Darkages.Types.Debuffs;

public class Shock : DebuffBase
{
    public override byte Icon => 179;
    public override int Length => 8;
    public override string Name => "shock";
    public override ICollection<string> Aliases { get; } = new List<string>
    {
    };
    
    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Debuffs.TryAdd(Name, this))
            return;

        affected.SendAnimation(385, affected, affected);
        
        base.OnApplied(affected, timeLeft);
    }

    public override void OnDurationUpdate(Sprite affected)
    {
        affected.SendAnimation(385, affected, affected);
        
        if(affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "An electric shock paralyzes you!");
        
        base.OnDurationUpdate(affected);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Debuffs.TryRemove(Name, out _))
            return;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "You are no longer electrocuted.");
        
        base.OnEnded(affected);
    }
}