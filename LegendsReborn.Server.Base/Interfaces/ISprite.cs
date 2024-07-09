using Darkages.Enums;
using Darkages.Sprites;
using Darkages.Types;

using System.Collections.Concurrent;

namespace Darkages.Interfaces;

public interface ISprite
{
    uint Serial { get; set; }
    int CurrentMapId { get; set; }
    double Amplified { get; set; }
    ElementManager.Element OffenseElement { get; set; }
    ElementManager.Element DefenseElement { get; set; }
    DateTime AbandonedDate { get; set; }
    Sprite Target { get; set; }
    int X { get; set; }
    int Y { get; set; }
    TileContent TileType { get; set; }
    byte Direction { get; set; }
    int PendingX { get; set; }
    int PendingY { get; set; }
    DateTime LastMenuInvoked { get; set; }
    DateTime LastMovementChanged { get; set; }
    DateTime LastTargetAcquired { get; set; }
    DateTime LastTurnUpdated { get; set; }
    DateTime LastUpdated { get; set; }
    ConcurrentDictionary<string, Buff> Buffs { get; }
    ConcurrentDictionary<string, Debuff> Debuffs { get; }

    #region Stats

    int CurrentHp { get; set; }
    int BaseHp { get; set; }
    int BonusHp { get; set; }

    int CurrentMp { get; set; }
    int BaseMp { get; set; }
    int BonusMp { get; set; }

    int _Regen { get; set; }
    int BonusRegen { get; set; }

    int _Dmg { get; set; }
    int BonusDmg { get; set; }

    int BonusAc { get; set; }
    int _ac { get; set; }
    
    int _Hit { get; set; }
    int BonusHit { get; set; }

    int _Mr { get; set; }
    int BonusMr { get; set; }

    int _Str { get; set; }
    int BonusStr { get; set; }

    int _Int { get; set; }
    int BonusInt { get; set; }

    int _Wis { get; set; }
    int BonusWis { get; set; }

    int _Con { get; set; }
    int BonusCon { get; set; }

    int _Dex { get; set; }
    int BonusDex { get; set; }

    #endregion

}