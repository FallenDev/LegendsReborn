namespace Darkages.Types.Debuffs;

public class Pramh : DebuffBase
{
    public override byte Icon => 90;
    public override int Length => 16;
    public override string Name => "pramh";
    public override ICollection<string> Aliases { get; } = new List<string>
    {
        "frozen",
        "mor pramh",
        "siolaidh"
    };

    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Debuffs.TryAdd(Name, this))
            return;

        affected.SendAnimation(388, affected, affected);
        base.OnApplied(affected, timeLeft);
    }

    public override void OnDurationUpdate(Sprite affected)
    {
        affected.SendAnimation(388, affected, affected);
        affected.SendSound(8);
        
        if(affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "You are locked in a deep slumber.");
        
        base.OnDurationUpdate(affected);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Debuffs.TryRemove(Name, out _))
            return;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "You have woken up!");
        
        base.OnEnded(affected);
    }
}