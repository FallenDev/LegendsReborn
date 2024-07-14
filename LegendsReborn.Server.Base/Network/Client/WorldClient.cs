using Chaos.Common.Definitions;
using Chaos.Common.Identity;
using Chaos.Cryptography.Abstractions;
using Chaos.Extensions.Networking;
using Chaos.Geometry;
using Chaos.Geometry.Abstractions.Definitions;
using Chaos.Networking.Abstractions;
using Chaos.Networking.Entities.Server;
using Chaos.Packets;
using Chaos.Packets.Abstractions;
using Chaos.Packets.Abstractions.Definitions;

using Dapper;

using Darkages.Common;
using Darkages.Database;
using Darkages.Enums;
using Darkages.Events;
using Darkages.Meta;
using Darkages.Models;
using Darkages.Network.Client.Abstractions;
using Darkages.Network.Server;
using Darkages.Object;
using Darkages.ScriptingBase;
using Darkages.Sprites;
using Darkages.Templates;
using Darkages.Types;

using JetBrains.Annotations;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

using ServiceStack;

using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using System.Numerics;
using Darkages.Managers;
using BodyColor = Chaos.Common.Definitions.BodyColor;
using BodySprite = Chaos.Common.Definitions.BodySprite;
using EquipmentSlot = Chaos.Common.Definitions.EquipmentSlot;
using Gender = Chaos.Common.Definitions.Gender;
using LanternSize = Chaos.Common.Definitions.LanternSize;
using MapFlags = Darkages.Enums.MapFlags;
using Nation = Chaos.Common.Definitions.Nation;
using RestPosition = Chaos.Common.Definitions.RestPosition;
using Darkages.Interfaces;
using static System.Formats.Asn1.AsnWriter;
using System.Linq;

namespace Darkages.Network.Client;

[UsedImplicitly]
public class WorldClient : SocketClientBase, IWorldClient
{
    private readonly IWorldServer<WorldClient> _server;
    public readonly WorldServerTimer SkillSpellTimer = new(TimeSpan.FromMilliseconds(1000));
    public readonly Stopwatch SkillControl = new();
    public readonly Stopwatch SpellControl = new();
    private readonly Stopwatch _afflictionControl = new();
    public Spell LastSpell = new();
    public readonly Stopwatch StatusControl = new();
    private readonly Stopwatch _aggroMessageControl = new();
    private readonly Stopwatch _lanternControl = new();
    private readonly Stopwatch _dayDreamingControl = new();
    private readonly Stopwatch _mailManControl = new();
    private readonly Stopwatch _itemAnimationControl = new();
    private readonly WorldServerTimer _lanternCheckTimer = new(TimeSpan.FromSeconds(2));
    private readonly WorldServerTimer _aggroTimer = new(TimeSpan.FromSeconds(20));
    private readonly WorldServerTimer _dayDreamingTimer = new(TimeSpan.FromSeconds(5));
    private readonly WorldServerTimer _itemAnimationTimer = new(TimeSpan.FromMilliseconds(100));
    private readonly WorldServerTimer _mailManTimer = new(TimeSpan.FromMilliseconds(30000));
    public readonly object SyncModifierRemovalLock = new();
    public bool ExitConfirmed;
    private static readonly SortedDictionary<long, string> AggroColors = new()
    {
        {100, "b"},
        {90, "s"},
        {75, "c"},
        {25, "g"}
    };

    public Aisling Aisling { get; set; }
    public bool MapUpdating { get; set; }
    public bool MapOpen { get; set; }
    private SemaphoreSlim LoadLock { get; } = new(1, 1);
    public DateTime BoardOpened { get; set; }
    public DialogSession DlgSession { get; set; }
    private List<LegendMarkInfo> _legendMarksPublic = [];
    private List<LegendMarkInfo> _legendMarksPrivate = [];

    public bool CanSendLocation
    {
        get
        {
            var readyTime = DateTime.UtcNow;
            return readyTime - LastLocationSent < new TimeSpan(0, 0, 0, 2);
        }
    }

    public bool IsRefreshing
    {
        get
        {
            var readyTime = DateTime.UtcNow;
            return readyTime - LastClientRefresh < new TimeSpan(0, 0, 0, 0, ServerSetup.Instance.Config.RefreshRate);
        }
    }

    public bool CanRefresh
    {
        get
        {
            var readyTime = DateTime.UtcNow;
            return readyTime - LastClientRefresh > new TimeSpan(0, 0, 0, 0, 100);
        }
    }

    public bool IsEquipping
    {
        get
        {
            var readyTime = DateTime.UtcNow;
            return readyTime - LastEquip > new TimeSpan(0, 0, 0, 0, 200);
        }
    }

    public bool IsDayDreaming
    {
        get
        {
            var readyTime = DateTime.UtcNow;
            return readyTime - LastMovement > new TimeSpan(0, 0, 2, 0, 0);
        }
    }

    public bool IsMoving
    {
        get
        {
            var readyTime = DateTime.UtcNow;
            return readyTime - LastMovement > new TimeSpan(0, 0, 0, 0, 850);
        }
    }

    public bool IsWarping
    {
        get
        {
            var readyTime = DateTime.UtcNow;
            return readyTime - LastWarp < new TimeSpan(0, 0, 0, 0, ServerSetup.Instance.Config.WarpCheckRate);
        }
    }

    public bool WasUpdatingMapRecently
    {
        get
        {
            var readyTime = DateTime.UtcNow;
            return readyTime - LastMapUpdated < new TimeSpan(0, 0, 0, 0, 100);
        }
    }

    public CastInfo SpellCastInfo { get; set; }
    public DateTime LastAssail { get; set; }
    public DateTime LastSpellCast { get; set; }
    public DateTime LastSelfProfileRequest { get; set; }
    public DateTime LastItemUsed { get; set; }
    public DateTime LastWorldListRequest { get; set; }
    public DateTime LastClientRefresh { get; set; }
    public DateTime LastWarp { get; set; }
    public Area LastMap { get; set; }
    public Item LastItemDropped { get; set; }
    public DateTime LastLocationSent { get; set; }
    public DateTime LastMapUpdated { get; set; }
    public DateTime LastMessageSent { get; set; }
    public DateTime LastMovement { get; set; }
    public DateTime LastEquip { get; set; }
    public Stopwatch Latency { get; set; } = new();
    public DateTime LastSave { get; set; }
    public DateTime LastWhisperMessageSent { get; set; }
    public PendingBuy PendingBuySessions { get; set; }
    public PendingSell PendingItemSessions { get; set; }
    public bool ShouldUpdateMap { get; set; }
    public DateTime LastNodeClicked { get; set; }
    public WorldPortal PendingNode { get; set; }
    public Position LastKnownPosition { get; set; }
    public int MapClicks { get; set; }
    public uint EntryCheck { get; set; }
    private readonly object _warpCheckLock = new();
    private readonly Queue<ExperienceEvent> _expQueue = [];
    private readonly Queue<AbilityEvent> _apQueue = [];
    private readonly Queue<DebuffEvent> _debuffApplyQueue = [];
    private readonly Queue<BuffEvent> _buffApplyQueue = [];
    private readonly Queue<DebuffEvent> _debuffUpdateQueue = [];
    private readonly Queue<BuffEvent> _buffUpdateQueue = [];
    private readonly object _expQueueLock = new();
    private readonly object _apQueueLock = new();
    private readonly object _buffQueueLockApply = new();
    private readonly object _debuffQueueLockApply = new();
    private readonly object _buffQueueLockUpdate = new();
    private readonly object _debuffQueueLockUpdate = new();
    private readonly Task _experienceTask;
    private readonly Task _apTask;
    private readonly Task _applyBuffTask;
    private readonly Task _applyDebuffTask;
    private readonly Task _updateBuffTask;
    private readonly Task _updateDebuffTask;

    public WorldClient([NotNull] IWorldServer<WorldClient> server, [NotNull] Socket socket,
        [NotNull] ICrypto crypto, [NotNull] IPacketSerializer packetSerializer,
        [NotNull] ILogger<SocketClientBase> logger) : base(socket, crypto, packetSerializer, logger)
    {
        _server = server;

        // Event-Driven Tasks
        _experienceTask = Task.Run(ProcessExperienceEvents);
        _apTask = Task.Run(ProcessAbilityEvents);
        _applyBuffTask = Task.Run(ProcessApplyingBuffsEvents);
        _applyDebuffTask = Task.Run(ProcessApplyingDebuffsEvents);
        _updateBuffTask = Task.Run(ProcessUpdatingBuffsEvents);
        _updateDebuffTask = Task.Run(ProcessUpdatingDebuffsEvents);
    }

    public void Update()
    {
        if (Aisling is not { LoggedIn: true }) return;
        CheckDayDreaming();
        CheckForMail();
        HandleBadTrades();
    }

    #region Events

    private void ProcessExperienceEvents()
    {
        while (ServerSetup.Instance.Running)
        {
            ExperienceEvent? expEvent = null;

            lock (_expQueueLock)
            {
                if (_expQueue.Count > 0)
                {
                    expEvent = _expQueue.Dequeue();
                }
            }

            if (expEvent.HasValue)
            {
                HandleExp(expEvent.Value.Player, expEvent.Value.Exp, expEvent.Value.Hunting);
            }
            else
            {
                Task.Delay(50).Wait(); // Delay to avoid busy-waiting
            }
        }
    }

    private void ProcessAbilityEvents()
    {
        while (ServerSetup.Instance.Running)
        {
            AbilityEvent? apEvent = null;

            lock (_apQueueLock)
            {
                if (_apQueue.Count > 0)
                {
                    apEvent = _apQueue.Dequeue();
                }
            }

            if (apEvent.HasValue)
            {
                HandleAp(apEvent.Value.Player, apEvent.Value.Exp, apEvent.Value.Hunting);
            }
            else
            {
                Task.Delay(50).Wait(); // Delay to avoid busy-waiting
            }
        }
    }

    private void ProcessApplyingDebuffsEvents()
    {
        while (ServerSetup.Instance.Running)
        {
            DebuffEvent? debuffEvent = null;

            lock (_debuffQueueLockApply)
            {
                if (_debuffApplyQueue.Count > 0)
                {
                    debuffEvent = _debuffApplyQueue.Dequeue();
                }
            }

            if (debuffEvent.HasValue)
            {
                debuffEvent.Value.Debuff.OnApplied(debuffEvent.Value.Affected, debuffEvent.Value.Debuff);
            }
            else
            {
                Task.Delay(50).Wait(); // Delay to avoid busy-waiting
            }
        }
    }

    private void ProcessApplyingBuffsEvents()
    {
        while (ServerSetup.Instance.Running)
        {
            BuffEvent? buffEvent = null;

            lock (_buffQueueLockApply)
            {
                if (_buffApplyQueue.Count > 0)
                {
                    buffEvent = _buffApplyQueue.Dequeue();
                }
            }

            if (buffEvent.HasValue)
            {
                buffEvent.Value.Buff.OnApplied(buffEvent.Value.Affected, buffEvent.Value.Buff);
            }
            else
            {
                Task.Delay(50).Wait(); // Delay to avoid busy-waiting
            }
        }
    }

    private void ProcessUpdatingDebuffsEvents()
    {
        while (ServerSetup.Instance.Running)
        {
            DebuffEvent? debuffEvent = null;

            lock (_debuffQueueLockUpdate)
            {
                if (_debuffUpdateQueue.Count > 0)
                {
                    debuffEvent = _debuffUpdateQueue.Dequeue();
                }
            }

            if (debuffEvent.HasValue)
            {
                debuffEvent.Value.Debuff.Update(debuffEvent.Value.Affected, debuffEvent.Value.TimeLeft);
            }
            else
            {
                Task.Delay(50).Wait(); // Delay to avoid busy-waiting
            }
        }
    }

    private void ProcessUpdatingBuffsEvents()
    {
        while (ServerSetup.Instance.Running)
        {
            BuffEvent? buffEvent = null;

            lock (_buffQueueLockUpdate)
            {
                if (_buffUpdateQueue.Count > 0)
                {
                    buffEvent = _buffUpdateQueue.Dequeue();
                }
            }

            if (buffEvent.HasValue)
            {
                buffEvent.Value.Buff.Update(buffEvent.Value.Affected, buffEvent.Value.TimeLeft);
            }
            else
            {
                Task.Delay(50).Wait(); // Delay to avoid busy-waiting
            }
        }
    }

    #endregion

    public void EquipLantern()
    {
        if (!_lanternControl.IsRunning)
            _lanternControl.Start();

        if (_lanternControl.Elapsed.TotalMilliseconds < _lanternCheckTimer.Delay.TotalMilliseconds) return;
        _lanternControl.Restart();
        if (Aisling.Map == null) return;
        if (Aisling.Map.Flags.MapFlagIsSet(MapFlags.Darkness))
        {
            if (Aisling.Lantern == 2) return;
            Aisling.Lantern = 2;
            SendDisplayAisling(Aisling);
            return;
        }

        if (Aisling.Lantern != 2) return;
        Aisling.Lantern = 0;
        SendDisplayAisling(Aisling);
    }

    public void CheckDayDreaming()
    {
        // Logic based on player's set ActiveStatus
        switch (Aisling.ActiveStatus)
        {
            case ActivityStatus.Awake:
            case ActivityStatus.NeedGroup:
            case ActivityStatus.LoneHunter:
            case ActivityStatus.Grouped:
            case ActivityStatus.GroupHunter:
            case ActivityStatus.DoNotDisturb:
                break;
            case ActivityStatus.DayDreaming:
            case ActivityStatus.NeedHelp:
                DaydreamingRoutine();
                break;
        }
    }

    public void CheckForMail()
    {
        if (!_mailManControl.IsRunning)
            _mailManControl.Start();

        if (_mailManControl.Elapsed.TotalMilliseconds < _mailManTimer.Delay.TotalMilliseconds) return;
        _mailManControl.Restart();

        BoardPostStorage.MailFromDatabase(this);
        SendAttributes(StatUpdateType.Secondary);
    }

    public void HandleBadTrades()
    {
        if (Aisling.Exchange?.Trader == null) return;
        if (Aisling.Exchange.Trader.LoggedIn && Aisling.WithinRangeOf(Aisling.Exchange.Trader)) return;
        Aisling.CancelExchange();
    }

    public void DeathStatusCheck()
    {
        var proceed = false;

        if (Aisling.CurrentHp <= 0)
        {
            Aisling.CurrentHp = 1;
            proceed = true;
        }

        if (!proceed) return;
        SendAttributes(StatUpdateType.Vitality);

        if (Aisling.Map.Flags.MapFlagIsSet(MapFlags.PlayerKill))
        {
            for (var i = 0; i < 2; i++)
                Aisling.RemoveBuffsAndDebuffs();

            Aisling.CastDeath();
            var target = Aisling.Target;

            if (target != null)
            {
                if (target is Aisling aisling)
                    aisling.SendTargetedClientMethod(PlayerScope.AislingsOnSameMap, c => c.SendServerMessage(ServerMessageType.ActiveMessage, $"{Aisling.Username} has been killed by {aisling.Username}."));
            }
            else
            {
                Aisling.SendTargetedClientMethod(PlayerScope.AislingsOnSameMap, c => c.SendServerMessage(ServerMessageType.ActiveMessage, $"{Aisling.Username} has died."));
            }

            return;
        }

        if (Aisling.CurrentMapId == ServerSetup.Instance.Config.DeathMap || Aisling.Map.Flags.MapFlagIsSet(MapFlags.SafeMap)) return;
        if (Aisling.Skulled) return;

        var debuff = new DebuffReaping();
        EnqueueDebuffAppliedEvent(Aisling, debuff, TimeSpan.FromSeconds(debuff.Length));
    }

    #region Player Load

    public async Task<WorldClient> Load()
    {
        if (Aisling == null || Aisling.AreaId == 0) return null;
        if (!ServerSetup.Instance.GlobalMapCache.ContainsKey(Aisling.AreaId)) return null;
        Aisling.Client = this;
        await using var loadConnection = new SqlConnection(AislingStorage.ConnectionString);
        await LoadLock.WaitAsync().ConfigureAwait(false);

        try
        {
            loadConnection.Open();
            SetAislingStartupVariables();
            SendUserId();
            SendProfileRequest();
            InitCombos();
            InitQuests();
            LoadEquipment(loadConnection).LoadInventory(loadConnection).LoadBank(loadConnection).InitSpellBar().InitDiscoveredMaps().InitIgnoreList().InitLegend();
            SendDisplayAisling(Aisling);
            Enter();
            if (Aisling.Username == "Death")
                Aisling.SendTargetedClientMethod(PlayerScope.NearbyAislings, c => c.SendAnimation(391, Aisling.Position));
        }
        catch (Exception ex)
        {
            ServerSetup.EventsLogger($"Unhandled Exception in {nameof(Load)}.");
            ServerSetup.EventsLogger(ex.Message, LogLevel.Error);
            ServerSetup.EventsLogger(ex.StackTrace, LogLevel.Error);

            LoadLock.Release();
            Disconnect();
            return null;
        }
        finally
        {
            LoadLock.Release();
            loadConnection.Close();
        }

        SendHeartBeat(0x14, 0x20);
        return this;
    }

    private void SetAislingStartupVariables()
    {
        var readyTime = DateTime.UtcNow;
        LastSave = readyTime;
        PendingItemSessions = null;
        LastLocationSent = readyTime;
        LastMovement = readyTime;
        LastClientRefresh = readyTime;
        LastMessageSent = readyTime;
        BoardOpened = readyTime;
        Aisling.Client = this;
        Aisling.BonusAc = 0;
        Aisling.Exchange = null;
        Aisling.LastMapId = ushort.MaxValue;
        Aisling.Aegis = 0;
        Aisling.Bleeding = 0;
        Aisling.Rending = 0;
        Aisling.Spikes = 0;
        Aisling.Reaping = 0;
        Aisling.Vampirism = 0;
        Aisling.Haste = 0;
        Aisling.Gust = 0;
        Aisling.Quake = 0;
        Aisling.Rain = 0;
        Aisling.Flame = 0;
        Aisling.Dusk = 0;
        Aisling.Dawn = 0;
        Aisling.Hacked = false;
        Aisling.PasswordAttempts = 0;
        Aisling.MonsterKillCounters = [];
        ReapplyKillCount();
        Aisling.Loading = true;
    }

    public WorldClient LoadEquipment(SqlConnection sConn)
    {
        try
        {
            const string procedure = "[SelectEquipped]";
            var values = new { Serial = (long)Aisling.Serial };
            var itemList = sConn.Query<Item>(procedure, values, commandType: CommandType.StoredProcedure).ToList();
            var aislingEquipped = Aisling.EquipmentManager.Equipment;

            foreach (var item in itemList.Where(s => s is { Name: not null }))
            {
                if (!ServerSetup.Instance.GlobalItemTemplateCache.ContainsKey(item.Name)) continue;

                var itemName = item.Name;
                var template = ServerSetup.Instance.GlobalItemTemplateCache[itemName];
                {
                    item.Template = template;
                }

                var color = (byte)ItemColors.ItemColorsToInt(item.Template.Color);

                var newGear = new Item
                {
                    ItemId = item.ItemId,
                    Template = item.Template,
                    Serial = item.Serial,
                    ItemPane = item.ItemPane,
                    Slot = item.Slot,
                    InventorySlot = item.InventorySlot,
                    Color = color,
                    Durability = item.Durability,
                    Identified = item.Identified,
                    ItemVariance = item.ItemVariance,
                    WeapVariance = item.WeapVariance,
                    ItemQuality = item.ItemQuality,
                    OriginalQuality = item.OriginalQuality,
                    Stacks = item.Stacks,
                    Enchantable = item.Template.Enchantable,
                    Tarnished = item.Tarnished,
                    GearEnhancement = item.GearEnhancement,
                    ItemMaterial = item.ItemMaterial,
                    Image = item.Template.Image,
                    DisplayImage = item.Template.DisplayImage
                };

                newGear.GetDisplayName();
                aislingEquipped[newGear.Slot] = new Models.EquipmentSlot(newGear.Slot, newGear);
            }
        }
        catch (Exception e)
        {
            ServerSetup.EventsLogger(e.Message, LogLevel.Error);
            ServerSetup.EventsLogger(e.StackTrace, LogLevel.Error);
        }

        LoadSkillBook();
        LoadSpellBook();
        EquipGearAndAttachScripts();
        return this;
    }

    public WorldClient LoadInventory(SqlConnection sConn)
    {
        try
        {
            const string procedure = "[SelectInventory]";
            var values = new { Serial = (long)Aisling.Serial };
            var itemList = sConn.Query<Item>(procedure, values, commandType: CommandType.StoredProcedure).OrderBy(s => s.InventorySlot);

            foreach (var item in itemList)
            {
                if (!ServerSetup.Instance.GlobalItemTemplateCache.ContainsKey(item.Name)) continue;
                if (item.InventorySlot is <= 0 or >= 60)
                    item.InventorySlot = Aisling.Inventory.FindEmpty();

                var itemName = item.Name;
                var template = ServerSetup.Instance.GlobalItemTemplateCache[itemName];
                {
                    item.Template = template;
                }

                var color = (byte)ItemColors.ItemColorsToInt(item.Template.Color);

                var newItem = new Item
                {
                    ItemId = item.ItemId,
                    Template = item.Template,
                    Serial = item.Serial,
                    ItemPane = item.ItemPane,
                    Slot = item.Slot,
                    InventorySlot = item.InventorySlot,
                    Color = color,
                    Durability = item.Durability,
                    Identified = item.Identified,
                    ItemVariance = item.ItemVariance,
                    WeapVariance = item.WeapVariance,
                    ItemQuality = item.ItemQuality,
                    OriginalQuality = item.OriginalQuality,
                    Stacks = item.Stacks,
                    Enchantable = item.Template.Enchantable,
                    Tarnished = item.Tarnished,
                    GearEnhancement = item.GearEnhancement,
                    ItemMaterial = item.ItemMaterial,
                    Image = item.Template.Image,
                    DisplayImage = item.Template.DisplayImage
                };

                if (Aisling.Inventory.Items[newItem.InventorySlot] != null)
                {
                    var routineCheck = 0;

                    for (byte i = 1; i < 60; i++)
                    {
                        if (Aisling.Inventory.Items[i] is null)
                        {
                            newItem.InventorySlot = i;
                            break;
                        }

                        if (i == 59)
                            routineCheck++;

                        if (routineCheck <= 4) continue;
                        ServerSetup.EventsLogger($"{Aisling.Username} has somehow exceeded their inventory, and have hanging items.");
                        Disconnect();
                        break;
                    }
                }

                newItem.GetDisplayName();
                Aisling.Inventory.Items[newItem.InventorySlot] = newItem;
            }
        }
        catch (Exception e)
        {
            ServerSetup.EventsLogger(e.Message, LogLevel.Error);
            ServerSetup.EventsLogger(e.StackTrace, LogLevel.Error);
        }

        var itemsAvailable = Aisling.Inventory.Items.Values;

        foreach (var item in itemsAvailable)
        {
            if (item == null) continue;
            if (string.IsNullOrEmpty(item.Template.Name)) continue;

            Aisling.CurrentWeight += item.Template.CarryWeight;
            Aisling.Inventory.Items.TryUpdate(item.InventorySlot, item, null);
            Aisling.Inventory.UpdateSlot(Aisling.Client, item);
            item.Scripts = ScriptManager.Load<ItemScript>(item.Template.ScriptName, item);

            if (!string.IsNullOrEmpty(item.Template.WeaponScript))
                item.WeaponScripts = ScriptManager.Load<WeaponScript>(item.Template.WeaponScript, item);
        }

        return this;
    }

    public WorldClient LoadBank(SqlConnection sConn)
    {
        Aisling.BankManager = new BankManager();

        try
        {
            const string procedure = "[SelectBanked]";
            var values = new { Serial = (long)Aisling.Serial };
            var itemList = sConn.Query<Item>(procedure, values, commandType: CommandType.StoredProcedure).ToList();

            foreach (var item in itemList)
            {
                if (!ServerSetup.Instance.GlobalItemTemplateCache.ContainsKey(item.Name)) continue;

                var itemName = item.Name;
                var template = ServerSetup.Instance.GlobalItemTemplateCache[itemName];
                {
                    item.Template = template;
                }

                var color = (byte)ItemColors.ItemColorsToInt(item.Template.Color);

                var newItem = new Item
                {
                    ItemId = item.ItemId,
                    Template = item.Template,
                    Serial = item.Serial,
                    ItemPane = item.ItemPane,
                    Slot = item.Slot,
                    InventorySlot = item.InventorySlot,
                    Color = color,
                    Durability = item.Durability,
                    Identified = item.Identified,
                    ItemVariance = item.ItemVariance,
                    WeapVariance = item.WeapVariance,
                    ItemQuality = item.ItemQuality,
                    OriginalQuality = item.OriginalQuality,
                    Stacks = item.Stacks,
                    Enchantable = item.Template.Enchantable,
                    Tarnished = item.Tarnished,
                    GearEnhancement = item.GearEnhancement,
                    ItemMaterial = item.ItemMaterial,
                    Image = item.Template.Image,
                    DisplayImage = item.Template.DisplayImage
                };

                newItem.GetDisplayName();
                Aisling.BankManager.Items[newItem.ItemId] = newItem;
            }
        }
        catch (Exception e)
        {
            ServerSetup.EventsLogger(e.Message, LogLevel.Error);
            ServerSetup.EventsLogger(e.StackTrace, LogLevel.Error);
        }

        return this;
    }

    public void LoadSkillBook()
    {
        try
        {
            const string procedure = "[SelectSkills]";
            var values = new { Serial = (long)Aisling.Serial };
            using var sConn = new SqlConnection(AislingStorage.ConnectionString);
            sConn.Open();
            var skillList = sConn.Query<Skill>(procedure, values, commandType: CommandType.StoredProcedure).ToList();

            foreach (var skill in skillList.Where(s => s is { SkillName: not null }))
            {
                if (!ServerSetup.Instance.GlobalSkillTemplateCache.ContainsKey(skill.SkillName)) continue;

                var skillName = skill.SkillName;
                var template = ServerSetup.Instance.GlobalSkillTemplateCache[skillName];
                {
                    skill.Template = template;
                }

                var newSkill = new Skill
                {
                    Icon = skill.Template.Icon,
                    Level = skill.Level,
                    Slot = skill.Slot,
                    SkillName = skill.SkillName,
                    Uses = skill.Uses,
                    CurrentCooldown = skill.CurrentCooldown,
                    Template = skill.Template
                };

                Aisling.SkillBook.Skills[skill.Slot] = newSkill;
            }

            sConn.Close();
        }
        catch (Exception e)
        {
            ServerSetup.EventsLogger(e.Message, LogLevel.Error);
            ServerSetup.EventsLogger(e.StackTrace, LogLevel.Error);
        }

        SkillCleanup();
    }

    public void LoadSpellBook()
    {
        try
        {
            const string procedure = "[SelectSpells]";
            var values = new { Serial = (long)Aisling.Serial };
            using var sConn = new SqlConnection(AislingStorage.ConnectionString);
            sConn.Open();
            var spellList = sConn.Query<Spell>(procedure, values, commandType: CommandType.StoredProcedure).ToList();

            foreach (var spell in spellList.Where(s => s is { SpellName: not null }))
            {
                if (!ServerSetup.Instance.GlobalSpellTemplateCache.ContainsKey(spell.SpellName)) continue;

                var spellName = spell.SpellName;
                var template = ServerSetup.Instance.GlobalSpellTemplateCache[spellName];
                {
                    spell.Template = template;
                }

                var newSpell = new Spell()
                {
                    Icon = spell.Template.Icon,
                    Level = spell.Level,
                    Slot = spell.Slot,
                    SpellName = spell.SpellName,
                    Casts = spell.Casts,
                    CurrentCooldown = spell.CurrentCooldown,
                    Template = spell.Template
                };

                Aisling.SpellBook.Spells[spell.Slot] = newSpell;
            }

            sConn.Close();
        }
        catch (Exception e)
        {
            ServerSetup.EventsLogger(e.Message, LogLevel.Error);
            ServerSetup.EventsLogger(e.StackTrace, LogLevel.Error);
        }

        SpellCleanup();
    }

    private WorldClient InitSpellBar()
    {
        return InitBuffs()
            .InitDeBuffs();
    }

    private WorldClient InitBuffs()
    {
        try
        {
            const string procedure = "[SelectBuffs]";
            var values = new { Serial = (long)Aisling.Serial };
            using var sConn = new SqlConnection(AislingStorage.ConnectionString);
            sConn.Open();
            var buffs = sConn.Query<Buff>(procedure, values, commandType: CommandType.StoredProcedure).ToList();
            var orderedBuffs = buffs.OrderBy(b => b.TimeLeft);

            foreach (var buffDb in orderedBuffs.Where(s => s is { Name: not null }))
            {
                var buffCheck = false;
                Buff buffFromCache = null;

                foreach (var buffInCache in ServerSetup.Instance.GlobalBuffCache.Values.Where(buffCache =>
                             buffCache.Name == buffDb.Name))
                {
                    buffCheck = true;
                    buffFromCache = buffInCache;
                }

                if (!buffCheck) continue;
                // Set script to Buff
                var buff = buffDb.ObtainBuffName(Aisling, buffFromCache);
                buff.Icon = buffFromCache.Icon;
                buff.Name = buffDb.Name;
                buff.Cancelled = buffFromCache.Cancelled;
                buff.Length = buffFromCache.Length;
                // Apply Buff on login - Use direct call, so we can set the db TimeLeft
                buff.OnApplied(Aisling, buff);
                // Set Time left
                buff.TimeLeft = buffDb.TimeLeft;
            }

            sConn.Close();
        }
        catch (Exception e)
        {
            ServerSetup.EventsLogger(e.Message, LogLevel.Error);
            ServerSetup.EventsLogger(e.StackTrace, LogLevel.Error);
        }

        return this;
    }

