using Darkages.Database;

namespace Darkages.Templates;

public class ClanTemplate : Template
{
    public string GuildName { get; set; }
    public string Primogen { get; set; }
    public string Primarch { get; set; }
    public int HallID { get; set; }
    public int Points { get; set; }
    public int HallX { get; set; }
    public int HallY { get; set; }
    public uint gold { get; set; }
    public List<string> Members { get; set; }
    public List<string> Council { get; set; }

    public override string[] GetMetaData() =>
    [
        ""
    ];

    public static ClanTemplate Create(string name, string primogen, string primarch)
    {
        var result = new ClanTemplate
        {
            Name = name,
            GuildName = name,
            Primogen = primogen,
            Primarch = primarch,
            gold = 0,
            Members = [],
            Council = [],
            Points = 0,
            HallID = 0,
            HallX = 0,
            HallY = 0,
        };
        result.Members.Add(primogen);
        ServerSetup.Instance.GlobalClanTemplateCache.Add(result.GuildName, result);
        StorageManager.ClanBucket.SaveClan(result, false);
        return result;
    }

    public static ClanTemplate AddMember(string clanname, string playername)
    {
        var result = ServerSetup.Instance.GlobalClanTemplateCache[clanname];
        result.Members.Add(playername);
        StorageManager.ClanBucket.SaveClan(result, true);
        ServerSetup.Instance.GlobalClanTemplateCache.Remove(clanname);
        ServerSetup.Instance.GlobalClanTemplateCache.Add(result.GuildName, result);
        return result;
    }
    public static ClanTemplate RemoveMember(string clanname, string playername)
    {

        var result = ServerSetup.Instance.GlobalClanTemplateCache[clanname];
        result.Members.Remove(playername);
        if (result.Council.Contains(playername))
            result.Council.Remove(playername);

        if (result.Primarch == playername)
            result.Primarch = string.Empty;

        StorageManager.ClanBucket.SaveClan(result, true);
        ServerSetup.Instance.GlobalClanTemplateCache.Remove(clanname);
        ServerSetup.Instance.GlobalClanTemplateCache.Add(result.GuildName, result);
        return result;

    }
    public static ClanTemplate AddCouncil (string clanname, string playername)
    {
        var result = ServerSetup.Instance.GlobalClanTemplateCache[clanname];
        result.Council.Add(playername);
        if (result.Primarch == playername)
            result.Primarch = string.Empty;

        StorageManager.ClanBucket.SaveClan(result, true);
        ServerSetup.Instance.GlobalClanTemplateCache.Remove(clanname);
        ServerSetup.Instance.GlobalClanTemplateCache.Add(result.GuildName, result);
        return result;
    }
    public static ClanTemplate RemoveCouncil (string clanname, string playername)
    {
        var result = ServerSetup.Instance.GlobalClanTemplateCache[clanname];
        result.Council.Remove(playername);
        StorageManager.ClanBucket.SaveClan(result, true);
        ServerSetup.Instance.GlobalClanTemplateCache.Remove(clanname);
        ServerSetup.Instance.GlobalClanTemplateCache.Add(result.GuildName, result);
        return result;
    }
    public static ClanTemplate RemovePrimarch (string clanname)
    {
        var result = ServerSetup.Instance.GlobalClanTemplateCache[clanname];
        result.Primarch = string.Empty;
        StorageManager.ClanBucket.SaveClan(result, true);
        ServerSetup.Instance.GlobalClanTemplateCache.Remove(clanname);
        ServerSetup.Instance.GlobalClanTemplateCache.Add(result.GuildName, result);
        return result;
    }
    public static ClanTemplate NewPrimogen (string clanname, string oldprimogen, string newprimogen)
    {
        var result = ServerSetup.Instance.GlobalClanTemplateCache[clanname];
        result.Primogen = newprimogen;
        result.Council.Add(oldprimogen);
        if (result.Council.Contains(newprimogen))
            result.Council.Remove(newprimogen);

        StorageManager.ClanBucket.SaveClan(result, true);
        ServerSetup.Instance.GlobalClanTemplateCache.Remove(clanname);
        ServerSetup.Instance.GlobalClanTemplateCache.Add(result.GuildName, result);
        return result;
    }
    public static ClanTemplate NewPrimarch (string clanname, string oldprimarch, string newprimarch)
    {
        var result = ServerSetup.Instance.GlobalClanTemplateCache[clanname];
        result.Primarch = newprimarch;
        result.Council.Add(oldprimarch);
        if (result.Council.Contains(newprimarch))
            result.Council.Remove(newprimarch);

        StorageManager.ClanBucket.SaveClan(result, true);
        ServerSetup.Instance.GlobalClanTemplateCache.Remove(clanname);
        ServerSetup.Instance.GlobalClanTemplateCache.Add(result.GuildName, result);
        return result;
    }
    public static ClanTemplate AdjustGold(string clanName, uint goldValue)
    {
        var result = ServerSetup.Instance.GlobalClanTemplateCache[clanName];
        result.gold = goldValue;
        StorageManager.ClanBucket.SaveClan(result, true);
        ServerSetup.Instance.GlobalClanTemplateCache.Remove(clanName);
        ServerSetup.Instance.GlobalClanTemplateCache.Add(result.GuildName, result);
        return result;
    }
        
    public static ClanTemplate Disband (string clanname, string primogen)
    {
        string directory = $@"{ServerSetup.Instance.StoragePath}\templates\Clans";
        var result = ServerSetup.Instance.GlobalClanTemplateCache[clanname];
        try 
        {
            ServerSetup.Instance.GlobalClanTemplateCache.Remove(clanname);
            string[] piclist = Directory.GetFiles(directory, $"{clanname}.json");
            foreach (string f in piclist)
                File.Delete(f);

            ServerSetup.Instance.LoadClanTemplates();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"{clanname} Clan has been disbanded by {primogen}.");
            Console.ForegroundColor = ConsoleColor.White;
        }
        catch
        {
            Console.WriteLine($"There was an error disbanding {clanname}.");
        }

        return result;
            
    }
}