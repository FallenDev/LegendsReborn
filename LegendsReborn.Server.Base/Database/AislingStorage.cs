using Chaos.Common.Identity;

using Dapper;

using Darkages.Enums;
using Darkages.Interfaces;
using Darkages.Sprites;
using Microsoft.Data.SqlClient;
using System.Data;
using Darkages.Models;
using Darkages.Templates;
using Chaos.Common.Synchronization;
using Microsoft.Extensions.Logging;

namespace Darkages.Database;

public record AislingStorage : Sql, IAislingStorage
{
    public const string ConnectionString = "Data Source=.;Initial Catalog=ZolianPlayers;Integrated Security=True;Encrypt=False;MultipleActiveResultSets=True;";
    public const string PersonalMailString = "Data Source=.;Initial Catalog=ZolianBoardsMail;Integrated Security=True;Encrypt=False;MultipleActiveResultSets=True;";
    private const string EncryptedConnectionString = "Data Source=.;Initial Catalog=ZolianPlayers;Integrated Security=True;Column Encryption Setting=enabled;TrustServerCertificate=True;MultipleActiveResultSets=True;";
    public FifoAutoReleasingSemaphoreSlim SaveLock { get; } = new(1, 1);
    private FifoAutoReleasingSemaphoreSlim BuffDebuffSaveLock { get; } = new(1, 1);
    private FifoAutoReleasingSemaphoreSlim PasswordSaveLock { get; } = new(1, 1);
    private FifoAutoReleasingSemaphoreSlim LoadLock { get; } = new(1, 1);
    private FifoAutoReleasingSemaphoreSlim CreateLock { get; } = new(1, 1);

    public async Task<Aisling> LoadAisling(string name, long serial)
    {
        await LoadLock.WaitAsync(TimeSpan.FromSeconds(5));
        var aisling = new Aisling();

        try
        {
            var continueLoad = await CheckIfPlayerExists(name, serial);
            if (!continueLoad) return null;

            var sConn = ConnectToDatabase(ConnectionString);
            var values = new { Name = name };
            aisling = await sConn.QueryFirstAsync<Aisling>("[SelectPlayer]", values, commandType: CommandType.StoredProcedure);
            sConn.Close();
        }
        catch (Exception e)
        {
            ServerSetup.ConnectionLogger(e.ToString());
        }
        finally
        {
            LoadLock.Release();
        }

        return aisling;
    }

    public async Task<bool> PasswordSave(Aisling obj)
    {
        if (obj == null) return false;
        if (obj.Loading) return false;
        var continueLoad = await CheckIfPlayerExists(obj.Username, obj.Serial);
        if (!continueLoad) return false;

        await PasswordSaveLock.WaitAsync(TimeSpan.FromSeconds(5));

        try
        {
            var connection = ConnectToDatabase(EncryptedConnectionString);
            var cmd = ConnectToDatabaseSqlCommandWithProcedure("PasswordSave", connection);
            cmd.Parameters.Add("@Name", SqlDbType.VarChar).Value = obj.Username;
            cmd.Parameters.Add("@Pass", SqlDbType.VarChar).Value = obj.Password;
            cmd.Parameters.Add("@Attempts", SqlDbType.Int).Value = obj.PasswordAttempts;
            cmd.Parameters.Add("@Hacked", SqlDbType.Bit).Value = obj.Hacked;
            cmd.Parameters.Add("@LastIP", SqlDbType.VarChar).Value = obj.LastIP;
            cmd.Parameters.Add("@LastAttemptIP", SqlDbType.VarChar).Value = obj.LastAttemptIP;
            ExecuteAndCloseConnection(cmd, connection);
        }
        catch (Exception e)
        {
            ServerSetup.ConnectionLogger(e.ToString());
        }
        finally
        {
            PasswordSaveLock.Release();
        }

        return true;
    }

    public async Task AuxiliarySave(Aisling obj)
    {
        if (obj == null) return;
        if (obj.Loading) return;

        await BuffDebuffSaveLock.WaitAsync(TimeSpan.FromSeconds(5));

        try
        {
            var connection = ConnectToDatabase(ConnectionString);
            SaveBuffs(obj, connection);
            SaveDebuffs(obj, connection);
            connection.Close();
        }
        catch (Exception e)
        {
            ServerSetup.ConnectionLogger(e.ToString());
        }
        finally
        {
            BuffDebuffSaveLock.Release();
        }
    }

