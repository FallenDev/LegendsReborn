using System;
using System.Collections.Generic;
using Legends.Server.Base.Network.ServerFormats;

namespace Legends.Server.Base.Types.Debuffs;

public class ArdPuinsein : DebuffBase
{
    public double Modifier { get; set; }
    public override byte Icon => 35;
    public override int Length { get; set; } = 12;
    public override string Name => "ard puinsein";
    public override ICollection<string> Aliases { get; } = new List<string>
    {
        "beag puinsein",
        "puinsein",
        "mor puinsein",
        "siolaidh"
    };

    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Debuffs.TryAdd(Name, this))
            return;

        if (affected is Aisling)
            Length = 180;
        
        var hpBar = new ServerFormat13
        {
            Serial = affected.Serial,
            Health = Convert.ToByte(affected.HpPct),
            Sound = 0xFF
        };

        affected.Show(Scope.NearbyAislings, hpBar);
        affected.SendAnimation(118, affected, affected);
        
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
        affected.SendAnimation(118, affected, affected);
        
        if(affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Deadly venom flows through your veins.");
        
        base.OnDurationUpdate(affected);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Debuffs.TryRemove(Name, out _))
            return;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "You feel better now.");
        
        base.OnEnded(affected);
    }

    private void ApplyPoison(Sprite affected)
    {
        var modifier = 0.1;

        if (affected is not Aisling)
            modifier /= 2;
        
        if (affected.CurrentHp > 100)
        {
            var venom = Convert.ToInt32(affected.CurrentHp * modifier);

            if (affected is Monster)
                venom = Math.Min(12000, venom);

            //dont drop hp below 100
            var newHp = Math.Max(100, affected.CurrentHp - venom);
            affected.CurrentHp = newHp;
        }
    }
}