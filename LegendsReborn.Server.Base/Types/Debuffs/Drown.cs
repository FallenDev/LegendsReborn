namespace Darkages.Types.Debuffs;

public class Drown : DebuffBase
{
    public double Modifier { get; set; }
    public override byte Icon => 40;
    public override int Length { get; set; } = 6;
    public override string Name => "drown";
    
    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Debuffs.TryAdd(Name, this))
            return;
        
        var hpBar = new ServerFormat13
        {
            Serial = affected.Serial,
            Health = Convert.ToByte(affected.HpPct),
            Sound = 0xFF
        };

        affected.Show(Scope.NearbyAislings, hpBar);
        base.OnApplied(affected, timeLeft);
    }

    public override void OnDurationUpdate(Sprite affected)
    {
        ApplyPoison(affected);
        
        var hpBar = new ServerFormat13
        {
            Serial = affected.Serial,
            Health = Convert.ToByte(affected.HpPct),
            Sound = 0xFF
        };

        affected.Show(Scope.NearbyAislings, hpBar);
        affected.SendAnimation(222, affected, affected);
        
        if(affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "You are drowning.");
        
        base.OnDurationUpdate(affected);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Debuffs.TryRemove(Name, out _))
            return;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "You are no longer drowning.");
        
        base.OnEnded(affected);
    }

    private void ApplyPoison(Sprite affected)
    {
        var modifier = 0.125;

        if (affected is not Aisling)
            modifier = 0;
        
        if (affected.CurrentHp > 100)
        {
            var venom = Convert.ToInt32(affected.MaximumHp * modifier);

            if (affected is Monster)
                venom = Math.Min(25000, venom);

            //dont drop hp below 100
            var newHp = Math.Max(100, affected.CurrentHp - venom);
            affected.CurrentHp = newHp;
        }
    }
}