    private WorldClient InitDeBuffs()
    {
        try
        {
            const string procedure = "[SelectDeBuffs]";
            var values = new { Serial = (long)Aisling.Serial };
            using var sConn = new SqlConnection(AislingStorage.ConnectionString);
            sConn.Open();
            var deBuffs = sConn.Query<Debuff>(procedure, values, commandType: CommandType.StoredProcedure).ToList();
            var orderedDebuffs = deBuffs.OrderBy(d => d.TimeLeft);

            foreach (var deBuffDb in orderedDebuffs.Where(s => s is { Name: not null }))
            {
                var debuffCheck = false;
                Debuff debuffFromCache = null;

                foreach (var debuffInCache in ServerSetup.Instance.GlobalDeBuffCache.Values.Where(debuffCache => debuffCache.Name == deBuffDb.Name))
                {
                    debuffCheck = true;
                    debuffFromCache = debuffInCache;
                }

                if (!debuffCheck) continue;
                // Set script to Debuff
                var debuff = deBuffDb.ObtainDebuffName(Aisling, debuffFromCache);
                debuff.Icon = debuffFromCache.Icon;
                debuff.Name = deBuffDb.Name;
                debuff.Cancelled = debuffFromCache.Cancelled;
                debuff.Length = debuffFromCache.Length;
                // Apply Debuff on login - Use direct call, so we can set the db TimeLeft
                debuff.OnApplied(Aisling, debuff);
                // Set Time left
                debuff.TimeLeft = deBuffDb.TimeLeft;
            }

            sConn.Close();
        }
        catch (Exception e)
        {
            ServerSetup.EventsLogger(e.Message, LogLevel.Error);
            ServerSetup.EventsLogger(e.StackTrace, LogLevel.Error);
        }

        return this;
    }

    private WorldClient InitDiscoveredMaps()
    {
        try
        {
            const string procedure = "[SelectDiscoveredMaps]";
            var values = new { Serial = (long)Aisling.Serial };
            using var sConn = new SqlConnection(AislingStorage.ConnectionString);
            sConn.Open();
            var discovered = sConn.Query<DiscoveredMap>(procedure, values, commandType: CommandType.StoredProcedure).ToList();

            foreach (var map in discovered.Where(s => s is not null))
            {
                var temp = new DiscoveredMap()
                {
                    Serial = map.Serial,
                    MapId = map.MapId
                };

                Aisling.DiscoveredMaps.Add(temp.MapId);
            }

            sConn.Close();
        }
        catch (Exception e)
        {
            ServerSetup.EventsLogger(e.Message, LogLevel.Error);
            ServerSetup.EventsLogger(e.StackTrace, LogLevel.Error);
        }

        return this;
    }

    private WorldClient InitIgnoreList()
    {
        try
        {
            const string procedure = "[SelectIgnoredPlayers]";
            var values = new { Serial = (long)Aisling.Serial };
            using var sConn = new SqlConnection(AislingStorage.ConnectionString);
            sConn.Open();
            var ignoredRecords = sConn.Query<IgnoredRecord>(procedure, values, commandType: CommandType.StoredProcedure).ToList();

            foreach (var ignored in ignoredRecords.Where(s => s is not null))
            {
                if (ignored.PlayerIgnored is null) continue;

                var temp = new IgnoredRecord()
                {
                    Serial = ignored.Serial,
                    PlayerIgnored = ignored.PlayerIgnored
                };

                Aisling.IgnoredList.Add(temp.PlayerIgnored);
            }

            sConn.Close();
        }
        catch (Exception e)
        {
            ServerSetup.EventsLogger(e.Message, LogLevel.Error);
            ServerSetup.EventsLogger(e.StackTrace, LogLevel.Error);
        }

        return this;
    }

    private WorldClient InitLegend()
    {
        try
        {
            const string procedure = "[SelectLegends]";
            var values = new { Serial = (long)Aisling.Serial };
            using var sConn = new SqlConnection(AislingStorage.ConnectionString);
            sConn.Open();
            var legends = sConn.Query<Legend.LegendItem>(procedure, values, commandType: CommandType.StoredProcedure).ToList();

            foreach (var legend in legends.Where(s => s is not null).OrderBy(s => s.Time))
            {
                var newLegend = new Legend.LegendItem()
                {
                    LegendId = legend.LegendId,
                    Key = legend.Key,
                    IsPublic = legend.IsPublic,
                    Time = legend.Time,
                    Color = legend.Color,
                    Icon = legend.Icon,
                    Text = legend.Text
                };

                Aisling.LegendBook.LegendMarks.Add(newLegend);
            }

            sConn.Close();
        }
        catch (Exception e)
        {
            ServerSetup.EventsLogger(e.Message, LogLevel.Error);
            ServerSetup.EventsLogger(e.StackTrace, LogLevel.Error);
        }

        // Initial
        ObtainProfileLegendMarks(null, null);
        Aisling.LegendBook.LegendMarks.CollectionChanged += ObtainProfileLegendMarks;
        Aisling.Loading = false;
        return this;
    }

    private void InitCombos()
    {
        try
        {
            const string procedure = "[SelectCombos]";
            var values = new { Serial = (long)Aisling.Serial };
            using var sConn = new SqlConnection(AislingStorage.ConnectionString);
            sConn.Open();
            Aisling.ComboManager = sConn.QueryFirstOrDefault<ComboScroll>(procedure, values, commandType: CommandType.StoredProcedure);
            Aisling.ComboManager ??= new ComboScroll();
            sConn.Close();
        }
        catch (Exception e)
        {
            ServerSetup.EventsLogger(e.Message, LogLevel.Error);
            ServerSetup.EventsLogger(e.StackTrace, LogLevel.Error);
        }
    }

    private void InitQuests()
    {
        try
        {
            const string procedure = "[SelectQuests]";
            var values = new { Serial = (long)Aisling.Serial };
            using var sConn = new SqlConnection(AislingStorage.ConnectionString);
            sConn.Open();
            Aisling.QuestManager = sConn.QueryFirst<Quests>(procedure, values, commandType: CommandType.StoredProcedure);
            sConn.Close();
        }
        catch (Exception e)
        {
            ServerSetup.EventsLogger(e.Message, LogLevel.Error);
            ServerSetup.EventsLogger(e.StackTrace, LogLevel.Error);
        }
    }

    private void SkillCleanup()
    {
        var skillsAvailable = Aisling.SkillBook.Skills.Values.Where(i => i?.Template != null);
        var hasAssail = false;

        foreach (var skill in skillsAvailable)
        {
            switch (skill.SkillName)
            {
                case null:
                    continue;
                case "Assail":
                    hasAssail = true;
                    break;
            }

            SendAddSkillToPane(skill);

            if (skill.CurrentCooldown < skill.Template.Cooldown && skill.CurrentCooldown != 0)
            {
                SendCooldown(true, skill.Slot, skill.CurrentCooldown);
            }

            Skill.AttachScript(skill);
            {
                Aisling.SkillBook.Set(skill.Slot, skill, null);
            }
        }

        if (hasAssail) return;

        Skill.GiveTo(Aisling, "Assail");
    }

    private void SpellCleanup()
    {
        var spellsAvailable = Aisling.SpellBook.Spells.Values.Where(i => i?.Template != null);

        foreach (var spell in spellsAvailable)
        {
            if (spell.SpellName == null) continue;

            spell.Lines = spell.Template.BaseLines;
            SendAddSpellToPane(spell);

            if (spell.CurrentCooldown < spell.Template.Cooldown && spell.CurrentCooldown != 0)
            {
                SendCooldown(false, spell.Slot, spell.CurrentCooldown);
            }

            Spell.AttachScript(spell);
            {
                Aisling.SpellBook.Set(spell.Slot, spell, null);
            }
        }
    }

    private void EquipGearAndAttachScripts()
    {
        foreach (var (_, equipment) in Aisling.EquipmentManager.Equipment)
        {
            if (equipment?.Item?.Template == null) continue;

            Aisling.CurrentWeight += equipment.Item.Template.CarryWeight;
            SendEquipment(equipment.Item.Slot, equipment.Item);
            equipment.Item.Scripts = ScriptManager.Load<ItemScript>(equipment.Item.Template.ScriptName, equipment.Item);

            if (!string.IsNullOrEmpty(equipment.Item.Template.WeaponScript))
                equipment.Item.WeaponScripts = ScriptManager.Load<WeaponScript>(equipment.Item.Template.WeaponScript, equipment.Item);

            var script = equipment.Item.Scripts.Values.FirstOrDefault();
            script?.Equipped(Aisling, equipment.Item.Slot);

        }

        var item = new Item();
        item.ReapplyItemModifiers(this);
    }

    #endregion

    #region Handlers

    protected override ValueTask HandlePacketAsync(Span<byte> span)
    {
        var opCode = span[3];
        var isEncrypted = Crypto.ShouldBeEncrypted(opCode);
        var packet = new ClientPacket(ref span, isEncrypted);

        if (isEncrypted)
            Crypto.Decrypt(ref packet);

        return _server.HandlePacketAsync(this, in packet);
    }

    /// <summary>
    /// 0x02 - Send Login Message
    /// </summary>
    public void SendLoginMessage(LoginMessageType loginMessageType, string message = null)
    {
        var args = new LoginMessageArgs
        {
            LoginMessageType = loginMessageType,
            Message = message
        };

        Send(args);
    }

    /// <summary>
    /// 0x0F - Add Inventory
    /// </summary>
    public void SendAddItemToPane(Item item)
    {
        var args = new AddItemToPaneArgs
        {
            Item = new ItemInfo
            {
                Color = (DisplayColor)item.Color,
                Cost = (int?)item.Template.Value,
                Count = item.Stacks,
                CurrentDurability = (int)item.Durability,
                MaxDurability = (int)item.MaxDurability,
                Name = item.DisplayName,
                Group = item.Template.Group,
                Slot = item.InventorySlot,
                Sprite = item.DisplayImage,
                Stackable = item.Template.CanStack
            }
        };

        Send(args);
    }

    /// <summary>
    /// 0x2C - Add Skill
    /// </summary>
    public void SendAddSkillToPane(Skill skill)
    {
        var args = new AddSkillToPaneArgs
        {
            Skill = new SkillInfo
            {
                Name = skill.SkillName,
                PanelName = skill.Name,
                Slot = skill.Slot,
                Sprite = skill.Icon
            }
        };

        Send(args);
    }

    /// <summary>
    /// 0x17 - Add Spell
    /// </summary>
    public void SendAddSpellToPane(Spell spell)
    {
        var args = new AddSpellToPaneArgs
        {
            Spell = new SpellInfo
            {
                Name = spell.SpellName,
                PanelName = spell.Name,
                Slot = spell.Slot,
                Sprite = spell.Icon,
                CastLines = Math.Clamp((byte)spell.Lines, (byte)0, (byte)9),
                Prompt = string.Empty,
                SpellType = (SpellType)spell.Template.TargetType
            }
        };

        Send(args);
    }

    /// <summary>
    /// 0x29 - Animation
    /// </summary>
    public void SendAnimation(ushort targetEffect, Position position = null, uint targetSerial = 0, ushort speed = 100, ushort casterEffect = 0, uint casterSerial = 0)
    {
        Point? point;

        if (position is null)
            point = null;
        else
            point = new Point(position.X, position.Y);

        var args = new AnimationArgs
        {
            AnimationSpeed = speed,
            SourceAnimation = casterEffect,
            SourceId = casterSerial,
            TargetAnimation = targetEffect,
            TargetId = targetSerial,
            TargetPoint = point
        };

        Send(args);
    }

    /// <summary>
    /// 0x08 - Attributes
    /// </summary>
    public void SendAttributes(StatUpdateType statUpdateType)
    {
        byte levelCap;
        byte abCap;

        if (Aisling.ExpLevel > 255)
            levelCap = 255;
        else
            levelCap = (byte)Aisling.ExpLevel;

        if (Aisling.AbpLevel > 255)
            abCap = 255;
        else
            abCap = (byte)Aisling.AbpLevel;

        var hasUnreadMail = false;

        // ToDo: Disabling until logic is worked to turn off read letters
        //foreach (var letter in Aisling.PersonalLetters.Values)
        //{
        //    if (letter.ReadPost) continue;
        //    hasUnreadMail = true;
        //    break;
        //}

        var args = new AttributesArgs
        {
            Ability = abCap,
            Ac = (sbyte)Math.Clamp(Aisling.SealedAc, sbyte.MinValue, sbyte.MaxValue),
            Blind = Aisling.IsBlind,
            Con = (byte)Math.Clamp(Aisling.Con, byte.MinValue, byte.MaxValue),
            CurrentHp = (uint)Aisling.CurrentHp is >= uint.MaxValue or <= 0 ? 1 : (uint)Aisling.CurrentHp,
            CurrentMp = (uint)Aisling.CurrentMp is >= uint.MaxValue or <= 0 ? 1 : (uint)Aisling.CurrentMp,
            CurrentWeight = Aisling.CurrentWeight,
            DefenseElement = (Element)Aisling.DefenseElement,
            Dex = (byte)Math.Clamp(Aisling.Dex, 0, 255),
            Dmg = (byte)Math.Clamp((sbyte)Aisling.Dmg, sbyte.MinValue, sbyte.MaxValue),
            GamePoints = (uint)Aisling.GamePoints,
            Gold = (uint)Aisling.GoldPoints,
            Hit = (byte)Math.Clamp((sbyte)Aisling.Hit, sbyte.MinValue, sbyte.MaxValue),
            Int = (byte)Math.Clamp(Aisling.Int, 0, 255),
            IsAdmin = Aisling.GameMaster,
            CanSwim = true,
            Level = levelCap,
            MagicResistance = (byte)(Aisling.Regen / 10),
            HasUnreadMail = hasUnreadMail,
            MaximumHp = (uint)Aisling.MaximumHp is >= uint.MaxValue or <= 0 ? 1 : (uint)Aisling.MaximumHp,
            MaximumMp = (uint)Aisling.MaximumMp is >= uint.MaxValue or <= 0 ? 1 : (uint)Aisling.MaximumMp,
            MaxWeight = (short)Aisling.MaximumWeight,
            OffenseElement = (Element)Aisling.OffenseElement,
            StatUpdateType = statUpdateType,
            Str = (byte)Math.Clamp(Aisling.Str, byte.MinValue, byte.MaxValue),
            ToNextAbility = (uint)Aisling.AbpNext,
            ToNextLevel = (uint)Aisling.ExpNext,
            TotalAbility = (uint)Aisling.AbpTotal,
            TotalExp = (uint)Math.Clamp(Aisling.ExpTotal, 0, uint.MaxValue),
            UnspentPoints = (byte)Aisling.StatPoints,
            Wis = (byte)Math.Clamp(Aisling.Wis, byte.MinValue, byte.MaxValue)
        };

        Send(args);
    }

    /// <summary>
    /// 0x31 - Show Board
    /// </summary>
    public bool SendBoard(BoardTemplate board)
    {
        try
        {
            var postsCollection = board.Posts.Values.Select(postFormat => new PostInfo
            {
                Author = postFormat.Sender,
                CreationDate = postFormat.DatePosted,
                IsHighlighted = postFormat.Highlighted,
                Message = postFormat.Message,
                PostId = postFormat.PostId,
                Subject = postFormat.SubjectLine
            }).ToList();

            var boardInfo = new BoardInfo
            {
                BoardId = board.BoardId,
                Name = board.Name,
                Posts = postsCollection
            };

            var args = new BoardArgs
            {
                Type = BoardOrResponseType.PublicBoard,
                Board = boardInfo,
                StartPostId = short.MaxValue
            };

            Send(args);
            return true;
        }
        catch
        {
            SendBoardResponse(BoardOrResponseType.SubmitPostResponse, "Issue with board", false);
        }

        return false;
    }

    /// <summary>
    /// 0x31 - Show Mailbox
    /// </summary>
    public bool SendMailBox()
    {
        try
        {
            var postsCollection = Aisling.PersonalLetters.Values.Select(postFormat => new PostInfo
            {
                Author = postFormat.Sender,
                CreationDate = postFormat.DatePosted,
                IsHighlighted = postFormat.Highlighted,
                Message = postFormat.Message,
                PostId = postFormat.PostId,
                Subject = postFormat.SubjectLine
            }).ToList();

            var boardInfo = new BoardInfo
            {
                BoardId = (ushort)Aisling.QuestManager.MailBoxNumber,
                Name = "Mail",
                Posts = postsCollection!
            };

            var args = new BoardArgs
            {
                Type = BoardOrResponseType.MailBoard,
                Board = boardInfo,
                StartPostId = short.MaxValue
            };

            Send(args);
            return true;
        }
        catch
        {
            SendBoardResponse(BoardOrResponseType.SubmitPostResponse, "Issue with mailbox, try again", false);
        }

        return false;
    }

    /// <summary>
    /// Show Posts and Letters
    /// </summary>
    public bool SendPost(PostTemplate post, bool isMail, bool enablePrevBtn = true)
    {
        try
        {
            var args = new BoardArgs
            {
                Type = isMail ? BoardOrResponseType.MailPost : BoardOrResponseType.PublicPost,
                Post = new PostInfo
                {
                    Author = post.Sender,
                    CreationDate = post.DatePosted,
                    IsHighlighted = post.Highlighted,
                    Message = post.Message,
                    PostId = post.PostId,
                    Subject = post.SubjectLine
                },
                EnablePrevBtn = enablePrevBtn
            };

            Send(args);
            return true;
        }
        catch
        {
            SendBoardResponse(BoardOrResponseType.SubmitPostResponse, "Issue opening", false);
        }

        return false;
    }

    public void SendBoardResponse(BoardOrResponseType responseType, string message, bool success)
    {
        var args = new BoardArgs
        {
            Type = responseType,
            ResponseMessage = message,
            Success = success
        };

        Send(args);
    }

    /// <summary>
    /// 0x1A - Player Body Animation
    /// </summary>
    public void SendBodyAnimation(uint id, BodyAnimation bodyAnimation, ushort speed, byte? sound = null)
    {
        if (bodyAnimation is BodyAnimation.None) return;

        var args = new BodyAnimationArgs
        {
            SourceId = id,
            BodyAnimation = bodyAnimation,
            Sound = sound,
            AnimationSpeed = speed
        };

        Send(args);
    }

    /// <summary>
    /// Attempts to cast a spell from cache, creating a temporary copy of it
    /// </summary>
    /// <param name="spellName">Used for finding the spell in cache</param>
    /// <param name="caster">Sprite that cast the spell</param>
    /// <param name="target">Sprite the spell is cast on</param>
    /// <returns>Spell with an attached script was found and called</returns>
    public bool AttemptCastSpellFromCache(string spellName, Sprite caster, Sprite target = null)
    {
        if (!ServerSetup.Instance.GlobalSpellTemplateCache.TryGetValue(spellName, out var value)) return false;

        var scripts = ScriptManager.Load<SpellScript>(spellName, Spell.Create(1, value));
        if (scripts == null) return false;

        scripts.Values.First().OnUse(caster, target);

        return true;
    }

    /// <summary>
    /// 0x48 - Cancel Casting
    /// </summary>
    public void SendCancelCasting()
    {
        var packet = ServerPacketEx.FromData(ServerOpCode.CancelCasting, PacketSerializer.Encoding);
        Send(ref packet);
    }

    /// <summary>
    /// 0x0B - Player Move
    /// </summary>
    public void SendConfirmClientWalk(Position oldPoint, Direction direction)
    {
        var args = new ConfirmClientWalkArgs
        {
            Direction = direction,
            OldPoint = new Point(oldPoint.X, oldPoint.Y)
        };

        Send(args);
    }

    /// <summary>
    /// 0x4C - Reconnect
    /// </summary>
    public void SendConfirmExit()
    {
        // Close Popups
        this.CloseDialog();
        Aisling.CancelExchange();

        // Exit Party
        if (Aisling.GroupId != 0)
            Party.RemovePartyMember(Aisling);

        // Set Timestamps
        Aisling.LastLogged = DateTime.UtcNow;
        Aisling.LoggedIn = false;

        // Save
        var saved = Save();
        ExitConfirmed = saved.Result;

        // Cleanup
        Aisling.Remove(true);

        var args = new ConfirmExitArgs
        {
            ExitConfirmed = ExitConfirmed
        };

        Send(args);
    }

    /// <summary>
    /// 0x3F - Cooldown
    /// </summary>
    public void SendCooldown(bool skill, byte slot, int cooldownSeconds)
    {
        if (Aisling.Overburden)
        {
            cooldownSeconds *= 2;
        }
        else
        {
            var haste = Haste(Aisling);
            cooldownSeconds = (int)(cooldownSeconds * haste);
        }

        var args = new CooldownArgs
        {
            IsSkill = skill,
            Slot = slot,
            CooldownSecs = (uint)cooldownSeconds
        };

        Send(args);
    }

    private static double Haste(Aisling player)
    {
        if (!player.Hastened) return 1;
        return player.Client.SkillSpellTimer.Delay.TotalMilliseconds switch
        {
            500 => 0.50,
            750 => 0.75,
            _ => 1
        };
    }

    /// <summary>
    /// 0x11 - Sprite Direction
    /// </summary>
    public void SendCreatureTurn(uint id, Direction direction)
    {
        var args = new CreatureTurnArgs
        {
            SourceId = id,
            Direction = direction
        };

        Send(args);
    }

    /// <summary>
    /// 0x0C - NPC Move
    /// </summary>
    public void SendCreatureWalk(uint id, Point startPoint, Direction direction)
    {
        var args = new CreatureWalkArgs
        {
            SourceId = id,
            OldPoint = startPoint,
            Direction = direction
        };

        Send(args);
    }

    /// <summary>
    /// 0x33 - Display Player
    /// </summary>
    public void SendDisplayAisling(Aisling aisling)
    {
        ushort? monsterForm = null;
        if (aisling.MonsterForm != 0)
            monsterForm = aisling.MonsterForm;

        var args = new DisplayAislingArgs
        {
            AccessoryColor1 = (DisplayColor)aisling.Accessory1Color,
            AccessoryColor2 = (DisplayColor)aisling.Accessory2Color,
            AccessoryColor3 = (DisplayColor)aisling.Accessory3Color,
            AccessorySprite1 = (ushort)aisling.Accessory1Img,
            AccessorySprite2 = (ushort)aisling.Accessory2Img,
            AccessorySprite3 = (ushort)aisling.Accessory3Img,
            ArmorSprite1 = (ushort)aisling.ArmorImg,
            ArmorSprite2 = (ushort)aisling.ArmorImg,
            PantsColor = (DisplayColor?)aisling.Pants,
            BodyColor = (BodyColor)aisling.BodyColor,
            BootsColor = (DisplayColor)aisling.BootColor,
            BootsSprite = (byte)aisling.BootsImg,
            Direction = (Direction)aisling.Direction,
            FaceSprite = 0,
            Gender = (Gender)aisling.Gender,
            GroupBoxText = "",
            HeadColor = (DisplayColor)aisling.HairColor,
            Id = aisling.Serial,
            IsDead = aisling.IsDead(),
            IsTransparent = aisling.IsInvisible,
            LanternSize = (LanternSize)aisling.Lantern,
            Name = aisling.Username,
            OvercoatColor = (DisplayColor)aisling.OverCoatColor,
            OvercoatSprite = (ushort)aisling.OverCoatImg,
            RestPosition = (RestPosition)aisling.Resting,
            ShieldSprite = (byte)aisling.ShieldImg,
            Sprite = monsterForm,
            WeaponSprite = (ushort)aisling.WeaponImg,
            X = aisling.X,
            Y = aisling.Y
        };

        if (aisling.EquipmentManager.OverHelm != null && aisling.HeadAccessoryImg != 0)
            args.HeadSprite = (ushort)aisling.HeadAccessoryImg;
        else if (aisling.EquipmentManager.Helmet != null && aisling.HelmetImg != 0)
            args.HeadSprite = (ushort)aisling.HelmetImg;
        else
            args.HeadSprite = aisling.HairStyle;

        if (aisling.Gender == Enums.Gender.Male)
        {
            if (aisling.IsInvisible)
                args.BodySprite = BodySprite.MaleInvis;
            else
                args.BodySprite = aisling.IsDead() ? BodySprite.MaleGhost : BodySprite.Male;
        }
        else
        {
            if (aisling.IsInvisible)
                args.BodySprite = BodySprite.FemaleInvis;
            else
                args.BodySprite = aisling.IsDead() ? BodySprite.FemaleGhost : BodySprite.Female;
        }

        if (!Aisling.Equals(aisling))
        {
            if (Aisling.Map.Flags.MapFlagIsSet(MapFlags.PlayerKill))
                args.NameTagStyle = !Aisling.Clan.IsNullOrEmpty() && Aisling.Clan == aisling.Clan ? NameTagStyle.Neutral : NameTagStyle.Hostile;
            else if (!Aisling.Clan.IsNullOrEmpty() && Aisling.Clan == aisling.Clan)
                args.NameTagStyle = NameTagStyle.FriendlyHover;
            else
                args.NameTagStyle = NameTagStyle.NeutralHover;
        }

        Send(args);
    }

    // ToDo: Create Doors Class, and Implement a Dictionary with the values 
    //public void SendDoors(IEnumerable<Door> doors)
    //{
    //    var args = new DoorArgs
    //    {
    //        Doors = Mapper.MapMany<DoorInfo>(doors).ToList()
    //    };

    //    if (args.Doors.Any())
    //        Send(args);
    //}

    /// <summary>
    /// 0x3A - Effect Duration
    /// </summary>
    public void SendEffect(EffectColor effectColor, byte effectIcon)
    {
        var args = new EffectArgs
        {
            EffectColor = effectColor,
            EffectIcon = effectIcon
        };

        Send(args);
    }

    /// <summary>
    /// 0x37 - Add Equipment
    /// </summary>
    public void SendEquipment(byte displaySlot, Item item)
    {
        if (displaySlot == 0) return;

        item.Slot = displaySlot;

        var args = new EquipmentArgs
        {
            Slot = (EquipmentSlot)displaySlot,
            Item = new ItemInfo
            {
                Color = (DisplayColor)item.Color,
                Cost = (int?)item.Template.Value,
                Count = item.Stacks,
                CurrentDurability = (int)item.Durability,
                MaxDurability = (int)item.MaxDurability,
                Name = item.NoColorDisplayName,
                Group = item.Template.Group,
                Slot = displaySlot,
                Sprite = item.DisplayImage,
                Stackable = item.Template.CanStack
            }
        };

        Send(args);
    }

    /// <summary>
    /// 0x42 - Start Exchange 
    /// </summary>
    public void SendExchangeStart(Aisling fromAisling)
    {
        var args = new ExchangeArgs
        {
            ExchangeResponseType = ExchangeResponseType.StartExchange,
            OtherUserId = fromAisling.Serial,
            OtherUserName = fromAisling.Username
        };

        Send(args);
    }

    /// <summary>
    /// 0x42 - Add Item to Exchange 
    /// </summary>
    public void SendExchangeAddItem(bool rightSide, byte index, Item item)
    {
        var args = new ExchangeArgs
        {
            ExchangeResponseType = ExchangeResponseType.AddItem,
            RightSide = rightSide,
            ExchangeIndex = index,
            ItemSprite = item.Template.DisplayImage,
            ItemColor = (DisplayColor?)item.Template.Color,
            ItemName = item.DisplayName
        };

        if (item.Stacks > 1)
            args.ItemName = $"{item.DisplayName} [{item.Stacks}]";

        Send(args);
    }

    /// <summary>
    /// 0x42 - Add Gold to Exchange 
    /// </summary>
    public void SendExchangeSetGold(bool rightSide, uint amount)
    {
        var args = new ExchangeArgs
        {
            ExchangeResponseType = ExchangeResponseType.SetGold,
            RightSide = rightSide,
            GoldAmount = (int)amount
        };

        Send(args);
    }

    /// <summary>
    /// 0x42 - Request To Exchange (Item | Money)
    /// </summary>
    public void SendExchangeRequestAmount(byte slot)
    {
        var args = new ExchangeArgs
        {
            ExchangeResponseType = ExchangeResponseType.RequestAmount,
            FromSlot = slot
        };

        Send(args);
    }

    /// <summary>
    /// 0x42 - Accept Exchange 
    /// </summary>
    public void SendExchangeAccepted(bool persistExchange)
    {
        var args = new ExchangeArgs
        {
            ExchangeResponseType = ExchangeResponseType.Accept,
            PersistExchange = persistExchange
        };

        Send(args);
    }

    /// <summary>
    /// 0x42 - Cancel Exchange 
    /// </summary>
    public void SendExchangeCancel(bool rightSide)
    {
        var args = new ExchangeArgs
        {
            ExchangeResponseType = ExchangeResponseType.Cancel,
            RightSide = rightSide
        };

        Send(args);
    }

    /// <summary>
    /// Forced Client Packet
    /// </summary>
    public void SendForcedClientPacket(ref ClientPacket clientPacket)
    {
        var args = new ForceClientPacketArgs
        {
            ClientOpCode = clientPacket.OpCode,
            Data = clientPacket.Buffer.ToArray()
        };

        Send(args);
    }

    /// <summary>
    /// 0x63 - Group Request
    /// </summary>
    public void SendGroupRequest(GroupRequestType groupRequestType, string fromName)
    {
        var args = new GroupRequestArgs
        {
            GroupRequestType = groupRequestType,
            SourceName = fromName
        };

        Send(args);
    }

