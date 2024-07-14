using Darkages.Database;
using Darkages.Object;
using Darkages.Templates;

using Microsoft.Data.SqlClient;
using Dapper;

namespace Darkages.Types;

public class SkillBook : ObjectManager
{
    private const int SkillLength = 35 * 3;

    public readonly Dictionary<int, Skill> Skills = new();

    public SkillBook()
    {
        for (var i = 0; i < SkillLength; i++)
            Skills[i + 1] = null;

        //Skills[35] = new Skill(); //Dummy skill to ensure nothing ends up in the 'blank' space in the bottom right corner.
    }

    public int Length => Skills.Count;

    public void Assign(Skill skill) => Set(skill);

    public int FindEmpty(int start = 0)
    {
        for (var i = start; i < Length; i++)
            if (Skills[i + 1] == null)
                return i + 1;

        return -1;
    }

    public new Skill[] Get(Predicate<Skill> predicate) => Skills.Values.Where(i => (i != null) && predicate(i)).ToArray();

    public bool Has(Skill s) =>
        Skills.Where(i => i.Value != null).Select(i => i.Value.Template)
            .FirstOrDefault(i => i.Name.Equals(s.Template.Name)) != null;

    public bool Has(SkillTemplate s) =>
        Skills.Where(i => i.Value?.Template != null).Select(i => i.Value.Template)
            .FirstOrDefault(i => i.Name.Equals(s.Name)) != null;

    public Skill Remove(byte movingFrom, bool skillDelete = false)
    {
        if (!Skills.ContainsKey(movingFrom))
            return null;

        var copy = Skills[movingFrom];

        if (skillDelete)
            DeleteFromAislingDb(copy);

        Skills[movingFrom] = null;

        return copy;
    }

    private void Set(Skill s) => Skills[s.Slot] = Clone<Skill>(s);

    public void Set(Skill s, bool clone = false) => Skills[s.Slot] = clone ? Clone<Skill>(s) : s;

    private static void DeleteFromAislingDb(Skill skill)
    {
        var sConn = new SqlConnection(AislingStorage.ConnectionString);

        try
        {
            sConn.Open();

            const string cmd = "DELETE FROM LegendsPlayers.dbo.PlayersSkillBook WHERE SkillId = @SkillId";
            sConn.Execute(cmd, new { skill.SkillId });

            sConn.Close();
        }
        catch (SqlException e)
        {
            ServerSetup.EventsLogger(e.ToString());
        }
        catch (Exception e)
        {
            ServerSetup.EventsLogger(e.ToString());
        }
    }
}