namespace Darkages.Types.Debuffs;

public class BeagSuain : DebuffBase
{
    public override byte Icon => 97;
    public override int Length => 6;
    public override string Name => "beag suain";
    public override ICollection<string> Aliases { get; } = new List<string>
    {
        "frozen"
    };

    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Debuffs.TryAdd(Name, this))
            return;

        affected.SendAnimation(41, affected, affected);
        var sound = new ServerFormat19(64);
        affected.Show(Scope.NearbyAislings, sound);
        
        base.OnApplied(affected, timeLeft);
    }

    public override void OnDurationUpdate(Sprite affected)
    {
        affected.SendAnimation(41, affected, affected);
        
        if(affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "You are unable to move.");
        
        base.OnDurationUpdate(affected);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Debuffs.TryRemove(Name, out _))
            return;
        
        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "You are free to move again.");
        
        base.OnEnded(affected);
    }
}