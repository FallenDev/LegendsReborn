namespace Darkages.Types.Debuffs;

public class Herbalism : DebuffBase
{
    public override byte Icon => 132;
    public override int Length => 8;
    public override string Name => "herbalism";

    private readonly Random Rng = new();

    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (affected is not Aisling aisling)
            return;
        
        if (!affected.Debuffs.TryAdd(Name, this))
            return;

        aisling.SendAnimation(106, affected, affected);

        aisling.Client.SendMessage(0x02, "You are unable to move while gathering herbs.");
        
        base.OnApplied(affected, timeLeft);
    }

    public override void OnDurationUpdate(Sprite affected)
    {
        if (affected is not Aisling aisling) 
            return;

        aisling.SendAnimation(106, affected, affected);
        
        base.OnDurationUpdate(affected);
    }
    public override void OnEnded(Sprite affected)
    {
        if (!affected.Debuffs.TryRemove(Name, out _))
            return;

        if (affected is not Aisling aisling)
            return;

        var plantName = affected.CurrentMapId switch
        {
            99000 => "Hemloch Plant",
            99001 => "Persica Plant",
            99002 => "Fifleaf Plant",
            99003 => "Ancusa Plant",
            99004 => "Betony Plant",
            99005 => "Hydele Plant",
            _ => null
        };

        var adverb = Rng.Next(1, 3) switch
        {
            1 => "successfully",
            2 => "masterfully",
            _ => null
        };

        //Update to 
        var skillBase = affected.CurrentMapId switch
        {
            99000 => "9", //Hemloch
            99001 => "5", //Persica
            99002 => "3", //Fifleaf
            99003 => "7", //Ancusa
            99004 => "6", //Betony
            99005 => "1", //Hydele
            _ => null
        };

        var amount = adverb switch
        {
            "successfully" => 1,
            "masterfully" => 2,
            _ => 0
        };

        if (affected.Client.ShouldGather() && !string.IsNullOrEmpty(plantName) && !string.IsNullOrEmpty(adverb) && (amount != 0))
        {
            if (aisling.GiveManyItems(plantName, amount))
            {
                aisling.Client.SendMessage(0x02, $"You have {adverb} gathered {amount} {plantName}s!");
                aisling.Animate(84);
                if (aisling.Herbalism == (Convert.ToInt32(skillBase)) || aisling.Herbalism == (Convert.ToInt32(skillBase) + 1) || aisling.Herbalism == (Convert.ToInt32(skillBase) - 1))
                    aisling.HerbSuccess++;
                aisling.Client.CheckHerbalism();
            }
            else
                aisling.Client.SendMessage(2, "You cannot carry any more.");
        }

        base.OnEnded(affected);
    }
}