    /// <summary>
    /// 0x13 - Health Bar
    /// </summary>
    public void SendHealthBar(Sprite creature, byte? sound = null)
    {
        var args = new HealthBarArgs
        {
            SourceId = creature.Serial,
            HealthPercent = (byte)((double)100 * creature.CurrentHp / creature.MaximumHp),
            Sound = sound
        };

        Send(args);
    }

    /// <summary>
    /// 0x20 - Change Hour (Night - Day)
    /// </summary>
    /// <param name="lightLevel">
    /// Darkest = 0,
    /// Darker = 1,
    /// Dark = 2,
    /// Light = 3,
    /// Lighter = 4,
    /// Lightest = 5
    /// </param>
    public void SendLightLevel(LightLevel lightLevel)
    {
        var args = new LightLevelArgs
        {
            LightLevel = lightLevel
        };

        Send(args);
    }

    /// <summary>
    /// 0x04 - Location
    /// </summary>
    public void SendLocation()
    {
        var args = new LocationArgs
        {
            X = Aisling.X,
            Y = Aisling.Y
        };

        Send(args);
    }

    /// <summary>
    /// 0x1F - Map Change Complete
    /// </summary>
    public void SendMapChangeComplete()
    {
        var packet = ServerPacketEx.FromData(ServerOpCode.MapChangeComplete, PacketSerializer.Encoding, new byte[2]);

        Send(ref packet);
    }

    /// <summary>
    /// 0x67 - Map Change Pending
    /// </summary>
    public void SendMapChangePending()
    {
        var packet = ServerPacketEx.FromData(
            ServerOpCode.MapChangePending,
            PacketSerializer.Encoding,
            3,
            0,
            0,
            0,
            0,
            0);

        Send(ref packet);
    }

    /// <summary>
    /// 0x3C - Map Data
    /// </summary>
    public void SendMapData()
    {
        var mapTemplate = Aisling.Map;

        for (byte y = 0; y < mapTemplate.Height; y++)
        {
            var args = new MapDataArgs
            {
                CurrentYIndex = y,
                Width = mapTemplate.Width,
                MapData = mapTemplate.GetRowData(y).ToArray()
            };

            Send(args);
        }
    }

    /// <summary>
    /// 0x15 - Map Information
    /// </summary>
    public void SendMapInfo()
    {
        var args = new MapInfoArgs
        {
            CheckSum = Aisling.Map.Hash,
            Flags = (byte)Aisling.Map.Flags,
            Height = Aisling.Map.Height,
            MapId = (short)Aisling.Map.ID,
            Name = Aisling.Map.Name,
            Width = Aisling.Map.Width
        };

        Send(args);
    }

    /// <summary>
    /// 0x58 - Map Load Complete
    /// </summary>
    public void SendMapLoadComplete()
    {
        var packet = ServerPacketEx.FromData(ServerOpCode.MapLoadComplete, PacketSerializer.Encoding, 0);

        Send(ref packet);
    }

