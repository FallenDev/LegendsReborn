using Darkages.Enums;
using Darkages.Types;

using Microsoft.Data.SqlClient;

using ServiceStack;

using System.Collections.ObjectModel;

namespace Darkages.Templates;

public class MonsterTemplate : Template
{
    public Collection<string> Drops = [];
    public int AreaID { get; set; }
    public int AttackSpeed { get; set; }
    public string BaseName { get; set; }
    public int CastSpeed { get; set; }
    public ElementManager.Element DefenseElement { get; set; }
    public ushort DefinedX { get; set; }
    public ushort DefinedY { get; set; }
    public ElementQualifer ElementType { get; set; }
    public int EngagedWalkingSpeed { get; set; }
    public string FamilyKey { get; set; }
    public bool IgnoreCollision { get; set; }
    public ushort Image { get; set; }
    public int ImageVarience { get; set; }
    public int Level { get; set; }
    public LootQualifer LootType { get; set; }
    public int MaximumHP { get; set; }
    public int MaximumMP { get; set; }
    public MoodQualifer MoodType { get; set; }
    public int MovementSpeed { get; set; }
    public DateTime NextAvailableSpawn { get; set; }
    public ElementManager.Element OffenseElement { get; set; }
    public PathQualifer PathQualifer { get; set; }
    public bool Ready => DateTime.UtcNow > NextAvailableSpawn;
    public string ScriptName { get; set; }
    public List<string> SkillScripts { get; set; }
    public int SpawnMax { get; set; }
    public bool SpawnOnlyOnActiveMaps { get; set; }
    public int SpawnRate { get; set; }
    public int SpawnSize { get; set; }
    public SpawnQualifer SpawnType { get; set; }
    public List<string> SpellScripts { get; set; }
    public bool UpdateMapWide { get; set; }
    public double UpdateRate { get; set; } = 1000;
    public List<Position> Waypoints { get; set; }
    //Pill Fix - Monster Stats
    #region Mob Stats
    public int Str { get; set; }
    public int Int { get; set; }
    public int Wis { get; set; }
    public int Con { get; set; }
    public int Dex { get; set; }
    public int Ac { get; set; }
    public int MagicResist { get; set; } = 0;
    public int Experience { get; set; }
    public int Hit { get; set; } = 0;
    #endregion
    public override string[] GetMetaData() =>
    [
        ""
    ];

    public bool ReadyToSpawn()
    {
        if (Ready)
        {
            NextAvailableSpawn = DateTime.UtcNow.AddSeconds(SpawnRate);
            return true;
        }

        return false;
    }
}
public static class MonsterStorage
{
    public static void CacheFromDatabase(string conn, string type)
    {
        try
        {
            var sConn = new SqlConnection(conn);
            var sql = $"SELECT * FROM LegendsMonsters.dbo.{type}";

            sConn.Open();

            var cmd = new SqlCommand(sql, sConn);
            var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var temp = new MonsterTemplate();
                var image = (int)reader["Image"];
                var x = (int)reader["DefinedX"];
                var y = (int)reader["DefinedY"];
                var eleType = reader["ElementType"].ConvertTo<ElementQualifer>();
                var pathFind = reader["PathFinder"].ConvertTo<PathQualifer>();
                var spawnType = reader["SpawnType"].ConvertTo<SpawnQualifer>();
                var mood = reader["Mood"].ConvertTo<MoodQualifer>();
                var loot = reader["LootType"].ConvertTo<LootQualifer>();
                var offEle = reader["OffenseElement"].ConvertTo<ElementManager.Element>();
                var defEle = reader["DefenseElement"].ConvertTo<ElementManager.Element>();
                for (int A = 1; A < 20; A++)
                {
                    var drop = reader["Drop" + A];
                    if (drop != null)
                        temp.Drops.Add(drop.ToString());
                }
                temp.ID = (int)reader["ID"];
                temp.ScriptName = reader["ScriptName"].ToString();
                temp.BaseName = reader["BaseName"].ToString();
                temp.Name = reader["Name"].ToString();
                temp.AreaID = (int)reader["AreaID"];
                temp.Image = (ushort)image;
                temp.ImageVarience = (int)reader["ImageVariance"];
                temp.DefinedX = (ushort)x;
                temp.DefinedY = (ushort)y;
                temp.ElementType = eleType;
                temp.PathQualifer = pathFind;
                temp.SpawnType = spawnType;
                temp.SpawnSize = (int)reader["SpawnSize"];
                temp.SpawnMax = (int)reader["SpawnMax"];
                temp.SpawnRate = (int)reader["SpawnRate"];
                temp.MoodType = mood;
                temp.IgnoreCollision = (bool)reader["IgnoreCollision"];
                temp.MovementSpeed = (int)reader["MovementSpeed"];
                temp.EngagedWalkingSpeed = (int)reader["EngagedSpeed"];
                temp.AttackSpeed = (int)reader["AttackSpeed"];
                temp.CastSpeed = (int)reader["CastSpeed"];
                temp.LootType = loot;
                temp.OffenseElement = offEle;
                temp.DefenseElement = defEle;
                temp.Ac = (int)reader["ArmorClass"];
                temp.Level = (int)reader["Level"];
                temp.Experience = (int)reader["EXP"];
                temp.MaximumHP = (int)reader["HP"];
                temp.MaximumMP = (int)reader["MP"];
                temp.Str = (int)reader["Strength"];
                temp.Int = (int)reader["Intelligence"];
                temp.Wis = (int)reader["Wisdom"];
                temp.Con = (int)reader["Constitution"];
                temp.Dex = (int)reader["Dexterity"];
                temp.Hit = (int)reader["Hit"];
                temp.MagicResist = (int)reader["MagicResist"];
                temp.SkillScripts = [];
                temp.SpellScripts = [];
                for (int L = 1; L < 15; L++)
                {
                    var skill = reader["Skill" + L];
                    if (skill != null)
                        temp.SkillScripts.Add(skill.ToString());
                }
                for (int M = 1; M < 15; M++)
                {
                    var spell = reader["Spell" + M];
                    if (spell != null)
                        temp.SpellScripts.Add(spell.ToString());
                }
                if (temp.Name == null)
                    continue;
                ServerSetup.Instance.GlobalMonsterTemplateCache[temp.ID.ToString()] = temp;
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

}