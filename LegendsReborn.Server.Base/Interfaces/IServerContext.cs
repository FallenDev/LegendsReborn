﻿using Darkages.CommandSystem.CLI;
using Darkages.Network.Server;
using Darkages.Sprites;
using Darkages.Templates;
using Darkages.Types;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Net;

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
    Dictionary<int, WarpTemplate> GlobalWarpTemplateCache { get; set; }
    Dictionary<string, SkillTemplate> GlobalSkillTemplateCache { get; set; }
    Dictionary<string, SpellTemplate> GlobalSpellTemplateCache { get; set; }
    Dictionary<string, ItemTemplate> GlobalItemTemplateCache { get; set; }
    Dictionary<string, NationTemplate> GlobalNationTemplateCache { get; set; }
    Dictionary<string, MonsterTemplate> GlobalMonsterTemplateCache { get; set; }
    Dictionary<string, MundaneTemplate> GlobalMundaneTemplateCache { get; set; }
    Dictionary<uint, string> GlobalKnownGoodActorsCache { get; set; }
    Dictionary<int, Area> GlobalMapCache { get; set; }
    ConcurrentDictionary<string, Buff> GlobalBuffCache { get; set; }
    ConcurrentDictionary<string, Debuff> GlobalDeBuffCache { get; set; }
    ConcurrentDictionary<ushort, BoardTemplate> GlobalBoardPostCache { get; set; }
    ConcurrentDictionary<int, Party> GlobalGroupCache { get; set; }
    ConcurrentDictionary<uint, Mundane> GlobalMundaneCache { get; set; }
    ConcurrentDictionary<long, Item> GlobalSqlItemCache { get; set; }
    ConcurrentDictionary<long, Money> GlobalGroundMoneyCache { get; set; }
    ConcurrentDictionary<uint, Trap> Traps { get; set; }
    ConcurrentDictionary<long, ConcurrentDictionary<string, KillRecord>> GlobalKillRecordCache { get; set; }
    ConcurrentDictionary<IPAddress, IPAddress> GlobalLobbyConnection { get; set; }
    ConcurrentDictionary<IPAddress, IPAddress> GlobalLoginConnection { get; set; }
    ConcurrentDictionary<IPAddress, IPAddress> GlobalWorldConnection { get; set; }
    ConcurrentDictionary<IPAddress, byte> GlobalCreationCount { get; set; }
    bool Running { get; set; }
    SqlConnection ServerSaveConnection { get; set; }
    IServerConstants Config { get; set; }
    WorldServer Game { get; set; }
    LoginServer LoginServer { get; set; }
    LobbyServer LobbyServer { get; set; }
    public CommandParser Parser { get; set; }
    public string StoragePath { get; set; }
    public string MoonPhase { get; set; }
    public byte LightPhase { get; set; }
    public byte LightLevel { get; set; }
    public string KeyCode { get; set; }
    public string Unlock { get; set; }
    public IPAddress IpAddress { get; set; }
    public string InternalAddress { get; set; }
}