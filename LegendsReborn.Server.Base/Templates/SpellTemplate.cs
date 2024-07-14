using Darkages.Enums;
using Darkages.Interfaces;
using Darkages.Types;
using Legends.Server.Base.Types.Debuffs;

using Darkages.Types.Buffs;

using Microsoft.Data.SqlClient;

using ServiceStack;

namespace Darkages.Templates;

public class SpellTemplate : Template
{
     public SpellTemplate() => Text = string.Empty + "\0";

    public enum SpellUseType : byte
    {
        Unusable = 0,
        Prompt = 1,
        ChooseTarget = 2,
        FourDigit = 3,
        ThreeDigit = 4,
        NoTarget = 5,
        TwoDigit = 6,
        OneDigit = 7
    }
    //Pill Fix - Allow mundanes to show multiclass abilities
    #region multiclass
    public string MundaneName { get; set; }
    #endregion multiclass
    public ushort Animation { get; set; }
    public int BaseLines { get; set; }
    public BuffBase BuffBase { get; set; }
    public int Cooldown { get; set; } = 0;
    public double DamageExponent { get; set; }
    public DebuffBase DebuffBase { get; set; }
    public ElementManager.Element ElementalProperty { get; set; }
    public byte Icon { get; set; }
    public bool IsTrap { get; set; }
    public List<LearningPredicate> LearningRequirements { get; set; } = new();
    public double LevelRate { get; set; }
    public int ManaCost { get; set; }
    public byte MaxLevel { get; set; }
    public int MaxLines { get; set; }
    public int MinLines { get; set; }
    public string NpcKey { get; set; }
    public Pane Pane { get; set; }
    public LearningPredicate Prerequisites { get; set; }
    public string ScriptKey { get; set; }
    public byte Sound { get; set; }
    public ushort TargetAnimation { get; set; }
    public SpellUseType TargetType { get; set; }
    public string Text { get; set; }
    public Tier TierLevel { get; set; }

    public override string[] GetMetaData()
    {
        if (Prerequisites != null) 
            return Prerequisites.MetaData;

        return default;
    }
}

public static class SpellStorage
{
    public static void CacheFromDatabase(string conn)
    {
        try
        {
            var sConn = new SqlConnection(conn);
            const string sql = "SELECT * FROM LegendsAbilities.dbo.Spells";

            sConn.Open();

            var cmd = new SqlCommand(sql, sConn);
            var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var temp = new SpellTemplate();
                var icon = (int)reader["Icon"];
                var level = (int)reader["MaxLevel"];
                var pane = reader["Pane"].ConvertTo<Pane>();
                var sound = (int)reader["Sound"];
                //var post = reader["PostAttribute"].ConvertTo<PostQualifier>();
                var levelRate = (int)reader["LevelRate"];
                var predicateId = (int)reader["PredicateId"];
                var element = reader["Element"].ConvertTo<ElementManager.Element>();
                var targetAnim = (int)reader["TargetAnimation"];
                var anim = (int)reader["Animation"];
                var spellScope = reader["SpellScope"].ConvertTo<SpellTemplate.SpellUseType>();
                temp.ID = (int)reader["ID"];
                temp.Icon = (byte)icon;
                temp.ScriptKey = reader["ScriptKey"].ToString();
                temp.Pane = pane;
                temp.Cooldown = (int)reader["Cooldown"];
                temp.Sound = (byte)sound;
                temp.LevelRate = (double)levelRate;

                #region LearningPredicate

                var sConn2 = new SqlConnection(conn);
                var sql2 = $"SELECT * FROM LegendsAbilities.dbo.SpellsPrerequisites WHERE PredicateId={predicateId.ToString()}";
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

                    var primClass = reader2["Class"].ConvertTo<Class>();
                    var secClass = reader2["Displayclass"].ConvertTo<Class>();
                    var stage = reader2["Stage"].ConvertTo<ClassStage>();
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

                temp.ElementalProperty = element;
                temp.BaseLines = (int)reader["BaseLines"];
                temp.MinLines = (int)reader["MinLines"];
                temp.MaxLines = (int)reader["MaxLines"];
                temp.TargetAnimation = (ushort)targetAnim;
                temp.Animation = (ushort)anim;
                temp.MaxLevel = (byte)level;
                temp.ManaCost = (int)reader["ManaCost"];
                temp.NpcKey = reader["NpcKey"].ToString();
                temp.MundaneName = reader["MundaneName"].ToString();
                temp.TargetType = spellScope;
                temp.Description = reader["Description"].ToString();
                if (temp.Name == null) 
                    continue;

                ServerSetup.Instance.GlobalSpellTemplateCache[temp.ID.ToString()] = temp;
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