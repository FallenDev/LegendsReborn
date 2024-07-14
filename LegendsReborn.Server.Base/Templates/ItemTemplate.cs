using Darkages.Enums;
using Microsoft.Data.SqlClient;
using Darkages.CommandSystem.Loot.Interfaces;
using Darkages.Types;
using Gender = Darkages.Enums.Gender;

namespace Darkages.Templates;

public class ItemTemplate : Template, ILootDefinition
{
    public int Acmodifier { get; set; }
    public bool CanStack { get; set; }
    public byte CarryWeight { get; set; }
    public Class Class { get; set; }
    public ItemColor Color { get; set; }
    public int Conmodifier { get; set; }
    public ElementManager.Element DefenseElement { get; set; }
    public int Dexmodifier { get; set; }
    public ushort DisplayImage { get; set; }
    public int DmgMax { get; set; }
    public int DmgMin { get; set; }
    public int Dmgmodifier { get; set; }
    public decimal DropRate { get; set; }
    public bool Enchantable { get; set; }
    public int EquipmentSlot { get; set; }
    public EquipSlot EquipSlot { get; set; }
    public ItemFlags Flags { get; set; }
    public Gender Gender { get; set; }
    public bool HasPants { get; set; }
    public int Healthmodifier { get; set; }
    public int Hitmodifier { get; set; }
    public ushort Image { get; set; }
    public int Intmodifier { get; set; }
    public byte LevelRequired { get; set; }
    public int Manamodifier { get; set; }
    public int MaxDurability { get; set; }
    public byte MaxStack { get; set; }
    public int Mrmodifier { get; set; }
    public string NpcKey { get; set; }
    public ElementManager.Element OffenseElement { get; set; }
    public int Regenmodifier { get; set; }
    public string ScriptName { get; set; }
    //Spell Line Reduction Value
    public int SpellLineValue { get; set; }
    //Spell Line Reduction Type
    public int SpellType { get; set; }
    public int SpellMinValue { get; set; }
    public int SpellMaxValue { get; set; }
    public int SpellSetValue { get; set; }
    public ClassStage StageRequired { get; set; }
    public int Strmodifier { get; set; }
    public uint Value { get; set; }
    public uint GPValue { get; set; }
    public string WeaponScript { get; set; }
    public decimal Weight
    {
        get => DropRate;
        set { }
    }
    public int Wismodifier { get; set; }


