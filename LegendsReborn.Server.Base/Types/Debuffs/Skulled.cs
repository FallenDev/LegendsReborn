using System;
using Legends.Server.Base.Infrastructure;
using Legends.Server.Base.Network.ServerFormats;

namespace Legends.Server.Base.Types.Debuffs;

public class Skulled : DebuffBase
{
    public override byte Icon => 89;
    public override int Length => ServerContext.Config.SkullLength;
    public override string Name => "skulled";
    public string[] Messages =>
        ServerContext.Config.ReapMessage.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
    public int Count => Messages.Length;
    private readonly Random Rng = new();

    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (affected is not Aisling aisling)
            return;
        
        //if we're already on death map, dont apply
        if (aisling.CurrentMapId == ServerContext.Config.DeathMap)
            return;
        
        if (aisling.GameMaster)
            return;
        
        if (!affected.Debuffs.TryAdd(Name, this))
            return;
        
        //if we're invisible, remove invisible so others can revive us.
        if (affected.HasBuff("hide") || affected.HasBuff("veil"))
        {
            aisling.Invisible = false;
            aisling.Client.UpdateDisplay();
            aisling.Client.SendMessage(0x02, "You emerge from the shadows.");
            aisling.RemoveBuff("hide");
            aisling.RemoveBuff("veil");
        }

        //show an empty hp bar
        var hpBar = new ServerFormat13
        {
            Serial = aisling.Serial,
            Health = 0,
            Sound = 6
        };
        
        aisling.SendAnimation(24, aisling, aisling);
        aisling.Show(Scope.NearbyAislings, hpBar);
        aisling.CurrentHp = 1;
        
        base.OnApplied(affected, timeLeft);
    }

    public override void OnDurationUpdate(Sprite affected)
    {
        if ((affected.CurrentMapId != ServerContext.Config.DeathMap) && affected is Aisling aisling)
        {
            if (aisling.GameMaster)
                aisling.CurrentHp = aisling.MaximumHp;
            
            var hpBar = new ServerFormat13
            {
                Serial = aisling.Serial,
                Health = 0,
                Sound = 6
            };
            
            affected.Client.SendMessage(0x02, Messages[Rng.Next(Count) % Messages.Length]);
            aisling.SendAnimation(24, aisling, aisling);
            aisling.Show(Scope.NearbyAislings, hpBar);
            aisling.CurrentHp = 1;
        }
        
        base.OnDurationUpdate(affected);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Debuffs.TryRemove(Name, out _))
            return;

        if (affected is not Aisling aisling)
            return;

        if (aisling.GameMaster)
            return;

        if (affected.CurrentMapId == ServerContext.Config.DeathMap)
            return;

        if (!Cancelled)
        {
            var hpBar = new ServerFormat13
            {
                Serial = aisling.Serial,
                Health = Convert.ToByte(aisling.HpPct),
                Sound = 5
            };
            
            aisling.Client.SendMessage(0x02, "Your soul is pulled away by something sinister.");
            aisling.Show(Scope.NearbyAislings, hpBar);
            aisling.CastDeath();
            aisling.SendToHell();
        }
        
        base.OnEnded(affected);
    }
}