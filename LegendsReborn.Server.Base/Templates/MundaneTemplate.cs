using Darkages.Common;
using Darkages.Enums;
using Darkages.Types;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using ServiceStack;

using System.Collections.ObjectModel;

using System.ComponentModel;

namespace Darkages.Templates;

[Flags]
public enum ViewQualifer
{
    None = 0,
    Peasants = 1 << 1,
    Warriors = 1 << 2,
    Wizards = 1 << 3,
    Monks = 1 << 4,
    Rogues = 1 << 5,
    Priests = 1 << 6,
    All = Peasants | Warriors | Wizards | Monks | Rogues | Priests
}

public class MundaneTemplate : Template
{
    public MundaneTemplate() => Speech = [];

    public int AreaID { get; set; }
    [Browsable(false)] [JsonIgnore] public WorldServerTimer AttackTimer { get; set; }
    public int CastRate { get; set; }
    public int ChatRate { get; set; }
    [Browsable(false)] [JsonIgnore] public WorldServerTimer ChatTimer { get; set; }
    public List<string> DefaultMerchantStock { get; set; } = [];
    public List<string> RogueStock { get; set; } = [];
    public byte Direction { get; set; }
    public bool EnableAttacking { get; set; }
    public bool EnableCasting { get; set; }
    public bool EnableTurning { get; set; }
    public bool EnableWalking { get; set; }
    public short Image { get; set; }
    public int Level { get; set; }
    [Browsable(false)] public int MaximumHp { get; set; }
    [Browsable(false)] public int MaximumMp { get; set; }
    [JsonProperty] public PathQualifer PathQualifer { get; set; }
    public string QuestKey { get; set; }
    public string ScriptKey { get; set; }
    public List<string> Skills { get; set; }
    public Collection<string> Speech { get; set; }
    public List<string> Spells { get; set; }
    [Browsable(false)] [JsonIgnore] public WorldServerTimer SpellTimer { get; set; }
    public int TurnRate { get; set; }
    [Browsable(false)] [JsonIgnore] public WorldServerTimer TurnTimer { get; set; }
    [JsonProperty] public ViewQualifer ViewingQualifer { get; set; }
    public int WalkRate { get; set; }
    [Browsable(false)] [JsonIgnore] public WorldServerTimer WalkTimer { get; set; }
    [JsonProperty] public List<Position> Waypoints { get; set; }
    public ushort X { get; set; }
    public ushort Y { get; set; }

    public override string[] GetMetaData() =>
    [
        ""
    ];
}
public static class MundaneStorage
{
    public static void CacheFromDatabase(string conn, string type)
    {
        try
        {
            var sConn = new SqlConnection(conn);
            var sql = $"SELECT * FROM LegendsMundanes.dbo.{type}";

            sConn.Open();

            var cmd = new SqlCommand(sql, sConn);
            var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var temp = new MundaneTemplate();
                var image = (int)reader["Image"];
                var speech1 = reader["SpeechOne"].ToString();
                var speech2 = reader["SpeechTwo"].ToString();
                var speech3 = reader["SpeechThree"].ToString();
                var speech4 = reader["SpeechFour"].ToString();
                var speech5 = reader["SpeechFive"].ToString();
                var x = (int)reader["X"];
                var y = (int)reader["Y"];
                var direction = (int)reader["Direction"];
                var pathFind = reader["PathFinder"].ToString().ConvertTo<PathQualifer>();
                var way1 = WayPointConvert(reader["WayPointOne"].ToString());
                var way2 = WayPointConvert(reader["WayPointTwo"].ToString());
                var way3 = WayPointConvert(reader["WayPointThree"].ToString());
                var way4 = WayPointConvert(reader["WayPointFour"].ToString());
                var way5 = WayPointConvert(reader["WayPointFive"].ToString());
                for (int L = 1; L <=30; L++)
                {
                    var stock = reader["DefaultStock" + L];
                    if (stock != null)
                        temp.DefaultMerchantStock.Add(stock.ToString());
                }
                for(int M = 1; M <=30; M++)
                {
                    var roguestock = reader["RogueStock" + M];
                    if (roguestock != null)
                        temp.RogueStock.Add(roguestock.ToString());
                }

                temp.Image = (short)image;
                temp.ScriptKey = reader["ScriptKey"].ToString();
                temp.EnableWalking = (bool)reader["EnableWalking"];
                temp.EnableTurning = (bool)reader["EnableTurning"];
                if (speech1 != string.Empty)
                    temp.Speech.Add(speech1);
                if (speech2 != string.Empty)
                    temp.Speech.Add(speech2);
                if (speech3 != string.Empty)
                    temp.Speech.Add(speech3);
                if (speech4 != string.Empty)
                    temp.Speech.Add(speech4);
                if (speech5 != string.Empty)
                    temp.Speech.Add(speech5);
                temp.X = (ushort)x;
                temp.Y = (ushort)y;
                temp.AreaID = (int)reader["AreaId"];
                temp.Direction = (byte)direction;
                temp.Waypoints = [];
                if (way1 != null)
                    temp.Waypoints.Add(way1);
                if (way2 != null)
                    temp.Waypoints.Add(way2);
                if (way3 != null)
                    temp.Waypoints.Add(way3);
                if (way4 != null)
                    temp.Waypoints.Add(way4);
                if (way5 != null)
                    temp.Waypoints.Add(way5);
                temp.PathQualifer = pathFind;
                temp.Name = reader["Name"].ToString();
                if (temp.Name == null) 
                    continue;
                ServerSetup.Instance.GlobalMundaneTemplateCache[temp.Name] = temp;
            }

            reader.Close();
            sConn.Close();
        }
        catch (SqlException e)
        {
            Console.WriteLine(e.ToString());
            throw;
        }
    }

    private static Position WayPointConvert(string wayPointString)
    {
        if (wayPointString.IsNullOrEmpty()) 
            return null;
        const char delim = ',';
        var cords = wayPointString.Split(delim);
        var x = 0;
        var y = 0;

        if (cords.Length == 0) 
            return new Position(0, 0);

        foreach (var cord in cords)
        {
            if (cord == cords.FirstOrDefault())
                x = cord.ToInt();

            if (cord == cords.LastOrDefault())
                y = cord.ToInt();
        }

        return new Position(x, y);
    }
}