    public override string[] GetMetaData()
    {
        var category = string.IsNullOrEmpty(Group) ? string.Empty : Group;

        if (string.IsNullOrEmpty(category))
            category = Class == Class.Peasant ? "All" : Class.ToString();

        return
        [
            LevelRequired.ToString(),
            ((int) Class).ToString(),
            CarryWeight.ToString(),

            Gender switch
            {
                Gender.Unisex => category,
                Gender.Female => category,
                Gender.Male => category,
                _ => throw new ArgumentOutOfRangeException()
            },

            Gender switch
            {

                Gender.Unisex => category,
                Gender.Female => category,
                Gender.Male => category,
                _ => throw new ArgumentOutOfRangeException()
            } + $" {Gender}\n{Description}"
        ];
    }
}
public static class ItemStorage
{
    public static void CacheFromDatabaseEquipment(string conn, string type)
    {
        try
        {
            var sConn = new SqlConnection(conn);
            var sql = $"SELECT * FROM Legends.dbo.{type}";

            sConn.Open();

            var cmd = new SqlCommand(sql, sConn);
            var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var id = Convert.ToInt32(reader["ID"]);
                var temp = new ItemTemplate();
                var image = (int)reader["Image"];
                var disImage = (int)reader["DisplayImage"];
                var flags = ServiceStack.AutoMappingUtils.ConvertTo<ItemFlags>(reader["Flags"]);
                var gender = ServiceStack.AutoMappingUtils.ConvertTo<Gender>(reader["Gender"]);
                var offEle = ServiceStack.AutoMappingUtils.ConvertTo<ElementManager.Element>(reader["OffenseElement"]);
                var defEle = ServiceStack.AutoMappingUtils.ConvertTo<ElementManager.Element>(reader["DefenseElement"]);
                var weight = (int)reader["CarryWeight"];
                var maxDura = (int)reader["MaxDurability"];
                var value = (int)reader["Value"];
                var gpValue = (int)reader["GPValue"];
                var itemClass = ServiceStack.AutoMappingUtils.ConvertTo<Class>(reader["Class"]);
                var level = (int)reader["LevelRequired"];
                var drop = (decimal)reader["DropRate"];
                var drweight = (decimal)reader["DropWeight"];
                var classStage = ServiceStack.AutoMappingUtils.ConvertTo<ClassStage>(reader["StageRequired"]);
                var color = ServiceStack.AutoMappingUtils.ConvertTo<ItemColor>(reader["Color"]);
                //var spellpolicy = ServiceStack.AutoMappingUtils.ConvertTo<SpellOperator.SpellOperatorPolicy>(reader["SpellPolicy"]);
                //var spellscope = ServiceStack.AutoMappingUtils.ConvertTo<SpellOperator.SpellOperatorScope>(reader["SpellScope"]);

                temp.ID = id;
                temp.Name = reader["Name"].ToString();
                temp.Image = (ushort)image;
                temp.DisplayImage = (ushort)disImage;
                temp.Flags = flags;
                temp.Gender = gender;
                temp.OffenseElement = offEle;
                temp.DefenseElement = defEle;
                temp.CarryWeight = (byte)weight;
                temp.MaxDurability = (int)maxDura;
                temp.Value = (uint)value;
                temp.GPValue = (uint)gpValue;
                temp.Class = itemClass;
                temp.LevelRequired = (byte)level;
                temp.DropRate = drop;
                temp.StageRequired = classStage;
                temp.Color = color;
                temp.ScriptName = reader["ScriptName"].ToString();
                temp.Enchantable = (bool)reader["Enchantable"];
                temp.EquipmentSlot = (int)reader["EquipmentSlot"];
                temp.NpcKey = reader["NpcKey"].ToString();
                temp.DmgMin = (int)reader["DmgMin"];
                temp.DmgMax = (int)reader["DmgMax"];
                temp.HasPants = (bool)reader["HasPants"];
                temp.WeaponScript = reader["WeaponScript"].ToString();
                temp.Group = reader["Category"].ToString();
                temp.Healthmodifier = (int)reader["Vitality"];
                temp.Manamodifier = (int)reader["Mana"];
                temp.Acmodifier = (int)reader["ArmorClass"];
                temp.Strmodifier = (int)reader["Strength"];
                temp.Intmodifier = (int)reader["Intelligence"];
                temp.Wismodifier = (int)reader["Wisdom"];
                temp.Conmodifier = (int)reader["Constitution"];
                temp.Dexmodifier = (int)reader["Dexterity"];
                temp.Mrmodifier = (int)reader["MagicResistance"];
                temp.Hitmodifier = (int)reader["Hit"];
                temp.Dmgmodifier = (int)reader["Dmg"];
                temp.Regenmodifier = (int)reader["Regen"];
                temp.Weight = drweight;
                temp.SpellType = (int)reader["SpellType"];
                temp.SpellLineValue = (int)reader["SpellLineValue"];
                temp.SpellMinValue = (int)reader["SpellMinValue"];
                temp.SpellMaxValue = (int)reader["SpellMaxValue"];
                temp.SpellSetValue = (int)reader["SpellSetValue"];
                temp.Description = reader["Description"].ToString();

                if (temp.Name == null) 
                    continue;
                ServerSetup.Instance.GlobalItemTemplateCache[temp.Name] = temp;
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

    public static void CacheFromDatabaseConsumables(string conn, string type)
    {
        try
        {
            var sConn = new SqlConnection(conn);
            var sql = $"SELECT * FROM Legends.dbo.{type}";

            sConn.Open();

            var cmd = new SqlCommand(sql, sConn);
            var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var temp = new ItemTemplate();
                var max = (int)reader["MaxStack"];
                var image = (int)reader["Image"];
                var disImage = (int)reader["DisplayImage"];
                var gpValue = (int)reader["GPValue"];
                var flags = ServiceStack.AutoMappingUtils.ConvertTo<ItemFlags>(reader["Flags"]);
                var weight = (int)reader["CarryWeight"];
                var worth = (int)reader["Worth"];
                var itemClass = ServiceStack.AutoMappingUtils.ConvertTo<Class>(reader["Class"]);
                var level = (int)reader["LevelRequired"];
                var drop = (decimal)reader["DropRate"];
                var classStage = ServiceStack.AutoMappingUtils.ConvertTo<ClassStage>(reader["Stage"]);
                var color = ServiceStack.AutoMappingUtils.ConvertTo<ItemColor>(reader["Color"]);
                var gender = ServiceStack.AutoMappingUtils.ConvertTo<Gender>(reader["Gender"]);
                temp.CanStack = (bool)reader["CanStack"];
                temp.MaxStack = (byte)max;
                temp.Image = (ushort)image;
                temp.DisplayImage = (ushort)disImage;
                temp.ScriptName = reader["ScriptName"].ToString();
                temp.Flags = flags;
                temp.CarryWeight = (byte)weight;
                temp.Value = (uint)worth;
                temp.NpcKey = reader["NpcKey"].ToString();
                temp.Class = itemClass;
                temp.LevelRequired = (byte)level;
                temp.DropRate = drop;
                temp.StageRequired = classStage;
                temp.Color = color;
                temp.Name = reader["Name"].ToString();
                temp.Group = reader["Group"].ToString();
                temp.GPValue = (uint)gpValue;
                temp.Gender = gender;
                if (reader["Description"] != null)
                    temp.Description = reader["Description"].ToString();

                if (temp.Name == null) 
                    continue;
                ServerSetup.Instance.GlobalItemTemplateCache[temp.Name] = temp;
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