namespace Darkages.Types.Buffs;

public class Dion : BuffBase
{
    public override byte Icon => 53;
    public override int Length => 12;
    public override string Name => "dion";
    public override ICollection<string> Aliases { get; set; } = new List<string>
    {
        "sgiath"
    };
    private static readonly ICollection<string> DionScriptKeys = new List<string>
    {
        "dion",
        "mor dion",
    };
    public override bool TryApply(Sprite source, Sprite affected)
    {
        if (affected.Immunity)
        {
            OnEnded(affected);
            return false;
        }
        else
        {
            OnApplied(affected);
            return true;
        }
    }
    public override void OnDurationUpdate(Sprite affected)
    {
        if (affected is Aisling aisling)
        {
            var manaDrain = aisling.MaximumMp * 0.08;
            if (manaDrain > aisling.CurrentMp)
                OnEnded(affected);
            else
            {
                aisling.CurrentMp -= Convert.ToInt32(manaDrain);
                aisling.Client.SendStats(StatusFlags.All);
            }
        }
        base.OnDurationUpdate(affected);
    }

    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Buffs.TryAdd(Name, this))
            return;

        affected.Immunity = true;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Your body has been galvanized.");

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;
        if (affected is Aisling aisling)
        {
            aisling.Client.SendMessage(0x02, "Your body is no longer galvanized.");
            ToggleDion(aisling, 90);
        }

        affected.Immunity = false;
        base.OnEnded(affected);
    }
    protected void ToggleDion(Sprite affected, int time)
    {
            foreach (var spell in affected.Client.Aisling.SpellBook.Spells.Values)
                foreach (var key in DionScriptKeys)
                    if (spell != null && spell.Scripts.ContainsKey(key))
                    {
                        spell.NextAvailableUse = DateTime.UtcNow + TimeSpan.FromSeconds(time);
                        affected.Client.Send(new ServerFormat3F(0, spell.Slot, time));
                    }
    }
}