    /// <summary>
    /// 0x6F - MapData Send
    /// </summary>
    public void SendMetaData(MetaDataRequestType metaDataRequestType, MetafileManager metaDataStore, string name = null)
    {
        var args = new MetaDataArgs
        {
            MetaDataRequestType = metaDataRequestType
        };

        switch (metaDataRequestType)
        {
            case MetaDataRequestType.DataByName:
                {
                    try
                    {
                        var metaData = MetafileManager.GetMetaFile(name);

                        if (!name!.Contains("Class"))
                        {
                            args.MetaDataInfo = new MetaDataInfo
                            {
                                Name = metaData.Name,
                                Data = metaData.DeflatedData,
                                CheckSum = metaData.Hash
                            };

                            break;
                        }

                        var orgFileName = Aisling.Path switch
                        {
                            Class.Warrior => "SClass1",
                            Class.Rogue => "SClass2",
                            Class.Wizard => "SClass3",
                            Class.Priest => "SClass4",
                            Class.Monk => "SClass5",
                            Class.Diacht => "SClass6",
                            _ => metaData.Name
                        };

                        args.MetaDataInfo = new MetaDataInfo
                        {
                            Name = orgFileName,
                            Data = metaData.DeflatedData,
                            CheckSum = metaData.Hash
                        };
                    }
                    catch (Exception ex)
                    {
                        ServerSetup.EventsLogger(ex.Message, LogLevel.Error);
                        ServerSetup.EventsLogger(ex.StackTrace, LogLevel.Error);
                    }

                    break;
                }
            case MetaDataRequestType.AllCheckSums:
                {
                    try
                    {
                        args.MetaDataCollection = new List<MetaDataInfo>();
                        var metaFiles = MetafileManager.GetMetaFilesWithoutExtendedClasses();

                        foreach (var metafileInfo in metaFiles.Select(metaFile => new MetaDataInfo
                        {
                            CheckSum = metaFile.Hash,
                            Data = metaFile.DeflatedData,
                            Name = metaFile.Name
                        }))
                        {
                            args.MetaDataCollection.Add(metafileInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        ServerSetup.EventsLogger(ex.Message, LogLevel.Error);
                        ServerSetup.EventsLogger(ex.StackTrace, LogLevel.Error);
                    }

                    break;
                }
        }

        Send(args);
    }

    public void SendNotepad(byte identifier, NotepadType type, byte height, byte width, string message)
    {
        var args = new NotepadArgs
        {
            Slot = identifier,
            NotepadType = type,
            Height = height,
            Width = width,
            Message = message ?? string.Empty
        };

        Send(args);
    }

    /// <summary>
    /// 0x34 - Player Profile
    /// </summary>
    /// <param name="aisling">Target Player</param>
    public void SendProfile(Aisling aisling)
    {
        var equipment = new Dictionary<EquipmentSlot, ItemInfo>();
        var partyOpen = aisling.PartyStatus == (GroupStatus)1;

        #region Gear

        if (aisling.EquipmentManager.Weapon != null)
        {
            var equip = aisling.EquipmentManager.Weapon;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        if (aisling.EquipmentManager.Armor != null)
        {
            var equip = aisling.EquipmentManager.Armor;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        if (aisling.EquipmentManager.Shield != null)
        {
            var equip = aisling.EquipmentManager.Shield;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        if (aisling.EquipmentManager.Helmet != null)
        {
            var equip = aisling.EquipmentManager.Helmet;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        if (aisling.EquipmentManager.Earring != null)
        {
            var equip = aisling.EquipmentManager.Earring;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        if (aisling.EquipmentManager.Necklace != null)
        {
            var equip = aisling.EquipmentManager.Necklace;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        if (aisling.EquipmentManager.LHand != null)
        {
            var equip = aisling.EquipmentManager.LHand;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        if (aisling.EquipmentManager.RHand != null)
        {
            var equip = aisling.EquipmentManager.RHand;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        if (aisling.EquipmentManager.LArm != null)
        {
            var equip = aisling.EquipmentManager.LArm;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        if (aisling.EquipmentManager.RArm != null)
        {
            var equip = aisling.EquipmentManager.RArm;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        if (aisling.EquipmentManager.Waist != null)
        {
            var equip = aisling.EquipmentManager.Waist;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        if (aisling.EquipmentManager.Leg != null)
        {
            var equip = aisling.EquipmentManager.Leg;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        if (aisling.EquipmentManager.Foot != null)
        {
            var equip = aisling.EquipmentManager.Foot;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        if (aisling.EquipmentManager.FirstAcc != null)
        {
            var equip = aisling.EquipmentManager.FirstAcc;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        if (aisling.EquipmentManager.OverCoat != null)
        {
            var equip = aisling.EquipmentManager.OverCoat;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        if (aisling.EquipmentManager.OverHelm != null)
        {
            var equip = aisling.EquipmentManager.OverHelm;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        if (aisling.EquipmentManager.SecondAcc != null)
        {
            var equip = aisling.EquipmentManager.SecondAcc;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        if (aisling.EquipmentManager.ThirdAcc != null)
        {
            var equip = aisling.EquipmentManager.ThirdAcc;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        #endregion

        var args = new ProfileArgs
        {
            AdvClass = (AdvClass)ClassStrings.JobDisplayFlag(aisling.JobClass.ToString()),
            BaseClass = (BaseClass)ClassStrings.ClassDisplayInt(aisling.Path.ToString()),
            Equipment = equipment,
            GroupOpen = partyOpen,
            GuildName = $"{aisling.Clan} - {aisling.ClanRank}",
            GuildRank = $"GearP.: {aisling.GamePoints}",
            Id = aisling.Serial,
            LegendMarks = aisling.Client._legendMarksPublic,
            Name = aisling.Username,
            Nation = (Nation)aisling.Nation,
            Portrait = aisling.PictureData,
            ProfileText = aisling.ProfileMessage,
            SocialStatus = (SocialStatus)aisling.ActiveStatus,
            Title = $"Lvl: {aisling.ExpLevel}  Rnk: {aisling.AbpLevel}"
        };

        Send(args);
    }

    private void ObtainProfileLegendMarks(object sender, NotifyCollectionChangedEventArgs args)
    {
        _legendMarksPublic.Clear();
        _legendMarksPrivate.Clear();

        try
        {
            var currentMarks = Aisling.LegendBook.LegendMarks.ToList();
            var legends = currentMarks.DistinctBy(m => m.Text);

            _legendMarksPublic.AddRange(legends
                .Where(legend => legend is { IsPublic: true })
                .Select(legend =>
                {
                    var markCount = currentMarks.Count(item => item.Text == legend.Text);
                    var legendText = $"{legend.Text} - {legend.Time.ToShortDateString()} ({markCount})";
                    return new LegendMarkInfo
                    {
                        Color = (MarkColor)legend.Color,
                        Icon = (MarkIcon)legend.Icon,
                        Key = legend.Key,
                        Text = legendText
                    };
                }));

            _legendMarksPrivate.AddRange(legends
                .Where(legend => legend is not null)
                .Select(legend =>
                {
                    var markCount = currentMarks.Count(item => item.Text == legend.Text);
                    var legendText = $"{legend.Text} - {legend.Time.ToShortDateString()} ({markCount})";
                    return new LegendMarkInfo
                    {
                        Color = (MarkColor)legend.Color,
                        Icon = (MarkIcon)legend.Icon,
                        Key = legend.Key,
                        Text = legendText
                    };
                }));
        }
        catch
        {
            // ignored
        }
    }

    /// <summary>
    /// 0x49 - Request Portrait
    /// </summary>
    public void SendProfileRequest()
    {
        var packet = ServerPacketEx.FromData(ServerOpCode.ProfileRequest, PacketSerializer.Encoding);

        Send(ref packet);
    }

    public override void SendHeartBeat(byte first, byte second)
    {
        var args = new HeartBeatResponseArgs
        {
            First = first,
            Second = second
        };

        Latency.Restart();
        Send(args);
    }

    /// <summary>
    /// 0x0D - Public Messages / Chant
    /// </summary>
    /// <param name="sourceId">Sprite Serial</param>
    /// <param name="publicMessageType">Message Type</param>
    /// <param name="message">Message</param>
    public void SendPublicMessage(uint sourceId, PublicMessageType publicMessageType, string message)
    {
        var args = new PublicMessageArgs
        {
            SourceId = sourceId,
            PublicMessageType = publicMessageType,
            Message = message
        };

        Send(args);
    }

    /// <summary>
    /// 0x22 - Client Refresh
    /// </summary>
    public void SendRefreshResponse()
    {
        var packet = ServerPacketEx.FromData(ServerOpCode.RefreshResponse, PacketSerializer.Encoding);

        Send(ref packet);
    }

    /// <summary>
    /// 0x10 - Remove Item from Inventory
    /// </summary>
    /// <param name="slot"></param>
    public void SendRemoveItemFromPane(byte slot)
    {
        var args = new RemoveItemFromPaneArgs
        {
            Slot = slot
        };

        Send(args);
    }

    /// <summary>
    /// 0x0E - Remove World Object
    /// </summary>
    /// <param name="id"></param>
    public void SendRemoveObject(uint id)
    {
        var args = new RemoveObjectArgs
        {
            SourceId = id
        };

        Send(args);
    }

    /// <summary>
    /// 0x2D - Remove Skill
    /// </summary>
    /// <param name="slot"></param>
    public void SendRemoveSkillFromPane(byte slot)
    {
        var args = new RemoveSkillFromPaneArgs
        {
            Slot = slot
        };

        Send(args);
    }

    /// <summary>
    /// 0x18 - Remove Spell
    /// </summary>
    /// <param name="slot"></param>
    public void SendRemoveSpellFromPane(byte slot)
    {
        var args = new RemoveSpellFromPaneArgs
        {
            Slot = slot
        };

        Send(args);
    }

    /// <summary>
    /// 0x39 - Self Profile
    /// </summary>
    public void SendSelfProfile()
    {
        if (Aisling.ProfileOpen) return;

        var equipment = new Dictionary<EquipmentSlot, ItemInfo>();
        var partyOpen = Aisling.PartyStatus == (GroupStatus)1;

        #region Gear

        if (Aisling.EquipmentManager.Weapon != null)
        {
            var equip = Aisling.EquipmentManager.Weapon;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        if (Aisling.EquipmentManager.Armor != null)
        {
            var equip = Aisling.EquipmentManager.Armor;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        if (Aisling.EquipmentManager.Shield != null)
        {
            var equip = Aisling.EquipmentManager.Shield;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        if (Aisling.EquipmentManager.Helmet != null)
        {
            var equip = Aisling.EquipmentManager.Helmet;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        if (Aisling.EquipmentManager.Earring != null)
        {
            var equip = Aisling.EquipmentManager.Earring;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        if (Aisling.EquipmentManager.Necklace != null)
        {
            var equip = Aisling.EquipmentManager.Necklace;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        if (Aisling.EquipmentManager.LHand != null)
        {
            var equip = Aisling.EquipmentManager.LHand;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        if (Aisling.EquipmentManager.RHand != null)
        {
            var equip = Aisling.EquipmentManager.RHand;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        if (Aisling.EquipmentManager.LArm != null)
        {
            var equip = Aisling.EquipmentManager.LArm;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        if (Aisling.EquipmentManager.RArm != null)
        {
            var equip = Aisling.EquipmentManager.RArm;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        if (Aisling.EquipmentManager.Waist != null)
        {
            var equip = Aisling.EquipmentManager.Waist;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        if (Aisling.EquipmentManager.Leg != null)
        {
            var equip = Aisling.EquipmentManager.Leg;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        if (Aisling.EquipmentManager.Foot != null)
        {
            var equip = Aisling.EquipmentManager.Foot;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        if (Aisling.EquipmentManager.FirstAcc != null)
        {
            var equip = Aisling.EquipmentManager.FirstAcc;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        if (Aisling.EquipmentManager.OverCoat != null)
        {
            var equip = Aisling.EquipmentManager.OverCoat;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        if (Aisling.EquipmentManager.OverHelm != null)
        {
            var equip = Aisling.EquipmentManager.OverHelm;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        if (Aisling.EquipmentManager.SecondAcc != null)
        {
            var equip = Aisling.EquipmentManager.SecondAcc;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        if (Aisling.EquipmentManager.ThirdAcc != null)
        {
            var equip = Aisling.EquipmentManager.ThirdAcc;

            var item = new ItemInfo
            {
                Color = (DisplayColor)equip.Item.Color,
                Cost = (int?)equip.Item.Template.Value,
                Count = equip.Item.Stacks,
                CurrentDurability = (int)equip.Item.Durability,
                MaxDurability = (int)equip.Item.MaxDurability,
                Name = equip.Item.NoColorDisplayName,
                Group = equip.Item.Template.Group,
                Slot = (byte)equip.Slot,
                Sprite = equip.Item.DisplayImage,
                Stackable = equip.Item.Template.CanStack
            };

            equipment.Add((EquipmentSlot)item.Slot, item);
        }

        #endregion

        var args = new SelfProfileArgs
        {
            AdvClass = (AdvClass)ClassStrings.JobDisplayFlag(Aisling.JobClass.ToString()),
            BaseClass = (BaseClass)ClassStrings.ClassDisplayInt(Aisling.Path.ToString()),
            Equipment = equipment,
            GroupOpen = partyOpen,
            GroupString = Aisling.GroupParty?.PartyMemberString ?? "",
            GuildName = $"{Aisling.Clan} - {Aisling.ClanRank}",
            GuildRank = $"GearP.: {Aisling.GamePoints}",
            IsMaster = Aisling.Stage.StageFlagIsSet(ClassStage.Master),
            LegendMarks = _legendMarksPrivate,
            Name = Aisling.Username,
            Nation = (Nation)Aisling.Nation,
            Portrait = Aisling.PictureData,
            ProfileText = Aisling.ProfileMessage,
            SpouseName = "",
            Title = $"Lvl: {Aisling.ExpLevel}  Rnk: {Aisling.AbpLevel}"
        };

        Send(args);
    }

    /// <summary>
    /// 0x0A - System Messages / Private Messages
    /// </summary>
    public void SendServerMessage(ServerMessageType serverMessageType, string message)
    {
        var args = new ServerMessageArgs
        {
            ServerMessageType = serverMessageType,
            Message = message
        };

        Send(args);
    }

    /// <summary>
    /// 0x19 - Send Sound
    /// </summary>
    /// <param name="sound">Sound Number</param>
    /// <param name="isMusic">Whether the sound is a song</param>
    public void SendSound(byte sound, bool isMusic)
    {
        var args = new SoundArgs
        {
            Sound = sound,
            IsMusic = isMusic
        };

        Send(args);
    }

    /// <summary>
    /// 0x38 - Remove Equipment
    /// </summary>
    /// <param name="equipmentSlot"></param>
    public void SendUnequip(EquipmentSlot equipmentSlot)
    {
        var args = new UnequipArgs
        {
            EquipmentSlot = equipmentSlot
        };

        Send(args);
    }

    /// <summary>
    /// 0x05 - UserID, Direction, Rogue Map, Gender
    /// </summary>
    public void SendUserId()
    {
        var args = new UserIdArgs
        {
            BaseClass = (BaseClass)2,
            Direction = (Direction)Aisling.Direction,
            Gender = (Gender)Aisling.Gender,
            Id = Aisling.Serial
        };

        Send(args);
    }

    /// <summary>
    /// 0x07 - Add World Objects
    /// </summary>
    /// <param name="objects">Objects that are visible to a player</param>
    public void SendVisibleEntities(List<Sprite> objects)
    {
        if (objects.Count <= 0) return;

        // Split this into chunks so as not to crash the client
        foreach (var chunk in objects.Where(o => o != null).OrderBy(o => o.AbandonedDate).Chunk(500))
        {
            var args = new DisplayVisibleEntitiesArgs();
            var visibleArgs = new List<VisibleEntityInfo>();
            args.VisibleObjects = visibleArgs;

            foreach (var obj in chunk)
                switch (obj)
                {
                    case Item groundItem:
                        var groundItemInfo = new GroundItemInfo
                        {
                            Id = groundItem.ItemVisibilityId,
                            Sprite = groundItem.DisplayImage,
                            X = groundItem.X,
                            Y = groundItem.Y,
                            Color = (DisplayColor)groundItem.Template.Color
                        };

                        visibleArgs.Add(groundItemInfo);

                        break;
                    case Money money:
                        var moneyInfo = new GroundItemInfo
                        {
                            Id = money.Serial,
                            Sprite = money.Image,
                            X = money.X,
                            Y = money.Y,
                            Color = DisplayColor.Default
                        };

                        visibleArgs.Add(moneyInfo);

                        break;
                    case Monster creature:
                        var creatureInfo = new CreatureInfo
                        {
                            Id = creature.Serial,
                            Sprite = creature.Image,
                            X = creature.X,
                            Y = creature.Y,
                            CreatureType = CreatureType.Normal,
                            /*
                             * Normal = 0
                             * WalkThrough = 1
                             * Merchant = 2
                             * WhiteSquare = 3
                             * User = 4
                             */
                            Direction = (Direction)creature.Direction,
                            Name = creature.Template.BaseName
                        };

                        visibleArgs.Add(creatureInfo);

                        break;
                    case Mundane npc:
                        var npcInfo = new CreatureInfo
                        {
                            Id = npc.Serial,
                            Sprite = npc.Sprite,
                            X = npc.X,
                            Y = npc.Y,
                            CreatureType = CreatureType.Merchant,
                            Direction = (Direction)npc.Direction,
                            Name = npc.Template.Name
                        };

                        //none visible creature that can be seen
                        //if (creature.Visibility is not VisibilityType.Normal &&
                        //    (Aisling.IsAdmin || Aisling.Script.CanSee(creature)))
                        //    creatureInfo.Sprite = 405;

                        visibleArgs.Add(npcInfo);
                        break;
                }

            Send(args);
        }
    }

    /// <summary>
    /// 0x36 - World User List
    /// </summary>
    /// <param name="aislings"></param>
    public void SendWorldList(IEnumerable<Aisling> aislings)
    {
        var worldList = new List<WorldListMemberInfo>();
        var orderedAislings = aislings.OrderByDescending(aisling => aisling.BaseMp * 2 + aisling.BaseHp);

        var args = new WorldListArgs
        {
            WorldList = worldList
        };

        foreach (var aisling in orderedAislings)
        {
            var classList = aisling.Path switch
            {
                Class.Peasant => 0,
                Class.Warrior => 1,
                Class.Rogue => 2,
                Class.Wizard => 3,
                Class.Priest => 4,
                Class.Monk => 5,
                Class.Diacht => 6,
                _ => 0
            };

            var jobClass = aisling.JobClass switch
            {
                AdvClass.Gladiator => "Gladiator",
                AdvClass.Druid => "Druid",
                AdvClass.Archer => "Archer",
                AdvClass.Bard => "Bard",
                AdvClass.Summoner => "Summoner",
                AdvClass.None => "",
                _ => ""
            };

            var vitality = $"Vit: {aisling.BaseHp + aisling.BaseMp * 2}";

            if (!jobClass.IsNullOrEmpty())
                vitality = jobClass;

            var arg = new WorldListMemberInfo
            {
                BaseClass = (BaseClass)classList,
                Color = (WorldListColor)GetUserColor(aisling),
                IsMaster = aisling.Stage >= ClassStage.Master,
                Name = aisling.Username,
                SocialStatus = (SocialStatus)aisling.ActiveStatus,
                Title = aisling.GameMaster
                    ? "Game Master"
                    : $"{vitality}"
            };

            worldList.Add(arg);
        }

        Send(args);
    }

    private ListColor GetUserColor(Player user)
    {
        var color = ListColor.White;
        if (Aisling.ExpLevel > user.ExpLevel)
            if (Aisling.ExpLevel - user.ExpLevel < 15)
                color = ListColor.Orange;
        if (!string.IsNullOrEmpty(user.Clan) && user.Clan == Aisling.Clan)
            color = ListColor.Clan;
        if (user.GameMaster)
            color = ListColor.Red;
        if (user.Knight)
            color = ListColor.Green;
        if (user.ArenaHost)
            color = ListColor.Teal;
        return color;
    }

    /// <summary>
    /// 0x2E - Send Field Map
    /// </summary>
    /// <param name="worldMap"></param>
    public void SendWorldMap()
    {
        var mapExists = ServerSetup.Instance.GlobalWorldMapTemplateCache.TryGetValue(Aisling.World, out var portal);
        if (!mapExists) return;
        MapOpen = true;
        var name = $"field{portal.FieldNumber:000}";
        var warpsList = new List<WorldMapNodeInfo>();

        foreach (var warp in portal.Portals.Where(warps => warps?.Destination != null))
        {
            var map = ServerSetup.Instance.GlobalMapCache[warp.Destination.AreaID];
            var x = warp.Destination.Location.X;
            var y = warp.Destination.Location.Y;
            var addWarp = new WorldMapNodeInfo
            {
                CheckSum = EphemeralRandomIdGenerator<ushort>.Shared.NextId, // map.Hash
                DestinationPoint = new Point(x, y),
                MapId = (ushort)map.ID,
                ScreenPosition = new Point(warp.PointY, warp.PointX), // Client expects this backwards
                Text = warp.DisplayName,
            };

            warpsList.Add(addWarp);
        }

        var args = new WorldMapArgs
        {
            FieldIndex = (byte)portal.FieldNumber,
            FieldName = name,
            Nodes = warpsList
        };

        Send(args);
    }

    #endregion

    #region WorldClient Logic

    public WorldClient AislingToGhostForm()
    {
        Aisling.Flags = AislingFlags.Ghost;
        Aisling.CurrentHp = 0;
        Aisling.CurrentMp = 0;
        Aisling.RegenTimerDisabled = true;
        UpdateDisplay();
        Task.Delay(500).ContinueWith(ct => { ClientRefreshed(); });
        return this;
    }

    public WorldClient GhostFormToAisling()
    {
        Aisling.Flags = AislingFlags.Normal;
        Aisling.RegenTimerDisabled = false;
        UpdateDisplay();
        Task.Delay(500).ContinueWith(ct => { ClientRefreshed(); });
        return this;
    }

    public void LearnSkill(Mundane source, SkillTemplate subject, string message)
    {
        var canLearn = false;

        if (subject.Prerequisites != null) canLearn = PayPrerequisites(subject.Prerequisites);
        if (subject.LearningRequirements is { Count: > 0 }) canLearn = subject.LearningRequirements.TrueForAll(PayPrerequisites);
        if (!canLearn)
        {
            this.SendOptionsDialog(source, "You do not seem to possess what is necessary to learn this skill");
            return;
        }

        var skill = Skill.GiveTo(Aisling, subject.Name);
        if (skill) LoadSkillBook();

        // Recall message set in message variable back to the npc
        this.SendOptionsDialog(source, message);
        Aisling.SendTargetedClientMethod(PlayerScope.NearbyAislings, c => c.SendAnimation(subject.TargetAnimation, null, Aisling.Serial));

        // After learning, ensure player's modifiers are set
        var item = new Item();
        item.ReapplyItemModifiers(this);
    }

    public void LearnSpell(Mundane source, SpellTemplate subject, string message)
    {
        var canLearn = false;

        if (subject.Prerequisites != null) canLearn = PayPrerequisites(subject.Prerequisites);
        if (subject.LearningRequirements is { Count: > 0 }) canLearn = subject.LearningRequirements.TrueForAll(PayPrerequisites);
        if (!canLearn)
        {
            this.SendOptionsDialog(source, "You do not seem to possess what is necessary to learn this spell");
            return;
        }

        var spell = Spell.GiveTo(Aisling, subject.Name);
        if (spell) LoadSpellBook();

        // Recall message set in message variable back to the npc
        this.SendOptionsDialog(source, message);
        Aisling.SendTargetedClientMethod(PlayerScope.NearbyAislings, c => c.SendAnimation(subject.TargetAnimation, null, Aisling.Serial));

        // After learning, ensure player's modifiers are set
        var item = new Item();
        item.ReapplyItemModifiers(this);
    }

    public void ClientRefreshed()
    {
        if (Aisling.Map.ID != ServerSetup.Instance.Config.TransitionZone) MapOpen = false;
        if (MapOpen) return;
        if (!CanRefresh) return;

        SendMapInfo();
        SendLocation();
        SendAttributes(StatUpdateType.Full);
        UpdateDisplay(true);

        var objects = ObjectManager.GetObjects(Aisling.Map, s => s.WithinRangeOf(Aisling), ObjectManager.Get.AllButAislings).ToList();

        if (objects.Count != 0)
        {
            objects.Reverse();
            SendVisibleEntities(objects);
        }

        SendMapLoadComplete();
        SendDisplayAisling(Aisling);
        SendRefreshResponse();

        Aisling.Client.LastMapUpdated = DateTime.UtcNow;
        Aisling.Client.LastLocationSent = DateTime.UtcNow;
        Aisling.Client.LastClientRefresh = DateTime.UtcNow;
    }

    public void DaydreamingRoutine()
    {
        if (!_dayDreamingControl.IsRunning)
        {
            _dayDreamingControl.Start();
        }

        if (_dayDreamingControl.Elapsed.TotalMilliseconds < _dayDreamingTimer.Delay.TotalMilliseconds) return;
        _dayDreamingControl.Restart();
        if (Aisling.Direction is not (1 or 2)) return;
        if (!((DateTime.UtcNow - Aisling.AislingTracker).TotalMinutes > 2)) return;
        if (!Socket.Connected || !IsDayDreaming) return;

        Aisling.SendTargetedClientMethod(PlayerScope.NearbyAislings, c => c.SendBodyAnimation(Aisling.Serial, (BodyAnimation)16, 100));
        Aisling.SendTargetedClientMethod(PlayerScope.NearbyAislings, c => c.SendAnimation(32, Aisling.Position));
        if (Aisling.Resting == Enums.RestPosition.RestPosition1) return;
        Aisling.Resting = Enums.RestPosition.RestPosition1;
        Aisling.Client.UpdateDisplay();
        Aisling.Client.SendDisplayAisling(Aisling);
    }

    public WorldClient SystemMessage(string message)
    {
        SendServerMessage(ServerMessageType.ActiveMessage, message);
        return this;
    }

    public async Task<bool> Save()
    {
        if (Aisling == null) return false;
        LastSave = DateTime.UtcNow;
        var saved = await StorageManager.AislingBucket.Save(Aisling);
        return saved;
    }

    public WorldClient UpdateDisplay(bool excludeSelf = false)
    {
        if (!excludeSelf)
            SendDisplayAisling(Aisling);

        var nearbyAislings = Aisling.AislingsNearby();

        if (nearbyAislings.Length == 0) return this;

        var self = Aisling;

        foreach (var nearby in nearbyAislings)
        {
            if (nearby is null) return this;
            if (self.Serial == nearby.Serial) continue;

            if (self.CanSeeSprite(nearby))
                nearby.ShowTo(self);
            else
                nearby.HideFrom(self);

            if (nearby.CanSeeSprite(self))
                self.ShowTo(nearby);
            else
                self.HideFrom(nearby);
        }

        return this;
    }

    public WorldClient PayItemPrerequisites(LearningPredicate prerequisites)
    {
        if (prerequisites.ItemsRequired is not { Count: > 0 }) return this;

        // Item Required
        foreach (var retainer in prerequisites.ItemsRequired)
        {
            // Inventory Fetch
            var items = Aisling.Inventory.Get(i => i.Template.Name == retainer.Item);

            // Loop for item
            foreach (var item in items)
            {
                // Loop for non-stacked item
                if (!item.Template.CanStack)
                {
                    for (var j = 0; j < retainer.AmountRequired; j++)
                    {
                        var itemLoop = Aisling.Inventory.Get(i => i.Template.Name == retainer.Item);
                        Aisling.Inventory.RemoveFromInventory(this, itemLoop.First());
                    }

                    break;
                }

                // Handle stacked item
                Aisling.Inventory.RemoveRange(Aisling.Client, item, retainer.AmountRequired);
                break;
            }
        }

        return this;
    }

    public bool PayPrerequisites(LearningPredicate prerequisites)
    {
        if (prerequisites == null) return false;
        if (Aisling.GameMaster) return true;

        PayItemPrerequisites(prerequisites);
        {
            if (prerequisites.GoldRequired > 0)
            {
                if (Aisling.GoldPoints < prerequisites.GoldRequired) return false;
                Aisling.GoldPoints -= prerequisites.GoldRequired;
            }

            SendAttributes(StatUpdateType.ExpGold);
            return true;
        }
    }

    public bool CheckReqs(WorldClient client, Item item)
    {
        // Game Master check
        if (client.Aisling.GameMaster)
        {
            if (item.Durability >= 1)
            {
                return true;
            }
        }

        // Durability check
        if (item.Durability <= 0 && item.Template.Flags.FlagIsSet(ItemFlags.Equipable))
        {
            client.SendServerMessage(ServerMessageType.ActiveMessage, "I'll need to repair this before I can use it again.");
            return false;
        }

        // Level check
        if (client.Aisling.ExpLevel < item.Template.LevelRequired)
        {
            client.SendServerMessage(ServerMessageType.ActiveMessage, "This item is simply too powerful for me.");
            return false;
        }

        // Stage check
        if (client.Aisling.Stage < item.Template.StageRequired)
        {
            client.SendServerMessage(ServerMessageType.ActiveMessage, "I do not have the expertise for this.");
            return false;
        }

        // Job Level Check
        if (client.Aisling.AbpLevel < item.Template.JobLevelRequired)
        {
            client.SendServerMessage(ServerMessageType.ActiveMessage, "I do not have the job level for this.");
            return false;
        }

        // Class check
        if (item.Template.Class != Class.Peasant)
        {
            // Past class check
            if (item.Template.Class != client.Aisling.PastClass)
            {
                // Current class check
                if (item.Template.Class != client.Aisling.Path)
                {
                    client.SendServerMessage(ServerMessageType.ActiveMessage, "This doesn't fit my class.");
                    return false;
                }
            }
        }

        // Job Check
        if (item.Template.JobRequired != AdvClass.None)
        {
            if (client.Aisling.JobClass != item.Template.JobRequired)
            {
                client.SendServerMessage(ServerMessageType.ActiveMessage, "This doesn't quite match my profession.");
                return false;
            }
        }

        switch (item.Template.Gender)
        {
            case Enums.Gender.Male:
                var canUseMale = Aisling.Gender is Enums.Gender.Male;
                if (canUseMale) return true;
                break;
            case Enums.Gender.Female:
                var canUseFemale = Aisling.Gender is Enums.Gender.Female;
                if (canUseFemale) return true;
                break;
            case Enums.Gender.Unisex:
                return true;
        }

        client.SendServerMessage(ServerMessageType.ActiveMessage, "Doesn't seem to fit.");
        return false;
    }

    public WorldClient Insert(bool update, bool delete)
    {
        var obj = ObjectManager.GetObject<Aisling>(null, aisling => aisling.Serial == Aisling.Serial
                                                                    || string.Equals(aisling.Username, Aisling.Username, StringComparison.CurrentCultureIgnoreCase));

        if (obj == null)
        {
            ObjectManager.AddObject(Aisling);
        }
        else
        {
            obj.Remove(update, delete);
            ObjectManager.AddObject(Aisling);
        }

        return this;
    }

    public void Interrupt()
    {
        WorldServer.CancelIfCasting(this);
        ClientRefreshed();
    }

    public void WorldMapInterrupt()
    {
        WorldServer.CancelIfCasting(this);
    }

    public void ForgetSkill(string s)
    {
        var subject = Aisling.SkillBook.Skills.Values
            .FirstOrDefault(i =>
                i?.Template != null && !string.IsNullOrEmpty(i.Template.Name) &&
                string.Equals(i.Template.Name, s, StringComparison.CurrentCultureIgnoreCase));

        if (subject != null)
        {
            ForgetSkillSend(subject);
            DeleteSkillFromDb(subject);
        }

        LoadSkillBook();
    }

    public void ForgetSkills()
    {
        var skills = Aisling.SkillBook.Skills.Values
            .Where(i => i?.Template != null).ToList();

        foreach (var skill in skills)
        {
            Task.Delay(100).ContinueWith(_ => ForgetSkillSend(skill));
            DeleteSkillFromDb(skill);
        }

        LoadSkillBook();
    }

    private void ForgetSkillSend(Skill skill)
    {
        Aisling.SkillBook.Remove(this, skill.Slot);
    }

    public void ForgetSpell(string s)
    {
        var subject = Aisling.SpellBook.Spells.Values
            .FirstOrDefault(i =>
                i?.Template != null && !string.IsNullOrEmpty(i.Template.Name) &&
                string.Equals(i.Template.Name, s, StringComparison.CurrentCultureIgnoreCase));

        if (subject != null)
        {
            ForgetSpellSend(subject);
            DeleteSpellFromDb(subject);
        }

        LoadSpellBook();
    }

    public void ForgetSpells()
    {
        var spells = Aisling.SpellBook.Spells.Values
            .Where(i => i?.Template != null).ToList();

        foreach (var spell in spells)
        {
            Task.Delay(100).ContinueWith(_ => ForgetSpellSend(spell));
            DeleteSpellFromDb(spell);
        }

        LoadSpellBook();
    }

    public void ForgetSpellSend(Spell spell)
    {
        Aisling.SpellBook.Remove(this, spell.Slot);
    }

    public void TrainSkill(Skill skill)
    {
        if (skill.Level >= skill.Template.MaxLevel) return;

        var levelUpRand = Generator.RandomNumPercentGen();
        if (skill.Uses >= 40 && skill.Template.SkillType != SkillScope.Assail)
            levelUpRand += 0.1;

        switch (levelUpRand)
        {
            case <= 0.99:
                return;
            case <= 0.995:
                skill.Level++;
                skill.Uses = 0;
                break;
            case <= 1:
                skill.Level++;
                skill.Level++;
                skill.Uses = 0;
                break;
        }

        TrainSkillAnnounce(skill);
        SendAddSkillToPane(skill);
        skill.CurrentCooldown = skill.Template.Cooldown;
        SendCooldown(true, skill.Slot, skill.CurrentCooldown);
    }

    private void TrainSkillAnnounce(Skill skill)
    {
        if (Aisling.Stage < ClassStage.Master)
        {
            if (skill.Level > 100) skill.Level = 100;
            SendServerMessage(ServerMessageType.ActiveMessage,
                skill.Level >= 100
                    ? string.Format(CultureInfo.CurrentUICulture, "{0} locked until master", skill.Template.Name)
                    : string.Format(CultureInfo.CurrentUICulture, "{0}, Lv:{1}", skill.Template.Name, skill.Level));
            return;
        }

        switch (skill.Template.SkillType)
        {
            case SkillScope.Assail:
                {
                    if (skill.Level > 350) skill.Level = 350;
                    SendServerMessage(ServerMessageType.ActiveMessage,
                        skill.Level >= 350
                            ? string.Format(CultureInfo.CurrentUICulture, "{0} mastered!", skill.Template.Name)
                            : string.Format(CultureInfo.CurrentUICulture, "{0}, Lv:{1}", skill.Template.Name, skill.Level));
                    break;
                }
            case SkillScope.Ability:
                {
                    if (skill.Level > 500) skill.Level = 500;
                    SendServerMessage(ServerMessageType.ActiveMessage,
                        skill.Level >= 500
                            ? string.Format(CultureInfo.CurrentUICulture, "{0} mastered!", skill.Template.Name)
                            : string.Format(CultureInfo.CurrentUICulture, "{0}, Lv:{1}", skill.Template.Name, skill.Level));
                    break;
                }
        }
    }

    public void TrainSpell(Spell spell)
    {
        if (spell.Level >= spell.Template.MaxLevel) return;

        var levelUpRand = Generator.RandomNumPercentGen();
        if (spell.Casts >= 40)
            levelUpRand += 0.1;

        switch (levelUpRand)
        {
            case <= 0.93:
                return;
            case <= 0.98:
                spell.Level++;
                spell.Casts = 0;
                break;
            case <= 1:
                spell.Level++;
                spell.Level++;
                spell.Casts = 0;
                break;
        }

        SendAddSpellToPane(spell);
        spell.CurrentCooldown = spell.Template.Cooldown;
        SendCooldown(false, spell.Slot, spell.CurrentCooldown);
        SendServerMessage(ServerMessageType.ActiveMessage,
            spell.Level >= 100
                ? string.Format(CultureInfo.CurrentUICulture, "{0} has been mastered.", spell.Template.Name)
                : string.Format(CultureInfo.CurrentUICulture, "{0} improved, Lv:{1}", spell.Template.Name, spell.Level));
    }

    public WorldClient ApproachGroup(Aisling targetAisling, IReadOnlyList<string> allowedMaps)
    {
        if (targetAisling.GroupParty?.PartyMembers == null) return this;
        foreach (var member in targetAisling.GroupParty.PartyMembers.Values.Where(member => member.Serial != Aisling.Serial).Where(member => allowedMaps.ListContains(member.Map.Name)))
        {
            member.Client.SendAnimation(67);
            member.Client.TransitionToMap(targetAisling.Map, targetAisling.Position);
        }

        return this;
    }

    public bool GiveItem(string itemName)
    {
        var item = new Item();
        item = item.Create(Aisling, itemName);

        return item.Template.Name != null && item.GiveTo(Aisling);
    }

    public void GiveQuantity(Aisling aisling, string itemName, int range)
    {
        var item = new Item();
        item = item.Create(aisling, itemName);
        item.Stacks = (ushort)range;
        var given = item.GiveTo(Aisling);
        if (given) return;
        Aisling.BankManager.Items.TryAdd(item.ItemId, item);
        SendServerMessage(ServerMessageType.ActiveMessage, "Issue with giving you the item directly, deposited to bank");
    }

    public void TakeAwayQuantity(Sprite owner, string item, int range)
    {
        var foundItem = Aisling.Inventory.Has(i => i.Template.Name.Equals(item, StringComparison.OrdinalIgnoreCase));
        if (foundItem == null) return;

        Aisling.Inventory.RemoveRange(Aisling.Client, foundItem, range);
    }

    public WorldClient LoggedIn(bool state)
    {
        Aisling.LoggedIn = state;

        return this;
    }

    public void Port(int i, int x = 0, int y = 0)
    {
        TransitionToMap(i, new Position(x, y));
    }

    public void ResetLocation(WorldClient client)
    {
        var reset = 0;

        while (reset == 0)
        {
            client.Aisling.Abyss = true;
            client.Port(ServerSetup.Instance.Config.TransitionZone, ServerSetup.Instance.Config.TransitionPointX, ServerSetup.Instance.Config.TransitionPointY);
            client.Aisling.Abyss = false;
            reset++;
        }
    }

    public void Recover()
    {
        Revive();
    }

    public void RevivePlayer(string u)
    {
        if (u is null) return;
        var user = ObjectManager.GetObject<Aisling>(null, i => i.Username.Equals(u, StringComparison.OrdinalIgnoreCase));

        if (user is { LoggedIn: true })
            user.Client.Revive();
    }

    public void GiveScar()
    {
        var item = new Legend.LegendItem
        {
            Key = $"Sp{EphemeralRandomIdGenerator<uint>.Shared.NextId}ark{EphemeralRandomIdGenerator<uint>.Shared.NextId}",
            IsPublic = true,
            Time = DateTime.UtcNow,
            Color = LegendColor.Red,
            Icon = (byte)LegendIcon.Warrior,
            Text = "Fragment of spark taken.."
        };

        Aisling.LegendBook.AddLegend(item, this);
    }

    public void RepairEquipment()
    {


        var reapplyMods = new Item()
        reapplyMods.ReapplyItemModifiers(Aisling.Client);

        SendAttributes(StatUpdateType.Full);
    }

    public bool Revive()
    {
        Aisling.Flags = AislingFlags.Normal;
        Aisling.RegenTimerDisabled = false;
        Aisling.CurrentHp = (long)(Aisling.MaximumHp * 0.80);
        Aisling.CurrentMp = (long)(Aisling.MaximumMp * 0.80);

        SendAttributes(StatUpdateType.Vitality);
        return Aisling.CurrentHp > 0;
    }

    public bool IsBehind(Sprite sprite)
    {
        var delta = sprite.Direction - Aisling.Direction;
        return Aisling.Position.IsNextTo(sprite.Position) && delta == 0;
    }

    public void KillPlayer(Area map, string u)
    {
        if (u is null) return;
        var user = ObjectManager.GetObject<Aisling>(map, i => i.Username.Equals(u, StringComparison.OrdinalIgnoreCase));

        if (user != null)
        {
            user.CurrentHp = 0;
            user.Client.DeathStatusCheck();
        }
    }

    #endregion

    #region Give Base Stats

    public void GiveHp(int v = 1)
    {
        Aisling.BaseHp += v;

        if (Aisling.BaseHp > ServerSetup.Instance.Config.MaxHP)
            Aisling.BaseHp = ServerSetup.Instance.Config.MaxHP;

        SendAttributes(StatUpdateType.Primary);
    }

    public void GiveMp(int v = 1)
    {
        Aisling.BaseMp += v;

        if (Aisling.BaseMp > ServerSetup.Instance.Config.MaxHP)
            Aisling.BaseMp = ServerSetup.Instance.Config.MaxHP;

        SendAttributes(StatUpdateType.Primary);
    }

    public void GiveStr(byte v = 1)
    {
        Aisling._Str += v;
        SendAttributes(StatUpdateType.Primary);
    }

    public void GiveInt(byte v = 1)
    {
        Aisling._Int += v;
        SendAttributes(StatUpdateType.Primary);
    }

    public void GiveWis(byte v = 1)
    {
        Aisling._Wis += v;
        SendAttributes(StatUpdateType.Primary);
    }

    public void GiveCon(byte v = 1)
    {
        Aisling._Con += v;
        SendAttributes(StatUpdateType.Primary);
    }

    public void GiveDex(byte v = 1)
    {
        Aisling._Dex += v;
        SendAttributes(StatUpdateType.Primary);
    }

    public void EnqueueExperienceEvent(Aisling player, long exp, bool hunting)
    {
        lock (_expQueueLock)
        {
            _expQueue.Enqueue(new ExperienceEvent(player, exp, hunting));
        }
    }

    public void EnqueueAbilityEvent(Aisling player, int exp, bool hunting)
    {
        lock (_apQueueLock)
        {
            _apQueue.Enqueue(new AbilityEvent(player, exp, hunting));
        }
    }

    public void EnqueueDebuffAppliedEvent(Sprite affected, Debuff debuff, TimeSpan timeLeft)
    {
        lock (_debuffQueueLockApply)
        {
            _debuffApplyQueue.Enqueue(new DebuffEvent(affected, debuff, timeLeft));
        }
    }

    public void EnqueueBuffAppliedEvent(Sprite affected, Buff buff, TimeSpan timeLeft)
    {
        lock (_buffQueueLockApply)
        {
            _buffApplyQueue.Enqueue(new BuffEvent(affected, buff, timeLeft));
        }
    }

    public void EnqueueDebuffUpdatedEvent(Sprite affected, Debuff debuff, TimeSpan timeLeft)
    {
        lock (_debuffQueueLockUpdate)
        {
            _debuffUpdateQueue.Enqueue(new DebuffEvent(affected, debuff, timeLeft));
        }
    }

    public void EnqueueBuffUpdatedEvent(Sprite affected, Buff buff, TimeSpan timeLeft)
    {
        lock (_buffQueueLockUpdate)
        {
            _buffUpdateQueue.Enqueue(new BuffEvent(affected, buff, timeLeft));
        }
    }

    public void GiveExp(long exp)
    {
        if (exp <= 0) exp = 1;

        // Enqueue experience event
        EnqueueExperienceEvent(Aisling, exp, false);
    }

    private static void HandleExp(Aisling player, long exp, bool hunting)
    {
        if (exp <= 0) exp = 1;

        if (hunting)
        {
            if (player.HasBuff("Double XP"))
                exp *= 2;

            if (player.HasBuff("Triple XP"))
                exp *= 3;

            if (player.GroupParty != null)
            {
                var groupSize = player.GroupParty.PartyMembers.Count;
                var adjustment = ServerSetup.Instance.Config.GroupExpBonus;

                if (groupSize > 7)
                {
                    adjustment = ServerSetup.Instance.Config.GroupExpBonus = (groupSize - 7) * 0.05;
                    if (adjustment < 0.75)
                    {
                        adjustment = 0.75;
                    }
                }

                var bonus = exp * (1 + player.GroupParty.PartyMembers.Count - 1) * adjustment / 100;
                if (bonus > 0)
                    exp += (int)bonus;
            }
        }

        if (long.MaxValue - player.ExpTotal < exp)
        {
            player.ExpTotal = long.MaxValue;
            player.Client.SendServerMessage(ServerMessageType.ActiveMessage, "Your experience box is full, ascend to carry more");
        }

        try
        {
            if (player.ExpLevel >= 500)
            {
                player.ExpNext = 0;
                player.ExpTotal += exp;
                player.Client.SendServerMessage(ServerMessageType.ActiveMessage, $"Received {exp:n0} experience points!");
                player.Client.SendAttributes(StatUpdateType.ExpGold);
                return;
            }

            player.ExpNext -= exp;

            if (player.ExpNext <= 0)
            {
                var extraExp = Math.Abs(player.ExpNext);
                var expToTotal = exp - extraExp;
                player.ExpTotal += expToTotal;
                player.Client.SendServerMessage(ServerMessageType.ActiveMessage, $"Received {expToTotal:n0} experience points!");
                player.Client.LevelUp(player, extraExp);
            }
            else
            {
                player.ExpTotal += exp;
                player.Client.SendServerMessage(ServerMessageType.ActiveMessage, $"Received {exp:n0} experience points!");
                player.Client.SendAttributes(StatUpdateType.ExpGold);
            }
        }
        catch (Exception e)
        {
            ServerSetup.EventsLogger($"Issue giving {player.Username} experience.");
        }
    }

    public void LevelUp(Aisling player, long extraExp)
    {
        // Set next level
        player.ExpLevel++;

        var seed = player.ExpLevel * 0.1 + 0.5;
        {
            if (player.ExpLevel > ServerSetup.Instance.Config.PlayerLevelCap) return;
        }

        if (player.ExpLevel >= 99)
            player.ExpNext = (long)(player.ExpLevel * seed * 25000 * 2.5);
        else if (player.ExpLevel >= 250)
            player.ExpNext = (long)(player.ExpLevel * seed * 50000 * 5);
        else if (player.ExpLevel >= 400)
            player.ExpNext = (long)(player.ExpLevel * seed * 75000 * 10);
        else
            player.ExpNext = (long)(player.ExpLevel * seed * 5000);

        if (player.ExpNext <= 0)
        {
            player.Client.SendServerMessage(ServerMessageType.ActiveMessage, "Issue leveling up; Error: Mt. Everest");
            return;
        }

        if (player.ExpLevel >= 250)
            player.StatPoints += 1;
        else
            player.StatPoints += (short)ServerSetup.Instance.Config.StatsPerLevel;

        // Set vitality
        player.BaseHp += ((int)(ServerSetup.Instance.Config.HpGainFactor * player._Con * 0.65)).IntClamp(0, 300);
        player.BaseMp += ((int)(ServerSetup.Instance.Config.MpGainFactor * player._Wis * 0.45)).IntClamp(0, 300);
        player.CurrentHp = player.MaximumHp;
        player.CurrentMp = player.MaximumMp;
        player.Client.SendAttributes(StatUpdateType.Full);

        player.Client.SendServerMessage(ServerMessageType.ActiveMessage, $"{ServerSetup.Instance.Config.LevelUpMessage}, Insight:{player.ExpLevel}");
        player.SendTargetedClientMethod(PlayerScope.NearbyAislings, c => c.SendAnimation(79, null, player.Serial, 64));

        if (extraExp > 0)
            GiveExp(extraExp);
    }

    public void GiveAp(int exp)
    {
        if (exp <= 0) exp = 1;

        // Enqueue ap event
        EnqueueAbilityEvent(Aisling, exp, false);
    }

    private static void HandleAp(Aisling player, int exp, bool hunting)
    {
        if (exp <= 0) exp = 1;

        if (hunting)
        {
            if (player.HasBuff("Double XP"))
                exp *= 2;

            if (player.HasBuff("Triple XP"))
                exp *= 3;

            if (player.GroupParty != null)
            {
                var groupSize = player.GroupParty.PartyMembers.Count;
                var adjustment = ServerSetup.Instance.Config.GroupExpBonus;

                if (groupSize > 7)
                {
                    adjustment = ServerSetup.Instance.Config.GroupExpBonus = (groupSize - 7) * 0.05;
                    if (adjustment < 0.75)
                    {
                        adjustment = 0.75;
                    }
                }

                var bonus = exp * (1 + player.GroupParty.PartyMembers.Count - 1) * adjustment / 100;
                if (bonus > 0)
                    exp += (int)bonus;
            }
        }

        if (long.MaxValue - player.AbpTotal < exp)
        {
            player.AbpTotal = long.MaxValue;
            player.Client.SendServerMessage(ServerMessageType.ActiveMessage, "Your ability box is full, ascend to carry more");
        }

        try
        {
            if (player.AbpLevel >= 500)
            {
                player.AbpNext = 0;
                player.AbpTotal += exp;
                player.Client.SendServerMessage(ServerMessageType.ActiveMessage, $"Received {exp:n0} ability points!");
                player.Client.SendAttributes(StatUpdateType.ExpGold);
                return;
            }

            player.AbpNext -= exp;

            if (player.AbpNext <= 0)
            {
                var extraExp = Math.Abs(player.AbpNext);
                var expToTotal = exp - extraExp;
                player.AbpTotal += expToTotal;
                player.Client.SendServerMessage(ServerMessageType.ActiveMessage, $"Received {expToTotal:n0} ability points!");
                player.Client.DarkRankUp(player, extraExp);
            }
            else
            {
                player.AbpTotal += exp;
                player.Client.SendServerMessage(ServerMessageType.ActiveMessage, $"Received {exp:n0} ability points!");
                player.Client.SendAttributes(StatUpdateType.ExpGold);
            }
        }
        catch (Exception e)
        {
            ServerSetup.EventsLogger($"Issue giving {player.Username} ability points.");
        }
    }

    public void DarkRankUp(Aisling player, int extraExp)
    {
        player.AbpLevel++;

        var seed = player.AbpLevel * 0.5 + 0.8;
        {
            if (player.AbpLevel > ServerSetup.Instance.Config.PlayerLevelCap) return;
        }
        player.AbpNext = (int)(player.AbpLevel * seed * 5000);

        if (player.AbpNext <= 0)
        {
            player.Client.SendServerMessage(ServerMessageType.ActiveMessage, "Issue leveling up; Error: Mt. Everest");
            return;
        }

        // Set next level
        player.StatPoints += 1;

        // Set vitality
        player.BaseHp += ((int)(ServerSetup.Instance.Config.HpGainFactor * player._Con * 1.23)).IntClamp(0, 1000);
        player.BaseMp += ((int)(ServerSetup.Instance.Config.MpGainFactor * player._Wis * 0.90)).IntClamp(0, 1000);
        player.CurrentHp = player.MaximumHp;
        player.CurrentMp = player.MaximumMp;
        player.Client.SendAttributes(StatUpdateType.Full);

        player.Client.SendServerMessage(ServerMessageType.ActiveMessage, $"{ServerSetup.Instance.Config.AbilityUpMessage}, Job Level:{player.AbpLevel}");
        player.SendTargetedClientMethod(PlayerScope.NearbyAislings, c => c.SendAnimation(385, null, player.Serial, 75));

        if (extraExp > 0)
            GiveAp(extraExp);
    }

    #endregion

    #region Warping & Maps

    public WorldClient RefreshMap(bool updateView = false)
    {
        MapUpdating = true;

        if (Aisling.IsBlind)
            SendAttributes(StatUpdateType.Secondary);

        SendMapChangePending();
        SendMapInfo();
        SendLocation();

        if (Aisling.Map is not { Script.Item1: null }) return this;

        if (string.IsNullOrEmpty(Aisling.Map.ScriptKey)) return this;
        var scriptToType = ScriptManager.Load<AreaScript>(Aisling.Map.ScriptKey, Aisling.Map);
        var scriptFoundGetValue = scriptToType.TryGetValue(Aisling.Map.ScriptKey, out var script);
        if (scriptFoundGetValue)
            Aisling.Map.Script = new Tuple<string, AreaScript>(Aisling.Map.ScriptKey, script);

        return this;
    }

    public WorldClient TransitionToMap(Area area, Position position)
    {
        if (area == null) return this;

        if (area.ID != Aisling.CurrentMapId)
        {
            LeaveArea(area.ID, true, true);

            Aisling.LastPosition = new Position(Aisling.Pos);
            Aisling.Pos = new Vector2(position.X, position.Y);
            Aisling.CurrentMapId = area.ID;

            Enter();
        }
        else
        {
            Aisling.LastPosition = new Position(Aisling.Pos);
            Aisling.Pos = new Vector2(position.X, position.Y);
            Aisling.CurrentMapId = area.ID;
            WarpToAndRefresh(position);
        }

        // ToDo: Logic to only play this if a menu is opened.
        this.CloseDialog();

        return this;
    }

    public WorldClient TransitionToMap(int area, Position position)
    {
        if (!ServerSetup.Instance.GlobalMapCache.TryGetValue(area, out var target)) return this;
        if (target == null) return this;

        if (Aisling.LastMapId != target.ID)
        {
            LeaveArea(target.ID, true, true);

            Aisling.LastPosition = new Position(Aisling.Pos);
            Aisling.Pos = new Vector2(position.X, position.Y);
            Aisling.CurrentMapId = target.ID;

            Enter();
        }
        else
        {
            Aisling.LastPosition = new Position(Aisling.Pos);
            Aisling.Pos = new Vector2(position.X, position.Y);
            Aisling.CurrentMapId = target.ID;
            WarpToAndRefresh(position);
        }

        // ToDo: Logic to only play this if a menu is opened.
        this.CloseDialog();

        return this;
    }

    public void WarpToAdjacentMap(WarpTemplate warps)
    {
        if (warps.WarpType == WarpType.World) return;

        if (!Aisling.GameMaster)
        {
            if (warps.LevelRequired > 0 && Aisling.ExpLevel < warps.LevelRequired)
            {
                var msgTier = Math.Abs(Aisling.ExpLevel - warps.LevelRequired);

                SendServerMessage(ServerMessageType.ActiveMessage, msgTier <= 15
                    ? $"You're too afraid to enter. {{=c{warps.LevelRequired} REQ"
                    : $"{{=bNightmarish visions of your own death repel you. {{=c{warps.LevelRequired} REQ)");

                return;
            }
        }

        if (Aisling.Map.ID != warps.To.AreaID)
        {
            TransitionToMap(warps.To.AreaID, warps.To.Location);
        }
        else
        {
            WarpToAndRefresh(warps.To.Location);
        }
    }

    public void WarpTo(Position position)
    {
        Aisling.Pos = new Vector2(position.X, position.Y);
    }

    public void WarpToAndRefresh(Position position)
    {
        Aisling.Pos = new Vector2(position.X, position.Y);
        ClientRefreshed();
    }

    public void CheckWarpTransitions(WorldClient client)
    {
        foreach (var (_, value) in ServerSetup.Instance.GlobalWarpTemplateCache)
        {
            var breakOuterLoop = false;
            if (value.ActivationMapId != client.Aisling.CurrentMapId) continue;

            lock (_warpCheckLock)
            {
                foreach (var warp in value.Activations.Where(o =>
                             o.Location.X == (int)client.Aisling.Pos.X &&
                             o.Location.Y == (int)client.Aisling.Pos.Y))
                {
                    if (value.WarpType == WarpType.Map)
                    {
                        if (client.Aisling.Map.ID == value.To.AreaID)
                        {
                            WarpToAndRefresh(value.To.Location);
                            breakOuterLoop = true;
                            break;
                        }

                        client.WarpToAdjacentMap(value);
                        breakOuterLoop = true;
                        break;
                    }

                    if (value.WarpType != WarpType.World) continue;
                    if (!ServerSetup.Instance.GlobalWorldMapTemplateCache.ContainsKey(value.To.PortalKey)) return;
                    if (client.Aisling.World != value.To.PortalKey) client.Aisling.World = (byte)value.To.PortalKey;

                    var portal = new PortalSession();
                    portal.TransitionToMap(client);
                    breakOuterLoop = true;
                    client.WorldMapInterrupt();
                    break;
                }
            }

            if (breakOuterLoop) break;
        }
    }

    public void CheckWarpTransitions(WorldClient client, int x, int y)
    {
        foreach (var (_, value) in ServerSetup.Instance.GlobalWarpTemplateCache)
        {
            var breakOuterLoop = false;
            if (value.ActivationMapId != client.Aisling.CurrentMapId) continue;

            lock (_warpCheckLock)
            {
                foreach (var _ in value.Activations.Where(o =>
                             o.Location.X == x &&
                             o.Location.Y == y))
                {
                    if (value.WarpType == WarpType.Map)
                    {
                        client.WarpToAdjacentMap(value);
                        breakOuterLoop = true;
                        break;
                    }

                    if (value.WarpType != WarpType.World) continue;
                    if (!ServerSetup.Instance.GlobalWorldMapTemplateCache.ContainsKey(value.To.PortalKey)) return;
                    if (client.Aisling.World != value.To.PortalKey) client.Aisling.World = (byte)value.To.PortalKey;

                    var portal = new PortalSession();
                    portal.TransitionToMap(client);
                    breakOuterLoop = true;
                    client.WorldMapInterrupt();
                    break;
                }
            }

            if (breakOuterLoop) break;
        }
    }

    public void ReapplyKillCount()
    {
        var hasKills = ServerSetup.Instance.GlobalKillRecordCache.TryGetValue(Aisling.Serial, out var killRecords);
        if (hasKills)
        {
            Aisling.MonsterKillCounters = killRecords;
        }
    }

    public WorldClient Enter()
    {
        Insert(true, false);
        RefreshMap();
        UpdateDisplay(true);
        CompleteMapTransition();

        Aisling.Client.LastMapUpdated = DateTime.UtcNow;
        Aisling.Client.LastLocationSent = DateTime.UtcNow;
        Aisling.Map.Script.Item2.OnMapEnter(this);

        return this;
    }

    public WorldClient LeaveArea(int travelTo, bool update = false, bool delete = false)
    {
        if (Aisling.LastMapId == ushort.MaxValue) Aisling.LastMapId = Aisling.CurrentMapId;

        Aisling.Remove(update, delete);

        if (Aisling.LastMapId != travelTo && Aisling.Map.Script.Item2 != null)
            Aisling.Map.Script.Item2.OnMapExit(this);

        return this;
    }

    public void CompleteMapTransition()
    {
        foreach (var (_, area) in ServerSetup.Instance.GlobalMapCache)
        {
            if (Aisling.CurrentMapId != area.ID) continue;
            var mapFound = ServerSetup.Instance.GlobalMapCache.TryGetValue(area.ID, out var newMap);
            if (mapFound)
            {
                Aisling.CurrentMapId = newMap.ID;

                var onMap = Aisling.Map.IsLocationOnMap(Aisling);
                if (!onMap)
                {
                    TransitionToMap(3052, new Position(27, 18));
                    SendServerMessage(ServerMessageType.OrangeBar1, "Something grabs your hand...");
                    return;
                }

                if (newMap.ID == 7000)
                {
                    SendServerMessage(ServerMessageType.ScrollWindow,
                        "{=bLife{=a, all that you know, love, and cherish. Everything, and the very fabric of their being. \n\nThe aisling spark, creativity, passion. All of that lives within you." +
                        "\n\nThis story begins shortly after Anaman Pact successfully revives {=bChadul{=a. \n\n-{=cYou feel a sense of unease come over you{=a-");
                }
            }
            else
            {
                TransitionToMap(3052, new Position(27, 18));
                SendServerMessage(ServerMessageType.OrangeBar1, "Something grabs your hand...");
                return;
            }
        }

        var objects = ObjectManager.GetObjects(Aisling.Map, s => s.WithinRangeOf(Aisling), ObjectManager.Get.AllButAislings).ToList();

        if (objects.Count != 0)
        {
            objects.Reverse();
            SendVisibleEntities(objects);
        }

        SendMapChangeComplete();

        if (LastMap == null || LastMap.Music != Aisling.Map.Music)
        {
            SendSound((byte)Aisling.Map.Music, true);
        }

        Aisling.LastMapId = Aisling.CurrentMapId;
        LastMap = Aisling.Map;

        if (Aisling.DiscoveredMaps.All(i => i != Aisling.CurrentMapId))
            AddDiscoveredMapToDb();

        SendMapLoadComplete();
        SendDisplayAisling(Aisling);
        MapUpdating = false;
    }

    #endregion

    #region SQL

    public void DeleteSkillFromDb(Skill skill)
    {
        var sConn = new SqlConnection(AislingStorage.ConnectionString);
        if (skill.SkillName is null) return;

        try
        {
            sConn.Open();
            const string cmd = "DELETE FROM LegendsPlayers.dbo.PlayersSkillBook WHERE SkillName = @SkillName AND Serial = @AislingSerial";
            sConn.Execute(cmd, new
            {
                skill.SkillName,
                AislingSerial = (long)Aisling.Serial
            });
            sConn.Close();
        }
        catch (Exception e)
        {
            ServerSetup.EventsLogger(e.Message, LogLevel.Error);
            ServerSetup.EventsLogger(e.StackTrace, LogLevel.Error);
        }
    }

    public void DeleteSpellFromDb(Spell spell)
    {
        var sConn = new SqlConnection(AislingStorage.ConnectionString);
        if (spell.SpellName is null) return;

        try
        {
            sConn.Open();
            const string cmd = "DELETE FROM LegendsPlayers.dbo.PlayersSpellBook WHERE SpellName = @SpellName AND Serial = @AislingSerial";
            sConn.Execute(cmd, new
            {
                spell.SpellName,
                AislingSerial = (long)Aisling.Serial
            });
            sConn.Close();
        }
        catch (Exception e)
        {
            ServerSetup.EventsLogger(e.Message, LogLevel.Error);
            ServerSetup.EventsLogger(e.StackTrace, LogLevel.Error);
        }
    }

    public void AddDiscoveredMapToDb()
    {
        try
        {
            Aisling.DiscoveredMaps.Add(Aisling.CurrentMapId);

            var sConn = new SqlConnection(AislingStorage.ConnectionString);
            sConn.Open();
            var cmd = new SqlCommand("FoundMap", sConn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add("@Serial", SqlDbType.BigInt).Value = (long)Aisling.Serial;
            cmd.Parameters.Add("@MapId", SqlDbType.Int).Value = Aisling.CurrentMapId;

            cmd.CommandTimeout = 5;
            cmd.ExecuteNonQuery();
            sConn.Close();
        }
        catch (SqlException e)
        {
            if (e.Message.Contains("PK__Players"))
            {
                SendServerMessage(ServerMessageType.ActiveMessage, "Issue with saving new found map. Contact GM");
                return;
            }

            ServerSetup.EventsLogger(e.Message, LogLevel.Error);
            ServerSetup.EventsLogger(e.StackTrace, LogLevel.Error);
        }
        catch (Exception e)
        {
            ServerSetup.EventsLogger(e.Message, LogLevel.Error);
            ServerSetup.EventsLogger(e.StackTrace, LogLevel.Error);
        }
    }

    public void AddToIgnoreListDb(string ignored)
    {
        try
        {
            Aisling.IgnoredList.Add(ignored);

            var sConn = new SqlConnection(AislingStorage.ConnectionString);
            sConn.Open();
            var cmd = new SqlCommand("IgnoredSave", sConn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add("@Serial", SqlDbType.BigInt).Value = (long)Aisling.Serial;
            cmd.Parameters.Add("@PlayerIgnored", SqlDbType.VarChar).Value = ignored;

            cmd.CommandTimeout = 5;
            cmd.ExecuteNonQuery();
            sConn.Close();
        }
        catch (SqlException e)
        {
            if (e.Message.Contains("PK__Players"))
            {
                SendServerMessage(ServerMessageType.ActiveMessage, "Issue with saving player to ignored list. Contact GM");
                return;
            }

            ServerSetup.EventsLogger(e.Message, LogLevel.Error);
            ServerSetup.EventsLogger(e.StackTrace, LogLevel.Error);
        }
        catch (Exception e)
        {
            ServerSetup.EventsLogger(e.Message, LogLevel.Error);
            ServerSetup.EventsLogger(e.StackTrace, LogLevel.Error);
        }
    }

    public void RemoveFromIgnoreListDb(string ignored)
    {
        try
        {
            Aisling.IgnoredList.Remove(ignored);

            var sConn = new SqlConnection(AislingStorage.ConnectionString);
            sConn.Open();
            const string playerIgnored = "DELETE FROM LegendsPlayers.dbo.PlayersIgnoreList WHERE Serial = @AislingSerial AND PlayerIgnored = @ignored";
            sConn.Execute(playerIgnored, new
            {
                AislingSerial = (long)Aisling.Serial,
                ignored
            });
            sConn.Close();
        }
        catch (Exception e)
        {
            ServerSetup.EventsLogger(e.Message, LogLevel.Error);
            ServerSetup.EventsLogger(e.StackTrace, LogLevel.Error);
        }
    }

    #endregion

    #region Api Modifications
    Random rand = new();
    #region Regular Legend Counters
    public void NightmareLegend()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var Fae in legend)
            if (Fae.Category == "Nightmare")
            {
                Aisling.LegendBook.LegendMarks.Remove(Fae);
                Legend.RemoveFromDB(Aisling.Client, Fae);
            }
    }
    public void MentorLegend()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var Fae in legend)
            if (Fae.Category == "Mentor")
            {
                Aisling.LegendBook.LegendMarks.Remove(Fae);
                Legend.RemoveFromDB(Aisling.Client, Fae);
            }
    }
    public void DojoLegend()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var Dojo in legend)
            if (Dojo.Category == "Dojo")
            {
                Aisling.LegendBook.LegendMarks.Remove(Dojo);
                Legend.RemoveFromDB(Aisling.Client, Dojo);
            }
    }
    public void MenteeLegend()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var Fae in legend)
            if (Fae.Category == "Mentee")
            {
                Aisling.LegendBook.LegendMarks.Remove(Fae);
                Legend.RemoveFromDB(Aisling.Client, Fae);
            }
    }
    public void FaeLegend()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var Fae in legend)
            if (Fae.Category == "Fae")
            {
                Aisling.LegendBook.LegendMarks.Remove(Fae);
                Legend.RemoveFromDB(Aisling.Client, Fae);
            }
    }
    public void ReligionLegend()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var God in legend)
            if (God.Category == "Religion")
            {
                Aisling.LegendBook.LegendMarks.Remove(God);
                Legend.RemoveFromDB(Aisling.Client, God);
            }
    }
    public void GeasLegend()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var God in legend)
            if (God.Category == "Geas")
            {
                Aisling.LegendBook.LegendMarks.Remove(God);
                Legend.RemoveFromDB(Aisling.Client, God);
            }
    }
    public void MassLegend()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var God in legend)
            if (God.Category == "Mass")
            {
                Aisling.LegendBook.LegendMarks.Remove(God);
                Legend.RemoveFromDB(Aisling.Client, God);
            }
    }
    public void MassCount()
    {
        {
            var legend = Aisling.LegendBook.LegendMarks.ToList();
            foreach (var God in legend)
                if (God.Category == "MassCount")
                {
                    Aisling.LegendBook.LegendMarks.Remove(God);
                    Legend.RemoveFromDB(Aisling.Client, God);
                }
        }
    }
    public void InitiateCount()
    {
        {
            var legend = Aisling.LegendBook.LegendMarks.ToList();
            foreach (var God in legend)
                if (God.Category == "Initiates")
                {
                    Aisling.LegendBook.LegendMarks.Remove(God);
                    Legend.RemoveFromDB(Aisling.Client, God);
                }
        }
    }
    public void AltarCount()
    {
        {
            var legend = Aisling.LegendBook.LegendMarks.ToList();
            foreach (var God in legend)
                if (God.Category == "DanaanAltar")
                {
                    Aisling.LegendBook.LegendMarks.Remove(God);
                    Legend.RemoveFromDB(Aisling.Client, God);
                }
        }
    }
    #endregion Regular Legend Counters
    #region Spell Methods
    public GameClient ApproachGroup(Aisling targetAisling, IReadOnlyList<string> allowedMaps)
    {
        var client = Aisling.Client;
        if (client.Aisling.PartyMembers != null)
        {
            if (client.Aisling.GroupParty.PartyMembers.Contains(targetAisling))
            {
                //don't include yourself.
                if (targetAisling.Serial == Aisling.Serial)
                {
                    Aisling.Client.SendMessage(0x02, "You can't teleport to yourself.");
                    return this;
                }

                //only work on maps we allow.
                if (!allowedMaps.Contains(targetAisling.Map.Name))
                {
                    Aisling.Client.SendMessage(0x02, "Powerful magic blocks your spell.");
                    return this;
                }
                else
                {
                    //summon myself to player's area and position.

                    client.SendMessage(0x02, $"You teleport to {targetAisling.Username}.");
                    client.Aisling.Show(Scope.NearbyAislings, new ServerFormat29((ushort)67, (ushort)client.Aisling.XPos, (ushort)client.Aisling.YPos));
                    targetAisling.Client.SendMessage(0x02, $"{client.Aisling.Username} teleports to your location.");
                    client.TransitionToMap(targetAisling.Map, targetAisling.Position);
                    var action = new ServerFormat1A
                    {
                        Serial = client.Serial,
                        Number = (byte)(client.Aisling.Path == Class.Priest ? 0x80 :
                            client.Aisling.Path == Class.Wizard ? 0x88 : 0x06),
                        Speed = 30
                    };

                    client.Aisling.Show(Scope.NearbyAislings, action);
                    client.Aisling.Show(Scope.NearbyAislings, new ServerFormat19(8));
                    client.Aisling.Show(Scope.NearbyAislings, new ServerFormat29((ushort)67, (ushort)targetAisling.XPos, (ushort)targetAisling.YPos));

                }
            }
            else if (!client.Aisling.GroupParty.PartyMembers.Contains(targetAisling))
                client.SendMessage(0x02, "You may only approach a member of your party.");
        }
        else
            Aisling.Client.SendMessage(0x02, "You must be grouped to use this spell.");

        return this;
    }
    public GameClient SummonGroup(Sprite summoner, Aisling targetAisling, IReadOnlyList<string> allowedMaps)
    {
        if (summoner.Client.Aisling.PartyMembers != null)
        {
            if (summoner.Client.Aisling.GroupParty.PartyMembers.Contains(targetAisling))
            {
                //don't include yourself.
                if (targetAisling.Serial == Aisling.Serial)
                {
                    summoner.Client.SendMessage(0x02, "You can't summon yourself.");
                    return this;

                }
                //only work on maps we allow.
                if (!allowedMaps.Contains(targetAisling.Map.Name) || (!allowedMaps.Contains(summoner.Map.Name)))
                {
                    summoner.Client.SendMessage(0x02, "Powerful magic blocks your spell.");
                    return this;
                }
                else
                {
                    //summon player to my area and position.
                    var client = (summoner as Aisling).Client;
                    client.Aisling.Show(Scope.NearbyAislings, new ServerFormat29((ushort)67, (ushort)targetAisling.XPos, (ushort)targetAisling.YPos));
                    targetAisling.Client.TransitionToMap(Aisling.Map, Aisling.Position);
                    targetAisling.Client.SendMessage(0x02, $"{client.Aisling.Username} summons you to their location.");
                    client.SendMessage(0x02, $"{targetAisling.Username} is summoned to your location.");
                    var action = new ServerFormat1A
                    {
                        Serial = summoner.Serial,
                        Number = (byte)(client.Aisling.Path == Class.Priest ? 0x80 :
                            client.Aisling.Path == Class.Wizard ? 0x88 : 0x06),
                        Speed = 30
                    };

                    client.Aisling.Show(Scope.NearbyAislings, action);
                    client.Aisling.Show(Scope.NearbyAislings, new ServerFormat19(8));
                    client.Aisling.Show(Scope.NearbyAislings, new ServerFormat29((ushort)67, (ushort)summoner.XPos, (ushort)summoner.YPos));

                }
            }
            else if (!summoner.Client.Aisling.GroupParty.PartyMembers.Contains(targetAisling))
                summoner.Client.SendMessage(0x02, "You may only summon your own party members.");
        }
        else
            summoner.Client.SendMessage(0x02, "You must be grouped to use this spell.");

        return this;
    }
    #endregion Spell Methods
    #region Religion Methods
    #region Prayer
    public void PrayAlone(GameClient client)
    {
        int r = 1 + (client.Aisling.Faith / 25);
        if (r <= 0)
            r = 1;

        if (r > 5)
            r = 5;

        switch (r)
        {
            case 1:
                {
                    client.SendAnimation(1, client.Aisling, client.Aisling);
                    client.Aisling.Faith += 1;
                    client.Aisling.ExpTotal += 5;
                    client.SendMessage(0x02, "You bow your head in reverence.");
                    client.Aisling.LastPrayed = DateTime.UtcNow;
                    client.CloseDialog();
                }
                break;
            case 2:
                {
                    client.SendAnimation(127, client.Aisling, client.Aisling);
                    client.Aisling.Faith += 2;
                    client.Aisling.ExpTotal += 100;
                    client.SendMessage(0x02, "Your fervent prayers are heard.");
                    client.SendStats(StatusFlags.All);
                    client.Aisling.LastPrayed = DateTime.UtcNow;
                    client.CloseDialog();
                }
                break;
            case 3:
                {
                    client.SendAnimation(128, client.Aisling, client.Aisling);
                    client.Aisling.Faith += 3;
                    client.Aisling.ExpTotal += 250;
                    client.SendMessage(0x02, "You feel your Patron's hand on your shoulder.");
                    client.SendStats(StatusFlags.All);
                    client.Aisling.LastPrayed = DateTime.UtcNow;
                    client.CloseDialog();
                }
                break;
            case 4:
                {
                    client.SendAnimation(277, client.Aisling, client.Aisling);
                    client.Aisling.Faith += 4;
                    client.Aisling.ExpTotal += 500;
                    client.SendMessage(0x02, "Blinding light surrounds you as your prayers are heard.");
                    client.SendStats(StatusFlags.All);
                    client.Aisling.LastPrayed = DateTime.UtcNow;
                    client.CloseDialog();
                }
                break;
            case 5:
                {
                    client.SendAnimation(299, client.Aisling, client.Aisling);
                    client.Aisling.Faith += 5;
                    client.Aisling.ExpTotal += 1000;
                    client.SendMessage(0x02, "Your Patron recognizes your devout faith, and touches your very soul.");
                    client.SendStats(StatusFlags.All);
                    client.Aisling.LastPrayed = DateTime.UtcNow;
                    client.CloseDialog();
                }
                break;
        }

    }
    public void PrayTogether(GameClient client, GameClient ally)
    {
        int r = 1 + (client.Aisling.Faith / 25) + (ally.Aisling.Faith / 50);
        if (r <= 0)
            r = 1;

        if (r > 6)
            r = 6;

        switch (r)
        {
            case 1:
                {
                    client.SendAnimation(1, client.Aisling, client.Aisling);
                    client.Aisling.Faith += 1;
                    client.Aisling.ExpTotal += 5;
                    client.SendMessage(0x02, "You bow your head in reverence.");
                    client.Aisling.LastPrayed = DateTime.UtcNow;
                    client.CloseDialog();
                }
                break;
            case 2:
                {
                    client.SendAnimation(127, client.Aisling, client.Aisling);
                    client.Aisling.Faith += 2;
                    client.Aisling.ExpTotal += 100;
                    client.SendMessage(0x02, "Your fervent prayers are heard.");
                    client.SendStats(StatusFlags.All);
                    client.Aisling.LastPrayed = DateTime.UtcNow;
                    client.CloseDialog();
                }
                break;
            case 3:
                {
                    client.SendAnimation(128, client.Aisling, client.Aisling);
                    client.Aisling.Faith += 3;
                    client.Aisling.ExpTotal += 250;
                    client.SendMessage(0x02, "You feel your Patron's hand on your shoulder.");
                    client.SendStats(StatusFlags.All);
                    client.Aisling.LastPrayed = DateTime.UtcNow;
                    client.CloseDialog();
                }
                break;
            case 4:
                {
                    client.SendAnimation(277, client.Aisling, client.Aisling);
                    client.Aisling.Faith += 4;
                    client.Aisling.ExpTotal += 500;
                    client.SendMessage(0x02, "Blinding light surrounds you as your prayers are heard.");
                    client.SendStats(StatusFlags.All);
                    client.Aisling.LastPrayed = DateTime.UtcNow;
                    client.CloseDialog();
                }
                break;
            case 5:
                {
                    client.SendAnimation(299, client.Aisling, client.Aisling);
                    client.Aisling.Faith += 5;
                    client.Aisling.ExpTotal += 1000;
                    client.SendMessage(0x02, "Your Patron recognizes your devout faith, and touches your very soul.");
                    client.SendStats(StatusFlags.All);
                    client.Aisling.LastPrayed = DateTime.UtcNow;
                    client.CloseDialog();
                }
                break;
            case 6:
                {
                    client.SendAnimation(299, client.Aisling, client.Aisling);
                    client.Aisling.Faith += 7;
                    client.Aisling.ExpTotal += 25000;
                    client.SendMessage(0x02, "You can feel your soul becoming one with the divine.");
                    client.SendStats(StatusFlags.All);
                    client.Aisling.LastPrayed = DateTime.UtcNow;
                    client.CloseDialog();
                }
                break;
        }
    }
    #endregion Prayer
    #endregion Religion Methods
    #region Quest Methods
    #region Armor Weight Check
    public bool Circle2ArmorWeightCheck()
    {
        var circle2Weight = Aisling.Path switch
        {
            Class.Warrior => 14,
            Class.Rogue => 6,
            Class.Wizard => 6,
            Class.Priest => 5,
            Class.Monk => 6,
            _ => 0
        };
        if (Aisling.CurrentWeight + circle2Weight <= Aisling.MaximumWeight)
            return true;
        else
            return false;
    }
    public bool Circle3ArmorWeightCheck()
    {
        var circle3Weight = Aisling.Path switch
        {
            Class.Warrior => 23,
            Class.Rogue => 9,
            Class.Wizard => 11,
            Class.Priest => 7,
            Class.Monk => 8,
            _ => 0
        };
        if (Aisling.CurrentWeight + circle3Weight <= Aisling.MaximumWeight)
            return true;
        else
            return false;
    }
    public bool Circle4ArmorWeightCheck()
    {
        var circle4Weight = Aisling.Path switch
        {
            Class.Warrior => 40,
            Class.Rogue => 13,
            Class.Wizard => 20,
            Class.Priest => 10,
            Class.Monk => 11,
            _ => 0
        };
        if (Aisling.CurrentWeight + circle4Weight <= Aisling.MaximumWeight)
            return true;
        else
            return false;
    }
    public bool NightmareArmorWeightCheck()
    {
        var circle5Weight = Aisling.Path switch
        {
            Class.Warrior => 45,
            Class.Rogue => 31,
            Class.Wizard => 26,
            Class.Priest => 11,
            Class.Monk => 25,
            _ => 0
        };
        if (Aisling.CurrentWeight + circle5Weight <= Aisling.MaximumWeight)
            return true;
        else
            return false;
    }
    public bool Circle5ArmorWeightCheck()
    {
        var circle5Weight = Aisling.Path switch
        {
            Class.Warrior => 64,
            Class.Rogue => 18,
            Class.Wizard => 30,
            Class.Priest => 14,
            Class.Monk => 14,
            _ => 0
        };
        if (Aisling.CurrentWeight + circle5Weight <= Aisling.MaximumWeight)
            return true;
        else
            return false;
    }
    #endregion Armor Weight Check
    #endregion Quest Methods
    #region Arena Legend Updaters
    public void ElixirWinLegend()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var Elixir in legend)
            if (Elixir.Category == "ElixirVictory")
            {
                Aisling.LegendBook.LegendMarks.Remove(Elixir);
                Legend.RemoveFromDB(Aisling.Client, Elixir);
            }
    }
    public void ElixirPlayLegend()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var Elixir in legend)
            if (Elixir.Category == "ElixirParticipation")
            {
                Aisling.LegendBook.LegendMarks.Remove(Elixir);
                Legend.RemoveFromDB(Aisling.Client, Elixir);
            }
    }
    public void VictoryLegend()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var Crafting in legend)
            if (Crafting.Category == "Victory")
            {
                Aisling.LegendBook.LegendMarks.Remove(Crafting);
                Legend.RemoveFromDB(Aisling.Client, Crafting);
            }
    }
    public void ParticipationLegend()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var Crafting in legend)
            if (Crafting.Category == "Participation")
            {
                Aisling.LegendBook.LegendMarks.Remove(Crafting);
                Legend.RemoveFromDB(Aisling.Client, Crafting);
            }
    }
    #endregion Arena Legend Updaters
    #region Crafting Legend Updaters
    #region Weaving
    public void UpdateWeavingRank()
    {
        WeavingLegend();
        int weavingSuccess = Aisling.WeavingSuccess;
        int weaving = Aisling.WeavingSkill;
        Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
        {
            Category = "Weaving",
            Color = (byte)LegendColor.White,
            Icon = (byte)LegendIcon.Victory,
            Value = string.Format("Weaver ({0})", Aisling.WeavingSuccess)
        });
        Dictionary<int, string> ranks = new Dictionary<int, string>()
        {
            {20, "Novice" },
            {50, "Initiate" },
            {75, "Apprentice" },
            {100, "Accomplished" },
            {300, "Adept" },
            {500, "Talented" },
            {750, "Skilled" },
            {1000, "Expert" },
            {1500, "Professional" },
            {2000, "Master" },
            {25000, "Legendary" },
        };

        if (weavingSuccess is 20 or 50 or 75 or 100 or 300 or 500 or 750 or 1000 or 1500)
        {
            weaving++;
            WeavingRank();
            Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
            {
                Category = "Weaving Rank",
                Color = (byte)LegendColor.White,
                Icon = (byte)LegendIcon.Victory,
                Value = $"{ranks[weavingSuccess]} Weaver"
            });
        }
        if (weavingSuccess == 2000)
        {
            weaving++;
            WeavingRank();
            Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
            {
                Category = "Weaving Rank",
                Color = (byte)LegendColor.White,
                Icon = (byte)LegendIcon.Victory,
                Value = $"{ranks[weavingSuccess]} Weaver"
            });
            SystemMessage("You have Mastered Weaving!");
        }
        if (weavingSuccess == 25000)
        {
            weaving++;
            WeavingRank();
            Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
            {
                Category = "Weaving Rank",
                Color = (byte)LegendColor.Blue,
                Icon = (byte)LegendIcon.Victory,
                Value = $"{ranks[weavingSuccess]} Weaver"
            });
            SystemMessage("You are now a Legendary Weaver!");
            Aisling.Animate(98);
        }
        else
            SystemMessage("Your Weaving skill has improved!");

        Aisling.WeavingSkill = weaving;
    }
    public void WeavingLegend()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var Crafting in legend)
            if (Crafting.Category == "Weaving")
            {
                Aisling.LegendBook.LegendMarks.Remove(Crafting);
                Legend.RemoveFromDB(Aisling.Client, Crafting);
            }
    }
    public void WeavingRank()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var Crafting in legend)
            if (Crafting.Category == "Weaving Rank")
            {
                Aisling.LegendBook.LegendMarks.Remove(Crafting);
                Legend.RemoveFromDB(Aisling.Client, Crafting);
            }
    }
    #endregion Weaving
    #region Herbalism
    public void HerbLegend()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var crafting in legend)
            if (crafting.Category == "Herbalism")
                Aisling.LegendBook.Remove(crafting, Aisling.Client);
    }
    public void HerbRank()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var crafting in legend)
            if (crafting.Category == "Herbalism Rank")
                Aisling.LegendBook.Remove(crafting, Aisling.Client);
    }
    #endregion Herbalism
    #region Alchemy
    public void UpdateAlchemyRank()
    {
        AlchemyLegend();
        int alchemySuccess = Aisling.AlchemySuccess;
        int alchemy = Aisling.AlchemySkill;
        Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
        {
            Category = "Alchemy",
            Color = (byte)LegendColor.White,
            Icon = (byte)LegendIcon.Victory,
            Value = string.Format("Alchemist ({0})", Aisling.AlchemySuccess)
        });
        Dictionary<int, string> ranks = new Dictionary<int, string>()
        {
            {20, "Novice" },
            {50, "Initiate" },
            {75, "Apprentice" },
            {100, "Accomplished" },
            {300, "Adept" },
            {500, "Talented" },
            {750, "Skilled" },
            {1000, "Expert" },
            {1500, "Professional" },
            {2000, "Master" },
            {25000, "Legendary" },
        };

        if (alchemySuccess is 20 or 50 or 75 or 100 or 300 or 500 or 750 or 1000 or 1500)
        {
            alchemy++;
            AlchemyRank();
            Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
            {
                Category = "Alchemy Rank",
                Color = (byte)LegendColor.White,
                Icon = (byte)LegendIcon.Victory,
                Value = $"{ranks[alchemySuccess]} Alchemist"
            });
        }
        if (alchemySuccess == 2000)
        {
            alchemy++;
            AlchemyRank();
            Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
            {
                Category = "Alchemy Rank",
                Color = (byte)LegendColor.White,
                Icon = (byte)LegendIcon.Victory,
                Value = $"{ranks[alchemySuccess]} Alchemist"
            });
            SystemMessage("You have Mastered Alchemy!");
        }
        if (alchemySuccess == 25000)
        {
            alchemy++;
            AlchemyRank();
            Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
            {
                Category = "Alchemy Rank",
                Color = (byte)LegendColor.Blue,
                Icon = (byte)LegendIcon.Victory,
                Value = $"{ranks[alchemySuccess]} Alchemist"
            });
            SystemMessage("You are now a Legendary Alchemist!");
            Aisling.Animate(98);
        }
        else
            SystemMessage("Your Alchemy skill has improved!");

