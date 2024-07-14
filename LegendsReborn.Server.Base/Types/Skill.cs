using Chaos.Time;

using Darkages.Common;
using Darkages.Database;
using Darkages.Enums;
using Darkages.ScriptingBase;
using Darkages.Sprites;
using Darkages.Templates;

using Microsoft.Data.SqlClient;
using Darkages.Network.Client;

namespace Darkages.Types;

public class Skill
{
    public int SkillId { get; set; }
    public byte Icon { get; set; }
    public bool InUse { get; internal set; }
    public int Level { get; set; }
    public string Name => Template.MaxLevel > 0 ? $"{Template.Name} (Lev:{Level}/{Template.MaxLevel})" : $"{Template.Name}";
    public string SkillName { get; set; }
    public DateTime NextAvailableUse { get; set; }
    public ResettingCounter ActionThrottle { get; }
    public bool Ready
    {
        get
        {
            var time = DateTime.UtcNow;
            return time > NextAvailableUse;
        }
    }
    public Dictionary<string, SkillScript> Scripts { get; set; }

    public byte Slot { get; set; }
    public SkillTemplate Template { get; set; }
    public int Uses { get; set; }

    public static void AttachScript(Skill skill) => skill.Scripts = ScriptManager.Load<SkillScript>(skill.Template.ScriptName, skill);

    public static Skill Create(int slot, SkillTemplate skillTemplate)
    {
        int skillID;

        lock (Generator.Random)
            skillID = Generator.GenerateNumber();

        var obj = new Skill
        {
            Template = skillTemplate,
            SkillId = skillID,
            Level = 0,
            Slot = (byte)slot,
            Icon = skillTemplate.Icon,
            NextAvailableUse = DateTime.UtcNow
        };

        obj.Template.ID = obj.SkillId;

        return obj;
    }

    public static bool GiveTo(WorldClient client, string args)
    {
        if (!client.Aisling.LoggedIn)
            return false;
        if (!ServerSetup.Instance.GlobalSkillTemplateCache.ContainsKey(args))
            return false;

        var skillTemplate = ServerSetup.Instance.GlobalSkillTemplateCache[args];

        if (skillTemplate == null)
            return false;
        if (client.Aisling.SkillBook.Has(skillTemplate))
            return false;

        var slot = client.Aisling.SkillBook.FindEmpty(skillTemplate.Pane == Pane.Skills ? 0 : 72);

        if (slot <= 0)
            return false;

        var skill = Create(slot, skillTemplate);
        {
            AttachScript(skill);
            {
                client.Aisling.SkillBook.Set(skill);
                client.SendAddSkillToPane(skill);
                client.Aisling.SendAnimation(22, client.Aisling, client.Aisling);
            }
        }
        using var sConn = new SqlConnection(AislingStorage.ConnectionString);
        var adapter = new SqlDataAdapter();
        var level = 0;
        if (client.Aisling.GameMaster)
            level = 100;
        try
        {
            sConn.Open();

            int skillId;

            lock (Generator.Random)
                skillId = Generator.GenerateNumber();

            var scriptNameReplaced = skill.Template.ScriptName.Replace("'", "''");
            var skillNameReplaced = skill.Template.Name.Replace("'", "''");
            var playerSkillBook = "INSERT INTO LegendsPlayers.dbo.PlayersSkillBook (SkillId, SkillName, Serial,Level, Slot, Uses, ScriptName, NextAvailableUse)" +
                                  $" VALUES ('{skillId}','{skillNameReplaced}','{client.Aisling.Serial}','{level}','{skill.Slot}','{0}','{scriptNameReplaced}', '{DateTime.UtcNow}')";

            var cmd = new SqlCommand(playerSkillBook, sConn);
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

    public static bool GiveTo(Aisling aisling, string args, int level = 100)
    {
        if (!aisling.LoggedIn)
            return false;
        if (!ServerSetup.Instance.GlobalSkillTemplateCache.ContainsKey(args))
            return false;

        var skillTemplate = ServerSetup.Instance.GlobalSkillTemplateCache[args];

        if (skillTemplate == null)
            return false;
        if (aisling.SkillBook.Has(skillTemplate))
            return false;

        var slot = aisling.SkillBook.FindEmpty(skillTemplate.Pane == Pane.Skills ? 0 : 72);

        if (slot <= 0)
            return false;

        var skill = Create(slot, skillTemplate);
        {
            AttachScript(skill);
            {
                aisling.SkillBook.Set(skill);
                aisling.Client.SendAddSkillToPane(skill);
                aisling.SendAnimation(22, aisling, aisling);
            }
        }
        level = 0;
        if (aisling.GameMaster)
            level = 100;
        using var sConn = new SqlConnection(AislingStorage.ConnectionString);
        var adapter = new SqlDataAdapter();

        try
        {
            sConn.Open();

            int skillId;

            lock (Generator.Random)
                skillId = Generator.GenerateNumber();

            var scriptNameReplaced = skill.Template.ScriptName.Replace("'", "''");
            var skillNameReplaced = skill.Template.Name.Replace("'", "''");
            var playerSkillBook = "INSERT INTO LegendsPlayers.dbo.PlayersSkillBook (SkillId, SkillName, Serial, Level, Slot, Uses, ScriptName, NextAvailableUse)" +
                                  $" VALUES ('{skillId}','{skillNameReplaced}','{aisling.Serial}','{level}','{skill.Slot}','{0}','{scriptNameReplaced}', '{DateTime.UtcNow}')";

            var cmd6 = new SqlCommand(playerSkillBook, sConn);
            adapter.InsertCommand = cmd6;
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

    public bool CanUse(Aisling aisling) => Ready;
    public bool CanUse() => Ready;
}