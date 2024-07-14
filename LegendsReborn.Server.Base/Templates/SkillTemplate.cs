using Chaos.Common.Definitions;
using Darkages.Enums;
using Darkages.Interfaces;
using Darkages.Types;
using Legends.Server.Base.Types.Debuffs;

using Darkages.Types.Buffs;

using Microsoft.Data.SqlClient;

using ServiceStack;

namespace Darkages.Templates;

public class SkillTemplate : Template
{
    public BuffBase BuffBase { get; set; }
    public int Cooldown { get; set; }
    public DebuffBase DebuffBase { get; set; }
    public string FailMessage { get; set; }
    public byte Icon { get; set; }
    //Pill Fix - Allow mundanes to show multiclass abilities
    #region multiclass
    public string MundaneName { get; set; }
    #endregion multiclass
    public List<LearningPredicate> LearningRequirements { get; set; } = new();
    public double LevelRate { get; set; }
    public int MaxLevel { get; set; }
    public ushort MissAnimation { get; set; }
    public string NpcKey { get; set; }
    public Pane Pane { get; set; }
    public PostQualifier PostQualifers { get; set; }
    public LearningPredicate Prerequisites { get; set; }
    public string ScriptName { get; set; }
    public byte Sound { get; set; }
    public ushort TargetAnimation { get; set; }
    public Tier TierLevel { get; set; }
    public SkillScope Type { get; set; }

    public override string[] GetMetaData() => Prerequisites?.MetaData;
}
public static class SkillStorage
{
    public static void CacheFromDatabase(string conn)
    {
        try
        {
            var sConn = new SqlConnection(conn);
            const string sql = "SELECT * FROM LegendsAbilities.dbo.Skills";

            sConn.Open();

            var cmd = new SqlCommand(sql, sConn);
            var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var temp = new SkillTemplate();
                var icon = (int)reader["Icon"];
                var pane = reader["Pane"].ToString().ConvertTo<Pane>();
                var sound = (int)reader["Sound"];
                var post = reader["Scope"].ToString().ConvertTo<PostQualifier>();
                var levelRate = (int)reader["LevelRate"];
                var predicateId = (int)reader["PredicateId"];
                var targetAnim = (int)reader["TargetAnimation"];
                var missAnim = (int)reader["MissAnimation"];
                var skillType = reader["Type"].ToString().ConvertTo<SkillScope>();

                temp.Icon = (byte)icon;
                temp.ScriptName = reader["ScriptKey"].ToString();
                temp.Pane = pane;
                temp.Cooldown = (int)reader["Cooldown"];
                temp.Sound = (byte)sound;
                temp.PostQualifers = post;
                temp.Type = skillType;
                temp.LevelRate = (double)levelRate;
                    

                #region LearningPredicate

                var sConn2 = new SqlConnection(conn);
                var sql2 = $"SELECT * FROM LegendsAbilities.dbo.SkillsPrerequisites WHERE PredicateId={predicateId.ToString()}";
                sConn2.Open();
                var cmd2 = new SqlCommand(sql2, sConn2);
                var reader2 = cmd2.ExecuteReader();

                while (reader2.Read())
                {
                    var learning = new LearningPredicate();
                    var item1 = new ItemPredicate
                    {
                        Item = reader2["Item1Name"].ToString(),
                        AmountRequired = (int)reader2["Item1Qty"]
                    };
                    var item2 = new ItemPredicate
                    {
                        Item = reader2["Item2Name"].ToString(),
                        AmountRequired = (int)reader2["Item2Qty"]
                    };
                    var item3 = new ItemPredicate
                    {
                        Item = reader2["Item3Name"].ToString(),
                        AmountRequired = (int)reader2["Item3Qty"]
                    };
                    var item4 = new ItemPredicate
                    {
                        Item = reader2["Item4Name"].ToString(),
                        AmountRequired = (int)reader2["Item4Qty"]
                    };
                    var item5 = new ItemPredicate
                    {
                        Item = reader2["Item5Name"].ToString(),
                        AmountRequired = (int)reader2["Item5Qty"]
                    };

                    var itemList = new List<ItemPredicate>();

                    if ((item1.Item != null) && (item1.AmountRequired > 0))
                        itemList.Add(item1);
                    if ((item2.Item != null) && (item2.AmountRequired > 0))
                        itemList.Add(item2);
                    if ((item3.Item != null) && (item3.AmountRequired > 0))
                        itemList.Add(item3);
                    if ((item4.Item != null) && (item4.AmountRequired > 0))
                        itemList.Add(item4);
                    if ((item5.Item != null) && (item5.AmountRequired > 0))
                        itemList.Add(item5);

                    var primClass = reader2["Class"].ToString().ConvertTo<Class>();
                    var secClass = reader2["DisplayClass"].ToString().ConvertTo<Class>();
                    var stage = reader2["Stage"].ToString().ConvertTo<ClassStage>();
                    temp.Prerequisites = learning;
                    temp.Prerequisites.Items_Required = itemList;
                    temp.Prerequisites.DisplayName = reader2["DisplayName"].ToString();
                    temp.Name = temp.Prerequisites.DisplayName;
                    temp.Prerequisites.Class_Required = primClass;
                    temp.Prerequisites.Displayclass_Required = secClass;
                    temp.Prerequisites.Str_Required = (int)reader2["Strength"];
                    temp.Prerequisites.Int_Required = (int)reader2["Intelligence"];
                    temp.Prerequisites.Wis_Required = (int)reader2["Wisdom"];
                    temp.Prerequisites.Con_Required = (int)reader2["Constitution"];
                    temp.Prerequisites.Dex_Required = (int)reader2["Dexterity"];
                    temp.Prerequisites.ExpLevel_Required = (int)reader2["Level"];
                    temp.Prerequisites.Gold_Required = (int)reader2["Gold"];
                    temp.Prerequisites.First_Ability_Level_Required = (int)reader2["FirstAbilityLevel"];
                    temp.Prerequisites.First_Ability_Required = reader2["FirstAbilityRequired"].ToString();
                    temp.Prerequisites.Second_Ability_Level_Required = (int)reader2["SecondAbilityLevel"];
                    temp.Prerequisites.Second_Ability_Required = reader2["SecondAbilityRequired"].ToString();
                    temp.Prerequisites.Stage_Required = stage;
                }

                reader2.Close();
                sConn2.Close();

                #endregion

                temp.TargetAnimation = (ushort)targetAnim;
                temp.MissAnimation = (ushort)missAnim;
                temp.MaxLevel = (int)reader["MaxLevel"];
                temp.Description = reader["Description"].ToString();
                temp.FailMessage = reader["FailMsg"].ToString();
                temp.NpcKey = reader["NpcKey"].ToString();
                temp.MundaneName = reader["MundaneName"].ToString();
                if (temp.Name == null) 
                    continue;
                ServerSetup.Instance.GlobalSkillTemplateCache[temp.Name] = temp;
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