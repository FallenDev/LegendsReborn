namespace Darkages.Types.Buffs;

public class Sgiath : BuffBase
{
    public override byte Icon => 91;
    public override int Length => 6;
    public override string Name => "sgiath";
    public override ICollection<string> Aliases { get; set; } = new List<string>
    {
        "dion",
    };
    private static readonly ICollection<string> DionScriptKeys = new List<string>
    {
        "sgiath",
        "mor sgiath",
        "dia sgiath"
    };
    public override bool TryApply(Sprite source, Sprite affected)
    {
        if (affected is Aisling)
            if (affected.Client.Aisling.Flags.HasFlag(AislingFlags.SpellImmune))
            {
                OnEnded(affected);
                return false;
            }
            else
            {
                OnApplied(affected);
                return true;
            }
        else
            return true;
    }
    public override void OnDurationUpdate(Sprite affected)
    {
        if (affected is Aisling aisling)
        {
            var manaDrain = aisling.MaximumMp * 0.03;
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
        if (affected is not Aisling aisling)
            return;

        if (aisling.Dead)
            return;

        if (!affected.Buffs.TryAdd(Name, this))
            return;

        aisling.Flags |= AislingFlags.SpellImmune;
        aisling.Client.SendMessage(0x02, "Your body is covered in a magical film.");

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        if (affected is Aisling aisling)
        {
            aisling.Flags &= ~AislingFlags.SpellImmune;
            aisling.Client.SendMessage(0x02, "The magical film dissipates.");
            ToggleSgiath(aisling, 30);
        }

        base.OnEnded(affected);
    }
    protected void ToggleSgiath(Sprite affected, int time)
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