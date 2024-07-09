namespace Darkages.Enums;

[Flags]
public enum ItemFlags
{
    Equipable = 1,
    Perishable = 1 << 1,
    Tradeable = 1 << 2,
    Dropable = 1 << 3,
    Bankable = 1 << 4,
    Sellable = 1 << 5,
    Repairable = 1 << 6,
    Stackable = 1 << 7,
    Consumable = 1 << 8,
    PerishIFEquipped = 1 << 9,
    Elemental = 1 << 10,
    QuestRelated = 1 << 11,
    Upgradeable = 1 << 12,
    TwoHanded = 1 << 13,
    LongRanged = 1 << 14,
    Trap = 1 << 15,
    RegenProc = 1 << 16,
    RefreshProc = 1 << 17,

    NormalEquipment = Equipable | Repairable | Tradeable | Sellable | Bankable | Dropable,
    NormalEquipPerish = NormalEquipment | Perishable,
    NormEquNoSell = Equipable | Repairable | Tradeable | Bankable | Dropable,
    NormEquNoTrade = Equipable | Repairable | Bankable | Sellable, 
    NormEquPerNoSell = NormEquNoSell |Perishable,
    NorEquEleNoSell = NormEquNoSell | Elemental,
    NorEquElePerNoSell = NorEquEleNoSell | Perishable,
    Tutorial = Equipable | Perishable,
    NormalEquipElement = NormalEquipment | Elemental,
    NormalEquipElementPerish = NormalEquipment | Perishable | Elemental,
    TwoHandedSwordEquip = NormalEquipment | TwoHanded,
    TwoHandedSwordPerish = TwoHandedSwordEquip | Perishable,
    Staff = TwoHanded | NormEquPerNoSell,


    //NonPerishable, Non-Equipable Items
    NonPerish = Dropable | Tradeable | Bankable | Sellable | Stackable | QuestRelated,
    NonPerishNoStack = Dropable | Tradeable | Bankable | Sellable | QuestRelated,
    //Flag for Scrolls
    Scrolls = Dropable | Consumable | Tradeable | Bankable | Sellable | Stackable | QuestRelated,
    ScrollsNoStack = Dropable | Consumable | Tradeable | Bankable | Sellable | QuestRelated,
    //Normal Consumable Items That Stack
    NormalConsumable = Dropable | Consumable | Tradeable | Bankable | Sellable | Stackable | QuestRelated,
    //Normal Consumable Items That Do Not Stack
    NormalConsumableNoStack = Dropable | Consumable | Tradeable | Bankable | Sellable | QuestRelated,
    //Special NonDroppable
    Special = QuestRelated | Bankable | Stackable,

    //Special NonDroppable NonStackable
    SpecialNoStack = QuestRelated | Bankable,
    //Special Nondroppable Consumable
    SpecialConsume = Special | Consumable,
    //Special Equipable NonDroppable
    SpecialEquip = Equipable | Repairable | Bankable,
    //Special Equipable NonDroppable Elemental
    SpecEle = SpecialEquip | Elemental,
    //Normal Ranged
    Ranged = NormalEquipment | LongRanged,
    //Perishable Ranged
    RangedPerish = Ranged | Perishable,
    // Master Gear
    MasterArmor = Equipable | Repairable | Perishable | Bankable,
    EmpMaster = Equipable | Repairable | Bankable,
    LightEmpMaster = Equipable | Repairable | Bankable | RegenProc,
    DarkEmpMaster = Equipable | Repairable | Bankable | RefreshProc,

    //Master Weapons
    MasterStaff = TwoHanded | Equipable | Repairable |Perishable | Bankable,
    MasterSword = TwoHanded | Equipable | Repairable | Perishable | Bankable,
    MasterWeapon = Equipable | Repairable | Perishable | Bankable,
    Emp2H = EmpMaster | TwoHanded,
    Emp1H = EmpMaster,
    //Rods
    Rod = Staff,
    //Elixir
    Elixir = LongRanged | Equipable,
}