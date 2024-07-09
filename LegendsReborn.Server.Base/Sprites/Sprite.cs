using Chaos.Common.Definitions;
using Chaos.Extensions.Common;
using Chaos.Geometry;
using Chaos.Geometry.Abstractions.Definitions;

using Darkages.Common;
using Darkages.Enums;
using Darkages.GameScripts.Items;
using Darkages.Interfaces;
using Darkages.Object;
using Darkages.ScriptingBase;
using Darkages.Types;

using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Formats.Asn1;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Darkages.Types.Buffs;
using Legends.Server.Base.Types.Debuffs;
using static System.Formats.Asn1.AsnWriter;

using MapFlags = Darkages.Enums.MapFlags;

namespace Darkages.Sprites;

public abstract class Sprite : ObjectManager, INotifyPropertyChanged, ISprite
{
    public bool Abyss;
    public Position LastPosition;
    public List<List<TileGrid>> MasterGrid = [];
    public event PropertyChangedEventHandler PropertyChanged;
    public readonly WorldServerTimer BuffAndDebuffTimer;
    public readonly Stopwatch MonsterBuffAndDebuffStopWatch = new();
    private readonly Stopwatch _threatControl = new();
    private readonly object _walkLock = new();

    public bool Alive => CurrentHp > 0;
    public bool Attackable => this is Monster || this is Aisling || this is Mundane;
    public int Ablasion;
    public Aisling PlayerNearby => AislingsNearby().FirstOrDefault();

    #region Buffs Debuffs

    public bool IsAblative => HasBuff("tionac");
    public bool IsAited => HasBuff("beag naomh aite") || HasBuff("naomh aite") || HasBuff("mor naomh aite") || HasBuff("ard naomh aite") || HasBuff("dia naomh aite") || HasBuff("io dia naomh aite ionad") || HasBuff("spionnadh");
    public bool IsArrested => HasBuff("bot check") || HasBuff("arrested") || HasBuff("investigation");
    public bool IsBleeding => HasDebuff("anaemia");
    public bool IsBlind => HasDebuff("blind") || HasDebuff(i => i.Name.ToLower().ContainsI("siolaidh"));
    public bool IsConfused => HasDebuff("confuse");
    public bool IsCursed => HasDebuff(i => i.Name.ToLower().Contains("cradh") || i.Name.ToLower().Contains("siolaidh"));
    public bool IsDoomed => HasDebuff("doom");
    public bool IsDragon => HasBuff("dragon mode");
    public bool IsFrozen => HasDebuff("frozen") || HasDebuff("shock") || HasDebuff(i => i.Name.ToLower().Contains("siolaidh"));
    public bool IsMist => HasBuff("mist");
    public bool IsParalyzed => HasDebuff("paralyze") || HasDebuff(i => i.Name.ToLower().Contains("suain") || HasDebuff("herbalism"));
    public bool IsPetrified => HasDebuff("petrify");
    public bool IsPhoenix => HasBuff("phoenix mode");
    public bool IsPoisoned => HasDebuff(i => i.Name.ToLower().ContainsI("puinsein")) || HasDebuff(i => i.Name.ToLower().ContainsI("siolaidh"));
    public bool IsRegen => HasBuff("beag aiseag beatha") || HasBuff("aiseag beatha") || HasBuff("mor aiseag beatha") || HasBuff("ard aiseag beatha");
    public bool IsRefresh => HasBuff("beag aiseag spiorad") || HasBuff("aiseag spiorad") || HasBuff("mor aiseag spiorad") || HasBuff("ard aiseag spiorad");
    public bool IsShield => HasBuff("sgiath");
    public bool IsSilenced => HasDebuff("silence") || HasDebuff(i => i.Name.ToLower().ContainsI("siolaidh"));
    public bool IsSleeping => HasDebuff("pramh") || HasDebuff("mor pramh") || HasDebuff(i => i.Name.ToLower().ContainsI("siolaidh"));
    public bool EmpoweredAssail { get; set; }
    public bool DragonScale { get; set; }
    public bool PhoenixAssail { get; set; }
    public bool WraithAssail { get; set; }
    public int Barrier { get; set; }
    public bool SpellReflect { get; set; }
    public bool SkillReflect { get; set; }
    public bool Immunity { get; set; }

    #endregion

    public bool CanCast => !(IsFrozen || IsSleeping || IsSilenced || IsPetrified);
    public bool CanMove => !(IsFrozen || IsSleeping || IsParalyzed || IsBlind || IsPetrified || IsShield);
    public bool CanBash => !(IsFrozen || IsSleeping || IsParalyzed || IsPetrified);
    public bool HasDoT => IsBleeding || IsPoisoned;
    public int? EventHp = null;
    public int? EventMp = null;
    public int? MapHp = null;
    public int? MapMp = null;
    public int MaximumHp =>
        EventHp != null && Map.Flags.HasFlag(MapFlags.ArenaTeam) ? EventHp.Value + BonusHp
        : MapHp != null && MapHp.Value < _MaximumHp ? MapHp.Value + BonusHp
        : _MaximumHp + BonusHp;
    public int MaximumMp =>
        EventMp != null && Map.Flags.HasFlag(MapFlags.ArenaTeam) ? EventMp.Value
        : MapMp != null && MapMp.Value < _MaximumMp ? MapMp.Value + BonusMp
        : _MaximumMp + BonusMp;
    public double HpPct
    {
        get => MaximumHp != 0 ? Math.Clamp(CurrentHp * 100.0 / MaximumHp, 0, 100) : 0;
        set => CurrentHp = (int)(MaximumHp / 100.0 * value);
    }
    public double MpPct
    {
        get => MaximumMp != 0 ? Math.Clamp(CurrentMp * 100.0 / MaximumMp, 0, 100) : 0;
        set => CurrentMp = (int)(MaximumMp / 100.0 * value);
    }
    public int Regen => (_Regen + BonusRegen).IntClamp(0, byte.MaxValue);
    public int Dmg => (_Dmg + BonusDmg).IntClamp(0, byte.MaxValue);
    private int Ac => (_ac + BonusAc).IntClamp(-127, 127);
    public int Hit => IsBlind ? 0 : (_Hit + BonusHit).IntClamp(0, byte.MaxValue);
    private int Mr => (_Mr + BonusMr).IntClamp(0, 70);
    public int Str => (_Str + BonusStr).IntClamp(0, ServerSetup.Instance.Config.StatCap);
    public int Int => (_Int + BonusInt).IntClamp(0, ServerSetup.Instance.Config.StatCap);
    public int Wis => (_Wis + BonusWis).IntClamp(0, ServerSetup.Instance.Config.StatCap);
    public int Con => (_Con + BonusCon).IntClamp(0, ServerSetup.Instance.Config.StatCap);
    public int Dex => (_Dex + BonusDex).IntClamp(0, ServerSetup.Instance.Config.StatCap);

    public Area Map => ServerSetup.Instance.GlobalMapCache.GetValueOrDefault(CurrentMapId);

    #region Map HP/MP Limiters

