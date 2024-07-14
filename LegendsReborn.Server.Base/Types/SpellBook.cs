using Dapper;
using Darkages.Database;
using Darkages.Object;
using Darkages.Templates;

using Microsoft.Data.SqlClient;

namespace Darkages.Types;

public class SpellBook : ObjectManager
{
    public static readonly int SpellLength = 35 * 3;

    public Dictionary<int, Spell> Spells = new();

    public SpellBook()
    {
        for (var i = 0; i < SpellLength; i++)
            Spells[i + 1] = null;
    }
    public int Length => Spells.Count;

    public int FindEmpty(int start = 0)
    {
        var slot = 0;

        for (var i = start; i < Length; i++)
            if (Spells[i + 1] == null)
            {
                slot = i + 1;
                break;
            }

        return slot > 0 ? slot : -1;
    }

    public Spell FindInSlot(int slot)
    {
        Spell ret = null;

        if (Spells.ContainsKey(slot))
            ret = Spells[slot];

        if ((ret != null) && (ret.Template != null)) 
            return ret;

        return null;
    }

    public new Spell[] Get(Predicate<Spell> prediate) =>
        Spells.Values.Where(i => (i != null) && prediate(i)).ToArray();

    public bool Has(string s) =>
        Spells.Where(i => (i.Value != null) && (i.Value.Template != null)).Select(i => i.Value.Template)
            .FirstOrDefault(i => s.Equals(i.Name)) != null;

    public bool Has(SpellTemplate s)
    {
        var obj = Spells.Where(i => (i.Value != null) && (i.Value.Template != null)).Select(i => i.Value.Template)
            .FirstOrDefault(i => i.Name.Equals(s.Name));

        return obj != null;
    }

    public Spell Remove(byte slot, bool spellDelete = false)
    {
        if (!Spells.ContainsKey(slot))
            return null;

        var copy = Spells[slot];

        if (spellDelete)
            DeleteFromAislingDb(copy);

        Spells[slot] = null;

        return copy;
    }

    public void Set(Spell s, bool clone = false) =>
        Spells[s.Slot] = s;


    private static void DeleteFromAislingDb(Spell spell)
    {
        var sConn = new SqlConnection(AislingStorage.ConnectionString);

        try
        {
            sConn.Open();

            const string cmd = $"DELETE FROM LegendsPlayers.dbo.PlayersSpellBook WHERE SpellId = @SpellId";
            sConn.Execute(cmd, new { spell.SpellId });

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