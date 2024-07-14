using Darkages.CommandSystem.CLI;
using Darkages.Network.Server;
using Darkages.Templates;
using Darkages.Types;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

using System.Collections.Concurrent;
using System.Net;
using Darkages.Meta;
using Darkages.Types.Buffs;
using Legends.Server.Base.Types.Debuffs;

namespace Darkages.Interfaces;

public interface IServerContext
{
    void InitFromConfig(string storagePath, string serverIp);
    void Start(IServerConstants config, ILogger<ServerSetup> logger);
    void Startup();
    void LoadAndCacheStorage();
    void BindTemplates();
    void LoadExtensions();
    void CacheBuffs();
    void CacheDebuffs();
    void CommandHandler();
    void DatabaseSaveConnection();
    void SetGoodActors();
    Dictionary<int, WorldMapTemplate> GlobalWorldMapTemplateCache { get; set; }
    List<WarpTemplate> GlobalWarpTemplateCache { get; set; }
    List<PopupTemplate> GlobalPopupCache { get; set; }
    Dictionary<string, SkillTemplate> GlobalSkillTemplateCache { get; set; }
    Dictionary<string, SpellTemplate> GlobalSpellTemplateCache { get; set; }
    Dictionary<string, ItemTemplate> GlobalItemTemplateCache { get; set; }
    Dictionary<string, NationTemplate> GlobalNationTemplateCache { get; set; }
    Dictionary<string, MonsterTemplate> GlobalMonsterTemplateCache { get; set; }
    Dictionary<string, MundaneTemplate> GlobalMundaneTemplateCache { get; set; }
    Dictionary<string, ClanTemplate> GlobalClanTemplateCache { get; set; }
    Dictionary<string, ParcelTemplate> GlobalParcelTemplateCache { get; set; }
    Dictionary<string, Reactor> GlobalReactorCache { get; set; }
    Dictionary<uint, string> GlobalKnownGoodActorsCache { get; set; }
    Dictionary<int, Area> GlobalMapCache { get; set; }
    ConcurrentDictionary<string, BuffBase> GlobalBuffCache { get; set; }
    ConcurrentDictionary<string, DebuffBase> GlobalDeBuffCache { get; set; }
    ConcurrentDictionary<string, BoardTemplate> GlobalBoardCache { get; set; }
    ConcurrentDictionary<int, Party> GlobalGroupCache { get; set; }
    ConcurrentDictionary<IPAddress, IPAddress> GlobalLobbyConnection { get; set; }
    ConcurrentDictionary<IPAddress, IPAddress> GlobalLoginConnection { get; set; }
    ConcurrentDictionary<IPAddress, IPAddress> GlobalWorldConnection { get; set; }
    ConcurrentDictionary<IPAddress, byte> GlobalCreationCount { get; set; }
    List<Metafile> GlobalMetaCache { get; set; }
    bool Running { get; set; }
    bool Paused { get; set; }
    SqlConnection ServerSaveConnection { get; set; }
    IServerConstants Config { get; set; }
    WorldServer Game { get; set; }
    LoginServer LoginServer { get; set; }
    LobbyServer LobbyServer { get; set; }
    public CommandParser Parser { get; set; }
    public CommandParser PlayerParser { get; set; }
    public string StoragePath { get; set; }
    public string MoonPhase { get; set; }
    public byte LightPhase { get; set; }
    public byte LightLevel { get; set; }
    public string KeyCode { get; set; }
    public string Unlock { get; set; }
    public IPAddress IpAddress { get; set; }
    public string InternalAddress { get; set; }
}