    /// <summary>
    /// Saves a player's state on disconnect or error
    /// Creates a new DB connection on event
    /// </summary>
    public async Task<bool> Save(Aisling obj)
    {
        if (obj == null) return false;
        if (obj.Loading) return false;

        var dt = PlayerDataTable();
        var qDt = QuestDataTable();
        var iDt = ItemsDataTable();
        var skillDt = SkillDataTable();
        var spellDt = SpellDataTable();
        var connection = ConnectToDatabase(ConnectionString);

        try
        {
            var itemList = obj.Inventory.Items.Values.Where(i => i is not null).ToList();
            itemList.AddRange(from item in obj.EquipmentManager.Equipment.Values.Where(i => i is not null) where item.Item != null select item.Item);
            itemList.AddRange(obj.BankManager.Items.Values.Where(i => i is not null));
            var skillList = obj.SkillBook.Skills.Values.Where(i => i is { SkillName: not null }).ToList();
            var spellList = obj.SpellBook.Spells.Values.Where(i => i is { SpellName: not null }).ToList();

            dt.Rows.Add(obj.Serial, obj.Created, obj.Username, obj.LoggedIn, obj.LastLogged, obj.X, obj.Y, obj.CurrentMapId,
                obj.Direction, obj.CurrentHp, obj.BaseHp, obj.CurrentMp, obj.BaseMp, obj._ac,
                obj._Regen, obj._Dmg, obj._Hit, obj._Mr, obj._Str, obj._Int, obj._Wis, obj._Con, obj._Dex, obj._Luck, obj.AbpLevel,
                obj.AbpNext, obj.AbpTotal, obj.ExpLevel, obj.ExpNext, obj.ExpTotal, obj.Stage.ToString(), obj.JobClass.ToString(),
                obj.Path.ToString(), obj.PastClass.ToString(), obj.Gender.ToString(), obj.HairColor, obj.HairStyle, obj.NameColor, 
                obj.ProfileMessage, obj.Nation, obj.Clan, obj.ClanRank, obj.ClanTitle, obj.MonsterForm, obj.ActiveStatus.ToString(), 
                obj.Flags.ToString(), obj.CurrentWeight, obj.World, obj.Lantern, obj.IsInvisible, obj.Resting.ToString(), 
                obj.PartyStatus.ToString(), obj.GameMaster, obj.ArenaHost, obj.Knight, obj.GoldPoints, obj.StatPoints, obj.GamePoints, 
                obj.BankedGold, obj.ArmorImg, obj.HelmetImg, obj.ShieldImg, obj.WeaponImg, obj.BootsImg, obj.HeadAccessoryImg, 
                obj.Accessory1Img, obj.Accessory2Img, obj.Accessory3Img, obj.Accessory1Color,
                obj.Accessory2Color, obj.Accessory3Color, obj.BodyColor, obj.BodySprite,
                obj.FaceSprite, obj.OverCoatImg, obj.BootColor, obj.OverCoatColor, obj.Pants);

            if (obj.QuestManager == null) return false;
            qDt.Rows.Add(obj.Serial, obj.QuestManager.MailBoxNumber);
            
            foreach (var item in itemList)
            {
                var pane = ItemEnumConverters.PaneToString(item.ItemPane);
                var color = ItemColors.ItemColorsToInt(item.Template.Color);
                var quality = ItemEnumConverters.QualityToString(item.ItemQuality);
                var orgQuality = ItemEnumConverters.QualityToString(item.OriginalQuality);
                var itemVariance = ItemEnumConverters.ArmorVarianceToString(item.ItemVariance);
                var weapVariance = ItemEnumConverters.WeaponVarianceToString(item.WeapVariance);
                var gearEnhanced = ItemEnumConverters.GearEnhancementToString(item.GearEnhancement);
                var itemMaterial = ItemEnumConverters.ItemMaterialToString(item.ItemMaterial);
                var existingRow = iDt.AsEnumerable().FirstOrDefault(row => row.Field<long>("ItemId") == item.ItemId);

                // Check for duplicated ItemIds -- If an ID exists, this will overwrite it
                if (existingRow != null)
                {
                    existingRow["Name"] = item.Template.Name;
                    existingRow["Serial"] = (long)obj.Serial;
                    existingRow["ItemPane"] = pane;
                    existingRow["Slot"] = item.Slot;
                    existingRow["InventorySlot"] = item.InventorySlot;
                    existingRow["Color"] = color;
                    existingRow["Cursed"] = item.Cursed;
                    existingRow["Durability"] = item.Durability;
                    existingRow["Identified"] = item.Identified;
                    existingRow["ItemVariance"] = itemVariance;
                    existingRow["WeapVariance"] = weapVariance;
                    existingRow["ItemQuality"] = quality;
                    existingRow["OriginalQuality"] = orgQuality;
                    existingRow["Stacks"] = item.Stacks;
                    existingRow["Enchantable"] = item.Enchantable;
                    existingRow["Tarnished"] = item.Tarnished;
                    existingRow["GearEnhancement"] = gearEnhanced;
                    existingRow["ItemMaterial"] = itemMaterial;
                }
                else
                {
                    // If the item hasn't already been added to the data table, add it
                    iDt.Rows.Add(
                        item.ItemId,
                    item.Template.Name,
                        (long)obj.Serial,
                        pane,
                        item.Slot,
                        item.InventorySlot,
                        color,
                        item.Cursed,
                        item.Durability,
                        item.Identified,
                        itemVariance,
                        weapVariance,
                        quality,
                        orgQuality,
                        item.Stacks,
                        item.Enchantable,
                        item.Tarnished,
                        gearEnhanced,
                        itemMaterial
                    );
                }
            }

            foreach (var skill in skillList)
            {
                skillDt.Rows.Add(
                    (long)obj.Serial,
                    skill.Level,
                    skill.Slot,
                    skill.SkillName,
                    skill.Uses,
                    skill.CurrentCooldown
                );
            }

            foreach (var spell in spellList)
            {
                spellDt.Rows.Add(
                    (long)obj.Serial,
                    spell.Level,
                    spell.Slot,
                    spell.SpellName,
                    spell.Casts,
                    spell.CurrentCooldown
                );
            }

            using (var cmd = new SqlCommand("PlayerSave", connection))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                var param = cmd.Parameters.AddWithValue("@Players", dt);
                param.SqlDbType = SqlDbType.Structured;
                param.TypeName = "dbo.PlayerType";
                cmd.ExecuteNonQuery();
            }

            using (var cmd2 = new SqlCommand("PlayerQuestSave", connection))
            {
                cmd2.CommandType = CommandType.StoredProcedure;
                var param2 = cmd2.Parameters.AddWithValue("@Quests", qDt);
                param2.SqlDbType = SqlDbType.Structured;
                param2.TypeName = "dbo.QuestType";
                cmd2.ExecuteNonQuery();
            }

            using (var cmd4 = new SqlCommand("ItemUpsert", connection))
            {
                cmd4.CommandType = CommandType.StoredProcedure;
                var param4 = cmd4.Parameters.AddWithValue("@Items", iDt);
                param4.SqlDbType = SqlDbType.Structured;
                param4.TypeName = "dbo.ItemType";
                cmd4.ExecuteNonQuery();
            }

            using (var cmd5 = new SqlCommand("PlayerSaveSkills", connection))
            {
                cmd5.CommandType = CommandType.StoredProcedure;
                var param5 = cmd5.Parameters.AddWithValue("@Skills", skillDt);
                param5.SqlDbType = SqlDbType.Structured;
                param5.TypeName = "dbo.SkillType";
                cmd5.ExecuteNonQuery();
            }

            using (var cmd6 = new SqlCommand("PlayerSaveSpells", connection))
            {
                cmd6.CommandType = CommandType.StoredProcedure;
                var param6 = cmd6.Parameters.AddWithValue("@Spells", spellDt);
                param6.SqlDbType = SqlDbType.Structured;
                param6.TypeName = "dbo.SpellType";
                cmd6.ExecuteNonQuery();
            }

            connection.Close();
        }
        catch (Exception e)
        {
            ServerSetup.ConnectionLogger(e.ToString());
        }

