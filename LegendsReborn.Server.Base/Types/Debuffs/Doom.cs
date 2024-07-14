namespace Darkages.Types.Debuffs;

public class Doom : DebuffBase
{
    public override byte Icon => 89;
    public override int Length => 12;
    public override string Name => "doom";
    public override ICollection<string> Aliases { get; } = new List<string>
    {
        "siolaidh"
    };
    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (affected is Aisling { GameMaster: true })
            return;
        
        if (!affected.Debuffs.TryAdd(Name, this))
            return;
        
        var sound = new ServerFormat19(8);
        affected.Show(Scope.NearbyAislings, sound);
        affected.SendAnimation(75, affected, affected);
        
        base.OnApplied(affected, timeLeft);
    }

    public override void OnDurationUpdate(Sprite affected)
    {
        affected.SendAnimation(75, affected, affected);
        
        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Your death is immanent.");
        
        base.OnDurationUpdate(affected);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Debuffs.TryRemove(Name, out _))
            return;

        if (affected is Aisling { GameMaster: true })
            return;

        if (!Cancelled)
        {
            affected.SendAnimation(49, affected, affected);

            if (affected is Monster)
                affected.CurrentHp = Convert.ToInt32(affected.MaximumHp * 0.01);

            if (affected is Aisling)
            {
                affected.CurrentHp = 0;
                affected.Client.SendMessage(0x02, "Your body has withered away.");
            }
        }

        base.OnEnded(affected);
    }
}