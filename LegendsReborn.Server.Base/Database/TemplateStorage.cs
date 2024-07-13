#region

using Darkages.Templates;
using Darkages.Types;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;

#endregion

namespace Darkages.Database;

public class TemplateStorage<T> where T : Template, new()
{
    private static readonly string StoragePath;
    private const string ConnectionString = "Data Source=.;Initial Catalog=Legends;Integrated Security=True;TrustServerCertificate=True";

    static TemplateStorage()
    {
        StoragePath = $@"{ServerContext.StoragePath}\templates";

        var tmp = new T();

        StoragePath = Path.Combine(StoragePath, "%");

        switch (tmp)
        {
            case WorldMapTemplate:
                StoragePath = StoragePath.Replace("%", "WorldMaps");
                break;
            case Reactor:
                StoragePath = StoragePath.Replace("%", "Reactors");
                break;
            case PopupTemplate:
                StoragePath = StoragePath.Replace("%", "Popups");
                break;
            case ServerTemplate:
                StoragePath = StoragePath.Replace("%", "ServerVars");
                break;
            case ClanTemplate:
                StoragePath = StoragePath.Replace("%", "Clans");
                break;
            case ParcelTemplate:
                StoragePath = StoragePath.Replace("%", "Parcels");
                break;

        }
        StoragePath = StoragePath.ToLower();
    }
    //Done
    public void CacheFromStorage()
    {
        var tmp = new T();

        var assetNames = Directory.GetFiles(StoragePath, "*.json",
            tmp is MonsterTemplate ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

        if (assetNames.Length == 0) 
            return;

        foreach (var obj in assetNames)
            switch (tmp)
            {

                case Reactor _:
                {
                    var template =TemplateStorage<Reactor>.Load<Reactor>(Path.GetFileNameWithoutExtension(obj));
                    if (template != null) 
                        ServerContext.GlobalReactorCache[template.Name] = template;
                    break;
                }
                case WorldMapTemplate _:
                {
                    var template =TemplateStorage<WorldMapTemplate>.Load<WorldMapTemplate>(
                        Path.GetFileNameWithoutExtension(obj));
                    if (template != null)
                        ServerContext.GlobalWorldMapTemplateCache[template.WorldIndex] = template;
                    break;
                }
                case ServerTemplate _:
                {
                    var template =
                        TemplateStorage<ServerTemplate>.Load<ServerTemplate>(
                            Path.GetFileNameWithoutExtension(obj));

                    if (template != null) 
                        ServerContext.GlobalServerVarCache[template.Name] = template;

                    break;
                }

                case PopupTemplate _:
                {
                    var template = TemplateStorage<PopupTemplate>.Load<PopupTemplate>(Path.GetFileNameWithoutExtension(obj));

                    switch (template.TypeOfTrigger)
                    {
                        case TriggerType.UserClick:
                            template = TemplateStorage<PopupTemplate>.Load<UserClickPopup>(Path.GetFileNameWithoutExtension(obj));
                            ServerContext.GlobalPopupCache.Add(template);
                            break;

                        case TriggerType.ItemDrop:
                            template = TemplateStorage<PopupTemplate>.Load<ItemDropPopup>(Path.GetFileNameWithoutExtension(obj));
                            ServerContext.GlobalPopupCache.Add(template);
                            break;

                        case TriggerType.ItemPickup:
                            template = TemplateStorage<PopupTemplate>.Load<ItemPickupPopup>(Path.GetFileNameWithoutExtension(obj));
                            ServerContext.GlobalPopupCache.Add(template);
                            break;

                        case TriggerType.MapLocation:
                            template = TemplateStorage<PopupTemplate>.Load<UserWalkPopup>(Path.GetFileNameWithoutExtension(obj));
                            ServerContext.GlobalPopupCache.Add(template);
                            break;
                    }

                    break;
                }
                case ClanTemplate _:
                {
                    var template = TemplateStorage<ClanTemplate>.Load<ClanTemplate>(Path.GetFileNameWithoutExtension(obj));
                    if (template != null)
                        ServerContext.GlobalClanTemplateCache[template.Name] = template;
                    break;
                }
                //Pill Fix - Parcels
                case ParcelTemplate _:
                {
                    var template = TemplateStorage<ParcelTemplate>.Load<ParcelTemplate>(Path.GetFileNameWithoutExtension(obj));
                    if (template != null)
                        ServerContext.GlobalParcelTemplateCache[template.Name] = template;
                    break;
                }
            }
    }

    private static TD Load<TD>(string name, string fixedPath = null) where TD : class, new()
    {
        var path = fixedPath ?? Path.Combine(StoragePath, $"{name.ToLower()}.json");

        if (!File.Exists(path)) 
            return null;

        using var openStream = File.OpenRead(path);
        using var s = File.OpenRead(path);
        using var f = new StreamReader(s);
        var content = f.ReadToEnd();
        var obj = StorageManager.Deserialize<TD>(content);
        return obj;
    }

    //public SkillTemplate Load(string name)
    //{
    //    var path = Path.Combine($@"{ServerContext.StoragePath}\Templates\Skills\", $"{name.ToLower()}");

    //    if (!File.Exists(path)) 
    //        return null;

    //    using var s = File.OpenRead(path);
    //    using var f = new StreamReader(s);
    //    var content = f.ReadToEnd();
    //    var settings = StorageManager.Settings;
    //    settings.TypeNameHandling = TypeNameHandling.None;

    //    try
    //    {
    //        var obj = JsonConvert.DeserializeObject<SkillTemplate>(content, settings);

    //        return obj;
    //    }
    //    catch (Exception ex)
    //    {
    //        ServerContext.Logger(ex.Message);
    //        ServerContext.Logger(ex.StackTrace);

    //        return null;
    //    }
    //}

    public void CacheFromDatabase(Template temp)
    {
        switch (temp)
        {
            case NationTemplate _:
                Nations(ConnectionString);
                break;
            case ItemTemplate _:
                Items(ConnectionString);
                break;
            case MonsterTemplate _:
                Monsters(ConnectionString);
                break;
            case MundaneTemplate _:
                Mundanes(ConnectionString);
                break;
            case SkillTemplate _:
                Abilities(ConnectionString, 1);
                break;
            case SpellTemplate _:
                Abilities(ConnectionString, 2);
                break;
        }
    }
    private static void Nations(string conn)
    {
        try
        {
            var sConn = new SqlConnection(conn);
            const string sql = "SELECT * FROM Legends.dbo.Nations";

            sConn.Open();

            var cmd = new SqlCommand(sql, sConn);
            var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var temp = new NationTemplate();
                temp.AreaId = (int)reader["AreaId"];
                var tempX = (int)reader["MapPositionX"];
                var tempY = (int)reader["MapPositionY"];
                var natId = (int)reader["NationId"];
                var pos = new Position { X = (ushort)tempX, Y = (ushort)tempY };
                temp.MapPosition = pos;
                temp.NationId = (byte)natId;
                temp.Name = reader["Name"].ToString();
                ServerContext.GlobalNationTemplateCache[temp.Name] = temp;
            }

            reader.Close();
            sConn.Close();
        }
        catch (SqlException e)
        {
            Console.WriteLine(e.ToString());
        }

        GameLog.Info($"Nation Templates Loaded: {ServerContext.GlobalNationTemplateCache.Count}");
    }
    private static void Items(string conn)
    {
        try
        {
            string[] dbTables  = ["Items"];
            string[] dbTables2 = ["Equipment"];

            foreach (var table in dbTables)
                ItemStorage.CacheFromDatabaseConsumables(conn, table);

            foreach (var table in dbTables2)
                ItemStorage.CacheFromDatabaseEquipment(conn, table);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
            //Crashes.TrackError(e);
        }

        GameLog.Info($"Item Templates Loaded: {ServerContext.GlobalItemTemplateCache.Count}");
    }
    //Done
    private static void Monsters(string conn)
    {
        try
        {
            string[] dbTables = ["Monsters"];

            foreach (var table in dbTables)
                MonsterStorage.CacheFromDatabase(conn, table);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
            //Crashes.TrackError(e);
        }

        GameLog.Info($"Monster Templates Loaded: {ServerContext.GlobalMonsterTemplateCache.Count}");
    }
    //Done
    private static void Mundanes(string conn)
    {
        try
        {
            string[] dbTables = ["Mundanes"];

            foreach (var table in dbTables)
                MundaneStorage.CacheFromDatabase(conn, table);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }

        GameLog.Info($"Mundane Templates Loaded: {ServerContext.GlobalMundaneTemplateCache.Count}");
    }
    //Done
    private static void Abilities(string conn, int num)
    {
        try
        {
            if (num == 1)
            {
                SkillStorage.CacheFromDatabase(conn);
                GameLog.Info($"Skill Templates Loaded: {ServerContext.GlobalSkillTemplateCache.Count}");
            }
            else
            {
                SpellStorage.CacheFromDatabase(conn);
                GameLog.Info($"Spell Templates Loaded: {ServerContext.GlobalSpellTemplateCache.Count}");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }
              
    public FileInfo MakeUnique(string path)
    {
        var dir = Path.GetDirectoryName(path);
        var fileName = Path.GetFileNameWithoutExtension(path);
        var fileExt = Path.GetExtension(path);

        for (var i = 1;; ++i)
        {
            if (!File.Exists(path))
                return new FileInfo(path);

            if (dir != null)
                path = Path.Combine(dir, fileName + " " + i + fileExt);
        }
    }
        
    public void SaveParcel(T obj, string sender, string recipient, string item, DateTime date, bool replace = false)
    {
        if (replace)
        {
            String s = $"{sender.ToLower()}_{recipient.ToLower()}_{item.ToLower()}_{date}.json";
            s = s.Replace("/", "_").Replace(" ", "_").Replace(":", "_");
            var path = Path.Combine(StoragePath, s);
                
            if (File.Exists(path))
                File.Delete(path);

            var objString = JsonConvert.SerializeObject(obj, Formatting.Indented, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All
            });
            File.WriteAllText(path, objString);
        }
        else
        {
            String s = $"{sender.ToLower()}_{recipient.ToLower()}_{item.ToLower()}_{date}.json";
            s = s.Replace("/", "_").Replace(" ", "_").Replace(":", "_");
            var path = MakeUnique(Path.Combine(StoragePath, s))
                .FullName;

            var objString = JsonConvert.SerializeObject(obj, Formatting.Indented, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All
            });
            File.WriteAllText(path, objString);
        }
    }
    public void SaveClan(T obj, bool replace = false)
    {
        if (replace)
        {
            var path = Path.Combine(StoragePath, $"{obj.Name.ToLower()}.json");

            if (File.Exists(path))
                File.Delete(path);

            var objString = JsonConvert.SerializeObject(obj, Formatting.Indented, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All
            });
            File.WriteAllText(path, objString);
        }
        else
        {
            var path = MakeUnique(Path.Combine(StoragePath, $"{obj.Name.ToLower()}.json"))
                .FullName;

            var objString = JsonConvert.SerializeObject(obj, Formatting.Indented, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All
            });
            File.WriteAllText(path, objString);
        }
    }


}