    public bool IsAosdanMountain => CurrentMapId is 312 or (>= 11553 and <= 11562);
    public bool IsCthonicRemains => CurrentMapId is >= 5001 and <= 5030;
    public bool IsDubhaimAdvance => CurrentMapId is 18 or 19 or 20 or 22 or 23 or 24 or 26 or 27;
    public bool IsOrenRuins => CurrentMapId is 58 or 59 or 61 or 62 or 64;
    public bool IsKas1F => CurrentMapId is 624 or 11522;
    public bool IsKas2F => CurrentMapId is 629 or 11523;
    public bool IsKas3F => CurrentMapId is 633 or 11524;
    public bool IsKas4F => CurrentMapId is 628 or 11525;
    public bool IsKas5F => CurrentMapId is 0;
    public bool IsLimbo => CurrentMapId is 6969 or 6970 or 6971;

    #endregion Map HP/MP Limiters

    public Position Position => new(Pos);
    public TileContent EntityType { get; protected set; }

    public int Level => EntityType == TileContent.Aisling ? ((Aisling)this).ExpLevel
        : EntityType == TileContent.Monster ? ((Monster)this).Template.Level
        : EntityType == TileContent.Mundane ? ((Mundane)this).Template.Level
        : EntityType == TileContent.Item ? ((Item)this).Template.LevelRequired : 1;
    public Aisling Summoner { get; set; }

    private static readonly int[][] Directions =
    [
        [+0, -1],
        [+1, +0],
        [+0, +1],
        [-1, +0]
    ];

    private static int[][] DirectionTable { get; } =
    [
        [-1, +3, -1],
        [+0, -1, +2],
        [-1, +1, -1]
    ];

    private double TargetDistance { get; set; }

    protected Sprite()
    {
        if (this is Aisling)
            TileType = TileContent.Aisling;
        if (this is Monster)
            TileType = TileContent.Monster;
        if (this is Mundane)
            TileType = TileContent.Mundane;
        if (this is Money)
            TileType = TileContent.Money;
        if (this is Item)
            TileType = TileContent.Item;
        var readyTime = DateTime.UtcNow;
        BuffAndDebuffTimer = new WorldServerTimer(TimeSpan.FromSeconds(1));
        Amplified = 0;
        Target = null;
        Buffs = [];
        Debuffs = [];
        LastTargetAcquired = readyTime;
        LastMovementChanged = readyTime;
        LastTurnUpdated = readyTime;
        LastUpdated = readyTime;
        LastPosition = new Position(Vector2.Zero);
    }

    public static Aisling Aisling(Sprite obj)
    {
        if (obj is Aisling aisling)
            return aisling;

        return null;
    }

