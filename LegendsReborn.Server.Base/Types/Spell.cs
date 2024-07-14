using Chaos.Time;

using Darkages.Common;
using Darkages.Database;
using Darkages.Enums;
using Darkages.Network.Client;
using Darkages.ScriptingBase;
using Darkages.Sprites;
using Darkages.Templates;

using Microsoft.Data.SqlClient;

namespace Darkages.Types;

public class Spell
{
    public int SpellId { get; set; }
    public byte Icon { get; set; }
    public bool InUse { get; set; }
    public byte Level { get; set; }
    public int Lines { get; set; }
    public int ManaCost { get; set; }
    public string Name => Template.MaxLevel > 0 ? $"{Template.Name} (Lev:{Level}/{Template.MaxLevel})" : $"{Template.Name}";
    public string SpellName { get; set; }
    public ResettingCounter SpellThrottle { get; }
    public DateTime NextAvailableUse { get; set; }
    private bool Ready
    {
        get
        {
            var readyTime = DateTime.UtcNow;
            return readyTime > NextAvailableUse;
        }
            
    }
    public Dictionary<string, SpellScript> Scripts { get; set; }

    public byte Slot { get; set; }
    public SpellTemplate Template { get; set; }
    public int Casts { get; set; }


    public static void AttachScript(Spell spell) => spell.Scripts = ScriptManager.Load<SpellScript>(spell.Template.ScriptKey, spell);

    public static Spell Create(int slot, SpellTemplate spellTemplate)
    {
        var spellID = Generator.GenerateNumber();
        var obj = new Spell
        {
            Template = spellTemplate,
            SpellId = spellID,
            Level = 0,
            Slot = (byte)slot,
            Icon = spellTemplate.Icon,
            Lines = spellTemplate.BaseLines,
            ManaCost = spellTemplate.ManaCost,
        };

        return obj;
    }

    public static bool GiveTo(WorldClient client, string args)
    {
        if (!client.Aisling.LoggedIn) 
            return false;
        var spellList = ServerSetup.Instance.GlobalSpellTemplateCache.Where(x => x.Value.Name == args).ToList();
        if (spellList == null)
            return false;

        var spellTemplate = ServerSetup.Instance.GlobalSpellTemplateCache.Where(x => (x.Value.Name == args) && ((x.Value.Prerequisites.Class_Required == client.Aisling.Path) || (x.Value.Prerequisites.Class_Required == Class.Peasant))).Select(x => x.Value).FirstOrDefault();

        if (spellTemplate == null) 
            return false;
        if (client.Aisling.SpellBook.Has(spellTemplate)) 
            return false;

        var slot = client.Aisling.SpellBook.FindEmpty(spellTemplate.Pane == Pane.Spells ? 0 : 72);

        if ((slot <= 0) || ((slot >= 36) && (slot <= 72)) || (slot >= 90))
            return false;

        var spell = Create(slot, spellTemplate);
        {
            AttachScript(spell);
            {
                client.Aisling.SpellBook.Set(spell);
                client.SendAddSpellToPane(spell);
                client.Aisling.SendAnimation(22, client.Aisling, client.Aisling);
            }
        }
        try
        {
            using var sConn = new SqlConnection(AislingStorage.ConnectionString);
            var adapter = new SqlDataAdapter();
            sConn.Open();

            var level = 0;
            if (client.Aisling.GameMaster)
                level = 100;

            var spellId = Generator.GenerateNumber();
            var spellNameReplaced = spell.Template.ScriptKey.Replace("'", "''");
            var playerSpellBook = "INSERT INTO LegendsPlayers.dbo.PlayersSpellBook (SpellId, SpellName, Serial, Level, Slot, ScriptName, Casts) VALUES " +
                                  $"('{spellId}','{spell.Template.Name}','{client.Aisling.Serial}','{level}','{spell.Slot}','{spellNameReplaced}','{0}')";

            using var cmd = new SqlCommand(playerSpellBook, sConn);
            adapter.InsertCommand = cmd;
            adapter.InsertCommand.ExecuteNonQuery();
        }
        catch (SqlException e)
        {
            ServerSetup.EventsLogger(e.ToString());
        }
        catch (Exception e)
        {
            ServerSetup.EventsLogger(e.ToString());
        }

        return true;
    }

    public static bool GiveTo(Aisling aisling, string spellName, int level = 0)
    {
        if (!aisling.LoggedIn) 
            return false;
        //if spellName = invalid, does this throw an error?
        var spellList = ServerSetup.Instance.GlobalSpellTemplateCache.Where(x => x.Value.Name.EqualsI(spellName)).ToList();
            
        var spellTemplate = spellList.Select(x => x.Value).FirstOrDefault();

        if (spellTemplate == null) 
            return false;
        if (aisling.SpellBook.Has(spellName)) 
            return false;

        var slot = aisling.SpellBook.FindEmpty(spellTemplate.Pane == Pane.Spells ? 0 : 72);

        if ((slot <= 0) || ((slot >= 36) && (slot <=72)) || (slot >= 90)) 
            return false;

        var spell = Create(slot, spellTemplate);
        {
            AttachScript(spell);
            {
                aisling.SpellBook.Set(spell);
                aisling.Client.SendAddSpellToPane(spell);
                aisling.SendAnimation(22, aisling, aisling);
            }
        }
        try
        {
            using var sConn = new SqlConnection(AislingStorage.ConnectionString);
            var adapter = new SqlDataAdapter();
            sConn.Open();

            level = 0;
            if (aisling.GameMaster)
                level = 100;

            var spellId = Generator.GenerateNumber();
            var scriptNameReplaced = spell.Template.ScriptKey.Replace("'", "''");
            var playerSpellBook = "INSERT INTO LegendsPlayers.dbo.PlayersSpellBook (SpellId, SpellName, Serial, Level, Slot, ScriptName, Casts) VALUES " +
                                  $"('{spellId}','{spell.Template.Name}','{aisling.Serial}','{level}','{spell.Slot}','{scriptNameReplaced}','{0}')";

            var cmd = new SqlCommand(playerSpellBook, sConn);
            adapter.InsertCommand = cmd;
            adapter.InsertCommand.ExecuteNonQuery();
        }
        catch (SqlException e)
        {
            ServerSetup.EventsLogger(e.ToString());
        }
        catch (Exception e)
        {
            ServerSetup.EventsLogger(e.ToString());
        }
        aisling.Client.LoadSpellBook();
        return true;
    }

    public bool CanUse() => Ready;
    public bool CanUse(Aisling aisling) => Ready && aisling.SpellThrottle.CanIncrement;

    public bool IsCurse => (Template.Name.ContainsIn("cradh") && !Template.Name.ContainsIn("ao")) || Template.Name.ContainsIn("siolaidh");
    public bool IsPoison => (Template.Name.ContainsIn("puinsein") && !Template.Name.ContainsIn("ao")) || Template.Name.ContainsIn("siolaidh");
    public bool IsSuain => (Template.Name.ContainsIn("suain") && !Template.Name.ContainsIn("ao")) || Template.Name.ContainsIn("siolaidh");
    public bool IsPramh => (Template.Name.ContainsIn("pramh") && !Template.Name.ContainsIn("ao")) || Template.Name.ContainsIn("siolaidh");
}