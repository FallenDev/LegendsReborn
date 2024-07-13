using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Darkages.Common;
using Darkages.Managers;
using Darkages.Sprites;
using Microsoft.Data.SqlClient;

namespace Darkages.Database;

public class AislingStorage : IStorage<Aisling>
{
    public const string ConnectionString = "Data Source=.;Initial Catalog=LegendsPlayers;Integrated Security=True;TrustServerCertificate=True";

    public Aisling Load(string name)
    {
        ServerSetup.EventsLogger("Incorrect Method called for Aisling Load.");
        return null;
    }
    public Aisling LoadAisling(string name)
    {
        var aisling = new Aisling();

        try
        {
            var continueLoad = CheckOnCreate(name);
            if (!continueLoad)
                return null;

            const string procedure = "[SelectPlayer]";
            var values = new { Name = name };
            using var sConn = new SqlConnection(ConnectionString);
            sConn.Open();
            using var multi = sConn.QueryMultiple(procedure, values, commandType: CommandType.StoredProcedure);
            return aisling = multi.Read<Aisling>().Single();

        }
        catch (SqlException e)
        {
            ServerSetup.EventsLogger(e.ToString());
        }
        catch (Exception e)
        {
            ServerSetup.EventsLogger(e.ToString());
        }
        aisling.Saving = false;
        return aisling;
    }
    public static void PasswordSave(Aisling obj)
    {
        if (obj == null)
            return;
        if (obj.Saving || obj.Loading)
            return;

        var continueLoad = CheckOnCreate(obj.Username);
        if (!continueLoad)
            return;

        obj.Saving = true;

        var dataTable = new DataTable();
        dataTable = MappedDataTablePlayers(dataTable, obj);
        var tableNumber = Generator.GenerateNumber();
        var table = $"TmpTable{tableNumber}";

        try
        {
            using var sConn = new SqlConnection(ConnectionString);
            using var cmd = new SqlCommand("", sConn);
            sConn.Open();

            cmd.CommandText =
                $"CREATE TABLE {table}([Serial] INT, [LastIP] VARCHAR(15),[Banned] BIT, [Username] VARCHAR(12), [Password] VARCHAR(64), [LoggedIn] BIT, [LastLogged] DATETIME, [X] INT, [Y] INT," +
                "[CurrentMapId] INT, [LastMapId] INT,[CurrentHP] INT, [_MaximumHP] INT, [CurrentMP] INT, [_MaximumMP] INT," +
                "[_Str] INT,[_Int] INT,[_Wis] INT,[_Con] INT,[_Dex] INT," +
                "[AbpLevel] INT, [AbpNext] INT, [Barrier] INT, [ExpLevel] INT, [ExpNext] INT, [ExpTotal] BIGINT," +
                "[SpouseName] VARCHAR(12), [Stage] VARCHAR(30), [Path] VARCHAR(20), [OriginalPath] VARCHAR(12), [Subbed] INT, [Gender] VARCHAR(6), [HairColor] INT, [HairStyle] INT, [ProfileMessage] VARCHAR(254), [BankedGold] INT," +
                "[Nation] VARCHAR(20),[AnimalForm] VARCHAR(10), [MonsterForm] INT, [ActiveStatus] VARCHAR(15), [Flags] VARCHAR(50), [World] INT, [GameMaster] BIT," +
                "[EventHost] BIT, [Developer] BIT, [GamePoints] INT, [GoldPoints] INT, [Scars] INT, [StatPoints] INT, [Title] VARCHAR(26), [Display] VARCHAR(12)," +
                "[Team] INT, [Guardian] BIT, [Participation] INT, [Victories] INT, [BombCount] INT, [BombLimit] INT, [BombRange] INT, [MentoredBy] VARCHAR(12), [MentorStart] DATETIME, [Students] INT, [CreationIP] VARCHAR(15), " +
                "[Created] DATETIME, [LostExp] INT, [ElixirPlay] INT, [ElixirWin] INT, [MinesFloor] INT,[Weddings] INT, [WakeInClan] BIT)";

            cmd.ExecuteNonQuery();

            using var bulkCopy = new SqlBulkCopy(sConn);
            //bulkCopy.BulkCopyTimeout = 5;
            bulkCopy.DestinationTableName = table;
            bulkCopy.WriteToServer(dataTable);


            //cmd.CommandTimeout = 5;
            cmd.CommandText =
                "BEGIN TRAN; " +
                "UPDATE P SET P.[Serial] = T.[Serial], P.[LastIP] = T.[LastIP], P.[Password] = T.[Password], P.[LoggedIn] = T.[LoggedIn] " +
                $"FROM LegendsPlayers.dbo.Players AS P INNER JOIN {table} AS T ON P.[Username] = T.[Username]; DROP TABLE {table}; " +
                "COMMIT;";
            cmd.ExecuteNonQuery();
        }
        catch (SqlException e)
        {
            ServerSetup.EventsLogger(e.ToString());

            //Crashes.TrackError(e);
        }
        catch (Exception e)
        {
            ServerSetup.EventsLogger(e.ToString());

            //Crashes.TrackError(e);
        }

        obj.Saving = false;
    }
    public bool Save<TA>(Aisling obj)
    {
        if (obj == null)
            return false;
        if (obj.Saving || obj.Loading)
            return false;

        var continueLoad = CheckOnCreate(obj.Username);
            
        if (!continueLoad)
            return false;

        obj.Saving = true;

        var dataTable = new DataTable();
        dataTable = MappedDataTablePlayers(dataTable, obj);
        var tableNumber = Generator.GenerateNumber();
        var table = $"TmpTable{tableNumber}";

        try
        {
            using var sConn = new SqlConnection(ConnectionString);
            using var cmd = new SqlCommand("", sConn);
            sConn.Open();

            cmd.CommandText =
                $"CREATE TABLE {table}([Serial] INT, [LastIP] VARCHAR(15),[Banned] BIT, [Username] VARCHAR(12), [Password] VARCHAR(64), [LoggedIn] BIT, [LastLogged] DATETIME, [X] INT, [Y] INT," +
                "[CurrentMapId] INT, [LastMapId] INT,[CurrentHP] INT, [_MaximumHP] INT, [CurrentMP] INT, [_MaximumMP] INT," +
                "[_Str] INT,[_Int] INT,[_Wis] INT,[_Con] INT,[_Dex] INT," +
                "[AbpLevel] INT, [AbpNext] INT, [Barrier] INT, [ExpLevel] INT, [ExpNext] INT, [ExpTotal] BIGINT," +
                "[SpouseName] VARCHAR(12), [Stage] VARCHAR(30), [Path] VARCHAR(20), [OriginalPath] VARCHAR(12), [Subbed] INT, [Gender] VARCHAR(6), [HairColor] INT, [HairStyle] INT, [ProfileMessage] VARCHAR(254), [BankedGold] INT," +
                "[Nation] VARCHAR(20),[AnimalForm] VARCHAR(10), [MonsterForm] INT, [ActiveStatus] VARCHAR(15), [Flags] VARCHAR(50), [World] INT, [GameMaster] BIT," +
                "[EventHost] BIT, [Developer] BIT, [GamePoints] INT, [GoldPoints] INT, [Scars] INT, [StatPoints] INT, [Title] VARCHAR(26), [Display] VARCHAR(12)," +
                "[Team] INT, [Guardian] BIT, [Participation] INT, [Victories] INT, [BombCount] INT, [BombLimit] INT, [BombRange] INT, [MentoredBy] VARCHAR(12), [MentorStart] DATETIME, [Students] INT, [CreationIP] VARCHAR(15), " +
                "[Created] DATETIME, [LostExp] INT, [ElixirPlay] INT, [ElixirWin] INT, [MinesFloor] INT, [Weddings] INT, [WakeInClan] BIT)";
            cmd.ExecuteNonQuery();

            using var bulkCopy = new SqlBulkCopy(sConn);
            //bulkCopy.BulkCopyTimeout = 5;
            bulkCopy.DestinationTableName = table;
            bulkCopy.WriteToServer(dataTable);


            //cmd.CommandTimeout = 5;
            cmd.CommandText =
                "BEGIN TRAN; " +
                "UPDATE P SET P.[Serial] = T.[Serial], P.[LastIP] = T.[LastIP], P.[Banned] = T.[Banned], P.[Username] = T.[Username], P.[Password] = T.[Password], P.[LoggedIn] = T.[LoggedIn], P.[LastLogged] = T.[LastLogged], P.[X] = T.[X], P.[Y] = T.[Y], " +
                "P.[CurrentMapId] = T.[CurrentMapId], P.[LastMapId] = T.[LastMapId]," +
                "P.[CurrentHp] = T.[CurrentHp], P.[_MaximumHp] = T.[_MaximumHp], P.[CurrentMp] = T.[CurrentMp], P.[_MaximumMp] = T.[_MaximumMp]," +
                "P.[_Str] = T.[_Str], P.[_Int] = T.[_Int],P.[_Wis] = T.[_Wis],P.[_Con] = T.[_Con],P.[_Dex] = T.[_Dex]," +
                "P.[AbpLevel] = T.[AbpLevel], P.[AbpNext] = T.[AbpNext], P.[Barrier] = T.[Barrier], P.[ExpLevel] = T.[ExpLevel], P.[ExpNext] = T.[ExpNext], P.[ExpTotal] = T.[ExpTotal], " +
                "P.[SpouseName] = T.[SpouseName], P.[Stage] = T.[Stage], P.[Path] = T.[Path], P.[OriginalPath] = T.[OriginalPath], P.[Subbed] = T.[Subbed], P.[Gender] = T.[Gender], P.[HairColor] = T.[HairColor], P.[HairStyle] = T.[HairStyle]," +
                "P.[ProfileMessage] = T.[ProfileMessage],P.[BankedGold] = T.[BankedGold]," +
                "P.[Nation] = T.[Nation],P.[AnimalForm] = T.[AnimalForm],P.[MonsterForm] = T.[MonsterForm],P.[ActiveStatus] = T.[ActiveStatus],P.[Flags] = T.[Flags]," +
                "P.[World] = T.[World],P.[GameMaster] = T.[GameMaster], P.[EventHost] = T.[EventHost], P.[Developer] = T.[Developer], P.[GamePoints] = T.[GamePoints]," +
                "P.[GoldPoints] = T.[GoldPoints], P.[Scars] = T.[Scars],P.[StatPoints] = T.[StatPoints], P.[Title] = T.[Title],P.[Display] = T.[Display]," +
                "P.[Team] = T.[Team],P.[Guardian] = T.[Guardian],P.[Participation] = T.[Participation], P.[Victories] = T.[Victories],P.[BombCount] = T.[BombCount],P.[BombLimit] = T.[BombLimit],P.[BombRange] = T.[BombRange],P.[MentoredBy] = T.[MentoredBy],P.[MentorStart] = T.[MentorStart]," +
                "P.[Students] = T.[Students], P.[CreationIP] = T.[CreationIP], P.[Created] = T.[Created], P.[LostExp] = T.[LostExp] , P.[ElixirPlay] = T.[ElixirPlay], P.[ElixirWin] = T.[ElixirWin]," +
                "P.[MinesFloor] = T.[MinesFloor], P.[Weddings] = T.[Weddings], P.[WakeInClan] = T.[WakeInClan]" +
                $"FROM LegendsPlayers.dbo.Players AS P INNER JOIN {table} AS T ON P.[Username] = T.[Username]; DROP TABLE {table}; " +
                "COMMIT;";
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

        obj.Inventory.Save();
        obj.Bank.Save();
        var equipment = SaveEquipment(obj);
        var skills = SaveSkills(obj);
        var spells = SaveSpells(obj);
        var religion = SaveReligion(obj);
        var quests = SaveQuests(obj);
        var crafting = SaveCrafting(obj);
        var ports = SavePorts(obj);
        var raids = SaveRaids(obj);
        obj.Saving = false;

        if (!skills)
        {
            ServerSetup.EventsLogger($"Skills did not save correctly. {obj.Username}");
            return false;
        }
        if (!spells)
        {
            ServerSetup.EventsLogger($"Spells did not save correctly. {obj.Username}");
            return false;
        }
        if (!equipment)
        {
            ServerSetup.EventsLogger($"Equipment did not save correctly. {obj.Username}");
            return false;
        }
        if (!religion)
        {
            ServerSetup.EventsLogger($"Religion did not save correctly. {obj.Username}");
            return false;
        }
        if (!quests)
        {
            ServerSetup.EventsLogger($"Quests did not save correctly. {obj.Username}");
            return false;
        }
        if (!crafting)
        {
            ServerSetup.EventsLogger($"Crafting did not save correctly. {obj.Username}");
            return false;
        }
        if (!ports)
        {
            ServerSetup.EventsLogger($"Teleports did not save correctly. {obj.Username}");
            return false;
        }
        if (!raids)
        {
            ServerSetup.EventsLogger($"Raids did not save correctly. {obj.Username}");
            return false;
        }
        return true;
    }
    public void QuickSave(Aisling obj)
    {
        if (obj == null)
            return;
        if (obj.Saving || obj.Loading)
            return;

        var continueLoad = CheckOnCreate(obj.Username);
        if (!continueLoad)
            return;

        obj.Saving = true;

        var skills = SaveSkills(obj);
        var spells = SaveSpells(obj);
        var equipment = SaveEquipment(obj);
        var religion = SaveReligion(obj);
        var quests = SaveQuests(obj);
        var crafting = SaveCrafting(obj);
        var ports = SavePorts(obj);
        var raids = SaveRaids(obj);

        var dataTable = new DataTable();
        dataTable = MappedDataTablePlayers(dataTable, obj);
        var tableNumber = Generator.GenerateNumber();
        var table = $"TmpTable{tableNumber}";


        try
        {
            using var sConn = new SqlConnection(ConnectionString);
            using var cmd = new SqlCommand("", sConn);
            sConn.Open();

            cmd.CommandText =
                $"CREATE TABLE {table}([Serial] INT, [LastIP] VARCHAR(15), [Banned] BIT, [Username] VARCHAR(12), [Password] VARCHAR(64), [LoggedIn] BIT, [LastLogged] DATETIME, [X] INT, [Y] INT," +
                "[CurrentMapId] INT, [LastMapId] INT,[CurrentHP] INT, [_MaximumHP] INT, [CurrentMP] INT, [_MaximumMP] INT," +
                "[_Str] INT,[_Int] INT,[_Wis] INT,[_Con] INT,[_Dex] INT," +
                "[AbpLevel] INT, [AbpNext] INT, [Barrier] INT, [ExpLevel] INT, [ExpNext] INT, [ExpTotal] BIGINT," +
                "[SpouseName] VARCHAR(12), [Stage] VARCHAR(30), [Path] VARCHAR(20), [OriginalPath] VARCHAR(12), [Subbed] INT, [Gender] VARCHAR(6), [HairColor] INT, [HairStyle] INT, [ProfileMessage] VARCHAR(254), [BankedGold] INT," +
                "[Nation] VARCHAR(20),[AnimalForm] VARCHAR(10), [MonsterForm] INT, [ActiveStatus] VARCHAR(15), [Flags] VARCHAR(50), [World] INT, [GameMaster] BIT," +
                "[EventHost] BIT, [Developer] BIT, [GamePoints] INT, [GoldPoints] INT, [Scars] INT, [StatPoints] INT, [Title] VARCHAR(26), [Display] VARCHAR(12)," +
                "[Team] INT, [Guardian] BIT, [Participation] INT, [Victories] INT, [BombCount] INT, [BombLimit] INT, [BombRange] INT, [MentoredBy] VARCHAR(12), [MentorStart] DATETIME, " +
                "[Students] INT, [CreationIP] VARCHAR(15), [Created] DATETIME, [LostExp] INT,[ElixirPlay] INT, [ElixirWin] INT, [MinesFloor] INT, [Weddings] INT, [WakeInClan] BIT)";
            cmd.ExecuteNonQuery();

            using var bulkCopy = new SqlBulkCopy(sConn);
            //bulkCopy.BulkCopyTimeout = 5;
            bulkCopy.DestinationTableName = table;
            bulkCopy.WriteToServer(dataTable);


            //cmd.CommandTimeout = 5;
            cmd.CommandText =
                "BEGIN TRAN; " +
                "UPDATE P SET P.[Serial] = T.[Serial], P.[LastIP] = T.[LastIP], P.[Banned] = T.[Banned], P.[Username] = T.[Username], P.[Password] = T.[Password], P.[LoggedIn] = T.[LoggedIn], P.[LastLogged] = T.[LastLogged], P.[X] = T.[X], P.[Y] = T.[Y], " +
                "P.[CurrentMapId] = T.[CurrentMapId], P.[LastMapId] = T.[LastMapId]," +
                "P.[CurrentHp] = T.[CurrentHp], P.[_MaximumHp] = T.[_MaximumHp], P.[CurrentMp] = T.[CurrentMp], P.[_MaximumMp] = T.[_MaximumMp]," +
                "P.[_Str] = T.[_Str], P.[_Int] = T.[_Int],P.[_Wis] = T.[_Wis],P.[_Con] = T.[_Con],P.[_Dex] = T.[_Dex]," +
                "P.[AbpLevel] = T.[AbpLevel], P.[AbpNext] = T.[AbpNext], P.[Barrier] = T.[Barrier], P.[ExpLevel] = T.[ExpLevel], P.[ExpNext] = T.[ExpNext], P.[ExpTotal] = T.[ExpTotal], " +
                "P.[SpouseName] = T.[SpouseName], P.[Stage] = T.[Stage], P.[Path] = T.[Path], P.[OriginalPath] = T.[OriginalPath], P.[Subbed] = T.[Subbed], P.[Gender] = T.[Gender] ,P.[HairColor] = T.[HairColor], P.[HairStyle] = T.[HairStyle]," +
                "P.[ProfileMessage] = T.[ProfileMessage],P.[BankedGold] = T.[BankedGold]," +
                "P.[Nation] = T.[Nation],P.[AnimalForm] = T.[AnimalForm],P.[MonsterForm] = T.[MonsterForm],P.[ActiveStatus] = T.[ActiveStatus],P.[Flags] = T.[Flags]," +
                "P.[World] = T.[World],P.[GameMaster] = T.[GameMaster], P.[EventHost] = T.[EventHost], P.[Developer] = T.[Developer], P.[GamePoints] = T.[GamePoints]," +
                "P.[GoldPoints] = T.[GoldPoints], P.[Scars] = T.[Scars],P.[StatPoints] = T.[StatPoints], P.[Title] = T.[Title],P.[Display] = T.[Display]," +
                "P.[Team] = T.[Team],P.[Guardian] = T.[Guardian], P.[Participation] = T.[Participation], P.[Victories] = T.[Victories],P.[BombCount] = T.[BombCount],P.[BombLimit] = T.[BombLimit],P.[BombRange] = T.[BombRange],P.[MentoredBy] = T.[MentoredBy],P.[MentorStart] = T.[MentorStart]," +
                "P.[Students] = T.[Students], P.[CreationIP] = T.[CreationIP], P.[Created] = T.[Created], P.[LostExp] = T.[LostExp], P.[ElixirPlay] = T.[ElixirPlay], P.[ElixirWin] = T.[ElixirWin], P.[MinesFloor] = T.[MinesFloor]," +
                "P.[Weddings] = T.[Weddings], P.[WakeInClan] = T.[WakeInClan]" +
                $"FROM LegendsPlayers.dbo.Players AS P INNER JOIN {table} AS T ON P.[Username] = T.[Username]; DROP TABLE {table}; " +
                "COMMIT;";
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

        obj.Saving = false;

        if (!skills)
        {
            ServerSetup.EventsLogger($"Skills did not save correctly. {obj.Username}");
            return;
        }
        if (!spells)
        {
            ServerSetup.EventsLogger($"Spells did not save correctly. {obj.Username}");
            return;
        }
        if (!equipment)
        {
            ServerSetup.EventsLogger($"Equipment did not save correctly. {obj.Username}");
            return;
        }
        if (!religion)
        {
            ServerSetup.EventsLogger($"Religion did not save correctly. {obj.Username}");
            return;
        }
        if (!quests)
        {
            ServerSetup.EventsLogger($"Quests did not save correctly. {obj.Username}");
            return;
        }
        if (!crafting)
        {
            ServerSetup.EventsLogger($"Crafting did not save correctly. {obj.Username}");
            return;
        }
        if (!ports)
        {
            ServerSetup.EventsLogger($"Teleports did not save correctly. {obj.Username}");
            return;
        }
        if (!raids)
        {
            ServerSetup.EventsLogger($"Raids did not save correctly. {obj.Username}");
            return;
        }
    }
    private static bool SaveSkills(Aisling obj)
    {
        if (obj?.SkillBook == null)
            return false;

        var dataTable = new DataTable();
        dataTable = MappedDataTablePlayersSkills(dataTable, obj);
        var tableNumber = Generator.GenerateNumber();
        var table = $"TmpTable{tableNumber}";


        try
        {
            using var sConn = new SqlConnection(ConnectionString);
            using var cmd = new SqlCommand("", sConn);
            sConn.Open();

            cmd.CommandText =
                $"CREATE TABLE {table}(" +
                $"[SkillId] INT, " +
                $"[SkillName] VARCHAR(30), " +
                $"[Serial] INT, " +
                $"[Level] INT, " +
                $"[Slot] INT, " +
                $"[Uses] INT, " +
                $"[ScriptName] VARCHAR(30)," +
                $"[NextAvailableUse] DATETIME)";
            cmd.ExecuteNonQuery();

            using var bulkCopy = new SqlBulkCopy(sConn);
            //bulkCopy.BulkCopyTimeout = 5;
            bulkCopy.DestinationTableName = table;
            bulkCopy.WriteToServer(dataTable);


            //cmd.CommandTimeout = 5;
            cmd.CommandText =
                "BEGIN TRAN; " +
                "UPDATE P SET P.[SkillId] = T.[SkillId],P.[SkillName] = T.[SkillName],P.[Serial] = T.[Serial], P.[Level] = T.[Level], P.[Slot] = T.[Slot], P.[Uses] = T.[Uses], P.[ScriptName] = T.[ScriptName], P.[NextAvailableUse] = T.[NextAvailableUse]" +
                $"FROM LegendsPlayers.dbo.PlayersSkillBook AS P INNER JOIN {table} AS T ON P.[Serial] = T.[Serial] AND P.[SkillName] = T.[SkillName]; DROP TABLE {table}; " +
                "COMMIT;";
            cmd.ExecuteNonQuery();
        }
        catch (SqlException e)
        {
            ServerSetup.EventsLogger(e.ToString());

            //Crashes.TrackError(e);
        }
        catch (Exception e)
        {
            ServerSetup.EventsLogger(e.ToString());

            //Crashes.TrackError(e);
        }

        return true;
    }
    private static bool SaveSpells(Aisling obj)
    {
        if (obj?.SpellBook == null)
            return false;

        var dataTable = new DataTable();
        dataTable = MappedDataTablePlayersSpells(dataTable, obj);
        var tableNumber = Generator.GenerateNumber();
        var table = $"TmpTable{tableNumber}";


        try
        {
            using var sConn = new SqlConnection(ConnectionString);
            using var cmd = new SqlCommand("", sConn);
            sConn.Open();

            cmd.CommandText =
                $"CREATE TABLE {table}(" +
                $"[SpellId] INT, " +
                $"[SpellName] VARCHAR(30), " +
                $"[Serial] INT, " +
                $"[Level] INT, " +
                $"[Slot] INT," +
                $"[ScriptName] VARCHAR(30), " +
                $"[Casts] INT)";
            cmd.ExecuteNonQuery();

            using var bulkCopy = new SqlBulkCopy(sConn);
            //bulkCopy.BulkCopyTimeout = 5;
            bulkCopy.DestinationTableName = table;
            bulkCopy.WriteToServer(dataTable);

            //cmd.CommandTimeout = 5;
            cmd.CommandText =
                "BEGIN TRAN; " +
                "UPDATE P SET " +
                "P.[SpellId] = T.[SpellId]," +
                "P.[SpellName] = T.[SpellName], " +
                "P.[Serial] = T.[Serial], " +
                "P.[Level] = T.[Level], " +
                "P.[Slot] = T.[Slot], " +
                "P.[ScriptName] = T.[ScriptName], " +
                "P.[Casts] = T.[Casts] " +
                $"FROM LegendsPlayers.dbo.PlayersSpellBook AS P INNER JOIN {table} AS T ON P.[Serial] = T.[Serial] AND P.[SpellName] = T.[SpellName]; DROP TABLE {table}; " +
                "COMMIT;";
            cmd.ExecuteNonQuery();
        }
        catch (SqlException e)
        {
            ServerSetup.EventsLogger(e.ToString());

            //Crashes.TrackError(e);
        }
        catch (Exception e)
        {
            ServerSetup.EventsLogger(e.ToString());

            //Crashes.TrackError(e);
        }

        return true;
    }
    private static bool SaveQuests(Aisling obj)
    {
        var dataTable = new DataTable();
        dataTable = MappedDataTablePlayersQuests(dataTable, obj);
        var tableNumber = Generator.GenerateNumber();
        var table = $"TmpTable{tableNumber}";


        try
        {
            using var sConn = new SqlConnection(ConnectionString);
            using var cmd = new SqlCommand("", sConn);
            sConn.Open();

            cmd.CommandText =
                $"CREATE TABLE {table}([Serial] INT, [Username] VARCHAR(50),[TutorialCompleted] BIT,[AiteQuest] INT, [Sun] INT, [Moon] INT, [TrustDevlin] BIT, [DevlinQuest] INT, [Devlinbif] INT, [Dar] INT, [DarReward] INT," +
                "[Circle2Armor] INT, [Whetstone] INT, [RionaQuest] BIT, [DarkFuture] INT, [DarkMistake] INT, [Nightmare] INT, [NightmareReplacement] INT, [FaeQueen] INT, [Pentagram] INT, [PentaComplete] VARCHAR(50), [Sage] INT," +
                "[PerfectLoaf] INT, [PerfectLoafTimer] DATETIME, [MukulFlower] INT, [DanaanCount] INT, [Higgle] INT, [Mines] INT, [FaeGaelic] INT, [Dornan] INT, [Richter] INT, [Tulia] INT, [Siobhan] INT, [Lothe] INT, [Dojo] INT, [Goblins] INT," +
                "[Grimloks] INT, [Alliance] INT, [AllianceTimer] DATETIME, [MasterBoss] INT, [TrialOfAmbition] INT, [TrialOfCommunity] INT, [TrialOfKnowledge] INT, [TrialOfSkill] INT, " +
                "[TrialOfStrength] INT, [TrialOfWealth] INT, [SilverMoon] INT, [Nunchaku] INT, [KasmaniumFloor] INT, [MasterAbilityQuest] INT, [MasterAbilityQuestTimer] DATETIME, [MasterAbilityRetryTimer] DATETIME," +
                "[AstKobolds] INT, [UndineAlliance] VARCHAR(50), [RoyalBetrayal] INT, [HiggleTimer] DATETIME, [SageTimer] DATETIME, [WizardPendant] INT, [RogueDagger] INT, [StatRefund] INT, [KarloposQuest] INT, " +
                "[OrderTimer] DATETIME, [Lazulite] INT, [Ruins] INT, [Valentine] INT, [EnhancedMasterArmor] INT)";

            cmd.ExecuteNonQuery();

            using var bulkCopy = new SqlBulkCopy(sConn);
            //bulkCopy.BulkCopyTimeout = 5;
            bulkCopy.DestinationTableName = table;
            bulkCopy.WriteToServer(dataTable);


            //cmd.CommandTimeout = 5;
            cmd.CommandText =
                "BEGIN TRAN; " +
                "UPDATE P SET P.[Serial] = T.[Serial], P.[Username] = T.[Username], P.[TutorialCompleted] = T.[TutorialCompleted],P.[AiteQuest] = T.[AiteQuest],P.[Sun] = T.[Sun],P.[Moon] = T.[Moon],P.[TrustDevlin] = T.[TrustDevlin],P.[DevlinQuest] = T.[DevlinQuest],P.[Devlinbif] = T.[Devlinbif],P.[Dar] = T.[Dar],P.[DarReward] = T.[DarReward]," +
                "P.[Circle2Armor] = T.[Circle2Armor],P.[Whetstone] = T.[Whetstone],P.[RionaQuest] = T.[RionaQuest],P.[DarkFuture] = T.[DarkFuture], P.[DarkMistake] = T.[DarkMistake], P.[Nightmare] = T.[Nightmare],P.[NightmareReplacement] = T.[NightmareReplacement],P.[FaeQueen] = T.[FaeQueen],P.[Pentagram] = T.[Pentagram]," +
                "P.[PentaComplete] = T.[PentaComplete],P.[Sage] = T.[Sage]," +
                "P.[PerfectLoaf] = T.[PerfectLoaf],P.[PerfectLoafTimer] = T.[PerfectLoafTimer],P.[MukulFlower] = T.[MukulFlower],P.[DanaanCount] = T.[DanaanCount],P.[Higgle] = T.[Higgle],P.[Mines] = T.[Mines],P.[FaeGaelic] = T.[FaeGaelic],P.[Dornan] = T.[Dornan],P.[Richter] = T.[Richter]," +
                "P.[Tulia] = T.[Tulia],P.[Siobhan] = T.[Siobhan],P.[Lothe] = T.[Lothe],P.[Dojo] = T.[Dojo],P.[Goblins] = T.[Goblins]," +
                "P.[Grimloks] = T.[Grimloks],P.[Alliance] = T.[Alliance],P.[AllianceTimer] = T.[AllianceTimer],P.[MasterBoss] = T.[MasterBoss],P.[TrialOfAmbition] = T.[TrialOfAmbition],P.[TrialOfCommunity] = T.[TrialOfCommunity],P.[TrialOfKnowledge] = T.[TrialOfKnowledge],P.[TrialOfSkill] = T.[TrialOfSkill]," +
                "P.[TrialOfStrength] = T.[TrialOfStrength],P.[TrialOfWealth] = T.[TrialOfWealth], P.[SilverMoon] = T.[SilverMoon], P.[Nunchaku] = T.[Nunchaku], P.[KasmaniumFloor] = T.[KasmaniumFloor], P.[MasterAbilityQuest] = T.[MasterAbilityQuest], P.[MasterAbilityQuestTimer] = T.[MasterAbilityQuestTimer], P.[MasterAbilityRetryTimer] = T.[MasterAbilityRetryTimer], " +
                "P.[AstKobolds] = T.[AstKobolds], P.[UndineAlliance] = T.[UndineAlliance], P.[RoyalBetrayal] = T.[RoyalBetrayal], P.[HiggleTimer] = T.[HiggleTimer], P.[SageTimer] = T.[SageTimer], P.[WizardPendant] = T.[WizardPendant], P.[RogueDagger] = T.[RogueDagger], P.[StatRefund] = T.[StatRefund], P.[KarloposQuest] = T.[KarloposQuest]," +
                "P.[OrderTimer] = T.[OrderTimer], P.[Lazulite] = T.[Lazulite], P.[Ruins] = T.[Ruins], P.[Valentine] = T.[Valentine], P.[EnhancedMasterArmor] = T.[EnhancedMasterarmor]" +
                $"FROM LegendsPlayers.dbo.PlayersQuests AS P INNER JOIN {table} AS T ON P.[Serial] = T.[Serial]; DROP TABLE {table};" +
                "COMMIT;";
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

        return true;
    }
    private static bool SaveCrafting(Aisling obj)
    {
        var dataTable = new DataTable();
        dataTable = MappedDataTablePlayersCrafting(dataTable, obj);
        var tableNumber = Generator.GenerateNumber();
        var table = $"TmpTable{tableNumber}";


        try
        {
            using var sConn = new SqlConnection(ConnectionString);
            using var cmd = new SqlCommand("", sConn);
            sConn.Open();

            cmd.CommandText =
                $"CREATE TABLE {table}(" +
                $"[Serial] INT," +
                $"[Username] VARCHAR(12)," +
                $"[GemCuttingSkill] INT, " +
                $"[GemSuccess] INT, " +
                $"[Herbalism] INT, " +
                $"[HerbSuccess] INT, " +
                $"[SmeltingSkill] INT, " +
                $"[SmeltingSuccess] INT, " +
                $"[WeavingSkill] INT," +
                $"[WeavingSuccess] INT, " +
                $"[AlchemySkill] INT, " +
                $"[AlchemySuccess] INT, " +
                $"[ForgingSkill] INT, " +
                $"[ForgingSuccess] INT, " +
                $"[JewelCraftingSkill] INT, " +
                $"[JewelCraftingSuccess] INT, " +
                $"[TailoringSkill] INT, " +
                $"[TailoringSuccess] INT, " +
                $"[SmithingSkill] INT," +
                $"[SmithingSuccess] INT, " +
                $"[MiningSkill] INT," +
                $"[MiningSuccess] INT, " +
                $"[HarvestSkill] INT," +
                $"[HarvestSuccess] INT," +
                $"[LastHarvested] DATETIME)";

            cmd.ExecuteNonQuery();

            using var bulkCopy = new SqlBulkCopy(sConn);
            //bulkCopy.BulkCopyTimeout = 5;
            bulkCopy.DestinationTableName = table;
            bulkCopy.WriteToServer(dataTable);


            //cmd.CommandTimeout = 5;
            cmd.CommandText =
                "BEGIN TRAN; " +
                "UPDATE P SET " +
                "P.[Serial] = T.[Serial]," +
                "P.[Username] = T.[Username], " +
                "P.[GemCuttingSkill] = T.[GemCuttingSkill], " +
                "P.[GemSuccess] = T.[GemSuccess], " +
                "P.[Herbalism] = T.[Herbalism], " +
                "P.[HerbSuccess] = T.[HerbSuccess], " +
                "P.[SmeltingSkill] = T.[SmeltingSkill], " +
                "P.[SmeltingSuccess] = T.[SmeltingSuccess], " +
                "P.[WeavingSkill] = T.[WeavingSkill]," +
                "P.[WeavingSuccess] = T.[WeavingSuccess]," +
                "P.[AlchemySkill] = T.[AlchemySkill]," +
                "P.[AlchemySuccess] = T.[AlchemySuccess]," +
                "P.[ForgingSkill] = T.[ForgingSkill]," +
                "P.[ForgingSuccess] = T.[ForgingSuccess]," +
                "P.[JewelCraftingSkill] = T.[JewelCraftingSkill]," +
                "P.[JewelCraftingSuccess] = T.[JewelCraftingSuccess]," +
                "P.[TailoringSkill] = T.[TailoringSkill]" +
                ",P.[TailoringSuccess] = T.[TailoringSuccess]," +
                "P.[SmithingSkill] = T.[SmithingSkill]," +
                "P.[SmithingSuccess] = T.[SmithingSuccess]," +
                "P.[MiningSkill] = T.[MiningSkill]," +
                "P.[MiningSuccess] = T.[MiningSuccess]," +
                "P.[HarvestSkill] = T.[HarvestSkill]," +
                "P.[HarvestSuccess] = T.[HarvestSuccess]," +
                "P.[LastHarvested] = T.[LastHarvested]" +
                $"FROM LegendsPlayers.dbo.PlayersCrafting AS P INNER JOIN {table} AS T ON P.[Serial] = T.[Serial]; DROP TABLE {table};" +
                "COMMIT;";
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

        return true;
    }
    private static bool SaveReligion(Aisling obj)
    {
        var dataTable = new DataTable();
        dataTable = MappedDataTablePlayersReligion(dataTable, obj);
        var tableNumber = Generator.GenerateNumber();
        var table = $"TmpTable{tableNumber}";


        try
        {
            using var sConn = new SqlConnection(ConnectionString);
            using var cmd = new SqlCommand("", sConn);
            sConn.Open();

            cmd.CommandText =
                $"CREATE TABLE {table}([Serial] INT,[Username] VARCHAR(50),[Religion] INT, [Faith] INT, [Scripture] INT, [InitiateCount] INT, [MassCount] INT,[LastPrayed] DATETIME," +
                "[CailMass] DATETIME, [CeannlaidirMass] DATETIME, [DeochMass] DATETIME, [FiosachdMass] DATETIME, [GliocaMass] DATETIME, [GramailMass] DATETIME," +
                "[LuathasMass] DATETIME, [SgriosMass] DATETIME, [HostMass] DATETIME, [LastGeas] DATETIME)";

            cmd.ExecuteNonQuery();

            using var bulkCopy = new SqlBulkCopy(sConn);
            //bulkCopy.BulkCopyTimeout = 5;
            bulkCopy.DestinationTableName = table;
            bulkCopy.WriteToServer(dataTable);


            //cmd.CommandTimeout = 5;
            cmd.CommandText =
                "BEGIN TRAN; " +
                "UPDATE P SET P.[Serial] = T.[Serial],P.[Username] = T.[Username],P.[Religion] = T.[Religion],P.[Faith] = T.[Faith],P.[Scripture] = T.[Scripture],P.[InitiateCount] = T.[InitiateCount],P.[MassCount] = T.[MassCount],P.[LastPrayed] = T.[LastPrayed]," +
                "P.[CailMass] = T.[CailMass],P.[CeannlaidirMass] = T.[CeannlaidirMass],P.[DeochMass] = T.[DeochMass],P.[FiosachdMass] = T.[FiosachdMass],P.[GliocaMass] = T.[GliocaMass],P.[GramailMass] = T.[GramailMass]," +
                "P.[LuathasMass] = T.[LuathasMass],P.[SgriosMass] = T.[SgriosMass],P.[HostMass] = T.[HostMass],P.[LastGeas] = T.[LastGeas]" +
                $"FROM LegendsPlayers.dbo.PlayersReligion AS P INNER JOIN {table} AS T ON P.[Serial] = T.[Serial]; DROP TABLE {table};" +
                "COMMIT;";
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

        return true;
    }
    private static bool SavePorts(Aisling obj)
    {
        var dataTable = new DataTable();
        dataTable = MappedDataTablePlayersTeleport(dataTable, obj);
        var tableNumber = Generator.GenerateNumber();
        var table = $"TmpTable{tableNumber}";

        try
        {
            using var sConn = new SqlConnection(ConnectionString);
            using var cmd = new SqlCommand("", sConn);
            sConn.Open();

            cmd.CommandText =
                $"CREATE TABLE {table}(" +
                $"[PlayerSerial] INT, " +
                $"[PlayerName] VARCHAR(12), " +
                $"[AbelCrypt] BIT, " +
                $"[MilethCrypt] BIT," +
                $"[PietSewer] BIT," +
                $"[CthonicTen] BIT," +
                $"[CthonicTwenty] BIT, " +
                $"[GramailPort] DATETIME, " +
                $"[QuestProg] INT," +
                $"[Conflux] BIT)";

            cmd.ExecuteNonQuery();

            using var bulkCopy = new SqlBulkCopy(sConn);
            //bulkCopy.BulkCopyTimeout = 5;
            bulkCopy.DestinationTableName = table;
            bulkCopy.WriteToServer(dataTable);

            //cmd.CommandTimeout = 5;
            cmd.CommandText =
                "BEGIN TRAN; " +
                "UPDATE P SET " +
                "P.[PlayerSerial] = T.[PlayerSerial]," +
                "P.[PlayerName] = T.[PlayerName]," +
                "P.[AbelCrypt] = T.[AbelCrypt]," +
                "P.[MilethCrypt] = T.[MilethCrypt]," +
                "P.[PietSewer] = T.[PietSewer]," +
                "P.[CthonicTen] = T.[CthonicTen]," +
                "P.[CthonicTwenty] = T.[CthonicTwenty], " +
                "P.[GramailPort] = T.[GramailPort], " +
                "P.[QuestProg] = T.[QuestProg]," +
                "P.[Conflux] = T.[Conflux]" +
                $"FROM LegendsPlayers.dbo.PlayersTeleport AS P INNER JOIN {table} AS T ON P.[PlayerSerial] = T.[PlayerSerial]; DROP TABLE {table};" +
                "COMMIT;";
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

        return true;
    }
    private static bool SaveRaids(Aisling obj)
    {
        var dataTable = new DataTable();
        dataTable = MappedDataTablePlayersRaids(dataTable, obj);
        var tableNumber = Generator.GenerateNumber();
        var table = $"TmpTable{tableNumber}";

        try
        {
            using var sConn = new SqlConnection(ConnectionString);
            using var cmd = new SqlCommand("", sConn);
            sConn.Open();

            cmd.CommandText =
                $"CREATE TABLE {table}(" +
                $"[Serial] INT, " +
                $"[Username] VARCHAR(12), " +
                $"[EarthRaidPoints] INT," +
                $"[EarthTimer] DATETIME," +
                $"[WindTimer] DATETIME," +
                $"[FireTimer] DATETIME," +
                $"[WaterTimer] DATETIME, " +
                $"[SpiritTimer] DATETIME, " +
                $"[MasterRaidProgress] INT," +
                $"[WindRaidPoints] INT," +
                $"[FireRaidPoints] INT," +
                $"[WaterRaidPoints] INT," +
                $"[SpiritRaidPoints] INT)";

            cmd.ExecuteNonQuery();

            using var bulkCopy = new SqlBulkCopy(sConn);
            bulkCopy.DestinationTableName = table;
            bulkCopy.WriteToServer(dataTable);

            cmd.CommandText =
                "BEGIN TRAN; " +
                "UPDATE P SET " +
                "P.[Serial] = T.[Serial]," +
                "P.[Username] = T.[Username]," +
                "P.[EarthRaidPoints] = T.[EarthRaidPoints]," +
                "P.[WindRaidPoints] = T.[WindRaidPoints]," +
                "P.[FireRaidPoints] = T.[FireRaidPoints]," +
                "P.[WaterRaidPoints] = T.[WaterRaidPoints]," +
                "P.[SpiritRaidPoints] = T.[SpiritRaidPoints]," +
                "P.[EarthTimer] = T.[EarthTimer]," +
                "P.[WindTimer] = T.[WindTimer]," +
                "P.[FireTimer] = T.[FireTimer]," +
                "P.[WaterTimer] = T.[WaterTimer], " +
                "P.[SpiritTimer] = T.[SpiritTimer], " +
                "P.[MasterRaidProgress] = T.[MasterRaidProgress]" +
                $"FROM LegendsPlayers.dbo.PlayersRaids AS P INNER JOIN {table} AS T ON P.[Serial] = T.[Serial]; DROP TABLE {table};" +
                "COMMIT;";
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
        return true;
    }
    private static bool SaveEquipment(Aisling obj)
    {
        if (obj != null)
        {
            foreach (var equipmentSlot in obj.EquipmentManager.Equipment.Values)
            {
                if (equipmentSlot != null)
                {
                    EquipmentManager.DeleteFromAislingDb(equipmentSlot.Item);
                    EquipmentManager.AddToAislingDb(obj, equipmentSlot.Item, equipmentSlot.Slot);
                }
            }
            return true;
        }
        return false;

    }
    public static bool CheckOnCreate(string name)
    {
        try
        {
            const string procedure = "[SelectPlayer]";
            using var sConn = new SqlConnection(ConnectionString);
            sConn.Open();

            var cmd = new SqlCommand(procedure, sConn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.Add("@Name", SqlDbType.VarChar).Value = name;

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var userName = reader["Username"].ToString();

                if (!string.Equals(userName, name, StringComparison.CurrentCultureIgnoreCase))
                    continue;

                return string.Equals(name, userName, StringComparison.CurrentCultureIgnoreCase);
            }
        }
        catch (SqlException e)
        {
            ServerSetup.EventsLogger(e.ToString());

        }
        catch (Exception e)
        {
            ServerSetup.EventsLogger(e.ToString());

        }
        return false;
    }
    public static void Create(Aisling obj)
    {
        var sConn = new SqlConnection(ConnectionString);
        var adapter = new SqlDataAdapter();

        try
        {
            sConn.Open();
            var serial = Generator.GenerateNumber();
            var discovered = Generator.GenerateNumber();

            var item = Generator.GenerateNumber();
            var skills = Generator.GenerateNumber();
            var spells = Generator.GenerateNumber();

            var passwordBytes = Encoding.UTF8.GetBytes(obj.Password);
            passwordBytes = SHA256.Create().ComputeHash(passwordBytes);
            var encodedPW = Convert.ToBase64String(passwordBytes);

            // Player
            var player = "INSERT INTO LegendsPlayers.dbo.Players (Serial, LastIP,  Username, Password, LoggedIn, LastLogged, X, Y," +
                         "CurrentMapId, LastMapId, CurrentHP, _MaximumHP, CurrentMP, _MaximumMP," +
                         "_Str, _Int, _Wis, _Con, _Dex," +
                         "AbpLevel, AbpNext, Barrier, ExpLevel, ExpNext, ExpTotal, " +
                         "SpouseName,Stage, Path, OriginalPath, Subbed, Gender, HairColor, HairStyle,ProfileMessage, BankedGold," +
                         "Nation, AnimalForm, MonsterForm, ActiveStatus, Flags, World, GameMaster, " +
                         "EventHost, Developer, GamePoints, GoldPoints, Scars, StatPoints, Title,Display," +
                         "Team, Guardian, Participation, Victories, BombCount, BombLimit, BombRange, MentoredBy, MentorStart, Students, CreationIP, Created, LostExp, ElixirPlay, ElixirWin, MinesFloor," +
                         "Weddings, WakeInClan)" +
                         $"VALUES('{serial}','{obj.LastIP}','{obj.Username}', '{encodedPW}', '{obj.LoggedIn}', '{obj.LastLogged}', {obj.XPos}, {obj.YPos}," +
                         $"{obj.CurrentMapId}, {obj.CurrentMapId}, {obj.CurrentHp}, {obj._MaximumHp}, {obj.CurrentMp}, {obj._MaximumMp}," +
                         $"'{obj._Str}','{obj._Int}','{obj._Wis}','{obj.Con}','{obj.Dex}'," +
                         $"{0},{0},{obj.Barrier},{obj.ExpLevel}, {obj.ExpNext}, {0}," +
                         $"'{null}', '{0}', '{obj.Path}', '{obj.Path}', '{0}', '{obj.Gender}', {obj.HairColor}, {obj.HairStyle}, '{null}', {0}," +
                         $"'Mileth','{obj.AnimalForm}', {obj.MonsterForm}, '{obj.ActiveStatus}', '{obj.Flags}', {0},'{obj.GameMaster}'," +
                         $"'{obj.EventHost}', '{obj.Developer}', {0},{0},{0},{0},'{obj.Title}','{obj.Display}'," +
                         $"{0},'False',{0},{0},{0},{0},{0},'{obj.MentoredBy}', '{obj.MentorStart}', {obj.Students}, '{obj.CreationIP}', '{obj.Created}',{0},{0},{0},{0}," +
                         $"{0}, 'True')";

            var cmd13 = new SqlCommand(player, sConn);
            adapter.InsertCommand = cmd13;
            adapter.InsertCommand.ExecuteNonQuery();

            // PlayersSkills
            var playerSkillBook = "INSERT INTO LegendsPlayers.dbo.PlayersSkillBook (SkillId, Serial, Level, Slot, SkillName, Uses, ScriptName, NextAvailableUse)" +
                                  $" VALUES ('{skills}','{serial}','{0}','{1}','Assail','{0}', 'Assail', '{DateTime.UtcNow}')";

            var cmd6 = new SqlCommand(playerSkillBook, sConn);
            adapter.InsertCommand = cmd6;
            adapter.InsertCommand.ExecuteNonQuery();

            // PlayerQuests
            var playerquests = "INSERT INTO LegendsPlayers.dbo.PlayersQuests (Serial, Username, TutorialCompleted, AiteQuest, Sun, Moon, TrustDevlin, DevlinQuest, Devlinbif, Dar, DarReward," +
                               "Circle2Armor, Whetstone, RionaQuest, DarkFuture, DarkMistake, Nightmare, NightmareReplacement, FaeQueen, Pentagram, PentaComplete, Sage," +
                               "PerfectLoaf, PerfectLoafTimer, MukulFlower, DanaanCount, Higgle, Mines, FaeGaelic, Dornan, Richter, Tulia, Siobhan, Lothe, Dojo, Goblins," +
                               "Grimloks, Alliance, AllianceTimer, MasterBoss, TrialOfAmbition, TrialOfCommunity, TrialOfKnowledge, TrialOfSkill, TrialOfStrength, TrialOfWealth, SilverMoon, Nunchaku, KasmaniumFloor, MasterAbilityQuest, MasterAbilityQuestTimer, MasterAbilityRetryTimer," +
                               "AstKobolds, UndineAlliance, RoyalBetrayal, HiggleTimer, SageTimer, WizardPendant, RogueDagger, StatRefund, KarloposQuest, OrderTimer, Lazulite, Ruins, Valentine, EnhancedMasterArmor)" +
                               $"VALUES ('{serial}','{obj.Username}','False','{0}','{0}','{0}', 'False','{0}','{0}','{0}','{0}'," +
                               $"'{0}','{0}','False','{0}','{0}','{0}','{0}','{0}','{0}','False','{0}'," +
                               $"'{0}','{obj.PerfectLoafTimer}','{0}','{0}','{0}','{0}','{0}','{0}','{0}','{0}','{0}','{0}','{0}','{0}'," +
                               $"'{0}','{0}','{obj.AllianceTimer}','{0}','{0}','{0}','{0}','{0}','{0}','{0}','{0}','{0}','{0}', '{0}', '{obj.MasterAbilityQuestTimer}','{obj.MasterAbilityRetryTimer}'," +
                               $"'{0}', 'False', '{0}', '{obj.HiggleTimer}', '{obj.SageTimer}', '{0}', '{0}', '{5}', '{0}', '{obj.OrderTimer}', '{0}', '{0}', '{0}', '{0}')";
            var cmd20 = new SqlCommand(playerquests, sConn);
            adapter.InsertCommand = cmd20;
            adapter.InsertCommand.ExecuteNonQuery();

            // PlayerCrafting
            var playercrafting = "INSERT INTO LegendsPlayers.dbo.PlayersCrafting(Serial, Username, GemCuttingSkill, GemSuccess, Herbalism, HerbSuccess, SmeltingSkill, SmeltingSuccess, WeavingSkill," +
                                 "WeavingSuccess, AlchemySkill, AlchemySuccess, ForgingSkill, ForgingSuccess, JewelCraftingSkill, JewelCraftingSuccess, TailoringSkill, TailoringSuccess, SmithingSkill," +
                                 "SmithingSuccess, MiningSkill, MiningSuccess, HarvestSkill, HarvestSuccess, LastHarvested)" +
                                 $"VALUES('{serial}','{obj.Username}','{0}','{0}','{0}','{0}','{0}','{0}','{0}'," +
                                 $"'{0}','{0}','{0}','{0}','{0}','{0}','{0}','{0}','{0}','{0}'," +
                                 $"'{0}','{1}','{0}','{1}','{0}','{obj.LastHarvested}')";
            var cmd21 = new SqlCommand(playercrafting, sConn);
            adapter.InsertCommand = cmd21;
            adapter.InsertCommand.ExecuteNonQuery();

            //PlayerReligion
            var playersreligion = "INSERT INTO LegendsPlayers.dbo.PlayersReligion(Serial, Username, Religion, Faith, Scripture, InitiateCount, MassCount,LastPrayed," +
                                  "CailMass, CeannlaidirMass, DeochMass, FiosachdMass, GliocaMass, GramailMass, LuathasMass, SgriosMass, HostMass, LastGeas)" +
                                  $"VALUES('{serial}','{obj.Username}','{obj.Religion}','{0}','{0}','{0}','{0}','{obj.LastPrayed}'," +
                                  $"'{obj.CailMass}','{obj.CeannlaidirMass}','{obj.DeochMass}','{obj.FiosachdMass}','{obj.GliocaMass}','{obj.GramailMass}'," +
                                  $"'{obj.LuathasMass}','{obj.SgriosMass}','{obj.HostMass}','{obj.LastGeas}')";
            var cmd22 = new SqlCommand(playersreligion, sConn);
            adapter.InsertCommand = cmd22;
            adapter.InsertCommand.ExecuteNonQuery();

            //PlayerPorts
            var playersport = "INSERT INTO LegendsPlayers.dbo.PlayersTeleport(PlayerSerial, PlayerName, AbelCrypt, MilethCrypt, PietSewer, CthonicTen, CthonicTwenty, GramailPort, QuestProg, Conflux)" +
                              $"VALUES('{serial}','{obj.Username}','{0}','{0}','{0}','{0}','{0}','{obj.GramailPort}','{0}', '{0}')";
            var cmd23 = new SqlCommand(playersport, sConn);
            adapter.InsertCommand = cmd23;
            adapter.InsertCommand.ExecuteNonQuery();

            //PlayerLegend
            var playersLegend = "INSERT INTO LegendsPlayers.dbo.PlayersLegend(Serial, Category, Color, Icon, Value)" +
                                $"VALUES('{serial}','Birth', '{32}', '{0}', 'Aisling - {Calendar.Now.LegendToString()}')";
            var cmd24 = new SqlCommand(playersLegend, sConn);
            adapter.InsertCommand = cmd24;
            adapter.InsertCommand.ExecuteNonQuery();

            //PlayerSpell
            var playersSpell = "INSERT INTO LegendsPlayers.dbo.PlayersSpellBook(SpellId, SpellName, Serial, [Level], Slot, ScriptName, Casts)" +
                               $"VALUES('{spells}', 'nis', '{serial}', '{0}', '{73}', 'nis', '{0}')";
            var cmd25 = new SqlCommand(playersSpell, sConn);
            adapter.InsertCommand = cmd25;
            adapter.InsertCommand.ExecuteNonQuery();
                
            //PlayerRaids
            var playersRaids = "INSERT INTO LegendsPlayers.dbo.PlayersRaids(Serial, Username, EarthRaidPoints, EarthTimer, WindTimer, FireTimer, WaterTimer, SpiritTimer, MasterRaidProgress, WindRaidPoints, FireRaidPoints, WaterRaidPoints, SpiritRaidPoints)" +
                               $"VALUES('{serial}', '{obj.Username}', '{0}', '{obj.EarthTimer}', '{obj.WindTimer}', '{obj.FireTimer}', '{obj.WaterTimer}', '{obj.SpiritTimer}', '{0}', '{0}', '{0}', '{0}', '{0}')";
            var cmd26 = new SqlCommand(playersRaids, sConn);
            adapter.InsertCommand = cmd26;
            adapter.InsertCommand.ExecuteNonQuery();
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
    private static DataTable MappedDataTablePlayers(DataTable dataTable, Aisling obj)
    {
        dataTable.Clear();
        var row = dataTable.NewRow();
        #region rows
        dataTable.Columns.Add("Serial");
        row["Serial"] = obj.Serial;
        dataTable.Columns.Add("LastIP");
        row["LastIP"] = obj.LastIP;
        dataTable.Columns.Add("Banned");
        row["Banned"] = obj.Banned.ToString();
        dataTable.Columns.Add("Username");
        row["Username"] = obj.Username;
        dataTable.Columns.Add("Password");
        row["Password"] = obj.Password;
        dataTable.Columns.Add("LoggedIn");
        row["LoggedIn"] = obj.LoggedIn.ToString();
        dataTable.Columns.Add("LastLogged");
        row["LastLogged"] = obj.LastLogged.ToString("yyyy-MM-dd HH:mm:ss");
        dataTable.Columns.Add("X");
        row["X"] = obj.X.ToString();
        dataTable.Columns.Add("Y");
        row["Y"] = obj.Y.ToString();
        dataTable.Columns.Add("CurrentMapId");
        row["CurrentMapId"] = obj.CurrentMapId.ToString();
        dataTable.Columns.Add("LastMapId");
        row["LastMapId"] = obj.LastMapId;
        dataTable.Columns.Add("CurrentHp");
        row["CurrentHp"] = obj.CurrentHp.ToString();
        dataTable.Columns.Add("_MaximumHp");
        row["_MaximumHp"] = obj._MaximumHp.ToString();
        dataTable.Columns.Add("CurrentMp");
        row["CurrentMp"] = obj.CurrentMp.ToString();
        dataTable.Columns.Add("_MaximumMp");
        row["_MaximumMp"] = obj._MaximumMp.ToString();
        dataTable.Columns.Add("_Str");
        row["_Str"] = obj._Str.ToString();
        dataTable.Columns.Add("_Int");
        row["_Int"] = obj._Int;
        dataTable.Columns.Add("_Wis");
        row["_Wis"] = obj._Wis;
        dataTable.Columns.Add("_Con");
        row["_Con"] = obj._Con;
        dataTable.Columns.Add("_Dex");
        row["_Dex"] = obj._Dex;
        dataTable.Columns.Add("AbpLevel");
        row["AbpLevel"] = obj.AbpLevel.ToString();
        dataTable.Columns.Add("AbpNext");
        row["AbpNext"] = obj.AbpNext.ToString();
        dataTable.Columns.Add("Barrier");
        row["Barrier"] = obj.Barrier;
        dataTable.Columns.Add("ExpLevel");
        row["ExpLevel"] = obj.ExpLevel.ToString();
        dataTable.Columns.Add("ExpNext");
        row["ExpNext"] = obj.ExpNext.ToString();
        dataTable.Columns.Add("ExpTotal");
        row["ExpTotal"] = obj.ExpTotal.ToString();
        dataTable.Columns.Add("SpouseName");
        row["SpouseName"] = obj.SpouseName.ToString();
        dataTable.Columns.Add("Stage");
        row["Stage"] = obj.Stage;
        dataTable.Columns.Add("Path");
        row["Path"] = obj.Path;
        dataTable.Columns.Add("OriginalPath");
        row["OriginalPath"] = obj.OriginalPath;
        dataTable.Columns.Add("Subbed");
        row["Subbed"] = obj.Subbed;
        dataTable.Columns.Add("Gender");
        row["Gender"] = obj.Gender;
        dataTable.Columns.Add("HairColor");
        row["HairColor"] = obj.HairColor.ToString();
        dataTable.Columns.Add("HairStyle");
        row["HairStyle"] = obj.HairStyle.ToString();
        dataTable.Columns.Add("ProfileMessage");
        row["ProfileMessage"] = obj.ProfileMessage;
        dataTable.Columns.Add("BankedGold");
        row["BankedGold"] = obj.BankedGold.ToString();
        dataTable.Columns.Add("Nation");
        row["Nation"] = obj.Nation;
        dataTable.Columns.Add("AnimalForm");
        row["AnimalForm"] = obj.AnimalForm;
        dataTable.Columns.Add("MonsterForm");
        row["MonsterForm"] = obj.MonsterForm;
        dataTable.Columns.Add("ActiveStatus");
        row["ActiveStatus"] = obj.ActiveStatus;
        dataTable.Columns.Add("Flags");
        row["Flags"] = obj.Flags;
        dataTable.Columns.Add("World");
        row["World"] = obj.World.ToString();
        dataTable.Columns.Add("GameMaster");
        row["GameMaster"] = obj.GameMaster.ToString();
        dataTable.Columns.Add("EventHost");
        row["EventHost"] = obj.EventHost.ToString();
        dataTable.Columns.Add("Developer");
        row["Developer"] = obj.Developer.ToString();
        dataTable.Columns.Add("GamePoints");
        row["GamePoints"] = obj.GamePoints.ToString();
        dataTable.Columns.Add("GoldPoints");
        row["GoldPoints"] = obj.GoldPoints.ToString();
        dataTable.Columns.Add("Scars");
        row["Scars"] = obj.Scars.ToString();
        dataTable.Columns.Add("StatPoints");
        row["StatPoints"] = obj.StatPoints.ToString();
        dataTable.Columns.Add("Title").ToString();
        row["Title"] = obj.Title;
        dataTable.Columns.Add("Display");
        row["Display"] = obj.Display;
        dataTable.Columns.Add("Team");
        row["Team"] = obj.Team.ToString();
        dataTable.Columns.Add("Guardian");
        row["Guardian"] = obj.Guardian.ToString();
        dataTable.Columns.Add("Participation");
        row["Participation"] = obj.Participation.ToString();
        dataTable.Columns.Add("Victories");
        row["Victories"] = obj.Victories.ToString();
        dataTable.Columns.Add("BombCount");
        row["BombCount"] = obj.BombCount.ToString();
        dataTable.Columns.Add("BombLimit");
        row["BombLimit"] = obj.BombLimit.ToString();
        dataTable.Columns.Add("BombRange");
        row["BombRange"] = obj.BombRange.ToString();
        dataTable.Columns.Add("MentoredBy");
        row["MentoredBy"] = obj.MentoredBy.ToString();
        dataTable.Columns.Add("MentorStart");
        row["MentorStart"] = obj.MentorStart.ToString("yyyy-MM-dd HH:mm:ss");
        dataTable.Columns.Add("Students");
        row["Students"] = obj.Students.ToString();
        dataTable.Columns.Add("CreationIP");
        row["CreationIP"] = obj.CreationIP.ToString();
        dataTable.Columns.Add("Created");
        row["Created"] = obj.Created.ToString("yyyy-MM-dd HH:mm:ss");
        dataTable.Columns.Add("LostExp");
        row["LostExp"] = obj.LostExp.ToString();
        dataTable.Columns.Add("ElixirPlay");
        row["ElixirPlay"] = obj.ElixirPlay;
        dataTable.Columns.Add("ElixirWin");
        row["ElixirWin"] = obj.ElixirWin;
        dataTable.Columns.Add("MinesFloor");
        row["MinesFloor"] = obj.MinesFloor;
        dataTable.Columns.Add("Weddings");
        row["Weddings"] = obj.Weddings;
        dataTable.Columns.Add("WakeInClan");
        row["WakeInClan"] = obj.WakeInClan;
        #endregion rows
        dataTable.Rows.Add(row);
        return dataTable;
    }
    private static DataTable MappedDataTablePlayersSkills(DataTable dataTable, Aisling obj)
    {
        dataTable.Clear();
        dataTable.Columns.Add("SkillId");
        dataTable.Columns.Add("SkillName");
        dataTable.Columns.Add("Serial");
        dataTable.Columns.Add("Level");
        dataTable.Columns.Add("Slot");
        dataTable.Columns.Add("Uses");
        dataTable.Columns.Add("ScriptName");
        dataTable.Columns.Add("NextAvailableUse");
        foreach (var skill in obj.SkillBook.Skills.Values.Where(i => i is { SkillName: { } }))
        {
            var row = dataTable.NewRow();
            row["SkillId"] = skill.SkillId;
            row["SkillName"] = skill.Template.Name;
            row["Serial"] = obj.Serial;
            row["Level"] = skill.Level;
            row["Slot"] = Convert.ToInt32(skill.Slot);
            row["Uses"] = skill.Uses;
            row["ScriptName"] = skill.Template.ScriptName;
            row["NextAvailableUse"] = skill.NextAvailableUse;
            dataTable.Rows.Add(row);
        }

        return dataTable;
    }
    private static DataTable MappedDataTablePlayersSpells(DataTable dataTable, Aisling obj)
    {
        dataTable.Clear();
        dataTable.Columns.Add("SpellId");
        dataTable.Columns.Add("SpellName");
        dataTable.Columns.Add("Serial");
        dataTable.Columns.Add("Level");
        dataTable.Columns.Add("Slot");
        dataTable.Columns.Add("ScriptName");
        dataTable.Columns.Add("Casts");

        foreach (var spell in obj.SpellBook.Spells.Values.Where(i => i is { SpellName: { } }))
        {
            var row = dataTable.NewRow();
            row["SpellId"] = spell.SpellId;
            row["SpellName"] = spell.Template.Name;
            row["Serial"] = obj.Serial;
            row["Level"] = spell.Level;
            row["Slot"] = spell.Slot;
            row["ScriptName"] = spell.Template.ScriptKey;
            row["Casts"] = spell.Casts;
            dataTable.Rows.Add(row);
        }

        return dataTable;
    }
    private static DataTable MappedDataTablePlayersQuests(DataTable dataTable, Aisling obj)
    {
        dataTable.Clear();
        var row = dataTable.NewRow();
        #region rows
        dataTable.Columns.Add("Serial");
        row["Serial"] = obj.Serial;
        dataTable.Columns.Add("Username");
        row["Username"] = obj.Username.ToString();
        dataTable.Columns.Add("TutorialCompleted");
        row["TutorialCompleted"] = obj.TutorialCompleted.ToString();
        dataTable.Columns.Add("AiteQuest");
        row["AiteQuest"] = obj.AiteQuest.ToString();
        dataTable.Columns.Add("Sun");
        row["Sun"] = obj.Sun.ToString();
        dataTable.Columns.Add("Moon");
        row["Moon"] = obj.Moon.ToString();
        dataTable.Columns.Add("TrustDevlin");
        row["TrustDevlin"] = obj.TrustDevlin.ToString();
        dataTable.Columns.Add("DevlinQuest");
        row["DevlinQuest"] = obj.DevlinQuest.ToString();
        dataTable.Columns.Add("Devlinbif");
        row["Devlinbif"] = obj.Devlinbif.ToString();
        dataTable.Columns.Add("Dar");
        row["Dar"] = obj.Dar.ToString();
        dataTable.Columns.Add("DarReward");
        row["DarReward"] = obj.DarReward.ToString();
        dataTable.Columns.Add("Circle2Armor");
        row["Circle2Armor"] = obj.Circle2Armor.ToString();
        dataTable.Columns.Add("Whetstone");
        row["Whetstone"] = obj.Whetstone.ToString();
        dataTable.Columns.Add("RionaQuest");
        row["RionaQuest"] = obj.RionaQuest.ToString();
        dataTable.Columns.Add("DarkFuture");
        row["DarkFuture"] = obj.DarkFuture.ToString();
        dataTable.Columns.Add("DarkMistake");
        row["DarkMistake"] = obj.DarkMistake.ToString();
        dataTable.Columns.Add("Nightmare");
        row["Nightmare"] = obj.Nightmare.ToString();
        dataTable.Columns.Add("NightmareReplacement");
        row["NightmareReplacement"] = obj.NightmareReplacement.ToString();
        dataTable.Columns.Add("FaeQueen");
        row["FaeQueen"] = obj.FaeQueen.ToString();
        dataTable.Columns.Add("Pentagram");
        row["Pentagram"] = obj.Pentagram.ToString();
        dataTable.Columns.Add("PentaComplete");
        row["PentaComplete"] = obj.PentaComplete.ToString();
        dataTable.Columns.Add("Sage");
        row["Sage"] = obj.Sage.ToString();
        dataTable.Columns.Add("PerfectLoaf");
        row["PerfectLoaf"] = obj.PerfectLoaf.ToString();
        dataTable.Columns.Add("PerfectLoafTimer");
        row["PerfectLoafTimer"] = obj.PerfectLoafTimer.ToString(CultureInfo.CurrentCulture);
        dataTable.Columns.Add("MukulFlower");
        row["MukulFlower"] = obj.MukulFlower.ToString();
        dataTable.Columns.Add("DanaanCount");
        row["DanaanCount"] = obj.DanaanCount.ToString();
        dataTable.Columns.Add("Higgle");
        row["Higgle"] = obj.Higgle.ToString();
        dataTable.Columns.Add("Mines");
        row["Mines"] = obj.Mines.ToString();
        dataTable.Columns.Add("FaeGaelic");
        row["FaeGaelic"] = obj.FaeGaelic.ToString();
        dataTable.Columns.Add("Dornan");
        row["Dornan"] = obj.Dornan.ToString();
        dataTable.Columns.Add("Richter");
        row["Richter"] = obj.Richter.ToString();
        dataTable.Columns.Add("Tulia");
        row["Tulia"] = obj.Tulia.ToString();
        dataTable.Columns.Add("Siobhan");
        row["Siobhan"] = obj.Siobhan.ToString();
        dataTable.Columns.Add("Lothe");
        row["Lothe"] = obj.Lothe.ToString();
        dataTable.Columns.Add("Dojo");
        row["Dojo"] = obj.Dojo.ToString();
        dataTable.Columns.Add("Goblins");
        row["Goblins"] = obj.Goblins.ToString();
        dataTable.Columns.Add("Grimloks");
        row["Grimloks"] = obj.Grimloks.ToString();
        dataTable.Columns.Add("Alliance");
        row["Alliance"] = obj.Alliance.ToString();
        dataTable.Columns.Add("AllianceTimer");
        row["AllianceTimer"] = obj.AllianceTimer.ToString(CultureInfo.CurrentCulture);
        dataTable.Columns.Add("MasterBoss");
        row["MasterBoss"] = obj.MasterBoss.ToString();
        dataTable.Columns.Add("TrialOfAmbition");
        row["TrialOfAmbition"] = obj.TrialOfAmbition.ToString();
        dataTable.Columns.Add("TrialOfCommunity");
        row["TrialOfCommunity"] = obj.TrialOfCommunity.ToString();
        dataTable.Columns.Add("TrialOfKnowledge");
        row["TrialOfKnowledge"] = obj.TrialOfKnowledge.ToString();
        dataTable.Columns.Add("TrialOfSkill");
        row["TrialOfSkill"] = obj.TrialOfSkill.ToString();
        dataTable.Columns.Add("TrialOfStrength");
        row["TrialOfStrength"] = obj.TrialOfStrength.ToString();
        dataTable.Columns.Add("TrialOfWealth");
        row["TrialOfWealth"] = obj.TrialOfWealth.ToString();
        dataTable.Columns.Add("SilverMoon");
        row["SilverMoon"] = obj.SilverMoon.ToString();
        dataTable.Columns.Add("Nunchaku");
        row["Nunchaku"] = obj.Nunchaku.ToString();
        dataTable.Columns.Add("KasmaniumFloor");
        row["KasmaniumFloor"] = obj.KasmaniumFloor.ToString();
        dataTable.Columns.Add("MasterAbilityQuest");
        row["MasterAbilityQuest"] = obj.MasterAbilityQuest.ToString();
        dataTable.Columns.Add("MasterAbilityQuestTimer");
        row["MasterAbilityQuestTimer"] = obj.MasterAbilityQuestTimer.ToString(CultureInfo.CurrentCulture);
        dataTable.Columns.Add("MasterAbilityRetryTimer");
        row["MasterAbilityRetryTimer"] = obj.MasterAbilityRetryTimer.ToString(CultureInfo.CurrentCulture);
        dataTable.Columns.Add("AstKobolds");
        row["AstKobolds"] = obj.AstKobolds.ToString();
        dataTable.Columns.Add("UndineAlliance");
        row["UndineAlliance"] = obj.UndineAlliance.ToString();
        dataTable.Columns.Add("RoyalBetrayal");
        row["RoyalBetrayal"] = obj.RoyalBetrayal;
        dataTable.Columns.Add("HiggleTimer");
        row["HiggleTimer"] = obj.HiggleTimer.ToString(CultureInfo.CurrentCulture);
        dataTable.Columns.Add("SageTimer");
        row["SageTimer"] = obj.SageTimer.ToString(CultureInfo.CurrentCulture);
        dataTable.Columns.Add("WizardPendant");
        row["WizardPendant"] = obj.WizardPendant;
        dataTable.Columns.Add("RogueDagger");
        row["RogueDagger"] = obj.RogueDagger;
        dataTable.Columns.Add("StatRefund");
        row["StatRefund"] = obj.StatRefund;
        dataTable.Columns.Add("KarloposQuest");
        row["KarloposQuest"] = obj.KarloposQuest;
        dataTable.Columns.Add("OrderTimer");
        row["OrderTimer"] = obj.OrderTimer.ToString(CultureInfo.CurrentCulture);
        dataTable.Columns.Add("Lazulite");
        row["Lazulite"] = obj.Lazulite;
        dataTable.Columns.Add("Ruins");
        row["Ruins"] = obj.Ruins;
        dataTable.Columns.Add("Valentine");
        row["Valentine"] = obj.Valentine;
        dataTable.Columns.Add("EnhancedMasterArmor");
        row["EnhancedMasterArmor"] = obj.EnhancedMasterArmor;
        #endregion rows
        dataTable.Rows.Add(row);
        return dataTable;
    }
    private static DataTable MappedDataTablePlayersCrafting(DataTable dataTable, Aisling obj)
    {
        dataTable.Clear();
        var row = dataTable.NewRow();
        #region rows
        dataTable.Columns.Add("Serial");
        row["Serial"] = obj.Serial;
        dataTable.Columns.Add("Username");
        row["Username"] = obj.Username.ToString();
        dataTable.Columns.Add("GemCuttingSkill");
        row["GemCuttingSkill"] = obj.GemCuttingSkill;
        dataTable.Columns.Add("GemSuccess");
        row["GemSuccess"] = obj.GemSuccess;
        dataTable.Columns.Add("Herbalism");
        row["Herbalism"] = obj.Herbalism;
        dataTable.Columns.Add("HerbSuccess");
        row["HerbSuccess"] = obj.HerbSuccess;
        dataTable.Columns.Add("SmeltingSkill");
        row["SmeltingSkill"] = obj.SmeltingSkill;
        dataTable.Columns.Add("SmeltingSuccess");
        row["SmeltingSuccess"] = obj.SmeltingSuccess;
        dataTable.Columns.Add("WeavingSkill");
        row["WeavingSkill"] = obj.WeavingSkill;
        dataTable.Columns.Add("WeavingSuccess");
        row["WeavingSuccess"] = obj.WeavingSuccess;
        dataTable.Columns.Add("AlchemySkill");
        row["AlchemySkill"] = obj.AlchemySkill;
        dataTable.Columns.Add("AlchemySuccess");
        row["AlchemySuccess"] = obj.AlchemySuccess;
        dataTable.Columns.Add("ForgingSkill");
        row["ForgingSkill"] = obj.ForgingSkill;
        dataTable.Columns.Add("ForgingSuccess");
        row["ForgingSuccess"] = obj.ForgingSuccess;
        dataTable.Columns.Add("JewelCraftingSkill");
        row["JewelCraftingSkill"] = obj.JewelCraftingSkill;
        dataTable.Columns.Add("JewelCraftingSuccess");
        row["JewelCraftingSuccess"] = obj.JewelCraftingSuccess;
        dataTable.Columns.Add("TailoringSkill");
        row["TailoringSkill"] = obj.TailoringSkill;
        dataTable.Columns.Add("TailoringSuccess");
        row["TailoringSuccess"] = obj.TailoringSuccess;
        dataTable.Columns.Add("SmithingSkill");
        row["SmithingSkill"] = obj.SmithingSkill;
        dataTable.Columns.Add("SmithingSuccess");
        row["SmithingSuccess"] = obj.SmithingSuccess;
        dataTable.Columns.Add("MiningSkill");
        row["MiningSkill"] = obj.MiningSkill;
        dataTable.Columns.Add("MiningSuccess");
        row["MiningSuccess"] = obj.MiningSuccess;
        dataTable.Columns.Add("HarvestSkill");
        row["HarvestSkill"] = obj.HarvestSkill;
        dataTable.Columns.Add("HarvestSuccess");
        row["HarvestSuccess"] = obj.HarvestSuccess;
        dataTable.Columns.Add("LastHarvested");
        row["LastHarvested"] = obj.LastHarvested.ToString(CultureInfo.CurrentCulture);
        #endregion rows
        dataTable.Rows.Add(row);
        return dataTable;
    }
    private static DataTable MappedDataTablePlayersReligion(DataTable dataTable, Aisling obj)
    {
        dataTable.Clear();
        var row = dataTable.NewRow();
        #region rows
        dataTable.Columns.Add("Serial");
        row["Serial"] = obj.Serial;
        dataTable.Columns.Add("Username");
        row["Username"] = obj.Username.ToString();
        dataTable.Columns.Add("Religion");
        row["Religion"] = obj.Religion.ToString();
        dataTable.Columns.Add("Faith");
        row["Faith"] = obj.Faith.ToString();
        dataTable.Columns.Add("Scripture");
        row["Scripture"] = obj.Scripture;
        dataTable.Columns.Add("InitiateCount");
        row["InitiateCount"] = obj.InitiateCount.ToString();
        dataTable.Columns.Add("MassCount");
        row["MassCount"] = obj.MassCount.ToString();
        dataTable.Columns.Add("LastPrayed");
        row["LastPrayed"] = obj.LastPrayed.ToString(CultureInfo.CurrentCulture);
        dataTable.Columns.Add("CailMass");
        row["CailMass"] = obj.CailMass.ToString(CultureInfo.CurrentCulture);
        dataTable.Columns.Add("CeannlaidirMass");
        row["CeannlaidirMass"] = obj.CeannlaidirMass.ToString(CultureInfo.CurrentCulture);
        dataTable.Columns.Add("DeochMass");
        row["DeochMass"] = obj.DeochMass.ToString(CultureInfo.CurrentCulture);
        dataTable.Columns.Add("FiosachdMass");
        row["FiosachdMass"] = obj.FiosachdMass.ToString(CultureInfo.CurrentCulture);
        dataTable.Columns.Add("GliocaMass");
        row["GliocaMass"] = obj.GliocaMass.ToString(CultureInfo.CurrentCulture);
        dataTable.Columns.Add("GramailMass");
        row["GramailMass"] = obj.GramailMass.ToString(CultureInfo.CurrentCulture);
        dataTable.Columns.Add("LuathasMass");
        row["LuathasMass"] = obj.LuathasMass.ToString(CultureInfo.CurrentCulture);
        dataTable.Columns.Add("SgriosMass");
        row["SgriosMass"] = obj.SgriosMass.ToString(CultureInfo.CurrentCulture);
        dataTable.Columns.Add("HostMass");
        row["HostMass"] = obj.HostMass.ToString(CultureInfo.CurrentCulture);
        dataTable.Columns.Add("LastGeas");
        row["LastGeas"] = obj.LastGeas.ToString(CultureInfo.CurrentCulture);
        #endregion rows
        dataTable.Rows.Add(row);
        return dataTable;
    }
    private static DataTable MappedDataTablePlayersTeleport(DataTable dataTable, Aisling obj)
    {
        dataTable.Clear();
        var row = dataTable.NewRow();
        #region rows
        dataTable.Columns.Add("PlayerSerial");
        row["PlayerSerial"] = obj.Serial;
        dataTable.Columns.Add("PlayerName");
        row["PlayerName"] = obj.Username;
        dataTable.Columns.Add("AbelCrypt");
        row["AbelCrypt"] = obj.AbelCrypt;
        dataTable.Columns.Add("MilethCrypt");
        row["MilethCrypt"] = obj.MilethCrypt;
        dataTable.Columns.Add("PietSewer");
        row["PietSewer"] = obj.PietSewer;
        dataTable.Columns.Add("CthonicTen");
        row["CthonicTen"] = obj.CthonicTen;
        dataTable.Columns.Add("CthonicTwenty");
        row["CthonicTwenty"] = obj.CthonicTwenty;
        dataTable.Columns.Add("GramailPort");
        row["GramailPort"] = obj.GramailPort.ToString(CultureInfo.CurrentCulture);
        dataTable.Columns.Add("QuestProg");
        row["QuestProg"] = obj.QuestProg;
        dataTable.Columns.Add("Conflux");
        row["Conflux"] = obj.Conflux;
        #endregion rows
        dataTable.Rows.Add(row);
        return dataTable;
    }
    private static DataTable MappedDataTablePlayersRaids(DataTable dataTable, Aisling obj)
    {
        dataTable.Clear();
        var row = dataTable.NewRow();
        #region rows
        dataTable.Columns.Add("Serial");
        row["Serial"] = obj.Serial;
        dataTable.Columns.Add("Username");
        row["Username"] = obj.Username;
        dataTable.Columns.Add("EarthRaidPoints");
        row["EarthRaidPoints"] = obj.EarthRaidPoints;
        dataTable.Columns.Add("EarthTimer");
        row["EarthTimer"] = obj.EarthTimer.ToString(CultureInfo.CurrentCulture);
        dataTable.Columns.Add("WindTimer");
        row["WindTimer"] = obj.WindTimer.ToString(CultureInfo.CurrentCulture);
        dataTable.Columns.Add("FireTimer");
        row["FireTimer"] = obj.FireTimer.ToString(CultureInfo.CurrentCulture);
        dataTable.Columns.Add("WaterTimer");
        row["WaterTimer"] = obj.WaterTimer.ToString(CultureInfo.CurrentCulture);
        dataTable.Columns.Add("SpiritTimer");
        row["SpiritTimer"] = obj.SpiritTimer.ToString(CultureInfo.CurrentCulture);
        dataTable.Columns.Add("MasterRaidProgress");
        row["MasterRaidProgress"] = obj.MasterRaidProgress;
        dataTable.Columns.Add("WindRaidPoints");
        row["WindRaidPoints"] = obj.WindRaidPoints;
        dataTable.Columns.Add("FireRaidPoints");
        row["FireRaidPoints"] = obj.FireRaidPoints;
        dataTable.Columns.Add("WaterRaidPoints");
        row["WaterRaidPoints"] = obj.WaterRaidPoints;
        dataTable.Columns.Add("SpiritRaidPoints");
        row["SpiritRaidPoints"] = obj.SpiritRaidPoints;
        #endregion rows
        dataTable.Rows.Add(row);
        return dataTable;
    }
}