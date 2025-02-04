namespace Darkages.Types.Debuffs;

public class Anaemia : DebuffBase
{
    public double Modifier { get; set; }
    public override byte Icon => 97;
    public override int Length => 6;
    public override string Name => "anaemia";
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
        affected.SendAnimation(310, affected, affected);
        
        base.OnApplied(affected, timeLeft);
    }

    public override void OnDurationUpdate(Sprite affected)
    {
        ApplyBleed(affected);
        
        //var hpBar = new ServerFormat13
        //{
        //    Serial = affected.Serial,
        //    Health = Convert.ToByte(affected.HpPct),
        //    Sound = 72
        //};

        //affected.Show(Scope.NearbyAislings, hpBar);
        affected.SendAnimation(310, affected, affected);
        
        if(affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "You feel dizzy from blood loss.");
        
        base.OnDurationUpdate(affected);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Debuffs.TryRemove(Name, out _))
            return;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(2, "You feel healthy again.");
        
        base.OnEnded(affected);
    }

    private void ApplyBleed(Sprite affected)
    {
        var modifier = 0.0125;

        if (affected is not Aisling)
            modifier /= 2;
        
        if (affected.CurrentHp > 100)
        {
            var bleed = Convert.ToInt32(affected.CurrentHp * modifier);

            if (affected is Monster)
                bleed = Math.Min(1000, bleed);

            //dont drop hp below 100
            var newHp = Math.Max(100, affected.CurrentHp - bleed);
            affected.CurrentHp = newHp;
        }
    }
}