    public uint Serial { get; set; }
    public int CurrentMapId { get; set; }
    public double Amplified { get; set; }
    public ElementManager.Element OffenseElement { get; set; }
    public ElementManager.Element DefenseElement { get; set; }
    public DateTime AbandonedDate { get; set; }
    public Sprite Target { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public Vector2 Pos
    {
        get => new(X, Y);
        set
        {
            if (Pos == value) return;
            X = (int)value.X;
            Y = (int)value.Y;
            NotifyPropertyChanged();
        }
    }
    public TileContent TileType { get; set; }
    public byte Direction { get; set; }
    public int PendingX { get; set; }
    public int PendingY { get; set; }
    public int XPos
    {
        get => X;
        set
        {
            if (X == value)
                return;

            X = value;
            NotifyPropertyChanged();
        }
    }
    public int YPos
    {
        get => Y;
        set
        {
            if (Y == value)
                return;

            Y = value;

            NotifyPropertyChanged();
        }
    }
    public DateTime LastMenuInvoked { get; set; }
    public DateTime LastMovementChanged { get; set; }
    public DateTime LastTargetAcquired { get; set; }
    public DateTime LastTurnUpdated { get; set; }
    public DateTime LastUpdated { get; set; }
    public DateTime LastEquipOrUnEquip { get; set; }
    public ConcurrentDictionary<string, Buff> Buffs { get; }
    public ConcurrentDictionary<string, Debuff> Debuffs { get; }

    #region Stats

    public int CurrentHp { get; set; }
    public int _MaximumHp { get; set; }
    public int BaseHp { get; set; }
    public int BonusHp { get; set; }

    public int CurrentMp { get; set; }
    public int _MaximumMp { get; set; }
    public int BaseMp { get; set; }
    public int BonusMp { get; set; }

    public int _Regen { get; set; }
    public int BonusRegen { get; set; }

    public int _Dmg { get; set; }
    public int BonusDmg { get; set; }

    public int BonusAc { get; set; }
    public int _ac { get; set; }

    public int BonusFortitude { get; set; }

    public int _Hit { get; set; }
    public int BonusHit { get; set; }

    public int _Mr { get; set; }
    public int BonusMr { get; set; }

    public int _Str { get; set; }
    public int BonusStr { get; set; }

    public int _Int { get; set; }
    public int BonusInt { get; set; }

    public int _Wis { get; set; }
    public int BonusWis { get; set; }

    public int _Con { get; set; }
    public int BonusCon { get; set; }

    public int _Dex { get; set; }
    public int BonusDex { get; set; }

    #endregion

    public bool SetMapHp(Sprite source)
    {
        var cthonicRemains = 15000;
        var orenRuins = 20000;
        var limbo = 30000;
        var aosdanMountain = 40000;
        var masterLimbo = 50000;
        var dubhaimAdvance = 60000;


        if (IsCthonicRemains || IsKas1F)
            source.MapHp = cthonicRemains;
        else if (IsOrenRuins)
            source.MapHp = orenRuins;
        else if (IsLimbo || IsKas2F)
            source.MapHp = limbo;
        else if (IsAosdanMountain || IsKas3F)
            source.MapHp = aosdanMountain;
        else if (/*IsMasterLimbo ||*/IsKas4F)
            source.MapHp = masterLimbo;
        else if (IsDubhaimAdvance || IsKas5F)
            source.MapHp = dubhaimAdvance;
        else
            source.MapHp = null;
        return true;
    }

    public bool SetMapMp(Sprite source)
    {
        var cthonicRemains = 7500;
        var orenRuins = 10000;
        var limbo = 15000;
        var aosdanMountain = 20000;
        var masterLimbo = 25000;
        var dubhaimAdvance = 30000;

        if (IsCthonicRemains || IsKas1F)
            source.MapMp = cthonicRemains;
        else if (IsOrenRuins)
            source.MapMp = orenRuins;
        else if (IsLimbo || IsKas2F)
            source.MapMp = limbo;
        else if (IsAosdanMountain || IsKas3F)
            source.MapMp = aosdanMountain;
        else if (/*IsMasterLimbo ||*/IsKas4F)
            source.MapMp = masterLimbo;
        else if (IsDubhaimAdvance  /*||IsKas5F*/)
            source.MapMp = dubhaimAdvance;
        else
            source.MapMp = null;
        return true;
    }

    public bool CanBeAttackedHere(Sprite source)
    {
        if (source is not Sprites.Aisling || this is not Sprites.Aisling) return true;
        if ((CurrentMapId <= 0) || !ServerSetup.Instance.GlobalMapCache.ContainsKey(CurrentMapId)) return true;
        return ServerSetup.Instance.GlobalMapCache[CurrentMapId].Flags.HasFlag(MapFlags.PlayerKill);
    }

    public void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #region Identification & Position

    public TSprite CastSpriteToType<TSprite>() where TSprite : Sprite
    {
        return this as TSprite;
    }

    public void ShowTo(Aisling nearbyAisling)
    {
        if (nearbyAisling == null) return;
        if (this is Aisling aisling)
        {
            nearbyAisling.Client.SendDisplayAisling(aisling);
            aisling.SpritesInView.AddOrUpdate(nearbyAisling.Serial, nearbyAisling, (_, _) => nearbyAisling);
        }
        else
        {
            var sprite = new List<Sprite> { this };
            nearbyAisling.Client.SendVisibleEntities(sprite);
        }
    }

    public Aisling[] AislingsNearby() => GetObjects<Aisling>(Map, i => (i != null) && i.WithinViewOf(this)).ToArray();
    public Aisling[] AislingsOnMap() => GetObjects<Aisling>(Map, i => (i != null) && i.WithinRangeOf(this, 100)).ToArray();
    public Item[] ItemsOnMap() => GetObjects<Item>(Map, i => (i != null) && i.WithinRangeOf(this, 100)).ToArray();
    private IEnumerable<Sprite> GetInFrontToSide(int tileCount = 1)
    {
        var results = new List<Sprite>();

        switch (Direction)
        {
            case 0:
                results.AddRange(GetSprites(XPos, YPos - tileCount));
                results.AddRange(GetSprites(XPos + tileCount, YPos - tileCount));
                results.AddRange(GetSprites(XPos - tileCount, YPos - tileCount));
                results.AddRange(GetSprites(XPos + tileCount, YPos));
                results.AddRange(GetSprites(XPos - tileCount, YPos));
                break;

            case 1:
                results.AddRange(GetSprites(XPos + tileCount, YPos));
                results.AddRange(GetSprites(XPos + tileCount, YPos + tileCount));
                results.AddRange(GetSprites(XPos + tileCount, YPos - tileCount));
                results.AddRange(GetSprites(XPos, YPos + tileCount));
                results.AddRange(GetSprites(XPos, YPos - tileCount));
                break;

            case 2:
                results.AddRange(GetSprites(XPos, YPos + tileCount));
                results.AddRange(GetSprites(XPos + tileCount, YPos + tileCount));
                results.AddRange(GetSprites(XPos - tileCount, YPos + tileCount));
                results.AddRange(GetSprites(XPos + tileCount, YPos));
                results.AddRange(GetSprites(XPos - tileCount, YPos));
                break;

            case 3:
                results.AddRange(GetSprites(XPos - tileCount, YPos));
                results.AddRange(GetSprites(XPos - tileCount, YPos + tileCount));
                results.AddRange(GetSprites(XPos - tileCount, YPos - tileCount));
                results.AddRange(GetSprites(XPos, YPos + tileCount));
                results.AddRange(GetSprites(XPos, YPos - tileCount));
                break;
        }

        return results;
    }
    private IEnumerable<Sprite> GetInfront(int tileCount = 1)
    {
        var results = new List<Sprite>();

        for (var i = 1; i <= tileCount; i++)
            switch (Direction)
            {
                case 0:
                    results.AddRange(GetSprites(XPos, YPos - i));
                    break;

                case 1:
                    results.AddRange(GetSprites(XPos + i, YPos));
                    break;

                case 2:
                    results.AddRange(GetSprites(XPos, YPos + i));
                    break;

                case 3:
                    results.AddRange(GetSprites(XPos - i, YPos));
                    break;
            }

        return results;
    }
    public List<Sprite> GetInfront(Sprite sprite, int tileCount = 1) => GetInfront(tileCount).Where(i => (i != null) && (i.Serial != sprite.Serial)).ToList();
    public List<Sprite> GetInfront(int tileCount = 1, bool intersect = false) => GetInfront(tileCount).ToList();
    public List<Sprite> AmbushInFront(int tilecount = 1, bool intersect = false) => AmbushInFront(tilecount).ToList();
    public List<Sprite> DamageableGetInFront(int tileCount = 1, bool intersect = false) => DamageableGetInFront(tileCount).ToList();
    public List<Sprite> StudyCreatureGetInFront(int tileCount = 1, bool intersect = false) => StudyCreatureInFront(tileCount).ToList();
    private IEnumerable<Sprite> AislingGetDamageableSprites(int x, int y) => GetObjects(Map, i => (i.XPos == x) && (i.YPos == y), Get.AislingDamage);
    private IEnumerable<Sprite> StudyCreatureSprites(int x, int y) => GetObjects(Map, i => (i.XPos == x) && (i.YPos == y), Get.Monsters);
    private IEnumerable<Sprite> AmbushSprites(int x, int y) => GetObjects(Map, i => (i.XPos == x) && (i.YPos == y), Get.Monsters & Get.Aislings & Get.Mundanes);
    private IEnumerable<Sprite> DamageableGetInFront(int tileCount = 1)
    {
        var results = new List<Sprite>();

        for (var i = 1; i <= tileCount; i++)
            switch (Direction)
            {
                case 0:
                    results.AddRange(AislingGetDamageableSprites(XPos, YPos - i));
                    break;

                case 1:
                    results.AddRange(AislingGetDamageableSprites(XPos + i, YPos));
                    break;

                case 2:
                    results.AddRange(AislingGetDamageableSprites(XPos, YPos + i));
                    break;

                case 3:
                    results.AddRange(AislingGetDamageableSprites(XPos - i, YPos));
                    break;
            }

        return results;
    }
    private IEnumerable<Sprite> StudyCreatureInFront(int tileCount = 1)
    {
        var results = new List<Sprite>();

        for (var i = 1; i <= tileCount; i++)
            switch (Direction)
            {
                case 0:
                    results.AddRange(StudyCreatureSprites(XPos, YPos - i));
                    break;

                case 1:
                    results.AddRange(StudyCreatureSprites(XPos + i, YPos));
                    break;

                case 2:
                    results.AddRange(StudyCreatureSprites(XPos, YPos + i));
                    break;

                case 3:
                    results.AddRange(StudyCreatureSprites(XPos - i, YPos));
                    break;
            }

        return results;
    }
    private IEnumerable<Sprite> AmbushInFront(int tileCount = 1)
    {
        var results = new List<Sprite>();

        for (var i = 1; i <= tileCount; i++)
            switch (Direction)
            {
                case 0:
                    results.AddRange(AmbushSprites(XPos, YPos - i));
                    break;

                case 1:
                    results.AddRange(AmbushSprites(XPos + i, YPos));
                    break;

                case 2:
                    results.AddRange(AmbushSprites(XPos, YPos + i));
                    break;

                case 3:
                    results.AddRange(AmbushSprites(XPos - i, YPos));
                    break;
            }

        return results;
    }
    public List<Sprite> GetInFrontToSide(Sprite sprite, int tileCount = 1) => GetInFrontToSide(tileCount).Where(i => (i != null) && (i.Serial != sprite.Serial)).ToList();
    public List<Sprite> GetInFrontToSide(int tileCount = 1, bool intersect = false) => GetInFrontToSide(tileCount).ToList();
    private IEnumerable<Sprite> GetSprites(int x, int y) => GetObjects(Map, i => (i.XPos == x) && (i.YPos == y), Get.All);
    public bool WithinRangeOf(Sprite other, bool checkMap = true) => (other != null) && WithinRangeOf(other, ServerSetup.Instance.Config.WithinRangeProximity, checkMap);
    public bool WithinViewOf(Sprite other, bool checkMap = true) => (other != null) && WithinRangeOf(other, ServerSetup.Instance.Config.WithinViewProximity, checkMap);
    public bool WithinRangeOf(Sprite other, int distance, bool checkMap = true)
    {
        if (other == null)
            return false;

        if (!checkMap)
            return WithinRangeOf(other.XPos, other.YPos, distance);

        return (CurrentMapId == other.CurrentMapId) && WithinRangeOf(other.XPos, other.YPos, distance);
    }
    private bool WithinRangeOf(int x, int y, int subjectLength) => DistanceFrom(x, y) <= subjectLength;
    public bool TrapsAreNearby() => Trap.Traps.Select(i => i.Value).Any(i => i.CurrentMapId == CurrentMapId);
    public Monster Monster(Sprite obj)
    {
        if (obj is Monster monster)
            return monster;

        return null;
    }
    public IEnumerable<Monster> MonstersNearby(int distance = 12) => GetObjects<Monster>(Map, i => (i != null) && i.WithinRangeOf(this, distance)).ToArray();
    public IEnumerable<Mundane> MundanesNearby() => GetObjects<Mundane>(Map, i => (i != null) && i.WithinViewOf(this)).ToArray();
    public IEnumerable<Item> ItemsNearby(int distance = 12) => GetObjects<Item>(Map, i => (i != null) && i.WithinRangeOf(this, distance)).ToArray();
    public bool NextTo(int x, int y)
    {
        var xDist = Math.Abs(x - X);
        var yDist = Math.Abs(y - Y);

        return xDist + yDist == 1;
    }
    private int DistanceFrom(int x, int y) => Math.Abs(X - x) + Math.Abs(Y - y);
    protected bool Facing(Sprite other, out int direction) => Facing(other.XPos, other.YPos, out direction);
    public bool Facing(int x, int y, out int direction)
    {
        var xDist = (x - XPos).IntClamp(-1, +1);
        var yDist = (y - YPos).IntClamp(-1, +1);

        direction = DirectionTable[xDist + 1][yDist + 1];
        return Direction == direction;
    }

    #endregion

    #region Movement

    public bool Walk()
    {
        if (this is Aisling user && !user.GameMaster && !user.ShouldWalk)
        {
            user.Client.UpdateDisplay();
            return false;
        }

        void Step0C(int x, int y)
        {
            var readyTime = DateTime.UtcNow;
            Pos = new Vector2(PendingX, PendingY);

            foreach (var player in AislingsNearby())
            {
                player.Client.SendCreatureWalk(Serial, new Point(x, y), (Direction)Direction);
            }

            LastMovementChanged = readyTime;
            LastPosition = new Position(x, y);
        }

        lock (_walkLock)
        {
            var currentPosX = X;
            var currentPosY = Y;

            PendingX = X;
            PendingY = Y;

            var allowGhostWalk = this is Aisling { GameMaster: true };

            if (this is Monster { Template: not null } monster)
            {
                allowGhostWalk = monster.Template.IgnoreCollision;
            }

            // Check position before we add direction, add direction, check position to see if we can commit
            if (!allowGhostWalk)
            {
                if (Map.IsWall(currentPosX, currentPosY)) return false;
                if (Map.IsSpriteInLocationOnWalk(this, PendingX, PendingY)) return false;
            }

            switch (Direction)
            {
                case 0:
                    PendingY--;
                    break;
                case 1:
                    PendingX++;
                    break;
                case 2:
                    PendingY++;
                    break;
                case 3:
                    PendingX--;
                    break;
            }

            if (!allowGhostWalk)
            {
                if (Map.IsWall(PendingX, PendingY)) return false;
                if (Map.IsSpriteInLocationOnWalk(this, PendingX, PendingY)) return false;
            }

            // Commit Walk to other Player Clients
            Step0C(currentPosX, currentPosY);

            // Check Trap Activation
            if (this is Monster trapCheck)
                CheckTraps(trapCheck);

            // Reset our PendingX & PendingY
            PendingX = currentPosX;
            PendingY = currentPosY;

            return true;
        }
    }
    public bool WalkTo(int x, int y) => WalkTo(x, y, false);

    public bool WalkTo(int x, int y, bool ignoreWalls = false)
    {
        var buffer = new byte[2];
        var length = float.PositiveInfinity;
        var offset = 0;

        for (byte i = 0; i < 4; i++)
        {
            var newX = X + Directions[i][0];
            var newY = Y + Directions[i][1];

            if ((newX == x) &&
                (newY == y))
                return false;

            if (Map.IsWall(newX, newY))
                continue;

            if (GetObjects(Map, n => (n.Serial == Serial) && (n.X == newX) && (n.Y == newY),
                    Get.Monsters | Get.Aislings | Get.Mundanes).Any())
                continue;

            var xDist = x - newX;
            var yDist = y - newY;
            //var tDist = Sqrt(xDist * xDist + yDist * yDist);
            var tDist = Math.Max(Math.Abs(xDist), Math.Abs(yDist));

            if (length < tDist)
                continue;

            if (length > tDist)
            {
                length = tDist;
                offset = 0;
            }

            if (offset < buffer.Length)
                buffer[offset] = i;

            offset++;
        }

        if (offset == 0)
            return false;

        lock (Generator.Random)
        {
            var pendingDirection = buffer[Generator.Random.Next(0, offset) % buffer.Length];
            Direction = pendingDirection;

            return Walk();
        }
    }

    public void Wander()
    {
        if (!CanUpdate()) return;

        var savedDirection = Direction;
        var update = false;

        Direction = (byte)RandomNumberGenerator.GetInt32(5);
        if (Direction != savedDirection) update = true;

        if (Walk() || !update) return;

        foreach (var player in AislingsNearby())
        {
            player?.Client.SendCreatureTurn(Serial, (Direction)Direction);
        }
    }

    public void CheckTraps(Monster monster)
    {
        foreach (var trap in ServerSetup.Instance.Traps.Values.Where(t => t.TrapItem.Map.ID == monster.Map.ID))
        {
            if (trap.Owner == null || trap.Owner.Serial == monster.Serial ||
                monster.X != trap.Location.X || monster.Y != trap.Location.Y) continue;

            var triggered = Trap.Activate(trap, monster);
            if (!triggered) continue;
            ServerSetup.Instance.Traps.TryRemove(trap.Serial, out _);
            break;
        }
    }

    public void Turn()
    {
        if (!CanUpdate()) return;

        foreach (var player in AislingsNearby())
        {
            player?.Client.SendCreatureTurn(Serial, (Direction)Direction);
        }

        LastTurnUpdated = DateTime.UtcNow;
    }

    #endregion

    #region Attributes

    private int ComputeDmgFromAc(int dmg)
    {
        var script = ScriptManager.Load<FormulaScript>(ServerSetup.Instance.Config.ACFormulaScript, this);

        return script?.Aggregate(dmg, (current, s) => s.Value.Calculate(this, current)) ?? dmg;
    }

    public static ElementManager.Element CheckRandomElement(ElementManager.Element element)
    {
        if (element == ElementManager.Element.Random)
            element = Generator.RandomEnumValue<ElementManager.Element>();

        return element;
    }

    public void ApplyBuff(string buffName)
    {
        if (HasBuff(buffName))
            return;

        var buff = BuffBase.CreateInstance(buffName);

        buff?.OnApplied(this);
    }

    public void ApplyDamage(Sprite source, int dmg, ElementManager.Element element, byte sound = 1)
    {
        element = CheckRandomElement(element);

        var saved = source.OffenseElement;
        {
            source.OffenseElement = element;
            ApplyDamage(source, dmg, sound);
            source.OffenseElement = saved;
        }
    }

    public void ApplyMagicDamage(Sprite source, int dmg, ElementManager.Element element, byte sound = 1)
    {
        element = CheckRandomElement(element);

        var saved = source.OffenseElement;
        {
            source.OffenseElement = element;
            ApplyMagicDamage(source, dmg, sound);
            source.OffenseElement = saved;
        }
    }

    public void ApplyMagicDamage(Sprite damageDealingSprite, int dmg, byte sound = 1,
        Action<int> dmgcb = null, bool forceTarget = false)
    {
        int ApplyPVPMod()
        {
            if (Map.Flags.HasFlag(MapFlags.PlayerKill))
                dmg = (int)(dmg * ServerSetup.Instance.Config.PvpDamageMod);

            return dmg;
        }
        if (!WithinRangeOf(damageDealingSprite))
            return;
        if (!Attackable)
            return;
        if (!CanBeAttackedHere(damageDealingSprite))
            return;

        dmg = ApplyPVPMod();

        if (!DamageTarget(damageDealingSprite, ref dmg, sound, dmgcb, forceTarget))
            return;
        OnDamaged(damageDealingSprite, dmg);
    }

    public bool IsBehind(Sprite other)
    {
        var relationalDirection = Position.DirectionalRelationTo(other.Position);
        var behindDirection = other.Direction.Reverse();
        return relationalDirection == behindDirection || Position.IsInterCardinalTo(other.Position, (Direction)behindDirection);
    }

    public void ApplyDamage(Sprite damageDealingSprite, int dmg, byte sound = 1,
        Action<int> dmgcb = null, bool forceTarget = false)
    {
        int ApplyPVPMod()
        {
            if (Map.Flags.HasFlag(MapFlags.PlayerKill))
                dmg = (int)(dmg * ServerSetup.Instance.Config.PvpDamageMod);

            return dmg;
        }
        int ApplyBehindTargetMod()
        {
            if (damageDealingSprite.IsBehind(this))
                dmg = Convert.ToInt32(dmg * ServerSetup.Instance.Config.BehindDamageMod);
            return dmg;
        }
        int ApplyBehindTargetRogueMod()
        {
            if (damageDealingSprite.IsBehind(this))
                dmg = Convert.ToInt32(dmg * ServerSetup.Instance.Config.RogueBehindDamageMod);
            return dmg;
        }

        if (!WithinRangeOf(damageDealingSprite))
            return;
        if (!Attackable)
            return;
        if (!CanBeAttackedHere(damageDealingSprite))
            return;

        dmg = ApplyPVPMod();
        if (damageDealingSprite is Aisling aisling && aisling.Path == Class.Rogue)
            dmg = ApplyBehindTargetRogueMod();
        else
            dmg = ApplyBehindTargetMod();
        dmg = ApplyWeaponBonuses(damageDealingSprite, dmg);

        if (dmg > 0)
            ApplyEquipmentDurability(dmg);
        if (!DamageTarget(damageDealingSprite, ref dmg, sound, dmgcb, forceTarget))
            return;

        OnDamaged(damageDealingSprite, dmg);
    }
    public int DamageBarrier(int dmg)
    {
        var oldBarrier = Barrier;

        if (Barrier >= dmg)
        {
            Barrier -= dmg;
            dmg = 0;
        }
        else if (Barrier <= dmg)
        {
            dmg -= Barrier;
            Barrier = 0;
        }
        else
        {
            Barrier = 0;
            dmg = 0;
        }
        if (Barrier < oldBarrier)
            Animate(91);

        return dmg;
    }
    public int CompleteDamageApplication(int dmg, byte sound, Action<int> dmgcb, double amplifier)
    {
        if (dmg <= 0)
            dmg = 1;

        if (CurrentHp > MaximumHp)
            CurrentHp = MaximumHp;

        var dmgApplied = (int)Math.Abs(dmg * amplifier);

        if (Barrier > 0)
            dmgApplied = DamageBarrier(dmgApplied);

        CurrentHp -= dmgApplied;

        if (CurrentHp < 0)
            CurrentHp = 0;

        var hpBar = new ServerFormat13
        {
            Serial = Serial,
            Health = (ushort)((double)100 * CurrentHp / MaximumHp),
            Sound = sound
        };
        if (sound < 127)
            SendSound(sound);

        Show(Scope.NearbyAislings, hpBar);
        {
            dmgcb?.Invoke(dmgApplied);
        }

        return dmgApplied;
    }
    public bool DamageTarget(Sprite damageDealingSprite,
        ref int dmg, byte sound,
        Action<int> dmgcb, bool forced)
    {
        if (this is Monster monster && damageDealingSprite is Aisling aisling)
        {
            if (HasDebuff("expert analysis"))
            {
                Analyze(monster, aisling);
                RemoveDebuff("expert analysis");
            }
            var stillaround = false;
            var objs = GetObjects(Map, i => i.Serial != Serial, Get.Aislings);

            if (monster.Owner != null)
                foreach (var ais in objs)
                    if (ais is Aisling aisling1 && (aisling1.Username.ToLower() == monster.Owner.ToLower()))
                        stillaround = true;

            if (!stillaround)
                monster.Owner = null;

            if (monster.Owner == null)
                monster.Owner = aisling.Username;
        }

        if (Immunity)
        {
            var empty = new ServerFormat13
            {
                Serial = Serial,
                Health = byte.MaxValue,
                Sound = sound
            };

            Show(Scope.NearbyAislings, empty);
            return false;
        }

        if (IsSleeping)
        {
            if (ServerSetup.Instance.Config.SleepProcsDoubleDmg && !HasDebuff("siolaidh"))
                dmg <<= 1;

            RemoveDebuff("pramh");
            RemoveDebuff("mor pramh");
        }

        if (IsMist && (dmg > 5))
        {
            var dmg1 = dmg * 0.95;
            dmg = Convert.ToInt32(dmg1);
        }

        if (IsDragon && (dmg > 5))
        {
            var reduction = dmg * 0.8;
            dmg = Convert.ToInt32(reduction);
        }

        if (IsPhoenix && (dmg > 5))
        {
            var penalty = dmg * 1.2;
            dmg = Convert.ToInt32(penalty);
        }

        if (IsAited && (dmg > 5))
            dmg /= ServerSetup.Instance.Config.AiteDamageReductionMod;

        Target ??= damageDealingSprite;

        var amplifier = GetElementalModifier(damageDealingSprite);
        {
            dmg = ComputeDmgFromAc(dmg);
            dmg = CompleteDamageApplication(dmg, sound, dmgcb, amplifier);
        }

        return true;
    }

    private void Analyze(Sprite target, Aisling aisling)
    {
        var n = "g";
        var o = "g";
        switch (target.DefenseElement)
        {
            case ElementManager.Element.Earth:
                {
                    n = "q";
                }
                break;
            case ElementManager.Element.Wind:
                {
                    n = "c";
                }
                break;
            case ElementManager.Element.Fire:
                {
                    n = "b";
                }
                break;
            case ElementManager.Element.Water:
                {
                    n = "v";
                }
                break;
            case ElementManager.Element.Light:
                {
                    n = "w";
                }
                break;
            case ElementManager.Element.Dark:
                {
                    n = "m";
                }
                break;
            case ElementManager.Element.None:
                {
                    n = "u";
                }
                break;

        }
        switch (target.OffenseElement)
        {
            case ElementManager.Element.Earth:
                {
                    o = "q";
                }
                break;
            case ElementManager.Element.Wind:
                {
                    o = "c";
                }
                break;
            case ElementManager.Element.Fire:
                {
                    o = "b";
                }
                break;
            case ElementManager.Element.Water:
                {
                    o = "v";
                }
                break;
            case ElementManager.Element.Light:
                {
                    o = "w";
                }
                break;
            case ElementManager.Element.Dark:
                {
                    o = "n";
                }
                break;
            case ElementManager.Element.None:
                {
                    o = "u";
                }
                break;
        }
        var titleText = string.Format("{0,33}", "{=sExpert Analysis");
        var levelText = string.Format("{0,-32} {1,-25}", "{=sLevel:{=u " + target.Level, "{=sMagic Resistance:{=u " + target.BonusMr);
        var hpmpText = string.Format("{0,-32} {1,-25}", "{=sCurrent Health:{=u " + target.CurrentHp, "{=sCurrent Mana:{=u " + target.CurrentMp);
        var elementText2 = string.Format("{0,-32} {1,-25}", "{=sOffense Element: " + "{=" + o + target.OffenseElement, "{=sDefense Element: " + "{=" + n + target.DefenseElement);
        aisling.Client.SendMessage(0x08, $"{titleText}\n\n{levelText}\n{hpmpText}\n{elementText2}");
    }

    public void ApplyDebuff(string debuffName)
    {
        if (HasDebuff(debuffName)) return;
        var debuff = DebuffBase.CreateInstance(debuffName);
        debuff?.OnApplied(this);
    }

    public void ApplyEquipmentDurability(int dmg)
    {
        if (this is Aisling aisling && (aisling.DamageCounter++ % 2 == 0) && (dmg > 0) && !aisling.Map.Flags.HasFlag(MapFlags.PlayerKill))
            aisling.EquipmentManager.DecreaseDurability();
    }

    public int ApplyWeaponBonuses(Sprite source, int dmg)
    {
        if (source is not Aisling aisling) return dmg;
        if (aisling.EquipmentManager == null || (aisling.EquipmentManager.Weapon?.Item != null && aisling.Weapon > 0)) return dmg;

        int dam = aisling.BonusDmg;
        int hit = aisling.BonusHit;

        if (aisling.Weapon > 0) return dmg;
        if (hit > dam) hit = dam - 1;

        dmg += Generator.Random.Next(hit, dam);
        return dmg;
    }

    public double CalculateElementalDamageMod(Element element)
    {
        var script = ScriptManager.Load<ElementFormulaScript>(ServerSetup.Instance.Config.ElementTableScript, this);

        return script?.Values.Sum(s => s.Calculate(this, element)) ?? 0.0;
    }
    public int GetBaseDamage(Sprite target, MonsterDamageType type)
    {
        var script = ScriptManager.Load<DamageFormulaScript>(ServerSetup.Instance.Config.BaseDamageScript, this, target, type);
        return script?.Values.Sum(s => s.Calculate(this, target, type)) ?? 1;
    }
    public string GetDebuffName(Func<DebuffBase, bool> p)
    {
        if ((Debuffs == null) || (Debuffs.Count == 0))
            return string.Empty;

        return Debuffs.Select(i => i.Value)
            .FirstOrDefault(p)
            ?.Name;
    }
    public double GetElementalModifier(Sprite damageDealingSprite)
    {
        if (damageDealingSprite == null)
            return 1;

        var element = CheckRandomElement(damageDealingSprite.OffenseElement);
        var saved = DefenseElement;

        var amplifier = CalculateElementalDamageMod(element);
        {
            DefenseElement = saved;
        }

        if (damageDealingSprite.Amplified == 0)
            return amplifier;

        amplifier *= Amplified == 1
            ? ServerSetup.Instance.Config.FasNadurStrength
            : ServerSetup.Instance.Config.MorFasNadurStrength;

        return amplifier;
    }
    public void OnDamaged(Sprite source, int dmg)
    {
        (this as Aisling)?.Client.SendStats(StatusFlags.Vitality | StatusFlags.ExpGold);
        (source as Aisling)?.Client.SendStats(StatusFlags.Vitality | StatusFlags.ExpGold);

        if (!(this is Monster))
        {
            if (this is Aisling player)
                if (player.EquipmentManager.Armor is not null)
                    ApplyArmorProc(player);
            return;
        }

        if (!(source is Aisling aisling))
            return;
        var monsterScripts = (this as Monster)?.Scripts;

        if (monsterScripts == null)
            return;

        foreach (var script in monsterScripts.Values)
            script?.OnDamaged(aisling?.Client, dmg, source);
    }
    public bool HasBuff(string buff)
    {
        if ((Buffs == null) || (Buffs.Count == 0))
            return false;

        return Buffs.ContainsKey(buff);
    }
    public bool HasDebuff(string debuff)
    {
        if ((Debuffs == null) || (Debuffs.Count == 0))
            return false;

        return Debuffs.ContainsKey(debuff);
    }
    private bool HasDebuff(Func<DebuffBase, bool> p)
    {
        if ((Debuffs == null) || (Debuffs.Count == 0))
            return false;

        return Debuffs.Select(i => i.Value).FirstOrDefault(p) != null;
    }
    private void RemoveAllBuffs()
    {
        if (Buffs == null)
            return;

        foreach (var buff in Buffs)
            RemoveBuff(buff.Key);
    }
    private void DispelBuffs()
    {
        if (Buffs == null)
            return;

        foreach (var buff in Buffs)
            if ((buff.Key != "mines") && (buff.Key != "Industry") && (buff.Key != "true sight")
                && !buff.Key.ContainsI("alliance") && !buff.Key.ContainsI("gathering") && !buff.Key.ContainsI("inspiration"))
                RemoveBuff(buff.Key);
    }
    private void DispelDebuffs()
    {
        if (Debuffs == null)
            return;
        foreach (var debuff in Debuffs)
            if ((debuff.Key != "herbalism") && (debuff.Key != "skulled") && (debuff.Key != "arrested"))
                RemoveDebuff(debuff.Key, true);
    }
    private void RemoveAllDebuffs()
    {
        if (Debuffs == null)
            return;

        foreach (var debuff in Debuffs)
            RemoveDebuff(debuff.Key);
    }
    public bool RemoveBuff(string buff)
    {
        if (!HasBuff(buff))
            return false;

        var buffObj = Buffs[buff];
        buffObj?.OnEnded(this);

        return true;
    }
    public void RemoveBuffsAndDebuffs()
    {
        RemoveAllBuffs();
        RemoveAllDebuffs();
    }
    public void DispelBuffsAndDebuffs()
    {
        DispelBuffs();
        DispelDebuffs();
    }
    public bool RemoveDebuff(string debuff, bool cancelled = false)
    {
        if (!cancelled && (debuff == "skulled"))
            return true;

        if (!HasDebuff(debuff))
            return false;

        var buffObj = Debuffs[debuff];

        if (buffObj == null)
            return false;

        buffObj.Cancelled = cancelled;
        buffObj.OnEnded(this);

        return true;
    }
    public void ApplyArmorProc(Aisling aisling)
    {
        if (aisling.EquipmentManager.Armor is not null)
        {
            if (aisling.EquipmentManager.Armor.Item.Template.Flags.HasFlag(ItemFlags.RegenProc))
            {
                var diceRoll = RollD10000();
                if (diceRoll <= 75)
                {
                    if (!aisling.HasBuff("beag aiseag beatha") && !aisling.HasBuff("aiseag beatha") && !aisling.HasBuff("mor aiseag beatha"))
                        aisling.ApplyBuff("beag aiseag beatha");
                }
            }
            if (aisling.EquipmentManager.Armor.Item.Template.Flags.HasFlag(ItemFlags.RefreshProc))
            {
                var diceRoll = RollD10000();
                if (diceRoll <= 75)
                {
                    if (!aisling.HasBuff("beag aiseag spiorad") && !aisling.HasBuff("aiseag spiorad") && !aisling.HasBuff("mor aiseag spiorad"))
                        aisling.ApplyBuff("beag aiseag spiorad");
                }
            }
        }
    }
    protected virtual int RollD10000() => Random.Shared.Next(1, 10001);
    #endregion

    #region Status
    public void UpdateAddAndRemove()
    {
        Show(Scope.NearbyAislings, new ServerFormat0E(Serial));
        Show(Scope.NearbyAislings, new ServerFormat07([this]));
    }
    public void UpdateBuffs(TimeSpan elapsedTime)
    {
        foreach (var (_, buff) in Buffs)
        {
            StatusBarDisplayUpdateBuff(buff);

            if (buff.TimeLeft <= 1)
            {
                buff.OnEnded(this);
                return;
            }
            buff.Update(this, elapsedTime);
        }
    }
    public void UpdateDebuffs(TimeSpan elapsedTime)
    {
        foreach (var (_, debuff) in Debuffs)
        {
            StatusBarDisplayUpdateDebuff(debuff);

            if (debuff.TimeLeft <= 1)
            {
                debuff.OnEnded(this);

                return;
            }

            debuff.Update(this, elapsedTime);
        }
    }
    public void StatusBarDisplayUpdateBuff(BuffBase buffBase)
    {
        if (this is not Aisling aisling)
            return;

        var countDown = buffBase.Length - buffBase.Timer.Tick;
        var previousColor = buffBase.CurrentColor;
        buffBase.TimeLeft = countDown;

        if (buffBase.TimeLeft.IsWithin(0, 1))
            buffBase.CurrentColor = StatusBarColor.Off;
        else if (buffBase.TimeLeft.IsWithin(1, 20))
            buffBase.CurrentColor = StatusBarColor.Blue;
        else if (buffBase.TimeLeft.IsWithin(21, 30))
            buffBase.CurrentColor = StatusBarColor.Green;
        else if (buffBase.TimeLeft.IsWithin(31, 40))
            buffBase.CurrentColor = StatusBarColor.Yellow;
        else if (buffBase.TimeLeft.IsWithin(41, 50))
            buffBase.CurrentColor = StatusBarColor.Orange;
        else if (buffBase.TimeLeft.IsWithin(51, 60))
            buffBase.CurrentColor = StatusBarColor.Red;
        else if (buffBase.TimeLeft.IsWithin(61, short.MaxValue))
            buffBase.CurrentColor = StatusBarColor.White;
        if (previousColor != buffBase.CurrentColor)
            aisling.Client.Send(new ServerFormat3A(buffBase.Icon, (byte)buffBase.CurrentColor));
    }
    public void StatusBarDisplayUpdateDebuff(DebuffBase deBuffBase)
    {
        if (this is not Aisling aisling)
            return;

        var countDown = deBuffBase.Length - deBuffBase.Timer.Tick;
        var previousColor = deBuffBase.CurrentColor;
        deBuffBase.TimeLeft = countDown;

        if (deBuffBase.TimeLeft.IsWithin(0, 1))
            deBuffBase.CurrentColor = StatusBarColor.Off;
        else if (deBuffBase.TimeLeft.IsWithin(1, 20))
            deBuffBase.CurrentColor = StatusBarColor.Blue;
        else if (deBuffBase.TimeLeft.IsWithin(21, 30))
            deBuffBase.CurrentColor = StatusBarColor.Green;
        else if (deBuffBase.TimeLeft.IsWithin(31, 40))
            deBuffBase.CurrentColor = StatusBarColor.Yellow;
        else if (deBuffBase.TimeLeft.IsWithin(41, 50))
            deBuffBase.CurrentColor = StatusBarColor.Orange;
        else if (deBuffBase.TimeLeft.IsWithin(51, 60))
            deBuffBase.CurrentColor = StatusBarColor.Red;
        else if (deBuffBase.TimeLeft.IsWithin(61, short.MaxValue))
            deBuffBase.CurrentColor = StatusBarColor.White;

        if (previousColor != deBuffBase.CurrentColor)
            aisling.Client.Send(new ServerFormat3A(deBuffBase.Icon, (byte)deBuffBase.CurrentColor));
    }
    private bool CanUpdate()
    {
        if (IsSleeping || IsFrozen || IsArrested || IsPetrified)
            return false;
        if (this is Monster || this is Mundane)
            if (CurrentHp == 0)
                return false;
        if (ServerSetup.Instance.Config.CanMoveDuringReap || this is not Aisling aisling)
            return true;
        if (!aisling.Skulled)
            return true;
        aisling.Client.SystemMessage(ServerSetup.Instance.Config.ReapMessageDuringAction);
        return false;
    }
    #endregion

    #region Sprite Methods

    public TSprite Cast<TSprite>() where TSprite : Sprite => this as TSprite;

    public void UpdateAddAndRemove()
    {
        foreach (var playerNearby in AislingsNearby())
        {
            uint objectId;

            if (this is Item item)
                objectId = item.ItemVisibilityId;
            else
                objectId = Serial;

            playerNearby.Client.SendRemoveObject(objectId);
            var obj = new List<Sprite> { this };
            playerNearby.Client.SendVisibleEntities(obj);
        }
    }

    public void Remove()
    {
        var nearby = GetObjects<Aisling>(null, i => (i != null) && i.LoggedIn);

        foreach (var o in nearby)
            o?.Client?.SendRemoveObject(Serial);

        DeleteObject();
    }

    public void HideFrom(Aisling nearbyAisling) => nearbyAisling.Client.SendRemoveObject(Serial);

    public void Animate(ushort effect, byte speed = 100)
    {
        var nearby = GetObjects<Aisling>(null, i => (i != null) && i.LoggedIn).FirstOrDefault();
        nearby?.SendTargetedClientMethod(PlayerScope.NearbyAislings, c => c.SendAnimation(effect, null, Serial, speed, effect, Serial));
    }

    public void SendSound(byte sound, PlayerScope scope = PlayerScope.NearbyAislings)
    {
        if (this is not Aisling aisling) return;
        if (sound < 255)
            aisling.Client.SendSound(sound, false);
    }

    public Aisling SendAnimation(ushort effect, Sprite to, Sprite from, ushort speed = 100)
    {
        var nearby = GetObjects<Aisling>(null, i => (i != null) && i.LoggedIn).FirstOrDefault();
        nearby?.SendTargetedClientMethod(PlayerScope.NearbyAislings, c => c.SendAnimation(0, null, to.Serial, speed, effect, from.Serial));
        return Aisling(this);
    }

    public void SendAnimation(ushort effect, Position pos)
    {
        var nearby = GetObjects<Aisling>(null, i => (i != null) && i.LoggedIn).FirstOrDefault();
        nearby?.SendTargetedClientMethod(PlayerScope.NearbyAislings, c => c.SendAnimation(effect, pos));
    }

    public void WarpTo(Position position)
    {
        var nearby = GetObjects<Aisling>(null, i => (i != null) && i.LoggedIn).FirstOrDefault();

        nearby?.Client?.SendRemoveObject(Serial);

        XPos = position.X;
        YPos = position.Y;

        nearby?.Client?.SendVisibleEntities([this]);
    }

    private void DeleteObject()
    {
        if (this is Monster)
            DelObject(this as Monster);
        if (this is Aisling)
            DelObject(this as Aisling);
        if (this is Money)
            DelObject(this as Money);
        if (this is Item)
            DelObject(this as Item);
        if (this is Mundane)
            DelObject(this as Mundane);
    }

    #endregion

    private Position GetFromAllSidesEmpty(Sprite target, int tileCount = 1)
    {
        var empty = Position;
        var blocks = target.Position.SurroundingContent(Map);

        if (blocks.Length <= 0)
            return empty;

        var selections = blocks.Where(i => (i.Content == TileContent.None)
                                           || (i.Content == TileContent.Item)
                                           || (i.Content == TileContent.Money)).ToArray();
        var selection = selections
            .OrderBy(i => i.Position.DistanceFrom(Position))
            .LastOrDefault();

        if (selection != null)
            empty = selection.Position;

        return empty;
    }

    private IEnumerable<Sprite> GetAllInFront(int tileCount = 1)
    {
        var results = new List<Sprite>();

        for (var i = 1; i <= tileCount; i++)
            switch (Direction)
            {
                case 0:
                    results.AddRange(GetSprites(XPos, YPos - i));
                    break;

                case 1:
                    results.AddRange(GetSprites(XPos + i, YPos));
                    break;

                case 2:
                    results.AddRange(GetSprites(XPos, YPos + i));
                    break;

                case 3:
                    results.AddRange(GetSprites(XPos - i, YPos));
                    break;
            }

        return results;
    }

    private IEnumerable<Sprite> MonsterGetInFront(int tileCount = 1)
    {
        var results = new List<Sprite>();

        for (var i = 1; i <= tileCount; i++)
            switch (Direction)
            {
                case 0:
                    results.AddRange(MonsterGetDamageableSprites(XPos, YPos - i));
                    break;

                case 1:
                    results.AddRange(MonsterGetDamageableSprites(XPos + i, YPos));
                    break;

                case 2:
                    results.AddRange(MonsterGetDamageableSprites(XPos, YPos + i));
                    break;

                case 3:
                    results.AddRange(MonsterGetDamageableSprites(XPos - i, YPos));
                    break;
            }

        return results;
    }

    public List<Sprite> GetAllInFront(Sprite sprite, int tileCount = 1) => GetAllInFront(tileCount).Where(i => (i != null) && (i.Serial != sprite.Serial)).ToList();

    public List<Sprite> GetAllInFront(int tileCount = 1, bool intersect = false) => GetAllInFront(tileCount).ToList();

    public Position GetFromAllSidesEmpty(Sprite sprite, Sprite target, int tileCount = 1) => GetFromAllSidesEmpty(target, tileCount);

    public Position GetFromAllSidesEmpty(Sprite target, int tileCount = 1, bool intersect = false) => GetFromAllSidesEmpty(target, tileCount);

    public List<Sprite> MonsterGetInFront(int tileCount = 1, bool intersect = false) => MonsterGetInFront(tileCount).ToList();

    private IEnumerable<Sprite> AislingGetOnlyMonsterDamageableSprites(int x, int y) => GetObjects(Map, i => (i.XPos == x) && (i.YPos == y), Get.Monsters);

    private IEnumerable<Sprite> MonsterGetDamageableSprites(int x, int y) => GetObjects(Map, i => (i.XPos == x) && (i.YPos == y), Get.MonsterDamage);

    public Position GetPendingChargePosition(int warp, Sprite sprite)
    {
        var pendingX = X;
        var pendingY = Y;

        for (var i = 0; i < warp; i++)
        {
            if (Direction == 0)
                pendingY--;
            if (!sprite.Map.IsWall(pendingX, pendingY))
                continue;
            pendingY++;
            break;
        }
        for (var i = 0; i < warp; i++)
        {
            if (Direction == 1)
                pendingX++;
            if (!sprite.Map.IsWall(pendingX, pendingY))
                continue;
            pendingX--;
            break;
        }
        for (var i = 0; i < warp; i++)
        {
            if (Direction == 2)
                pendingY++;
            if (!sprite.Map.IsWall(pendingX, pendingY))
                continue;
            pendingY--;
            break;
        }
        for (var i = 0; i < warp; i++)
        {
            if (Direction == 3)
                pendingX--;
            if (!sprite.Map.IsWall(pendingX, pendingY))
                continue;
            pendingX++;
            break;
        }

        return new Position(pendingX, pendingY);
    }

    public int DistanceTo(Position spritePos, Position inputPos)
    {
        var spriteX = spritePos.X;
        var spriteY = spritePos.Y;
        var inputX = inputPos.X;
        var inputY = inputPos.Y;
        var diffX = Math.Abs(spriteX - inputX);
        var diffY = Math.Abs(spriteY - inputY);

        return diffX + diffY;
    }
}