        Aisling.AlchemySkill = alchemy;
    }
    public void AlchemyLegend()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var Crafting in legend)
            if (Crafting.Category == "Alchemy")
            {
                Aisling.LegendBook.LegendMarks.Remove(Crafting);
                Legend.RemoveFromDB(Aisling.Client, Crafting);
            }
    }
    public void AlchemyRank()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var Crafting in legend)
            if (Crafting.Category == "Alchemy Rank")
            {
                Aisling.LegendBook.LegendMarks.Remove(Crafting);
                Legend.RemoveFromDB(Aisling.Client, Crafting);
            }
    }
    #endregion Alchemy
    #region Tailoring
    public void UpdateTailoringRank()
    {
        TailoringLegend();
        int tailoringSuccess = Aisling.TailoringSuccess;
        int tailoring = Aisling.TailoringSkill;
        Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
        {
            Category = "Tailoring",
            Color = (byte)LegendColor.White,
            Icon = (byte)LegendIcon.Victory,
            Value = string.Format("Tailor ({0})", Aisling.TailoringSuccess)
        });
        Dictionary<int, string> ranks = new Dictionary<int, string>()
        {
            {20, "Novice" },
            {50, "Initiate" },
            {75, "Apprentice" },
            {100, "Accomplished" },
            {300, "Adept" },
            {500, "Talented" },
            {750, "Skilled" },
            {1000, "Expert" },
            {1500, "Professional" },
            {2000, "Master" },
            {25000, "Legendary" },
        };

        if (tailoringSuccess is 20 or 50 or 75 or 100 or 300 or 500 or 750 or 1000 or 1500)
        {
            tailoring++;
            TailoringRank();
            Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
            {
                Category = "Tailoring Rank",
                Color = (byte)LegendColor.White,
                Icon = (byte)LegendIcon.Victory,
                Value = $"{ranks[tailoringSuccess]} Tailor"
            });
        }
        if (tailoringSuccess == 2000 && tailoring == 10)
        {
            tailoring++;
            TailoringRank();
            Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
            {
                Category = "Tailoring Rank",
                Color = (byte)LegendColor.White,
                Icon = (byte)LegendIcon.Victory,
                Value = $"{ranks[tailoringSuccess]} Tailor"
            });
            SystemMessage("You have Mastered Tailoring!");
        }
        if (tailoringSuccess == 25000 && tailoring == 11)
        {
            tailoring++;
            TailoringRank();
            Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
            {
                Category = "Tailoring Rank",
                Color = (byte)LegendColor.Blue,
                Icon = (byte)LegendIcon.Victory,
                Value = $"{ranks[tailoringSuccess]} Tailor"
            });
            SystemMessage("You are now a Legendary Tailor!");
            Aisling.Animate(98);
        }
        else
            SystemMessage("Your Tailoring skill has improved!");

        Aisling.TailoringSkill = tailoring;
    }
    public void TailoringLegend()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var Crafting in legend)
            if (Crafting.Category == "Tailoring")
            {
                Aisling.LegendBook.LegendMarks.Remove(Crafting);
                Legend.RemoveFromDB(Aisling.Client, Crafting);
            }
    }
    public void TailoringRank()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var Crafting in legend)
            if (Crafting.Category == "Tailoring Rank")
            {
                Aisling.LegendBook.LegendMarks.Remove(Crafting);
                Legend.RemoveFromDB(Aisling.Client, Crafting);
            }
    }
    #endregion Tailoring
    #region Jeweling
    public void UpdateJewelingRank()
    {
        JewelingLegend();
        int jewelSuccess = Aisling.JewelCraftingSuccess;
        int jewelCrafting = Aisling.JewelCraftingSkill;
        Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
        {
            Category = "Jeweling Skill",
            Color = (byte)LegendColor.White,
            Icon = (byte)LegendIcon.Victory,
            Value = string.Format("Jeweler ({0})", Aisling.JewelCraftingSuccess)
        });
        Dictionary<int, string> ranks = new Dictionary<int, string>()
        {
            {20, "Novice" },
            {50, "Initiate" },
            {75, "Apprentice" },
            {100, "Accomplished" },
            {300, "Adept" },
            {500, "Talented" },
            {750, "Skilled" },
            {1000, "Expert" },
            {1500, "Professional" },
            {2000, "Master" },
            {25000, "Legendary" },
        };

        if (jewelSuccess is 20 or 50 or 75 or 100 or 300 or 500 or 750 or 1000 or 1500)
        {
            jewelCrafting++;
            JewelingRank();
            Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
            {
                Category = "Jeweler Rank",
                Color = (byte)LegendColor.White,
                Icon = (byte)LegendIcon.Victory,
                Value = $"{ranks[jewelSuccess]} Jeweler"
            });
        }
        if (jewelSuccess == 2000 && jewelCrafting == 10)
        {
            jewelCrafting++;
            JewelingRank();
            Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
            {
                Category = "Jeweler Rank",
                Color = (byte)LegendColor.White,
                Icon = (byte)LegendIcon.Victory,
                Value = $"{ranks[jewelSuccess]} Jeweler"
            });
            SystemMessage("You have Mastered Jeweling!");
        }
        if (jewelSuccess == 25000 && jewelCrafting == 11)
        {
            jewelCrafting++;
            JewelingRank();
            Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
            {
                Category = "Jeweler Rank",
                Color = (byte)LegendColor.Blue,
                Icon = (byte)LegendIcon.Victory,
                Value = $"{ranks[jewelSuccess]} Jeweler"
            });
            SystemMessage("You are now a Legendary Jeweler!");
            Aisling.Animate(98);
        }
        else
            SystemMessage("Your Jeweling skill has improved!");

        Aisling.JewelCraftingSkill = jewelCrafting;
    }
    public void JewelingLegend()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var Crafting in legend)
            if (Crafting.Category == "Jeweling Skill")
            {
                Aisling.LegendBook.LegendMarks.Remove(Crafting);
                Legend.RemoveFromDB(Aisling.Client, Crafting);
            }
    }
    public void JewelingRank()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var Crafting in legend)
            if (Crafting.Category == "Jeweler Rank")
            {
                Aisling.LegendBook.LegendMarks.Remove(Crafting);
                Legend.RemoveFromDB(Aisling.Client, Crafting);
            }
    }
    #endregion Jeweling
    #region Gem Smithing
    public void UpdateGemCutRank()
    {
        GemCutLegend();
        int gemSuccess = Aisling.GemSuccess;
        int gemCutting = Aisling.GemCuttingSkill;
        Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
        {
            Category = "Gemcutting",
            Color = (byte)LegendColor.White,
            Icon = (byte)LegendIcon.Victory,
            Value = string.Format("Gemcutter ({0})", Aisling.GemSuccess)
        });
        Dictionary<int, string> ranks = new Dictionary<int, string>()
        {
            {20, "Novice" },
            {50, "Initiate" },
            {75, "Apprentice" },
            {100, "Accomplished" },
            {300, "Adept" },
            {500, "Talented" },
            {750, "Skilled" },
            {1000, "Expert" },
            {1500, "Professional" },
            {2000, "Master" },
            {25000, "Legendary" },
        };

        if (gemSuccess is 20 or 50 or 75 or 100 or 300 or 500 or 750 or 1000 or 1500)
        {
            gemCutting++;
            GemCutRank();
            Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
            {
                Category = "Gemcutter Rank",
                Color = (byte)LegendColor.White,
                Icon = (byte)LegendIcon.Victory,
                Value = $"{ranks[gemSuccess]} Gem Smith"
            });
        }
        if (gemSuccess == 2000 && gemCutting == 10)
        {
            gemCutting++;
            GemCutRank();
            Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
            {
                Category = "Gemcutter Rank",
                Color = (byte)LegendColor.White,
                Icon = (byte)LegendIcon.Victory,
                Value = $"{ranks[gemSuccess]} Gem Smith"
            });
            SystemMessage("You have Mastered Gem Smithing!");
        }
        if (gemSuccess == 25000 && gemCutting == 11)
        {
            gemCutting++;
            GemCutRank();
            Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
            {
                Category = "Gemcutter Rank",
                Color = (byte)LegendColor.Blue,
                Icon = (byte)LegendIcon.Victory,
                Value = $"{ranks[gemSuccess]} Gem Smith"
            });
            SystemMessage("You are now a Legendary Gem Smith!");
            Aisling.Animate(98);
        }
        else
            SystemMessage("Your Gem Smithing skill has improved!");

        Aisling.GemCuttingSkill = gemCutting;
    }
    public void GemCutLegend()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var Crafting in legend)
            if (Crafting.Category == "Gemcutting")
            {
                Aisling.LegendBook.LegendMarks.Remove(Crafting);
                Legend.RemoveFromDB(Aisling.Client, Crafting);
            }
    }
    public void GemCutRank()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var Crafting in legend)
            if (Crafting.Category == "Gemcutter Rank")
            {
                Aisling.LegendBook.LegendMarks.Remove(Crafting);
                Legend.RemoveFromDB(Aisling.Client, Crafting);
            }
    }
    #endregion Gem Smithing
    #region Smelting
    public void UpdateSmeltingRank()
    {
        SmeltLegend();
        int smeltingSuccess = Aisling.SmeltingSuccess;
        int smelting = Aisling.SmeltingSkill;
        Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
        {
            Category = "Smelting",
            Color = (byte)LegendColor.White,
            Icon = (byte)LegendIcon.Victory,
            Value = string.Format("Smelter ({0})", Aisling.SmeltingSuccess)
        });
        Dictionary<int, string> ranks = new Dictionary<int, string>()
        {
            {20, "Novice" },
            {50, "Initiate" },
            {75, "Apprentice" },
            {100, "Accomplished" },
            {300, "Adept" },
            {500, "Talented" },
            {750, "Skilled" },
            {1000, "Expert" },
            {1500, "Professional" },
            {2000, "Master" },
            {25000, "Legendary" },
        };
        if (smeltingSuccess is 20 or 50 or 75 or 100 or 300 or 500 or 750 or 1000 or 1500)
        {
            smelting++;
            SmeltRank();
            Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
            {
                Category = "Smelting Rank",
                Color = (byte)LegendColor.White,
                Icon = (byte)LegendIcon.Victory,
                Value = $"{ranks[smeltingSuccess]} Smelter"
            });
        }
        if (smeltingSuccess == 2000)
        {
            smelting++;
            SmeltRank();
            SystemMessage("You have Mastered Smelting!");
            Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
            {
                Category = "Smelting Rank",
                Color = (byte)LegendColor.White,
                Icon = (byte)LegendIcon.Victory,
                Value = $"{ranks[smeltingSuccess]} Smelter"
            });
        }
        if (smeltingSuccess == 25000)
        {
            smelting++;
            SmeltRank();
            SystemMessage("You are now a Legendary Smelter!");
            Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
            {
                Category = "Smelting Rank",
                Color = (byte)LegendColor.Blue,
                Icon = (byte)LegendIcon.Victory,
                Value = $"{ranks[smeltingSuccess]} Smelter"
            });
            Aisling.Animate(98);
        }
        else
            SystemMessage("Your Smelting skill has improved!");

        Aisling.SmeltingSkill = smelting;
    }
    public void SmeltRank()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var Crafting in legend)
            if (Crafting.Category == "Smelting Rank")
            {
                Aisling.LegendBook.LegendMarks.Remove(Crafting);
                Legend.RemoveFromDB(Aisling.Client, Crafting);
            }
    }
    public void SmeltLegend()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var Crafting in legend)
            if (Crafting.Category == "Smelting")
            {
                Aisling.LegendBook.LegendMarks.Remove(Crafting);
                Legend.RemoveFromDB(Aisling.Client, Crafting);
            }
    }
    #endregion Smelting
    #region Forging
    public void UpdateForgingRank()
    {
        ForgingLegend();
        int forgingSuccess = Aisling.ForgingSuccess;
        int forging = Aisling.ForgingSkill;
        Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
        {
            Category = "Forging",
            Color = (byte)LegendColor.White,
            Icon = (byte)LegendIcon.Victory,
            Value = string.Format("Forger ({0})", Aisling.ForgingSuccess)
        });
        Dictionary<int, string> ranks = new Dictionary<int, string>()
        {
            {20, "Novice" },
            {50, "Initiate" },
            {75, "Apprentice" },
            {100, "Accomplished" },
            {300, "Adept" },
            {500, "Talented" },
            {750, "Skilled" },
            {1000, "Expert" },
            {1500, "Professional" },
            {2000, "Master" },
            {25000, "Legendary" },
        };

        if (forgingSuccess is 20 or 50 or 75 or 100 or 300 or 500 or 750 or 1000 or 1500)
        {
            forging++;
            ForgingRank();
            Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
            {
                Category = "Forging Rank",
                Color = (byte)LegendColor.White,
                Icon = (byte)LegendIcon.Victory,
                Value = $"{ranks[forgingSuccess]} Forger"
            });
        }
        if (forgingSuccess == 2000 && forging == 10)
        {
            forging++;
            ForgingRank();
            Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
            {
                Category = "Forging Rank",
                Color = (byte)LegendColor.White,
                Icon = (byte)LegendIcon.Victory,
                Value = $"{ranks[forgingSuccess]} Forger"
            });
            SystemMessage("You have Mastered Forging!");
        }
        if (forgingSuccess == 25000 && forging == 11)
        {
            forging++;
            ForgingRank();
            Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
            {
                Category = "Forging Rank",
                Color = (byte)LegendColor.Blue,
                Icon = (byte)LegendIcon.Victory,
                Value = $"{ranks[forgingSuccess]} Forger"
            });
            SystemMessage("You are now a Legendary Forger!");
            Aisling.Animate(98);
        }
        else
            SystemMessage("Your Forging skill has improved!");

        Aisling.ForgingSkill = forging;
    }
    public void ForgingRank()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var Crafting in legend)
            if (Crafting.Category == "Forging Rank")
            {
                Aisling.LegendBook.LegendMarks.Remove(Crafting);
                Legend.RemoveFromDB(Aisling.Client, Crafting);
            }
    }
    public void ForgingLegend()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var Crafting in legend)
            if (Crafting.Category == "Forging")
            {
                Aisling.LegendBook.LegendMarks.Remove(Crafting);
                Legend.RemoveFromDB(Aisling.Client, Crafting);
            }
    }
    #endregion Forging
    #region Smithing
    public void UpdateSmithingRank()
    {
        SmithingLegend();
        int smithingSuccess = Aisling.SmithingSuccess;
        int smithing = Aisling.SmithingSkill;
        Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
        {
            Category = "Smithing",
            Color = (byte)LegendColor.White,
            Icon = (byte)LegendIcon.Victory,
            Value = string.Format("Smith ({0})", Aisling.SmithingSuccess)
        });
        Dictionary<int, string> ranks = new Dictionary<int, string>()
        {
            {20, "Novice" },
            {50, "Initiate" },
            {75, "Apprentice" },
            {100, "Accomplished" },
            {300, "Adept" },
            {500, "Talented" },
            {750, "Skilled" },
            {1000, "Expert" },
            {1500, "Professional" },
            {2000, "Master" },
            {25000, "Legendary" },
        };

        if (smithingSuccess is 20 or 50 or 75 or 100 or 300 or 500 or 750 or 1000 or 1500)
        {
            smithing++;
            WeaponRank();
            Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
            {
                Category = "Smithing Rank",
                Color = (byte)LegendColor.White,
                Icon = (byte)LegendIcon.Victory,
                Value = $"{ranks[smithingSuccess]} Smith"
            });
        }
        if (smithingSuccess == 2000 && smithing == 10)
        {
            smithing++;
            WeaponRank();
            Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
            {
                Category = "Smithing Rank",
                Color = (byte)LegendColor.White,
                Icon = (byte)LegendIcon.Victory,
                Value = $"{ranks[smithingSuccess]} Smith"
            });
            SystemMessage("You have Mastered Smithing!");
        }
        if (smithingSuccess == 25000 && smithing == 11)
        {
            smithing++;
            WeaponRank();
            Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
            {
                Category = "Smithing Rank",
                Color = (byte)LegendColor.Blue,
                Icon = (byte)LegendIcon.Victory,
                Value = $"{ranks[smithingSuccess]} Smith"
            });
            SystemMessage("You are now a Legendary Smith!");
            Aisling.Animate(98);
        }
        else
            SystemMessage("Your Smithing skill has improved!");

        Aisling.SmithingSkill = smithing;
    }
    public void WeaponRank()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var Crafting in legend)
            if (Crafting.Category == "Smithing Rank")
            {
                Aisling.LegendBook.LegendMarks.Remove(Crafting);
                Legend.RemoveFromDB(Aisling.Client, Crafting);
            }
    }
    public void SmithingLegend()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var Crafting in legend)
            if (Crafting.Category == "Smithing")
            {
                Aisling.LegendBook.LegendMarks.Remove(Crafting);
                Legend.RemoveFromDB(Aisling.Client, Crafting);
            }
    }
    #endregion Smithing
    #endregion
    #region Gathering Legend Updaters
    #region Mining
    public void UpdateMiningRank()
    {
        int miningSuccess = Aisling.MiningSuccess;
        int mining = Aisling.MiningSkill;
        Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
        {
            Category = "Mining",
            Color = (byte)LegendColor.White,
            Icon = (byte)LegendIcon.Victory,
            Value = string.Format("Miner ({0})", Aisling.MiningSuccess)
        });
        Dictionary<int, string> ranks = new Dictionary<int, string>()
        {
            {50, "Novice" },
            {125, "Initiate" },
            {250, "Apprentice" },
            {500, "Accomplished" },
            {1000, "Adept" },
            {1750, "Talented" },
            {2500, "Skilled" },
            {3500, "Expert" },
            {5000, "Professional" },
            {7500, "Master" },
            {25000, "Legendary" },
        };
        if (miningSuccess is 50 or 125 or 250 or 500 or 1000 or 1750 or 2500 or 3500 or 5000)
        {
            mining++;
            MiningRank();
            Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
            {
                Category = "Mining Rank",
                Color = (byte)LegendColor.White,
                Icon = (byte)LegendIcon.Victory,
                Value = $"{ranks[miningSuccess]} Miner"
            });
            Aisling.MiningSkill = mining;
        }
        if (miningSuccess == 7500 && mining == 10)
        {
            mining++;
            MiningRank();
            SystemMessage("You have Mastered Mining!");
            Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
            {
                Category = "Mining Rank",
                Color = (byte)LegendColor.White,
                Icon = (byte)LegendIcon.Victory,
                Value = $"{ranks[miningSuccess]} Miner"
            });
            Aisling.MiningSkill = mining;
            return;
        }
        if (miningSuccess == 25000 && mining == 11)
        {
            mining++;
            MiningRank();
            SystemMessage("You are now a Legendary Miner!");
            Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
            {
                Category = "Mining Rank",
                Color = (byte)LegendColor.Blue,
                Icon = (byte)LegendIcon.Victory,
                Value = $"{ranks[miningSuccess]} Miner"
            });
            Aisling.Animate(98);
            Aisling.MiningSkill = mining;
            return;
        }
        else
            SystemMessage($"Your Mining skill has improved!");
    }
    public void MiningLegend()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var Crafting in legend)
            if (Crafting.Category == "Mining")
            {
                Aisling.LegendBook.LegendMarks.Remove(Crafting);
                Legend.RemoveFromDB(Aisling.Client, Crafting);
            }
    }
    public void MiningRank()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var Crafting in legend)
            if (Crafting.Category == "Mining Rank")
            {
                Aisling.LegendBook.LegendMarks.Remove(Crafting);
                Legend.RemoveFromDB(Aisling.Client, Crafting);
            }
    }
    #endregion Mining
    #region Harvesting
    public void UpdateHarvestingRank()
    {
        int harvestingSuccess = Aisling.HarvestSuccess;
        int Harvesting = Aisling.HarvestSkill;
        Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
        {
            Category = "Harvesting",
            Color = (byte)LegendColor.White,
            Icon = (byte)LegendIcon.Victory,
            Value = string.Format("Farmer ({0})", Aisling.HarvestSuccess)
        });
        Dictionary<int, string> ranks = new Dictionary<int, string>()
        {
            {50, "Novice" },
            {125, "Initiate" },
            {250, "Apprentice" },
            {500, "Accomplished" },
            {1000, "Adept" },
            {1750, "Talented" },
            {2500, "Skilled" },
            {3500, "Expert" },
            {5000, "Professional" },
            {7500, "Master" },
            {25000, "Legendary" },
        };

        if (harvestingSuccess is 50 or 125 or 250 or 500 or 1000 or 1750 or 2500 or 3500 or 5000)
        {
            Harvesting++;
            HarvestingRank();
            Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
            {
                Category = "Harvesting Rank",
                Color = (byte)LegendColor.White,
                Icon = (byte)LegendIcon.Victory,
                Value = $"{ranks[harvestingSuccess]} Farmer"
            });
            Aisling.HarvestSkill = Harvesting;
        }
        if (harvestingSuccess == 7500 && Harvesting == 10)
        {
            Harvesting++;
            HarvestingRank();
            SystemMessage("You have Mastered Farming!");
            Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
            {
                Category = "Harvesting Rank",
                Color = (byte)LegendColor.White,
                Icon = (byte)LegendIcon.Victory,
                Value = $"{ranks[harvestingSuccess]} Farmer"
            });
            Aisling.HarvestSkill = Harvesting;
            return;
        }
        if (harvestingSuccess == 25000 && Harvesting == 11)
        {
            Harvesting++;
            HarvestingRank();
            SystemMessage("You are now a Legendary Farmer!");
            Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
            {
                Category = "Harvesting Rank",
                Color = (byte)LegendColor.Blue,
                Icon = (byte)LegendIcon.Victory,
                Value = $"{ranks[harvestingSuccess]} Farmer"
            });
            Aisling.Animate(98);
            Aisling.HarvestSkill = Harvesting;
            return;
        }
        else
            SystemMessage($"Your Farming skill has improved!");

        Aisling.HarvestSkill = Harvesting;
    }
    public void HarvestingLegend()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var Crafting in legend)
            if (Crafting.Category == "Harvesting")
            {
                Aisling.LegendBook.LegendMarks.Remove(Crafting);
                Legend.RemoveFromDB(Aisling.Client, Crafting);
            }
    }
    public void HarvestingRank()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var Crafting in legend)
            if (Crafting.Category == "Harvesting Rank")
            {
                Aisling.LegendBook.LegendMarks.Remove(Crafting);
                Legend.RemoveFromDB(Aisling.Client, Crafting);
            }
    }
    #endregion Harvesting
    #endregion Gathering Legend Updaters
    #region Update Herbalism Legend Marks
    public void CheckHerbalism()
    {
        var Success = Aisling.HerbSuccess;
        var Skill = Aisling.Herbalism;
        //Count how many successes
        Aisling.Client.HerbLegend();
        Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
        {
            Category = "Herbalism",
            Color = (byte)LegendColor.White,
            Icon = (byte)LegendIcon.Victory,
            Value = $"Herbalist ({Aisling.HerbSuccess})"
        });
        //If rank up required:
        if (((Success == 20) && (Skill == 1)) ||
            ((Success == 50) && (Skill == 2)) ||
            ((Success == 75) && (Skill == 3)) ||
            ((Success == 100) && (Skill == 4)) ||
            ((Success == 300) && (Skill == 5)) ||
            ((Success == 500) && (Skill == 6)) ||
            ((Success == 750) && (Skill == 7)) ||
            ((Success == 1000) && (Skill == 8)) ||
            ((Success == 1500) && (Skill == 9)))
        {
            Aisling.Herbalism++;
            Aisling.Client.SendMessage(0x02, "Your skill in Herbalism has increased!");

            switch (Skill)
            {
                case 1:
                    {
                        Aisling.Client.HerbRank();
                        Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
                        {
                            Category = "Herbalism Rank",
                            Color = (byte)LegendColor.White,
                            Icon = (byte)LegendIcon.Victory,
                            Value = "Novice Herbalist"
                        });
                    }
                    break;
                case 2:
                    {
                        Aisling.Client.HerbRank();
                        Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
                        {
                            Category = "Herbalism Rank",
                            Color = (byte)LegendColor.White,
                            Icon = (byte)LegendIcon.Victory,
                            Value = "Initiate Herbalist"
                        });
                    }
                    break;
                case 3:
                    {
                        Aisling.Client.HerbRank();
                        Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
                        {
                            Category = "Herbalism Rank",
                            Color = (byte)LegendColor.White,
                            Icon = (byte)LegendIcon.Victory,
                            Value = "Apprentice Herbalist"
                        });
                    }
                    break;
                case 4:
                    {
                        Aisling.Client.HerbRank();
                        Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
                        {
                            Category = "Herbalism Rank",
                            Color = (byte)LegendColor.White,
                            Icon = (byte)LegendIcon.Victory,
                            Value = "Accomplished Herbalist"
                        });
                    }
                    break;
                case 5:
                    {
                        Aisling.Client.HerbRank();
                        Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
                        {
                            Category = "Herbalism Rank",
                            Color = (byte)LegendColor.White,
                            Icon = (byte)LegendIcon.Victory,
                            Value = "Adept Herbalist"
                        });
                    }
                    break;
                case 6:
                    {
                        Aisling.Client.HerbRank();
                        Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
                        {
                            Category = "Herbalism Rank",
                            Color = (byte)LegendColor.White,
                            Icon = (byte)LegendIcon.Victory,
                            Value = "Talented Herbalist"
                        });
                    }
                    break;
                case 7:
                    {
                        Aisling.Client.HerbRank();
                        Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
                        {
                            Category = "Herbalism Rank",
                            Color = (byte)LegendColor.White,
                            Icon = (byte)LegendIcon.Victory,
                            Value = "Skilled Herbalist"
                        });
                    }
                    break;
                case 8:
                    {
                        Aisling.Client.HerbRank();
                        Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
                        {
                            Category = "Herbalism Rank",
                            Color = (byte)LegendColor.White,
                            Icon = (byte)LegendIcon.Victory,
                            Value = "Expert Herbalist"
                        });
                    }
                    break;
                case 9:
                    {
                        Aisling.Client.HerbRank();
                        Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
                        {
                            Category = "Herbalism Rank",
                            Color = (byte)LegendColor.White,
                            Icon = (byte)LegendIcon.Victory,
                            Value = "Professional Herbalist"
                        });
                    }
                    break;
            }
        }
        if ((Success >= 2000) && (Skill == 10))
        {
            Aisling.Herbalism++;
            Aisling.Client.HerbRank();
            Aisling.Client.SendMessage(0x02, "You have Mastered Herbalism!");
            Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
            {
                Category = "Herbalism Rank",
                Color = (byte)LegendColor.Blue,
                Icon = (byte)LegendIcon.Victory,
                Value = "Master Herbalist"
            });
        }
        if ((Success >= 25000) && (Skill == 11))
        {
            Aisling.Herbalism++;
            Aisling.Client.HerbRank();
            Aisling.Client.SendMessage(0x02, "You are now a Legendary Herbalist!");
            Aisling.Animate(98);
            Aisling.LegendBook.AddLegend(Aisling.Client, new Legend.LegendItem
            {
                Category = "Herbalism Rank",
                Color = (byte)LegendColor.Yellow,
                Icon = (byte)LegendIcon.Victory,
                Value = "Master Herbalist"
            });
        }
    }
    #endregion Update Herbalism Legend Marks
    #region Crafting Methods
    #region Tailoring
    public void TailorCircle1()
    {
        var TailorLevel = Aisling.TailoringSkill;
        var Tailor = 50.0 + (TailorLevel * 5);
        if ((Aisling.Religion == 4) && Aisling.HasBuff("Industry"))
            Tailor += 10.0;
        var Class = Aisling.CraftClass;
        var sex = Aisling.CraftSex;
        int r = rand.Next(0, 101);

        Aisling.Inventory.RemoveQuantity("Plain Fabric", 2);

        if (r + Tailor >= 100)
        {
            var gear = new Item();
            string gearName;

            switch (Class)
            {
                case 1:
                    gearName = (sex == 1) ? "Dobok" : "Earth Bodice";
                    break;
                case 2:
                    gearName = (sex == 1) ? "Cowl" : "Gorget Gown";
                    break;
                case 3:
                    gearName = (sex == 1) ? "Gardcorp" : "Magi Skirt";
                    break;
                default:
                    gearName = "";
                    break;
            }

            gear = Item.Create(Aisling, gearName);
            Aisling.Bank.Deposit(gear);
            SendMessage(0x02, $"You have succeeded! {gear.Template.Name} sent to storage.");

            if (Aisling.TailoringSkill <= Aisling.RecipeDifficulty + 3)
            {
                Aisling.TailoringSuccess++;
                UpdateTailoringRank();
            }
            else
                SystemMessage("You cannot gain any more skill from this product.");
        }
        else if (r + Tailor < 100)
        {
            SendMessage(0x02, "The materials were destroyed.");
        }

        SendStats(StatusFlags.All);
    }
    public void TailorCircle2()
    {
        var TailorLevel = Aisling.TailoringSkill;
        var Class = Aisling.CraftClass;
        var sex = Aisling.CraftSex;
        int r = rand.Next(0, 101);
        var Tailor = 50.0 + ((TailorLevel - 2) * 4);
        if ((Aisling.Religion == 4) && Aisling.HasBuff("Industry"))
            Tailor += 10.0;

        Aisling.Inventory.RemoveQuantity("Sturdy Fabric", 4);
        Aisling.Inventory.RemoveQuantity("Plain Fabric", 2);

        if (r + Tailor >= 100)
        {
            var gear = new Item();
            string gearName;

            switch (Class)
            {
                case 1:
                    gearName = (sex == 1) ? "Culotte" : "Lotus Bodice";
                    break;
                case 2:
                    gearName = (sex == 1) ? "Cloth Alb" : "Mystic Gown";
                    break;
                case 3:
                    gearName = (sex == 1) ? "Journeyman" : "Benusta";
                    break;
                default:
                    gearName = "";
                    break;
            }

            gear = Item.Create(Aisling, gearName);
            Aisling.Bank.Deposit(gear);
            SendMessage(0x02, $"You have succeeded! {gear.Template.Name} sent to storage.");

            if (Aisling.TailoringSkill <= Aisling.RecipeDifficulty + 3)
            {
                Aisling.TailoringSuccess++;
                UpdateTailoringRank();
            }
            else
                SystemMessage("You cannot gain any more skill from this product.");
        }
        else if (r + Tailor < 100)
        {
            SendMessage(0x02, "The materials were destroyed.");
        }

        SendStats(StatusFlags.All);
    }
    public void TailorCircle3()
    {
        var TailorLevel = Aisling.TailoringSkill;
        var Tailor = 50.0 + ((TailorLevel - 5) * 3);
        if ((Aisling.Religion == 4) && Aisling.HasBuff("Industry"))
            Tailor += 10.0;
        var Class = Aisling.CraftClass;
        var sex = Aisling.CraftSex;
        int r = rand.Next(0, 101);

        Aisling.Inventory.RemoveQuantity("Soft Fabric", 6);
        Aisling.Inventory.RemoveQuantity("Sturdy Fabric", 4);
        Aisling.Inventory.RemoveQuantity("Plain Fabric", 2);

        if (r + Tailor >= 100)
        {
            var gear = new Item();
            string gearName;

            switch (Class)
            {
                case 1:
                    gearName = (sex == 1) ? "Earth Garb" : "Moon Bodice";
                    break;
                case 2:
                    gearName = (sex == 1) ? "Mantle" : "Elle";
                    break;
                case 3:
                    gearName = (sex == 1) ? "Lorum" : "Stoller";
                    break;
                default:
                    gearName = "";
                    break;
            }

            gear = Item.Create(Aisling, gearName);
            Aisling.Bank.Deposit(gear);
            SendMessage(0x02, $"You have succeeded! {gear.Template.Name} sent to storage.");

            if (Aisling.TailoringSkill <= Aisling.RecipeDifficulty + 3)
            {
                Aisling.TailoringSuccess++;
                UpdateTailoringRank();
            }
            else
                SystemMessage("You cannot gain any more skill from this product.");
        }
        else if (r + Tailor < 100)
        {
            SendMessage(0x02, "The materials were destroyed.");
        }

        SendStats(StatusFlags.All);
    }
    public void TailorCircle4()
    {
        var TailorLevel = Aisling.TailoringSkill;
        var Tailor = 50.0 + ((TailorLevel - 8) * 2);
        if ((Aisling.Religion == 4) && Aisling.HasBuff("Industry"))
            Tailor += 10.0;

        var Class = Aisling.CraftClass;
        var sex = Aisling.CraftSex;
        int r = rand.Next(0, 101);

        Aisling.Inventory.RemoveQuantity("Elegant Fabric", 8);
        Aisling.Inventory.RemoveQuantity("Soft Fabric", 4);
        Aisling.Inventory.RemoveQuantity("Sturdy Fabric", 2);
        Aisling.Inventory.RemoveQuantity("Plain Fabric", 1);

        if (r + Tailor >= 100)
        {
            var gear = new Item();
            string gearName;

            switch (Class)
            {
                case 1:
                    gearName = (sex == 1) ? "Wind Garb" : "Lightning Garb";
                    break;
                case 2:
                    gearName = (sex == 1) ? "Hierophant" : "Dolman";
                    break;
                case 3:
                    gearName = (sex == 1) ? "Mane" : "Clymouth";
                    break;
                default:
                    gearName = "";
                    break;
            }

            gear = Item.Create(Aisling, gearName);
            Aisling.Bank.Deposit(gear);
            SendMessage(0x02, $"You have succeeded! {gear.Template.Name} sent to storage.");

            if (Aisling.TailoringSkill <= Aisling.RecipeDifficulty + 3)
            {
                Aisling.TailoringSuccess++;
                UpdateTailoringRank();
            }
            else
                SystemMessage("You cannot gain any more skill from this product.");
        }
        else if (r + Tailor < 100)
        {
            SendMessage(0x02, "The materials were destroyed.");
        }

        SendStats(StatusFlags.All);
    }
    public void TailorCircle5()
    {
        var Tailor = 50.0;
        if ((Aisling.Religion == 4) && Aisling.HasBuff("Industry"))
            Tailor += 10.0;
        var Class = Aisling.CraftClass;
        var sex = Aisling.CraftSex;
        int r = rand.Next(0, 101);

        Aisling.Inventory.RemoveQuantity("Shimmering Fabric", 10);
        Aisling.Inventory.RemoveQuantity("Elegant Fabric", 5);
        Aisling.Inventory.RemoveQuantity("Soft Fabric", 3);
        Aisling.Inventory.RemoveQuantity("Sturdy Fabric", 2);
        Aisling.Inventory.RemoveQuantity("Plain Fabric", 1);

        if (r + Tailor >= 100)
        {
            var gear = new Item();
            string gearName;

            switch (Class)
            {
                case 1:
                    gearName = (sex == 1) ? "Mountain Garb" : "Sea Garb";
                    break;
                case 2:
                    gearName = (sex == 1) ? "Dalmatica" : "Bangasart";
                    break;
                case 3:
                    gearName = (sex == 1) ? "Duin-Uasal" : "Clamyth";
                    break;
                default:
                    gearName = "";
                    break;
            }

            gear = Item.Create(Aisling, gearName);
            Aisling.Bank.Deposit(gear);
            SendMessage(0x02, $"You have succeeded! {gear.Template.Name} sent to storage.");

            if (Aisling.TailoringSkill <= Aisling.RecipeDifficulty + 3)
            {
                Aisling.TailoringSuccess++;
                UpdateTailoringRank();
            }
            else
                SystemMessage("You cannot gain any more skill from this product.");
        }
        else if (r + Tailor < 100)
        {
            SendMessage(0x02, "The materials were destroyed.");
        }

        SendStats(StatusFlags.All);
    }

    #endregion Tailoring
    #region Smithing
    public void SmithWeapon()
    {
        var smithLevel = Aisling.SmithingSkill;
        var smithSkill = 50.0 + (smithLevel * 5.0);
        int r = rand.Next(0, 101);
        string weaponName = Aisling.SmithName;
        var weaponGroup = Aisling.CraftCircle;
        var weapon = new Item();
        if ((Aisling.Religion == 7) && Aisling.HasBuff("Industry"))
            smithSkill += 10.0;

        if (Aisling.CraftCircle != 8)
            Aisling.RecipeDifficulty = Aisling.CraftCircle;
        else
            Aisling.RecipeDifficulty = 8;

        switch (weaponGroup)
        {
            case 1:
                {
                    Aisling.Inventory.RemoveQuantity("Bronze Ingot", 2);
                }
                break;
            case 2:
                {
                    Aisling.Inventory.RemoveQuantity("Bronze Sheet", 1);
                    Aisling.Inventory.RemoveQuantity("Bronze Ingot", 2);
                    smithSkill -= 2.5;
                }
                break;
            case 3:
                {
                    Aisling.Inventory.RemoveQuantity("Silver Ingot", 1);
                    Aisling.Inventory.RemoveQuantity("Bronze Sheet", 1);
                    Aisling.Inventory.RemoveQuantity("Bronze Ingot", 2);
                    smithSkill -= 5.0;
                }
                break;
            case 4:
                {
                    Aisling.Inventory.RemoveQuantity("Silver Sheet", 1);
                    Aisling.Inventory.RemoveQuantity("Silver Ingot", 2);
                    Aisling.Inventory.RemoveQuantity("Bronze Sheet", 2);
                    Aisling.Inventory.RemoveQuantity("Bronze Ingot", 1);
                    smithSkill -= 7.5;
                }
                break;
            case 5:
                {
                    Aisling.Inventory.RemoveQuantity("Silver Sheet", 2);
                    Aisling.Inventory.RemoveQuantity("Silver Ingot", 2);
                    Aisling.Inventory.RemoveQuantity("Bronze Sheet", 2);
                    Aisling.Inventory.RemoveQuantity("Bronze Ingot", 1);
                    smithSkill -= 10.0;
                }
                break;
            case 6:
                {
                    Aisling.Inventory.RemoveQuantity("Silver Sheet", 2);
                    Aisling.Inventory.RemoveQuantity("Silver Ingot", 2);
                    Aisling.Inventory.RemoveQuantity("Mythril Ingot", 1);
                    smithSkill -= 12.5;
                }
                break;
            case 7:
                {
                    Aisling.Inventory.RemoveQuantity("Silver Sheet", 2);
                    Aisling.Inventory.RemoveQuantity("Silver Ingot", 2);
                    Aisling.Inventory.RemoveQuantity("Mythril Ingot", 2);
                    smithSkill -= 15.0;
                }
                break;
            case 8:
                {
                    Aisling.Inventory.RemoveQuantity("Mythril Sheet", 2);
                    Aisling.Inventory.RemoveQuantity("Mythril Ingot", 2);
                    Aisling.Inventory.RemoveQuantity("Hy-Brasyl Ingot", 1);
                    smithSkill -= 17.5;
                }
                break;
            case 9:
                {
                    Aisling.Inventory.RemoveQuantity("Hy-Brasyl Sheet", 1);
                    Aisling.Inventory.RemoveQuantity("Hy-Brasyl Ingot", 1);
                    Aisling.Inventory.RemoveQuantity("Talgonite Ingot", 1);
                    smithSkill -= 20.0;
                }
                break;
            case 10:
                {
                    Aisling.Inventory.RemoveQuantity("Hy-Brasyl Sheet", 2);
                    Aisling.Inventory.RemoveQuantity("Hy-Brasyl Ingot", 2);
                    Aisling.Inventory.RemoveQuantity("Talgonite Sheet", 1);
                    Aisling.Inventory.RemoveQuantity("Talgonite Ingot", 1);
                    smithSkill -= 25.0;
                }
                break;
            case 11:
                {
                    Aisling.Inventory.RemoveQuantity("Finished Rose Quartz", 3);
                    Aisling.Inventory.RemoveQuantity("Mythril Sheet", 1);
                    Aisling.Inventory.RemoveQuantity("Mythril Ingot", 2);
                    smithSkill -= 17.5;
                }
                break;

        }

        if (r + smithSkill >= 100)
        {
            weapon = Item.Create(Aisling, weaponName);
            Aisling.Bank.Deposit(weapon);
            SendMessage(0x02, $"You have succeeded! {weapon.Template.Name} sent to storage.");
            if (Aisling.SmithingSkill <= Aisling.RecipeDifficulty + 2)
            {
                Aisling.SmithingSuccess++;
                UpdateSmithingRank();
            }
            else
                SystemMessage("You cannot gain any more skill from this product.");
        }
        if (r + smithSkill < 100)
        {
            SendMessage(0x02, "The materials were destroyed.");
        }

        SendStats(StatusFlags.All);
    }
    #endregion Smithing
    #region Forging
    public void ForgeCircle1()
    {
        var forgingLevel = Aisling.ForgingSkill;
        var forgingSkill = 50.0 + (forgingLevel * 5);
        if ((Aisling.Religion == 3) && Aisling.HasBuff("Industry"))
            forgingSkill += 10.0;
        var Class = Aisling.CraftClass;
        var sex = Aisling.CraftSex;
        int r = rand.Next(0, 101);

        Aisling.Inventory.RemoveQuantity("Bronze Ingot", 2);

        if (r + forgingSkill >= 100)
        {
            var gear = new Item();
            string gearName;

            switch (Class)
            {
                case 1:
                    gearName = (sex == 1) ? "Leather Tunic" : "Leather Bliaut";
                    break;
                case 2:
                    gearName = (sex == 1) ? "Scout Leather" : "Cotte";
                    break;
                default:
                    gearName = "";
                    break;
            }

            gear = Item.Create(Aisling, gearName);
            Aisling.Bank.Deposit(gear);
            SendMessage(0x02, $"You have succeeded! {gear.Template.Name} sent to storage.");
            if (Aisling.ForgingSkill <= Aisling.RecipeDifficulty + 3)
            {
                Aisling.ForgingSuccess++;
                UpdateForgingRank();
            }
            else
                SystemMessage("You cannot gain any more skill from this product.");
        }
        else if (r + forgingSkill < 100)
        {
            SendMessage(0x02, "The materials were destroyed.");
        }

        SendStats(StatusFlags.All);
    }
    public void ForgeCircle2()
    {
        var forgingLevel = Aisling.ForgingSkill;
        var Class = Aisling.CraftClass;
        var sex = Aisling.CraftSex;
        int r = rand.Next(0, 101);
        var forgingSkill = 50.0 + ((forgingLevel - 2) * 4);
        if ((Aisling.Religion == 3) && Aisling.HasBuff("Industry"))
            forgingSkill += 10.0;

        Aisling.Inventory.RemoveQuantity("Silver Ingot", 4);
        Aisling.Inventory.RemoveQuantity("Bronze Ingot", 2);

        if (r + forgingSkill >= 100)
        {
            var gear = new Item();
            string gearName;

            switch (Class)
            {
                case 1:
                    gearName = (sex == 1) ? "Jupe" : "Cuirass";
                    break;
                case 2:
                    gearName = (sex == 1) ? "Dwarvish Leather" : "Brigandine";
                    break;
                default:
                    gearName = "";
                    break;
            }

            gear = Item.Create(Aisling, gearName);
            Aisling.Bank.Deposit(gear);
            SendMessage(0x02, $"You have succeeded! {gear.Template.Name} sent to storage.");
            if (Aisling.ForgingSkill <= Aisling.RecipeDifficulty + 3)
            {
                Aisling.ForgingSuccess++;
                UpdateForgingRank();
            }
            else
                SystemMessage("You cannot gain any more skill from this product.");
        }
        else if (r + forgingSkill < 100)
        {
            SendMessage(0x02, "The materials were destroyed.");
        }

        SendStats(StatusFlags.All);
    }
    public void ForgeCircle3()
    {
        var forgingLevel = Aisling.ForgingSkill;
        var forgingSkill = 50.0 + ((forgingLevel - 5) * 3);
        if ((Aisling.Religion == 3) && Aisling.HasBuff("Industry"))
            forgingSkill += 10.0;
        var Class = Aisling.CraftClass;
        var sex = Aisling.CraftSex;
        int r = rand.Next(0, 101);

        Aisling.Inventory.RemoveQuantity("Mythril Ingot", 6);
        Aisling.Inventory.RemoveQuantity("Silver Ingot", 4);
        Aisling.Inventory.RemoveQuantity("Bronze Ingot", 2);

        if (r + forgingSkill >= 100)
        {
            var gear = new Item();
            string gearName;

            switch (Class)
            {
                case 1:
                    gearName = (sex == 1) ? "Kasmanium Armor" : "Kasmanium Hauberk";
                    break;
                case 2:
                    gearName = (sex == 1) ? "Paluten" : "Corsette";
                    break;

                default:
                    gearName = "";
                    break;
            }

            gear = Item.Create(Aisling, gearName);
            Aisling.Bank.Deposit(gear);
            SendMessage(0x02, $"You have succeeded! {gear.Template.Name} sent to storage.");
            if (Aisling.ForgingSkill <= Aisling.RecipeDifficulty + 3)
            {
                Aisling.ForgingSuccess++;
                UpdateForgingRank();
            }
            else
                SystemMessage("You cannot gain any more skill from this product.");
        }
        else if (r + forgingSkill < 100)
        {
            SendMessage(0x02, "The materials were destroyed.");
        }

        SendStats(StatusFlags.All);
    }
    public void ForgeCircle4()
    {
        var forgingLevel = Aisling.ForgingSkill;
        var forgingSkill = 50.0 + ((forgingLevel - 8) * 2);
        if ((Aisling.Religion == 3) && Aisling.HasBuff("Industry"))
            forgingSkill += 10.0;

        var Class = Aisling.CraftClass;
        var sex = Aisling.CraftSex;
        int r = rand.Next(0, 101);

        Aisling.Inventory.RemoveQuantity("Hy-Brasyl Ingot", 8);
        Aisling.Inventory.RemoveQuantity("Mythril Ingot", 4);
        Aisling.Inventory.RemoveQuantity("Silver Ingot", 2);
        Aisling.Inventory.RemoveQuantity("Bronze Ingot", 1);

        if (r + forgingSkill >= 100)
        {
            var gear = new Item();
            string gearName;

            switch (Class)
            {
                case 1:
                    gearName = (sex == 1) ? "Iplet Mail" : "Labyrinth Mail";
                    break;
                case 2:
                    gearName = (sex == 1) ? "Keaton" : "Pebble Rose";
                    break;
                default:
                    gearName = "";
                    break;
            }

            gear = Item.Create(Aisling, gearName);
            Aisling.Bank.Deposit(gear);
            SendMessage(0x02, $"You have succeeded! {gear.Template.Name} sent to storage.");
            if (Aisling.ForgingSkill <= Aisling.RecipeDifficulty + 3)
            {
                Aisling.ForgingSuccess++;
                UpdateForgingRank();
            }
            else
                SystemMessage("You cannot gain any more skill from this product.");
        }
        else if (r + forgingSkill < 100)
        {
            SendMessage(0x02, "The materials were destroyed.");
        }

        SendStats(StatusFlags.All);
    }
    public void ForgeCircle5()
    {
        var forgingSkill = 50.0;
        if ((Aisling.Religion == 3) && Aisling.HasBuff("Industry"))
            forgingSkill += 10.0;
        var Class = Aisling.CraftClass;
        var sex = Aisling.CraftSex;
        int r = rand.Next(0, 101);

        Aisling.Inventory.RemoveQuantity("Talgonite Ingot", 10);
        Aisling.Inventory.RemoveQuantity("Hy-Brasyl Ingot", 5);
        Aisling.Inventory.RemoveQuantity("Mythril Ingot", 3);
        Aisling.Inventory.RemoveQuantity("Silver Ingot", 2);
        Aisling.Inventory.RemoveQuantity("Bronze Ingot", 1);

        if (r + forgingSkill >= 100)
        {
            var gear = new Item();
            string gearName;

            switch (Class)
            {
                case 1:
                    gearName = (sex == 1) ? "Hy-Brasyl Plate" : "Hy-Brasyl Armor";
                    break;
                case 2:
                    gearName = (sex == 1) ? "Bardocle" : "Kagum";
                    break;
                default:
                    gearName = "";
                    break;
            }

            gear = Item.Create(Aisling, gearName);
            Aisling.Bank.Deposit(gear);
            SendMessage(0x02, $"You have succeeded! {gear.Template.Name} sent to storage.");
            if (Aisling.ForgingSkill <= Aisling.RecipeDifficulty + 3)
            {
                Aisling.ForgingSuccess++;
                UpdateForgingRank();
            }
            else
                SystemMessage("You cannot gain any more skill from this product.");
        }
        else if (r + forgingSkill < 100)
        {
            SendMessage(0x02, "The materials were destroyed.");
        }

        SendStats(StatusFlags.All);
    }
    #endregion Forging
    #region Smelting
    public void Smelt()
    {
        var smeltingLevel = Aisling.SmeltingSkill;
        var smeltingSkill = 50.0 + (smeltingLevel * 5.0);
        if ((Aisling.Religion == 8) && Aisling.HasBuff("Industry"))
            smeltingSkill += 10.0;
        var r = 0;
        lock (Generator.Random)
            r = Generator.Random.Next(0, 101);
        var oreGrade = "";
        var finishedProduct = "";
        var ore = new Item();
        switch (Aisling.CraftMaterial)
        {
            case 1:
                oreGrade = "Bronze";
                finishedProduct = "Ingot";

                break;
            case 2:
                oreGrade = "Bronze";
                finishedProduct = "Sheet";
                smeltingSkill -= 5.0;

                break;
            case 3:
                oreGrade = "Silver";
                finishedProduct = "Ingot";

                smeltingSkill -= 7.5;
                break;
            case 4:
                oreGrade = "Silver";
                finishedProduct = "Sheet";
                smeltingSkill -= 10.0;

                break;
            case 5:
                oreGrade = "Mythril";
                finishedProduct = "Ingot";
                smeltingSkill -= 12.5;

                break;
            case 6:
                oreGrade = "Mythril";
                finishedProduct = "Sheet";
                smeltingSkill -= 15.0;

                break;
            case 7:
                oreGrade = "Hy-Brasyl";
                finishedProduct = "Ingot";
                smeltingSkill -= 17.5;

                break;
            case 8:
                oreGrade = "Hy-Brasyl";
                finishedProduct = "Sheet";
                smeltingSkill -= 20.0;

                break;
            case 9:
                oreGrade = "Talgonite";
                finishedProduct = "Ingot";
                smeltingSkill -= 25.0;

                break;
            case 10:
                oreGrade = "Talgonite";
                finishedProduct = "Sheet";
                smeltingSkill -= 30.0;

                break;
        }
        if (finishedProduct == "Ingot")
            Aisling.Inventory.RemoveQuantity($"Rough {oreGrade}", 2);
        else
            Aisling.Inventory.RemoveQuantity($"{oreGrade} Ingot", 2);
        if (r + smeltingSkill >= 100)
        {
            ore = Item.Create(Aisling, $"{oreGrade} {finishedProduct}");
            Aisling.Bank.Deposit(ore);
            SystemMessage($"You are successful! {oreGrade} {finishedProduct} sent to storage.");
            if (Aisling.SmeltingSkill <= Aisling.RecipeDifficulty + 2)
            {
                Aisling.SmeltingSuccess++;
                UpdateSmeltingRank();
            }
            else
                SystemMessage("You cannot gain any more skill from this product.");
        }
        if (r + smeltingSkill < 100)
        {
            SystemMessage($"The {oreGrade} has been ruined!");
            Aisling.GiveItem($"{oreGrade} Shavings");
        }
        SendStats(StatusFlags.All);
    }
    #endregion Smelting
    #region Alchemy
    public void BrewPotion()
    {
        var AlchemySkill = Aisling.AlchemySkill;
        var AlchemySuccess = 25.0 + (AlchemySkill * 5.0);
        if ((Aisling.Religion == 1) && Aisling.HasBuff("Industry"))
        {
            AlchemySuccess += 10.0;
        }

        var Size = Aisling.PotionSize;
        var Type = Aisling.PotionType;

        var wineAmount = Size;
        var plantAmount = Size * 2;
        var itemToGive = "";
        var randomNumber = rand.Next(0, 100);
        var potion = new Item();
        var successMessage = $"Your concoction was successfully sent to storage!";
        potion.Stacks = 1;

        switch (Type)
        {
            #region Ioc Deum
            case 1:
                {
                    switch (Size)
                    {
                        case 1:
                            {
                                AlchemySuccess += 25.0;
                                itemToGive = "Mion Ioc Deum";
                            }
                            break;
                        case 2:
                            {
                                AlchemySuccess += 20.0;
                                itemToGive = "Ioc Deum";
                            }
                            break;
                        case 3:
                            {
                                AlchemySuccess += 10.0;
                                itemToGive = "Lan Ioc Deum";
                            }
                            break;
                        case 4:
                            {
                                AlchemySuccess += 5.0;
                                itemToGive = "Ainmeal Ioc Deum";
                            }
                            break;
                        case 5:
                            {
                                AlchemySuccess += 2.0;
                                itemToGive = "Uasal Ioc Deum";
                            }
                            break;
                    }

                    Aisling.Inventory.RemoveQuantity("Wine", wineAmount);
                    Aisling.Inventory.RemoveQuantity("Hydele Plant", plantAmount);

                    if (randomNumber + AlchemySuccess >= 100)
                    {
                        potion = Item.Create(Aisling, itemToGive);
                        Aisling.Bank.Deposit(potion);
                        Aisling.Client.SystemMessage(successMessage);
                        if (Aisling.AlchemySkill <= Aisling.RecipeDifficulty + 2)
                        {
                            Aisling.AlchemySuccess++;
                            UpdateAlchemyRank();
                        }
                        else
                            SystemMessage("You cannot gain any more skill from this product.");
                    }
                    else
                    {
                        Aisling.Client.SystemMessage("Your concoction explodes!");
                        Aisling.CurrentHp -= 250;
                        Aisling.Animate(14);
                    }

                    Aisling.Client.SendStats(StatusFlags.All);
                }
                break;
            #endregion Ioc Deum
            #region Spiorad Deum
            //Spiorad
            case 2:
                {
                    switch (Size)
                    {
                        case 1:
                            {
                                AlchemySuccess += 25.0;
                                itemToGive = "Mion Spiorad Deum";
                            }
                            break;
                        case 2:
                            {
                                AlchemySuccess += 20.0;
                                itemToGive = "Spiorad Deum";
                            }
                            break;
                        case 3:
                            {
                                AlchemySuccess += 10.0;
                                itemToGive = "Lan Spiorad Deum";
                            }
                            break;
                        case 4:
                            {
                                AlchemySuccess += 5.0;
                                itemToGive = "Ainmeal Spiorad Deum";
                            }
                            break;
                        case 5:
                            {
                                AlchemySuccess += 2.0;
                                itemToGive = "Uasal Spiorad Deum";
                            }
                            break;
                    }

                    Aisling.Inventory.RemoveQuantity("Brandy", wineAmount);
                    Aisling.Inventory.RemoveQuantity("Fifleaf Plant", plantAmount);

                    if (randomNumber + AlchemySuccess >= 100)
                    {
                        potion = Item.Create(Aisling, itemToGive);
                        Aisling.Bank.Deposit(potion);
                        Aisling.Client.SystemMessage(successMessage);
                        if (Aisling.AlchemySkill <= Aisling.RecipeDifficulty + 2)
                        {
                            Aisling.AlchemySuccess++;
                            UpdateAlchemyRank();
                        }
                        else
                            SystemMessage("You cannot gain any more skill from this product.");
                    }
                    else
                    {
                        Aisling.Client.SystemMessage("Your concoction explodes!");
                        Aisling.CurrentHp -= 250;
                        Aisling.Animate(14);
                    }

                    Aisling.Client.SendStats(StatusFlags.All);
                }
                break;
            #endregion Spiorad Deum
            #region Cothromach Deum
            //Cothromach
            case 3:
                {
                    switch (Size)
                    {
                        case 1:
                            {
                                AlchemySuccess += 15.0;
                                itemToGive = "Mion Cothromach Deum";
                            }
                            break;
                        case 2:
                            {
                                AlchemySuccess += 10.0;
                                itemToGive = "Cothromach Deum";
                            }
                            break;
                        case 3:
                            {
                                AlchemySuccess += 5.0;
                                itemToGive = "Lan Cothromach Deum";
                            }
                            break;
                        case 4:
                            {
                                AlchemySuccess += 0;
                                itemToGive = "Ainmeal Cothromach Deum";
                            }
                            break;
                        case 5:
                            {
                                AlchemySuccess -= 5.0;
                                itemToGive = "Uasal Cothromach Deum";
                            }
                            break;
                    }

                    Aisling.Inventory.RemoveQuantity("Reagent", wineAmount);
                    Aisling.Inventory.RemoveQuantity("Hydele Plant", plantAmount);
                    Aisling.Inventory.RemoveQuantity("Fifleaf Plant", plantAmount);

                    if (randomNumber + AlchemySuccess >= 100)
                    {
                        potion = Item.Create(Aisling, itemToGive);
                        Aisling.Bank.Deposit(potion);
                        Aisling.Client.SystemMessage(successMessage);
                        if (Aisling.AlchemySkill <= Aisling.RecipeDifficulty + 2)
                        {
                            Aisling.AlchemySuccess++;
                            UpdateAlchemyRank();
                        }
                        else
                            SystemMessage("You cannot gain any more skill from this product.");
                    }
                    else
                    {
                        Aisling.Client.SystemMessage("Your concoction explodes!");
                        Aisling.CurrentHp -= 250;
                        Aisling.Animate(14);
                    }

                    Aisling.Client.SendStats(StatusFlags.All);
                }
                break;
            #endregion Cothromach Deum
            #region Betony Deum
            //Betony
            case 4:
                {
                    switch (Size)
                    {
                        case 1:
                            {
                                AlchemySuccess += 30.0;
                                itemToGive = "Mion Betony Deum";
                            }
                            break;
                        case 2:
                            {
                                AlchemySuccess += 25.0;
                                itemToGive = "Betony Deum";
                            }
                            break;
                        case 3:
                            {
                                AlchemySuccess += 20.0;
                                itemToGive = "Lan Betony Deum";
                            }
                            break;
                        case 4:
                            {
                                AlchemySuccess += 10.0;
                                itemToGive = "Ainmeal Betony Deum";
                            }
                            break;
                        case 5:
                            {
                                AlchemySuccess += 5.0;
                                itemToGive = "Uasal Betony Deum";
                            }
                            break;
                    }

                    Aisling.Inventory.RemoveQuantity("Reagent", wineAmount);
                    Aisling.Inventory.RemoveQuantity("Ancusa Plant", plantAmount);
                    Aisling.Inventory.RemoveQuantity("Betony Plant", plantAmount);

                    if (randomNumber + AlchemySuccess >= 100)
                    {
                        potion = Item.Create(Aisling, itemToGive);
                        Aisling.Bank.Deposit(potion);
                        Aisling.Client.SystemMessage(successMessage);
                        if (Aisling.AlchemySkill <= Aisling.RecipeDifficulty + 2)
                        {
                            Aisling.AlchemySuccess++;
                            UpdateAlchemyRank();
                        }
                        else
                            SystemMessage("You cannot gain any more skill from this product.");
                    }
                    else
                    {
                        Aisling.Client.SystemMessage("Your concoction explodes!");
                        Aisling.CurrentHp -= 250;
                        Aisling.Animate(14);
                    }

                    Aisling.Client.SendStats(StatusFlags.All);
                }
                break;

            #endregion Betony Deum
            #region Other
            //Persica
            case 5:
                {
                    AlchemySuccess += 25.0;
                    itemToGive = "Persica Deum";

                    Aisling.Inventory.RemoveQuantity("Wine", wineAmount);
                    Aisling.Inventory.RemoveQuantity("Persica Plant", plantAmount);

                    if (randomNumber + AlchemySuccess >= 100)
                    {
                        potion = Item.Create(Aisling, itemToGive);
                        Aisling.Bank.Deposit(potion);
                        Aisling.Client.SystemMessage(successMessage);
                        if (Aisling.AlchemySkill <= Aisling.RecipeDifficulty + 2)
                        {
                            Aisling.AlchemySuccess++;
                            UpdateAlchemyRank();
                        }
                        else
                            SystemMessage("You cannot gain any more skill from this product.");
                    }
                    else
                    {
                        Aisling.Client.SystemMessage("Your concoction explodes!");
                        Aisling.CurrentHp -= 250;
                        Aisling.Animate(14);
                    }

                    Aisling.Client.SendStats(StatusFlags.All);
                }
                break;
            //Beothaich
            case 6:
                {

                    AlchemySuccess += 25.0;
                    itemToGive = "Beothaich Deum";
                    Aisling.Inventory.RemoveQuantity("Brandy", 1);
                    Aisling.Inventory.RemoveQuantity("Grapes", 2);
                    Aisling.Inventory.RemoveQuantity("Cherries", 1);

                    if (randomNumber + AlchemySuccess >= 100)
                    {
                        potion = Item.Create(Aisling, itemToGive);
                        potion.Stacks = 3;
                        Aisling.Bank.Deposit(potion);
                        Aisling.Client.SystemMessage(successMessage);
                        if (Aisling.AlchemySkill <= Aisling.RecipeDifficulty + 2)
                        {
                            Aisling.AlchemySuccess++;
                            UpdateAlchemyRank();
                        }
                        else
                            SystemMessage("You cannot gain any more skill from this product.");
                    }
                    else
                    {
                        Aisling.Client.SystemMessage("Your concoction explodes!");
                        Aisling.CurrentHp -= 250;
                        Aisling.Animate(14);
                    }

                    Aisling.Client.SendStats(StatusFlags.All);
                }
                break;
            //Hemloch
            case 7:
                {
                    AlchemySuccess -= 10.0;
                    itemToGive = "Hemloch Deum";

                    Aisling.Inventory.RemoveQuantity("Reagent", wineAmount);
                    Aisling.Inventory.RemoveQuantity("Hemloch Plant", plantAmount);
                    Aisling.Inventory.RemoveQuantity("Cherries", plantAmount);

                    if (randomNumber + AlchemySuccess >= 100)
                    {
                        potion = Item.Create(Aisling, itemToGive);
                        Aisling.Bank.Deposit(potion);
                        Aisling.Client.SystemMessage(successMessage);
                        if (Aisling.AlchemySkill <= Aisling.RecipeDifficulty + 2)
                        {
                            Aisling.AlchemySuccess++;
                            UpdateAlchemyRank();
                        }
                        else
                            SystemMessage("You cannot gain any more skill from this product.");
                    }
                    else
                    {
                        Aisling.Client.SystemMessage("Your concoction explodes!");
                        Aisling.CurrentHp -= 250;
                        Aisling.Animate(14);
                    }

                    Aisling.Client.SendStats(StatusFlags.All);
                }
                break;
                #endregion Other
        }
    }
    #endregion Alchemy
    #region Gem Smithing
    public void CutGem()
    {
        var gSmithLevel = Aisling.GemCuttingSkill;
        var gSmithSkill = 50.0 + (gSmithLevel * 5.0);
        var gemGrade = "";
        var finishedProduct = "";
        var gemName = "";
        var r = 0;
        var gem = new Item();
        lock (Generator.Random)
            r = Generator.Random.Next(0, 101);
        if ((Aisling.Religion == 6) && Aisling.HasBuff("Industry"))
            gSmithSkill += 10.0;
        switch (Aisling.Gem)
        {
            case 1:
                {
                    gemName = "Beryl";
                }
                break;
            case 2:
                {
                    gemName = "Sapphire";
                    gSmithSkill -= 5.0;
                }
                break;
            case 3:
                {
                    gemName = "Ruby";
                    gSmithSkill -= 7.5;
                }
                break;
            case 4:
                {
                    gemName = "Emerald";
                    gSmithSkill -= 10.0;
                }
                break;
            case 5:
                {
                    gemName = "Rose Quartz";
                    gSmithSkill -= 15.0;
                }
                break;
        }
        switch (Aisling.GemType)
        {
            case 1:
                {
                    gemGrade = "Raw";
                    if (gemName == "Rose Quartz")
                        finishedProduct = "Uncut";
                    else
                        finishedProduct = "Flawed";
                }
                break;
            case 2:
                {
                    gemGrade = "Flawed";
                    finishedProduct = "Uncut";
                    gSmithSkill -= 5.0;
                }
                break;
            case 3:
                {
                    gemGrade = "Uncut";
                    finishedProduct = "Finished";
                    gSmithSkill -= 10.0;
                }
                break;
            case 4:
                {
                    gemGrade = "Finished";
                    finishedProduct = "Faceted";
                    gSmithSkill -= 15.0;
                }
                break;
        }

        //remove items
        switch (gemGrade)
        {
            case "Raw":
                {
                    Aisling.Inventory.RemoveQuantity($"Raw {gemName}", 2);
                }
                break;
            case "Flawed":
                {
                    Aisling.Inventory.RemoveQuantity($"Flawed {gemName}", 2);
                }
                break;
            case "Uncut":
                {
                    Aisling.Inventory.RemoveQuantity($"Uncut {gemName}", 2);
                }
                break;
            case "Finished":
                {
                    Aisling.Inventory.RemoveQuantity($"Finished {gemName}", 2);
                }
                break;
        }
        //perform craft
        if (r + gSmithSkill >= 100)
        {
            gem = Item.Create(Aisling, $"{finishedProduct} {gemName}");
            Aisling.Bank.Deposit(gem);
            SystemMessage($"You are successful! {finishedProduct} {gemName} sent to storage.");
            if (Aisling.GemCuttingSkill <= Aisling.RecipeDifficulty + 2)
            {
                Aisling.GemSuccess++;
                UpdateGemCutRank();
            }
            else
                SystemMessage("You cannot gain any more skill from this product.");
        }
        if (r + gSmithSkill < 100)
        {
            Aisling.GiveItem($"{gemName} Fragments");
            SystemMessage($"The {gemName} shatters into tiny fragments!");
        }
        //produce result
        SendStats(StatusFlags.All);
    }
    #endregion Gem Smithing
    #region Jeweling
    public void CraftRing()
    {
        var r = 0;
        lock (Generator.Random)
            r = Generator.Random.Next(0, 101);

        var jewelCraftingLevel = Aisling.JewelCraftingSkill;
        var jewelCraftingSkill = 50.0 + (jewelCraftingLevel * 5);
        if ((Aisling.Religion == 5) && Aisling.HasBuff("Industry"))
            jewelCraftingSkill += 10.0;
        var Metal = Aisling.RingMat;
        var Metals = "";
        var Gems = "";

        switch (Aisling.RingMat)
        {
            case 1:
                {
                    Metals = "Bronze";
                }
                break;
            case 2:
                {
                    Metals = "Silver";
                    jewelCraftingSkill -= 5.0;
                }
                break;
            case 3:
                {
                    Metals = "Mythril";
                    jewelCraftingSkill -= 10.0;
                }
                break;
            case 4:
                {
                    Metals = "Hy-Brasyl";
                    jewelCraftingSkill -= 20.0;
                }
                break;
            case 5:
                {
                    Metals = "Talgonite";
                    jewelCraftingSkill -= 25.0;
                }
                break;

        }
        switch (Aisling.RingGem)
        {
            case 1: { Gems = "Beryl"; } break;
            case 2: { Gems = "Sapphire"; } break;
            case 3: { Gems = "Ruby"; } break;
            case 4: { Gems = "Emerald"; } break;
            case 5: { Gems = "Rose Quartz"; } break;
        }
        switch (Metal)
        {
            case 1:
                {
                    Aisling.Inventory.RemoveQuantity($"Flawed {Gems}", 1);
                    Aisling.Inventory.RemoveQuantity($"{Metals} Sheet", 2);
                }
                break;
            case 2:
                {
                    Aisling.Inventory.RemoveQuantity($"Uncut {Gems}", 1);
                    Aisling.Inventory.RemoveQuantity($"{Metals} Sheet", 2);
                }
                break;
            case 3:
                {
                    Aisling.Inventory.RemoveQuantity($"Finished {Gems}", 1);
                    Aisling.Inventory.RemoveQuantity($"{Metals} Sheet", 2);
                }
                break;
            case 4:
                {
                    Aisling.Inventory.RemoveQuantity($"Finished {Gems}", 3);
                    Aisling.Inventory.RemoveQuantity($"Faceted {Gems}", 1);
                    Aisling.Inventory.RemoveQuantity($"{Metals} Sheet", 2);
                }
                break;
            case 5:
                {
                    Aisling.Inventory.RemoveQuantity($"Finished {Gems}", 5);
                    Aisling.Inventory.RemoveQuantity($"Faceted {Gems}", 5);
                    Aisling.Inventory.RemoveQuantity($"{Metals} Sheet", 2);
                }
                break;
        }

        if (r + jewelCraftingSkill >= 100)
        {
            var itemname = $"{Metals} {Gems} Ring";
            var basegear = new Item();
            var gearTemplate = ServerContext.GlobalItemTemplateCache[itemname];
            {
                basegear.Template = gearTemplate;
            }
            var gear = new Item
            {
                Template = basegear.Template,
                DisplayImage = (ushort)basegear.Template.DisplayImage,
                Image = (ushort)basegear.Template.Image,
                Color = (byte)basegear.Template.Color,
                Stacks = 1,
                ItemId = Generator.GenerateNumber(),
            };
            if (Aisling.JewelCraftingSkill <= Aisling.RecipeDifficulty + 2)
            {
                Aisling.JewelCraftingSuccess++;
                UpdateJewelingRank();
            }
            else
                SystemMessage("You cannot gain any more skill from this product.");
            Aisling.Bank.Deposit(gear);
            SendMessage(0x02, $"You have succeeded! {gear.Template.Name} sent to storage.");
        }
        if (r + jewelCraftingSkill < 100)
        {
            Aisling.Client.SendMessage(0x02, "You have failed and destroyed the materials in the process!");
        }

        Aisling.Client.SendStats(StatusFlags.All);
    }
    public void CraftNecklace()
    {
        int r = 0;
        lock (Generator.Random)
            r = Generator.Random.Next(0, 101);
        var jewelCraftingLevel = Aisling.JewelCraftingSkill;
        var jewelCraftingSkill = 50.0 + (jewelCraftingLevel * 5);
        if ((Aisling.Religion == 5) && Aisling.HasBuff("Industry"))
            jewelCraftingSkill += 10.0;
        string Gems = string.Empty;
        string Grade = string.Empty;
        switch (Aisling.NecklaceType)
        {
            case 1:
                {
                    Gems = "Beryl";
                }
                break;
            case 2:
                {
                    Gems = "Sapphire";
                    jewelCraftingSkill -= 5.0;
                }
                break;
            case 3:
                {
                    Gems = "Ruby";
                    jewelCraftingSkill -= 10.0;
                }
                break;
            case 4:
                {
                    Gems = "Emerald";
                    jewelCraftingSkill -= 20.0;
                }
                break;
            case 5:
                {
                    Gems = "Rose Quartz";
                    jewelCraftingSkill -= 30.0;
                }
                break;
        }
        switch (Aisling.NecklaceGrade)
        {
            case 1:
                {
                    Grade = "Basic";
                    Aisling.Inventory.RemoveQuantity($"Flawed {Gems}", 1);
                    Aisling.Inventory.RemoveQuantity($"Bronze Sheet", 1);
                    Aisling.Inventory.RemoveQuantity($"Plain Fabric", 1);
                }
                break;
            case 2:
                {
                    Grade = "Flawed";
                    Aisling.Inventory.RemoveQuantity($"Uncut {Gems}", 1);
                    Aisling.Inventory.RemoveQuantity($"Silver Sheet", 1);
                    Aisling.Inventory.RemoveQuantity($"Sturdy Fabric", 1);
                }
                break;
            case 3:
                {
                    Grade = "Fine";
                    Aisling.Inventory.RemoveQuantity($"Finished {Gems}", 1);
                    Aisling.Inventory.RemoveQuantity($"Mythril Sheet", 1);
                    Aisling.Inventory.RemoveQuantity($"Soft Fabric", 1);
                }
                break;
            case 4:
                {
                    Grade = "Flawless";
                    Aisling.Inventory.RemoveQuantity($"Finished {Gems}", 1);
                    Aisling.Inventory.RemoveQuantity($"Faceted {Gems}", 1);
                    Aisling.Inventory.RemoveQuantity($"Hy-Brasyl Sheet", 1);
                    Aisling.Inventory.RemoveQuantity($"Elegant Fabric", 1);
                }
                break;
            case 5:
                {
                    Grade = "Perfect";
                    Aisling.Inventory.RemoveQuantity($"Faceted {Gems}", 2);
                    Aisling.Inventory.RemoveQuantity($"Talgonite Sheet", 1);
                    Aisling.Inventory.RemoveQuantity($"Shimmering Fabric", 1);
                }
                break;
        }
        //If Failed
        if (r + jewelCraftingSkill < 100)
        {
            Aisling.Client.SystemMessage("You have failed and destroyed the materials in the process!");
        }
        //If Successful
        if (r + jewelCraftingSkill >= 100)
        {
            var itemname = $"{Grade} {Gems} Necklace";
            var basegear = new Item();
            var gearTemplate = ServerContext.GlobalItemTemplateCache[itemname];
            {
                basegear.Template = gearTemplate;
            }
            var gear = new Item
            {
                Template = basegear.Template,
                DisplayImage = basegear.Template.DisplayImage,
                Image = basegear.Template.Image,
                Color = (byte)basegear.Template.Color,
                Stacks = 1,
                ItemId = Generator.GenerateNumber(),
            };
            if (Aisling.JewelCraftingSkill <= Aisling.RecipeDifficulty + 2)
            {
                Aisling.JewelCraftingSuccess++;
                UpdateJewelingRank();
            }
            else
                SystemMessage("You cannot gain any more skill from this product.");
            Aisling.Bank.Deposit(gear);
            SendMessage(0x02, $"You have succeeded! {gear.Template.Name} sent to storage.");
        }

        Aisling.Client.SendStats(StatusFlags.All);
    }
    public void CraftEarrings()
    { }
    public void AddJewel()
    {
        var r = 0;
        lock (Generator.Random)
            r = Generator.Random.Next(0, 101);
        var jewelCraftingLevel = Aisling.JewelCraftingSkill;
        var jewelCraftingSkill = 50.0 + (jewelCraftingLevel * 5);
        if ((Aisling.Religion == 5) && Aisling.HasBuff("Industry"))
            jewelCraftingSkill += 10;
        var gem = "";
        var material = "";
        var slot = "";

        switch (Aisling.GemType)
        {
            case 1: { gem = "Sapphire"; } break;
            case 2: { gem = "Ruby"; } break;
            case 3: { gem = "Emerald"; } break;
            case 4: { gem = "Rose Quartz"; } break;//Specific crafts only.
        }
        switch (Aisling.CraftSlot)
        {
            case 1:
                { slot = "Bracer"; }
                break;
            case 2:
                { slot = "Gauntlet"; }
                break;
            case 3:
                { slot = "Greaves"; }
                break;
            case 4:
                { slot = "Shield"; }
                break;
        }
        //take items
        switch (Aisling.CraftMaterial)
        {
            //Wooden
            case 1:
                {
                    material = "Wooden";
                    Aisling.Inventory.RemoveQuantity($"{material} {slot}", 1);
                    Aisling.Inventory.RemoveQuantity($"Flawed {gem}", 1);
                    Aisling.Inventory.RemoveQuantity($"Bronze Ingot", 1);
                    Aisling.Inventory.RemoveQuantity($"Bronze Sheet", 1);
                }
                break;
            //Leather
            case 2:
                {
                    material = "Leather";
                    jewelCraftingSkill -= 5.0;
                    Aisling.Inventory.RemoveQuantity($"{material} {slot}", 1);
                    Aisling.Inventory.RemoveQuantity($"Flawed {gem}", 1);
                    Aisling.Inventory.RemoveQuantity($"Bronze Sheet", 2);
                }
                break;
            //Bronze
            case 3:
                {
                    material = "Bronze";
                    jewelCraftingSkill -= 10.0;
                    Aisling.Inventory.RemoveQuantity($"{material} {slot}", 1);
                    Aisling.Inventory.RemoveQuantity($"Finished {gem}", 1);
                    Aisling.Inventory.RemoveQuantity($"Bronze Sheet", 2);
                }
                break;
            //Iron
            case 4:
                {
                    material = "Iron";
                    jewelCraftingSkill -= 12.5;
                    Aisling.Inventory.RemoveQuantity($"{material} {slot}", 1);
                    Aisling.Inventory.RemoveQuantity($"Uncut {gem}", 1);
                    Aisling.Inventory.RemoveQuantity($"Silver Sheet", 2);
                }
                break;
            //Silver
            case 5:
                {
                    //Doesn't exist?
                    material = "Silver";
                    jewelCraftingSkill -= 15.0;
                    Aisling.Inventory.RemoveQuantity($"{material} {slot}", 1);
                    Aisling.Inventory.RemoveQuantity($"Faceted {gem}", 1);
                    Aisling.Inventory.RemoveQuantity($"Faceted Beryl", 1);
                    Aisling.Inventory.RemoveQuantity($"Silver Sheet", 2);
                }
                break;
            //Mythril
            case 6:
                {
                    material = "Mythril";
                    jewelCraftingSkill -= 20.0;
                    Aisling.Inventory.RemoveQuantity($"{material} {slot}", 1);
                    Aisling.Inventory.RemoveQuantity($"Faceted {gem}", 1);
                    Aisling.Inventory.RemoveQuantity($"Mythril Sheet", 2);
                }
                break;
            //Hy-Brasyl
            case 7:
                {
                    material = "Hy-Brasyl";
                    jewelCraftingSkill -= 25.0;
                    Aisling.Inventory.RemoveQuantity($"{material} {slot}", 1);
                    Aisling.Inventory.RemoveQuantity($"Faceted {gem}", 2);
                    Aisling.Inventory.RemoveQuantity($"Hy-Brasyl Sheet", 2);
                }
                break;
            //Talos
            case 8:
                {
                    material = "Talos";
                    jewelCraftingSkill -= 30.0;
                    Aisling.Inventory.RemoveQuantity($"{material} {slot}", 1);
                    Aisling.Inventory.RemoveQuantity($"Faceted {gem}", 3);
                    Aisling.Inventory.RemoveQuantity($"Talgonite Sheet", 2);
                }
                break;

        }

        //success
        if (r + jewelCraftingSkill >= 100)
        {
            var itemname = $"{gem} {material} {slot}";
            var basegear = new Item();
            var gearTemplate = ServerContext.GlobalItemTemplateCache[itemname];
            {
                basegear.Template = gearTemplate;
            }
            var gear = new Item
            {
                Template = basegear.Template,
                DisplayImage = basegear.Template.DisplayImage,
                Image = basegear.Template.Image,
                Color = (byte)basegear.Template.Color,
                Stacks = 1,
                ItemId = Generator.GenerateNumber(),
            };

            if (Aisling.JewelCraftingSkill <= Aisling.RecipeDifficulty + 2)
            {
                Aisling.JewelCraftingSuccess++;
                UpdateJewelingRank();
            }
            else
                SystemMessage("You cannot gain any more skill from this product.");
            Aisling.Bank.Deposit(gear);
            SystemMessage($"You have succeeded! {gear.Template.Name} sent to storage.");
        }
        // or fail?
        if (r + jewelCraftingSkill < 100)
        {
            SystemMessage("The materials were destroyed in the process.");
        }
        //final messages
        Aisling.Client.SendStats(StatusFlags.All);
    }
    #endregion Jeweling
    #region Weaving
    public void Weave()
    {
        var weavingLevel = Aisling.WeavingSkill;
        var weavingSkill = 50.0 + (weavingLevel * 5.0);
        if ((Aisling.Religion == 2) && Aisling.HasBuff("Industry"))
            weavingSkill += 10.0;
        var fiberGrade = string.Empty;
        var finishedProduct = string.Empty;
        var r = 0;
        lock (Generator.Random)
            r = Generator.Random.Next(0, 101);
        var fiber = new Item();
        switch (Aisling.Fiber)
        {
            case 1:
                {
                    fiberGrade = "Rough";
                    finishedProduct = "Plain";
                }
                break;
            case 2:
                {
                    fiberGrade = "Thick";
                    finishedProduct = "Sturdy";
                    weavingSkill -= 5.0;
                }
                break;
            case 3:
                {
                    fiberGrade = "Fine";
                    finishedProduct = "Soft";
                    weavingSkill -= 10.0;
                }
                break;
            case 4:
                {
                    fiberGrade = "Delicate";
                    finishedProduct = "Elegant";
                    weavingSkill -= 20.0;
                }
                break;
            case 5:
                {
                    fiberGrade = "Rich";
                    finishedProduct = "Shimmering";
                    weavingSkill -= 30.0;
                }
                break;
        }

        Aisling.Inventory.RemoveQuantity($"{fiberGrade} Fiber", 2);
        if (r + weavingSkill >= 100)
        {
            if (Aisling.WeavingSkill <= Aisling.RecipeDifficulty + 2)
            {
                Aisling.WeavingSuccess++;
                UpdateWeavingRank();
            }
            else
                SystemMessage("You cannot gain any more skill from this product.");
            fiber = Item.Create(Aisling, $"{finishedProduct} Fabric");
            Aisling.Bank.Deposit(fiber);
            SystemMessage($"You are successful! {finishedProduct} Fabric sent to storage.");
        }
        if (r + weavingSkill < 100)
        {
            Aisling.GiveItem($"{fiberGrade} Tatters");
            SystemMessage($"The {fiberGrade} Fiber has been ruined!");
        }

        SendStats(StatusFlags.All);
    }
    #endregion Weaving
    #region Gathering
    public bool ShouldGather()
    {
        var herbalism = Aisling.Herbalism;
        var gather = 30.0 + (herbalism * 5);

        if (Aisling.HasBuff("Industry"))
            gather += 10.0;

        var r = rand.Next(0, 101);

        if (r + gather >= 100)
            return true;

        Aisling.Client.SendMessage(0x02, "You destroyed that which you were trying to harvest.");

        return false;
    }
    #endregion Gathering
    #endregion Crafting Methods
    #region Sgrios Scars
    public void ScarLegend()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var Scar in legend)
            if (Scar.Category == "Scar")
            {
                Aisling.LegendBook.LegendMarks.Remove(Scar);
                Legend.RemoveFromDB(Aisling.Client, Scar);
            }
    }
    #endregion Sgrios Scars
    #region Class Change Methods
    public void ClassChange(GameClient client)
    {
        client.Aisling.EquipmentManager.RemoveFromExisting(1);
        client.Aisling.EquipmentManager.RemoveFromExisting(2);
        client.Aisling.EquipmentManager.RemoveFromExisting(3);
        client.Aisling.EquipmentManager.RemoveFromExisting(4);
        client.Aisling.EquipmentManager.RemoveFromExisting(5);
        client.Aisling.EquipmentManager.RemoveFromExisting(6);
        client.Aisling.EquipmentManager.RemoveFromExisting(7);
        client.Aisling.EquipmentManager.RemoveFromExisting(8);
        client.Aisling.EquipmentManager.RemoveFromExisting(9);
        client.Aisling.EquipmentManager.RemoveFromExisting(10);
        client.Aisling.EquipmentManager.RemoveFromExisting(11);
        client.Aisling.EquipmentManager.RemoveFromExisting(12);
        client.Aisling.EquipmentManager.RemoveFromExisting(13);
        client.Aisling.EquipmentManager.RemoveFromExisting(14);
        client.Aisling.EquipmentManager.RemoveFromExisting(15);
        client.Aisling.EquipmentManager.RemoveFromExisting(16);
        client.Aisling.EquipmentManager.RemoveFromExisting(17);
        client.Aisling.ExpLevel = 1;
        client.Aisling.ExpNext = 60;
        client.Aisling.ExpTotal = 0;
        client.Aisling._Str = 3;
        client.Aisling._Int = 3;
        client.Aisling._Wis = 3;
        client.Aisling._Con = 3;
        client.Aisling._Dex = 3;
        client.Aisling.StatPoints = 14;
        client.Aisling._MaximumHp = 200;
        client.Aisling._MaximumMp = 100;
        var spellbook = client.Aisling.SpellBook.Get(i => i.Level > 0).ToList();
        var skillbook = client.Aisling.SkillBook.Get(i => i.Level > 0).ToList();
        foreach (var spell in spellbook)
        {
            spell.Level = 0;
            spell.Casts = 0;
        }
        foreach (var skill in skillbook)
        {
            skill.Level = 0;
            skill.Uses = 0;
        }
        client.Save();
        client.LoadSpellBook();
        client.LoadSkillBook();
        client.SendStats(StatusFlags.All);

    }
    public void NewClass(GameClient client)
    {
        client.Aisling.EquipmentManager.RemoveFromExisting(1);
        client.Aisling.EquipmentManager.RemoveFromExisting(2);
        client.Aisling.EquipmentManager.RemoveFromExisting(3);
        client.Aisling.EquipmentManager.RemoveFromExisting(4);
        client.Aisling.EquipmentManager.RemoveFromExisting(5);
        client.Aisling.EquipmentManager.RemoveFromExisting(6);
        client.Aisling.EquipmentManager.RemoveFromExisting(7);
        client.Aisling.EquipmentManager.RemoveFromExisting(8);
        client.Aisling.EquipmentManager.RemoveFromExisting(9);
        client.Aisling.EquipmentManager.RemoveFromExisting(10);
        client.Aisling.EquipmentManager.RemoveFromExisting(11);
        client.Aisling.EquipmentManager.RemoveFromExisting(12);
        client.Aisling.EquipmentManager.RemoveFromExisting(13);
        client.Aisling.EquipmentManager.RemoveFromExisting(14);
        client.Aisling.EquipmentManager.RemoveFromExisting(15);
        client.Aisling.EquipmentManager.RemoveFromExisting(16);
        client.Aisling.EquipmentManager.RemoveFromExisting(17);
        client.Aisling.ExpLevel = 1;
        client.Aisling.ExpNext = 60;
        client.Aisling.ExpTotal = 0;
        client.Aisling._Str = 3;
        client.Aisling._Int = 3;
        client.Aisling._Wis = 3;
        client.Aisling._Con = 3;
        client.Aisling._Dex = 3;
        if (client.Aisling.StatPoints > 14)
            client.Aisling.StatPoints = 14;

        client.Aisling._MaximumHp = 200;
        client.Aisling._MaximumMp = 100;
        var spellbook = client.Aisling.SpellBook.Get(i => i.Level > 0).ToList();
        var skillbook = client.Aisling.SkillBook.Get(i => i.Level > 0).ToList();
        foreach (var spell in spellbook)
        {
            spell.Level = 0;
            spell.Casts = 0;
        }
        foreach (var skill in skillbook)
        {
            skill.Level = 0;
            skill.Uses = 0;
        }
        client.Save();
        client.LoadSpellBook();
        client.LoadSkillBook();
        client.SendStats(StatusFlags.All);

    }
    #endregion Class Change Methods
    #region Trial Methods
    public void MasterTrials()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var mark in legend)
            if (mark.Category is "Trials")
                Aisling.LegendBook.LegendMarks.Remove(mark);
    }
    public void ResetMasterTrials()
    {
        Aisling.TrialOfAmbition = 0;
        Aisling.TrialOfCommunity = 0;
        Aisling.TrialOfKnowledge = 0;
        Aisling.TrialOfSkill = 0;
        Aisling.TrialOfStrength = 0;
        Aisling.TrialOfWealth = 0;
    }
    #endregion Trial Methods
    #region Kas Mines Methods
    public void MineFloorLegend()
    {
        var legend = Aisling.LegendBook.LegendMarks.ToList();
        foreach (var Floor in legend)
            if (Floor.Category == "MineFloor")
            {
                Aisling.LegendBook.LegendMarks.Remove(Floor);
                Legend.RemoveFromDB(Aisling.Client, Floor);
            }
    }

    #endregion Kas Mines Methods
    #region Alpha Rewards
    public GameClient AlphaRewards(Aisling aisling, IReadOnlyList<string> minor, IReadOnlyList<string> major)
    {
        if (!major.Contains(aisling.Username, StringComparer.OrdinalIgnoreCase))
        {
            if (minor.Contains(aisling.Username, StringComparer.OrdinalIgnoreCase))
            {
                aisling.Alpha = 1;
                return this;
            }
            else
                return this;
        }
        else
        {
            aisling.Alpha = 2;
            return this;
        }
    }
    #endregion Alpha Rewards
    #region Beta Rewards
    public GameClient BetaRewards(Aisling aisling, IReadOnlyList<string> minor, IReadOnlyList<string> major)
    {
        if (!major.Contains(aisling.Username, StringComparer.OrdinalIgnoreCase))
        {
            if (minor.Contains(aisling.Username, StringComparer.OrdinalIgnoreCase))
            {
                aisling.Beta = 1;
                return this;
            }
            else
                return this;
        }
        else
        {
            aisling.Beta = 2;
            return this;
        }
    }

    #endregion Beta Rewards
    #endregion Pill's API Modifications
}