using Dapper;

using Darkages.Database;
using Darkages.Network.Client;
using Darkages.Sprites;
using Microsoft.Data.SqlClient;

namespace Darkages.Types;

public class Legend
{
    public List<LegendItem> LegendMarks = new();

    public void AddLegend(WorldClient client, LegendItem legend)
    {
        var Date = Calendar.Now.LegendToString();
        ;
        if (legend == null)
            return;
        if (client.Aisling == null)
            return;
        if (LegendMarks.Contains(legend))
            return;
        legend.Value += $" - {Date}";
        LegendMarks.Add(legend);
        AddToAislingDb(client.Aisling, legend);
    }

    public bool Has(string lpVal) => LegendMarks.Any(i => i.Value.Equals(lpVal));

    public void Remove(LegendItem legend, WorldClient client)
    {
        if (legend == null) 
            return;
        if (client.Aisling == null) 
            return;
        LegendMarks.Remove(legend);
        DeleteFromAislingDb(client.Aisling, legend);
    }
    public static void RemoveFromDB(WorldClient client, LegendItem legend) => DeleteFromAislingDb(client.Aisling, legend);

    public class LegendItem
    {
        public string Category { get; set; }
        public byte Color { get; set; }
        public byte Icon { get; set; }
        public string Value { get; set; }
        public int LegendId { get; set; }

    }
    private static void AddToAislingDb(Aisling aisling, LegendItem legend)
    {
        using var sConn = new SqlConnection(AislingStorage.ConnectionString);
        using var cmd = sConn.CreateCommand();

        try
        {
            sConn.Open();

            //int legendId = /*zizette*/;
            var s = legend.Value;
            s = s.Replace("'", "''");
            var playerInventory = "INSERT INTO LegendsPlayers.dbo.PlayersLegend (Serial, Category, Color, Icon, Value)" +
                                  $" VALUES ('{aisling.Serial}','{legend.Category}','{legend.Color}','{legend.Icon}','{s}')";

            cmd.CommandText = playerInventory;
            cmd.ExecuteNonQuery();
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

    private static void DeleteFromAislingDb(Aisling aisling, LegendItem legend)
    {

        if (legend.Category == null) 
            return;

        try
        {
            var sConn = new SqlConnection(AislingStorage.ConnectionString);
            var category = legend.Category.ToString();
            var serial = aisling.Serial;
            sConn.Open();
            const string CMD = "DELETE FROM LegendsPlayers.dbo.PlayersLegend WHERE Category = @Category AND Serial = @Serial";
            sConn.Execute(CMD, new {
                Category = category,
                Serial = serial });
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