        return true;
    }

    /// <summary>
    /// Saves all players states
    /// Utilizes an active connection that self-heals if closed
    /// </summary>
    public async Task<bool> ServerSave(List<Aisling> playerList)
    {
        if (playerList.Count == 0) return false;
        await SaveLock.WaitAsync(TimeSpan.FromSeconds(5));

        var dt = PlayerDataTable();
        var qDt = QuestDataTable();
        var iDt = ItemsDataTable();
        var skillDt = SkillDataTable();
        var spellDt = SpellDataTable();
        var connection = ServerSetup.Instance.ServerSaveConnection;

        try
        {
            foreach (var player in playerList.Where(player => !player.Loading))
            {
                if (player?.Client == null) continue;
                player.Client.LastSave = DateTime.UtcNow;
                var itemList = player.Inventory.Items.Values.Where(i => i is not null).ToList();
                itemList.AddRange(from item in player.EquipmentManager.Equipment.Values.Where(i => i is not null) where item.Item != null select item.Item);
                itemList.AddRange(player.BankManager.Items.Values.Where(i => i is not null));
                var skillList = player.SkillBook.Skills.Values.Where(i => i is { SkillName: not null }).ToList();
                var spellList = player.SpellBook.Spells.Values.Where(i => i is { SpellName: not null }).ToList();

                dt.Rows.Add(player.Serial, player.Created, player.Username, player.LoggedIn, player.LastLogged, player.X, player.Y, player.CurrentMapId,
                    player.Direction, player.CurrentHp, player.BaseHp, player.CurrentMp, player.BaseMp, player._ac,
                    player._Regen, player._Dmg, player._Hit, player._Mr, player._Str, player._Int, player._Wis, player._Con, player._Dex, player._Luck, player.AbpLevel,
                    player.AbpNext, player.AbpTotal, player.ExpLevel, player.ExpNext, player.ExpTotal, player.Stage.ToString(), player.JobClass.ToString(),
                    player.Path.ToString(), player.PastClass.ToString(), player.Gender.ToString(), player.HairColor, player.HairStyle, player.NameColor, 
                    player.ProfileMessage, player.Nation, player.Clan, player.ClanRank, player.ClanTitle,
                    player.MonsterForm, player.ActiveStatus.ToString(), player.Flags.ToString(), player.CurrentWeight, player.World,
                    player.Lantern, player.IsInvisible, player.Resting.ToString(), player.PartyStatus.ToString(), player.GameMaster, player.ArenaHost, player.Knight,
                    player.GoldPoints, player.StatPoints, player.GamePoints, player.BankedGold, player.ArmorImg, player.HelmetImg, player.ShieldImg, player.WeaponImg,
                    player.BootsImg, player.HeadAccessoryImg, player.Accessory1Img, player.Accessory2Img, player.Accessory3Img, player.Accessory1Color, 
                    player.Accessory2Color, player.Accessory3Color, player.BodyColor, player.BodySprite,
                    player.FaceSprite, player.OverCoatImg, player.BootColor, player.OverCoatColor, player.Pants);

                qDt.Rows.Add(player.Serial, player.QuestManager.MailBoxNumber);
                
                foreach (var item in itemList)
                {
                    var pane = ItemEnumConverters.PaneToString(item.ItemPane);
                    var color = ItemColors.ItemColorsToInt(item.Template.Color);
                    var quality = ItemEnumConverters.QualityToString(item.ItemQuality);
                    var orgQuality = ItemEnumConverters.QualityToString(item.OriginalQuality);
                    var itemVariance = ItemEnumConverters.ArmorVarianceToString(item.ItemVariance);
                    var weapVariance = ItemEnumConverters.WeaponVarianceToString(item.WeapVariance);
                    var gearEnhanced = ItemEnumConverters.GearEnhancementToString(item.GearEnhancement);
                    var itemMaterial = ItemEnumConverters.ItemMaterialToString(item.ItemMaterial);
                    var existingRow = iDt.AsEnumerable().FirstOrDefault(row => row.Field<long>("ItemId") == item.ItemId);

                    // Check for duplicated ItemIds -- If an ID exists, this will overwrite it
                    if (existingRow != null)
                    {
                        existingRow["Name"] = item.Template.Name;
                        existingRow["Serial"] = (long)player.Serial;
                        existingRow["ItemPane"] = pane;
                        existingRow["Slot"] = item.Slot;
                        existingRow["InventorySlot"] = item.InventorySlot;
                        existingRow["Color"] = color;
                        existingRow["Cursed"] = item.Cursed;
                        existingRow["Durability"] = item.Durability;
                        existingRow["Identified"] = item.Identified;
                        existingRow["ItemVariance"] = itemVariance;
                        existingRow["WeapVariance"] = weapVariance;
                        existingRow["ItemQuality"] = quality;
                        existingRow["OriginalQuality"] = orgQuality;
                        existingRow["Stacks"] = item.Stacks;
                        existingRow["Enchantable"] = item.Enchantable;
                        existingRow["Tarnished"] = item.Tarnished;
                        existingRow["GearEnhancement"] = gearEnhanced;
                        existingRow["ItemMaterial"] = itemMaterial;
                    }
                    else
                    {
                        // If the item hasn't already been added to the data table, add it
                        iDt.Rows.Add(
                            item.ItemId,
                            item.Template.Name,
                            (long)player.Serial,
                            pane,
                            item.Slot,
                            item.InventorySlot,
                            color,
                            item.Cursed,
                            item.Durability,
                            item.Identified,
                            itemVariance,
                            weapVariance,
                            quality,
                            orgQuality,
                            item.Stacks,
                            item.Enchantable,
                            item.Tarnished,
                            gearEnhanced,
                            itemMaterial
                        );
                    }
                }

                foreach (var skill in skillList)
                {
                    skillDt.Rows.Add(
                        (long)player.Serial,
                        skill.Level,
                        skill.Slot,
                        skill.SkillName,
                        skill.Uses,
                        skill.CurrentCooldown
                    );
                }

                foreach (var spell in spellList)
                {
                    spellDt.Rows.Add(
                        (long)player.Serial,
                        spell.Level,
                        spell.Slot,
                        spell.SpellName,
                        spell.Casts,
                        spell.CurrentCooldown
                    );
                }
            }

            using (var cmd = new SqlCommand("PlayerSave", connection))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                var param = cmd.Parameters.AddWithValue("@Players", dt);
                param.SqlDbType = SqlDbType.Structured;
                param.TypeName = "dbo.PlayerType";
                cmd.ExecuteNonQuery();
            }

            using (var cmd2 = new SqlCommand("PlayerQuestSave", connection))
            {
                cmd2.CommandType = CommandType.StoredProcedure;
                var param2 = cmd2.Parameters.AddWithValue("@Quests", qDt);
                param2.SqlDbType = SqlDbType.Structured;
                param2.TypeName = "dbo.QuestType";
                cmd2.ExecuteNonQuery();
            }

            using (var cmd4 = new SqlCommand("ItemUpsert", connection))
            {
                cmd4.CommandType = CommandType.StoredProcedure;
                var param4 = cmd4.Parameters.AddWithValue("@Items", iDt);
                param4.SqlDbType = SqlDbType.Structured;
                param4.TypeName = "dbo.ItemType";
                cmd4.ExecuteNonQuery();
            }

            using (var cmd5 = new SqlCommand("PlayerSaveSkills", connection))
            {
                cmd5.CommandType = CommandType.StoredProcedure;
                var param5 = cmd5.Parameters.AddWithValue("@Skills", skillDt);
                param5.SqlDbType = SqlDbType.Structured;
                param5.TypeName = "dbo.SkillType";
                cmd5.ExecuteNonQuery();
            }

            using (var cmd6 = new SqlCommand("PlayerSaveSpells", connection))
            {
                cmd6.CommandType = CommandType.StoredProcedure;
                var param6 = cmd6.Parameters.AddWithValue("@Spells", spellDt);
                param6.SqlDbType = SqlDbType.Structured;
                param6.TypeName = "dbo.SpellType";
                cmd6.ExecuteNonQuery();
            }
        }
        catch (Exception e)
        {
            ServerSetup.ConnectionLogger(e.ToString());
        }
        finally
        {
            if (connection.State != ConnectionState.Open)
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("Reconnecting Player Save-State");
                ServerSetup.Instance.ServerSaveConnection = new SqlConnection(ConnectionString);
                ServerSetup.Instance.ServerSaveConnection.Open();
            }

            SaveLock.Release();
        }

        return true;
    }

    public void SaveBuffs(Aisling aisling, SqlConnection connection)
    {
        if (aisling.Buffs.IsEmpty) return;

        try
        {
            foreach (var buff in aisling.Buffs.Values.Where(i => i is { Name: not null }))
            {
                var cmd = ConnectToDatabaseSqlCommandWithProcedure("BuffSave", connection);
                cmd.Parameters.Add("@Serial", SqlDbType.BigInt).Value = (long)aisling.Serial;
                cmd.Parameters.Add("@Name", SqlDbType.VarChar).Value = buff.Name;
                cmd.Parameters.Add("@TimeLeft", SqlDbType.Int).Value = buff.TimeLeft;
                cmd.ExecuteNonQuery();
            }
        }
        catch (Exception e)
        {
            ServerSetup.ConnectionLogger(e.ToString());
        }
    }

    public void SaveDebuffs(Aisling aisling, SqlConnection connection)
    {
        if (aisling.Debuffs.IsEmpty) return;

        try
        {
            foreach (var deBuff in aisling.Debuffs.Values.Where(i => i is { Name: not null }))
            {
                var cmd = ConnectToDatabaseSqlCommandWithProcedure("DeBuffSave", connection);
                cmd.Parameters.Add("@Serial", SqlDbType.BigInt).Value = (long)aisling.Serial;
                cmd.Parameters.Add("@Name", SqlDbType.VarChar).Value = deBuff.Name;
                cmd.Parameters.Add("@TimeLeft", SqlDbType.Int).Value = deBuff.TimeLeft;
                cmd.ExecuteNonQuery();
            }
        }
        catch (Exception e)
        {
            ServerSetup.ConnectionLogger(e.ToString());
        }
    }

    public async Task<bool> CheckIfPlayerExists(string name)
    {
        try
        {
            var sConn = ConnectToDatabase(ConnectionString);
            var cmd = ConnectToDatabaseSqlCommandWithProcedure("CheckIfPlayerExists", sConn);
            cmd.Parameters.Add("@Name", SqlDbType.VarChar).Value = name;
            var reader = await cmd.ExecuteReaderAsync();
            var userFound = false;

            while (reader.Read())
            {
                var userName = reader["Username"].ToString();
                if (!string.Equals(userName, name, StringComparison.CurrentCultureIgnoreCase)) continue;
                if (string.Equals(name, userName, StringComparison.CurrentCultureIgnoreCase))
                {
                    userFound = true;
                }
            }

            reader.Close();
            sConn.Close();
            return userFound;
        }
        catch (Exception e)
        {
            ServerSetup.ConnectionLogger(e.ToString());
        }

        return false;
    }

    public async Task<bool> CheckIfPlayerExists(string name, long serial)
    {
        try
        {
            var sConn = ConnectToDatabase(ConnectionString);
            var cmd = ConnectToDatabaseSqlCommandWithProcedure("CheckIfPlayerHashExists", sConn);
            cmd.Parameters.Add("@Name", SqlDbType.VarChar).Value = name;
            cmd.Parameters.Add("@Serial", SqlDbType.BigInt).Value = serial;
            var reader = await cmd.ExecuteReaderAsync();
            var userFound = false;

            while (reader.Read())
            {
                var userName = reader["Username"].ToString();
                if (!string.Equals(userName, name, StringComparison.CurrentCultureIgnoreCase)) continue;
                if (string.Equals(name, userName, StringComparison.CurrentCultureIgnoreCase))
                {
                    userFound = true;
                }
            }

            reader.Close();
            sConn.Close();
            return userFound;
        }
        catch (Exception e)
        {
            ServerSetup.ConnectionLogger(e.ToString());
        }

        return false;
    }

    public async Task<Aisling> CheckPassword(string name)
    {
        var aisling = new Aisling();

        try
        {
            var continueLoad = await CheckIfPlayerExists(name);
            if (!continueLoad) return null;

            var sConn = ConnectToDatabase(EncryptedConnectionString);
            var values = new { Name = name };
            aisling = await sConn.QueryFirstAsync<Aisling>("[PlayerSecurity]", values, commandType: CommandType.StoredProcedure);
            sConn.Close();
        }
        catch (Exception e)
        {
            ServerSetup.ConnectionLogger(e.ToString());
        }

        return aisling;
    }

    public BoardTemplate ObtainMailboxId(long serial)
    {
        var board = new BoardTemplate();

        try
        {
            var sConn = ConnectToDatabase(ConnectionString);
            var values = new { Serial = serial };
            var quests = sConn.QueryFirst<Quests>("[ObtainMailBoxNumber]", values, commandType: CommandType.StoredProcedure);
            board.BoardId = (ushort)quests.MailBoxNumber;
            board.IsMail = true;
            board.Private = true;
            board.Serial = serial;
            sConn.Close();
        }
        catch (Exception e)
        {
            ServerSetup.ConnectionLogger(e.ToString());
        }

        return board;
    }

    public List<PostTemplate> ObtainPosts(ushort boardId)
    {
        var posts = new List<PostTemplate>();

        try
        {
            var sConn = new SqlConnection(PersonalMailString);
            sConn.Open();
            const string sql = "SELECT * FROM LegendsBoardsMail.dbo.Posts";
            var cmd = new SqlCommand(sql, sConn);
            cmd.CommandTimeout = 5;
            var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var readBoardId = (int)reader["BoardId"];
                if (boardId != readBoardId) continue;
                var postId = (int)reader["PostId"];

                var post = new PostTemplate()
                {
                    PostId = (short)postId,
                    Highlighted = (bool)reader["Highlighted"],
                    DatePosted = (DateTime)reader["DatePosted"],
                    Owner = reader["Owner"].ToString(),
                    Sender = reader["Sender"].ToString(),
                    ReadPost = (bool)reader["ReadPost"],
                    SubjectLine = reader["SubjectLine"].ToString(),
                    Message = reader["Message"].ToString()
                };

                posts.Add(post);
            }

            reader.Close();
            sConn.Close();
        }
        catch (SqlException e)
        {
            ServerSetup.ConnectionLogger(e.ToString());
        }

        return posts;
    }

    public void SendPost(PostTemplate postInfo, ushort boardId)
    {
        try
        {
            var connection = ConnectToDatabase(PersonalMailString);
            var cmd = ConnectToDatabaseSqlCommandWithProcedure("InsertPost", connection);
            cmd.Parameters.Add("@BoardId", SqlDbType.Int).Value = (int)boardId;
            cmd.Parameters.Add("@PostId", SqlDbType.Int).Value = postInfo.PostId;
            cmd.Parameters.Add("@Highlighted", SqlDbType.Bit).Value = postInfo.Highlighted;
            cmd.Parameters.Add("@DatePosted", SqlDbType.DateTime).Value = postInfo.DatePosted;
            cmd.Parameters.Add("@Owner", SqlDbType.VarChar).Value = postInfo.Owner;
            cmd.Parameters.Add("@Sender", SqlDbType.VarChar).Value = postInfo.Sender;
            cmd.Parameters.Add("@ReadPost", SqlDbType.Bit).Value = postInfo.ReadPost;
            cmd.Parameters.Add("@SubjectLine", SqlDbType.VarChar).Value = postInfo.SubjectLine;
            cmd.Parameters.Add("@Message", SqlDbType.VarChar).Value = postInfo.Message;
            cmd.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            ServerSetup.ConnectionLogger(e.ToString());
        }
    }

    public async Task Create(Aisling obj)
    {
        await using var @lock = await CreateLock.WaitAsync(TimeSpan.FromSeconds(5));

        if (@lock == null)
        {
            ServerSetup.EventsLogger("Failed to acquire lock for Create", LogLevel.Error);
            return;
        }

        var serial = EphemeralRandomIdGenerator<uint>.Shared.NextId;

        try
        {
            // Player
            var connection = ConnectToDatabase(EncryptedConnectionString);
            var cmd = ConnectToDatabaseSqlCommandWithProcedure("PlayerCreation", connection);
            var mailBoxNumber = EphemeralRandomIdGenerator<ushort>.Shared.NextId;

            #region Parameters

            cmd.Parameters.Add("@Serial", SqlDbType.BigInt).Value = (long)serial;
            cmd.Parameters.Add("@Created", SqlDbType.DateTime).Value = obj.Created;
            cmd.Parameters.Add("@UserName", SqlDbType.VarChar).Value = obj.Username;
            cmd.Parameters.Add("@Password", SqlDbType.VarChar).Value = obj.Password;
            cmd.Parameters.Add("@LastLogged", SqlDbType.DateTime).Value = obj.LastLogged;
            cmd.Parameters.Add("@CurrentHp", SqlDbType.Int).Value = obj.CurrentHp;
            cmd.Parameters.Add("@BaseHp", SqlDbType.Int).Value = obj.BaseHp;
            cmd.Parameters.Add("@CurrentMp", SqlDbType.Int).Value = obj.CurrentMp;
            cmd.Parameters.Add("@BaseMp", SqlDbType.Int).Value = obj.BaseMp;
            cmd.Parameters.Add("@Gender", SqlDbType.VarChar).Value = SpriteMaker.GenderValue(obj.Gender);
            cmd.Parameters.Add("@HairColor", SqlDbType.TinyInt).Value = obj.HairColor;
            cmd.Parameters.Add("@HairStyle", SqlDbType.TinyInt).Value = obj.HairStyle;

            #endregion

            ExecuteAndCloseConnection(cmd, connection);

            var sConn = ConnectToDatabase(ConnectionString);
            var adapter = new SqlDataAdapter();

            #region Adapter Inserts

            // Discovered
            var playerDiscoveredMaps =
                "INSERT INTO LegendsPlayers.dbo.PlayersDiscoveredMaps (Serial, MapId) VALUES " +
                $"('{(long)serial}','{obj.CurrentMapId}')";

            var cmd2 = new SqlCommand(playerDiscoveredMaps, sConn);
            cmd2.CommandTimeout = 5;

            adapter.InsertCommand = cmd2;
            adapter.InsertCommand.ExecuteNonQuery();

            // PlayersSkills
            var playerSkillBook =
                "INSERT INTO LegendsPlayers.dbo.PlayersSkillBook (Serial, Level, Slot, SkillName, Uses, CurrentCooldown) VALUES " +
                $"('{(long)serial}','{0}','{73}','Assail','{0}','{0}')";

            var cmd3 = new SqlCommand(playerSkillBook, sConn);
            cmd3.CommandTimeout = 5;

            adapter.InsertCommand = cmd3;
            adapter.InsertCommand.ExecuteNonQuery();

            #endregion

            var cmd5 = ConnectToDatabaseSqlCommandWithProcedure("InsertQuests", sConn);

            #region Parameters

            cmd5.Parameters.Add("@Serial", SqlDbType.BigInt).Value = (long)serial;
            cmd5.Parameters.Add("@MailBoxNumber", SqlDbType.Int).Value = mailBoxNumber;

            #endregion

            ExecuteAndCloseConnection(cmd5, sConn);
        }
        catch (Exception e)
        {
            ServerSetup.ConnectionLogger(e.ToString());
        }
    }

    private static DataTable PlayerDataTable()
    {
        var dt = new DataTable();
        dt.Columns.Add("Serial", typeof(long));
        dt.Columns.Add("Created", typeof(DateTime));
        dt.Columns.Add("Username", typeof(string));
        dt.Columns.Add("LoggedIn", typeof(bool));
        dt.Columns.Add("LastLogged", typeof(DateTime));
        dt.Columns.Add("X", typeof(byte));
        dt.Columns.Add("Y", typeof(byte));
        dt.Columns.Add("CurrentMapId", typeof(int));
        dt.Columns.Add("Direction", typeof(byte));
        dt.Columns.Add("CurrentHp", typeof(int));
        dt.Columns.Add("BaseHp", typeof(int));
        dt.Columns.Add("CurrentMp", typeof(int));
        dt.Columns.Add("BaseMp", typeof(int));
        dt.Columns.Add("_ac", typeof(short));
        dt.Columns.Add("_Regen", typeof(short));
        dt.Columns.Add("_Dmg", typeof(short));
        dt.Columns.Add("_Hit", typeof(short));
        dt.Columns.Add("_Mr", typeof(short));
        dt.Columns.Add("_Str", typeof(short));
        dt.Columns.Add("_Int", typeof(short));
        dt.Columns.Add("_Wis", typeof(short));
        dt.Columns.Add("_Con", typeof(short));
        dt.Columns.Add("_Dex", typeof(short));
        dt.Columns.Add("_Luck", typeof(short));
        dt.Columns.Add("AbpLevel", typeof(int));
        dt.Columns.Add("AbpNext", typeof(int));
        dt.Columns.Add("AbpTotal", typeof(long));
        dt.Columns.Add("ExpLevel", typeof(int));
        dt.Columns.Add("ExpNext", typeof(long));
        dt.Columns.Add("ExpTotal", typeof(long));
        dt.Columns.Add("Stage", typeof(string));
        dt.Columns.Add("JobClass", typeof(string));
        dt.Columns.Add("Path", typeof(string));
        dt.Columns.Add("PastClass", typeof(string));
        dt.Columns.Add("Gender", typeof(string));
        dt.Columns.Add("HairColor", typeof(byte));
        dt.Columns.Add("HairStyle", typeof(byte));
        dt.Columns.Add("NameColor", typeof(byte));
        dt.Columns.Add("ProfileMessage", typeof(string));
        dt.Columns.Add("Nation", typeof(string));
        dt.Columns.Add("Clan", typeof(string));
        dt.Columns.Add("ClanRank", typeof(string));
        dt.Columns.Add("ClanTitle", typeof(string));
        dt.Columns.Add("MonsterForm", typeof(short));
        dt.Columns.Add("ActiveStatus", typeof(string));
        dt.Columns.Add("Flags", typeof(string));
        dt.Columns.Add("CurrentWeight", typeof(short));
        dt.Columns.Add("World", typeof(byte));
        dt.Columns.Add("Lantern", typeof(byte));
        dt.Columns.Add("Invisible", typeof(bool));
        dt.Columns.Add("Resting", typeof(string));
        dt.Columns.Add("PartyStatus", typeof(string));
        dt.Columns.Add("GameMaster", typeof(bool));
        dt.Columns.Add("ArenaHost", typeof(bool));
        dt.Columns.Add("Knight", typeof(bool));
        dt.Columns.Add("GoldPoints", typeof(long));
        dt.Columns.Add("StatPoints", typeof(short));
        dt.Columns.Add("GamePoints", typeof(long));
        dt.Columns.Add("BankedGold", typeof(long));
        dt.Columns.Add("ArmorImg", typeof(short));
        dt.Columns.Add("HelmetImg", typeof(short));
        dt.Columns.Add("ShieldImg", typeof(short));
        dt.Columns.Add("WeaponImg", typeof(short));
        dt.Columns.Add("BootsImg", typeof(short));
        dt.Columns.Add("HeadAccessoryImg", typeof(short));
        dt.Columns.Add("Accessory1Img", typeof(short));
        dt.Columns.Add("Accessory2Img", typeof(short));
        dt.Columns.Add("Accessory3Img", typeof(short));
        dt.Columns.Add("Accessory1Color", typeof(byte));
        dt.Columns.Add("Accessory2Color", typeof(byte));
        dt.Columns.Add("Accessory3Color", typeof(byte));
        dt.Columns.Add("BodyColor", typeof(byte));
        dt.Columns.Add("BodySprite", typeof(byte));
        dt.Columns.Add("FaceSprite", typeof(byte));
        dt.Columns.Add("OverCoatImg", typeof(short));
        dt.Columns.Add("BootColor", typeof(byte));
        dt.Columns.Add("OverCoatColor", typeof(byte));
        dt.Columns.Add("Pants", typeof(byte));
        return dt;
    }

    private static DataTable ItemsDataTable()
    {
        var dt = new DataTable();
        dt.Columns.Add("ItemId", typeof(long));
        dt.Columns.Add("Name", typeof(string));
        dt.Columns.Add("Serial", typeof(long)); // Owner's Serial
        dt.Columns.Add("ItemPane", typeof(string));
        dt.Columns.Add("Slot", typeof(int));
        dt.Columns.Add("InventorySlot", typeof(int));
        dt.Columns.Add("Color", typeof(int));
        dt.Columns.Add("Cursed", typeof(bool));
        dt.Columns.Add("Durability", typeof(long));
        dt.Columns.Add("Identified", typeof(bool));
        dt.Columns.Add("ItemVariance", typeof(string));
        dt.Columns.Add("WeapVariance", typeof(string));
        dt.Columns.Add("ItemQuality", typeof(string));
        dt.Columns.Add("OriginalQuality", typeof(string));
        dt.Columns.Add("Stacks", typeof(int));
        return dt;
    }

    private static DataTable SkillDataTable()
    {
        var dt = new DataTable();
        dt.Columns.Add("Serial", typeof(long));
        dt.Columns.Add("Level", typeof(int));
        dt.Columns.Add("Slot", typeof(int));
        dt.Columns.Add("Skill", typeof(string));
        dt.Columns.Add("Uses", typeof(int));
        dt.Columns.Add("Cooldown", typeof(int));
        return dt;
    }

    private static DataTable SpellDataTable()
    {
        var dt = new DataTable();
        dt.Columns.Add("Serial", typeof(long));
        dt.Columns.Add("Level", typeof(int));
        dt.Columns.Add("Slot", typeof(int));
        dt.Columns.Add("Spell", typeof(string));
        dt.Columns.Add("Casts", typeof(int));
        dt.Columns.Add("Cooldown", typeof(int));
        return dt;
    }

    private static DataTable QuestDataTable()
    {
        var qDt = new DataTable();
        qDt.Columns.Add("Serial", typeof(long));
        qDt.Columns.Add("MailBoxNumber", typeof(int));
        return qDt;
    }
}