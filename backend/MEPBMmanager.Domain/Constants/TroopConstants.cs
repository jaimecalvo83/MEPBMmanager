using MEPBMmanager.Domain.Enums;

namespace MEPBMmanager.Domain.Constants;

public static class TroopConstants
{
    public static readonly Dictionary<TroopType, string> TroopNames = new()
    {
        [TroopType.Hc] = "Heavy Cavalry",
        [TroopType.Lc] = "Light Cavalry",
        [TroopType.Hi] = "Heavy Infantry",
        [TroopType.Li] = "Light Infantry",
        [TroopType.Ar] = "Archers",
        [TroopType.Ma] = "Men-at-Arms"
    };

    public static readonly Dictionary<TroopType, int> TroopCostPer100 = new()
    {
        [TroopType.Hc] = 3000,
        [TroopType.Lc] = 1500,
        [TroopType.Hi] = 2000,
        [TroopType.Li] = 1000,
        [TroopType.Ar] = 1000,
        [TroopType.Ma] = 500
    };

    public static readonly Dictionary<TroopType, int> FoodPerTroop = new()
    {
        [TroopType.Hc] = 2,
        [TroopType.Lc] = 3,
        [TroopType.Hi] = 1,
        [TroopType.Li] = 1,
        [TroopType.Ar] = 1,
        [TroopType.Ma] = 1
    };

    public static readonly Dictionary<TroopType, int> MaintenancePer100 = new()
    {
        [TroopType.Hc] = 300,
        [TroopType.Lc] = 150,
        [TroopType.Hi] = 100,
        [TroopType.Li] = 50,
        [TroopType.Ar] = 75,
        [TroopType.Ma] = 25
    };

    public static readonly Dictionary<MaterialRank, int> MaterialRankValues = new()
    {
        [MaterialRank.None] = 0,
        [MaterialRank.Wood] = 10,
        [MaterialRank.Leather] = 20,
        [MaterialRank.Bronze] = 40,
        [MaterialRank.Steel] = 60,
        [MaterialRank.Mithril] = 100
    };

    public static readonly Dictionary<MaterialRank, string> MaterialNames = new()
    {
        [MaterialRank.None] = "None",
        [MaterialRank.Wood] = "Wood",
        [MaterialRank.Leather] = "Leather",
        [MaterialRank.Bronze] = "Bronze",
        [MaterialRank.Steel] = "Steel",
        [MaterialRank.Mithril] = "Mithril"
    };

    public const int ArmySizeMin = 100;
    public const int ArmySizeMax = 10000;

    public static readonly Dictionary<string, Dictionary<string, int>> MovementCosts = new()
    {
        ["fed"] = new()
        {
            ["plains"] = 1,
            ["forest"] = 2,
            ["mountains"] = 3,
            ["rough"] = 2,
            ["desert"] = 1,
            ["swamp"] = 3,
            ["shore"] = 1
        },
        ["unfed"] = new()
        {
            ["plains"] = 2,
            ["forest"] = 4,
            ["mountains"] = 6,
            ["rough"] = 4,
            ["desert"] = 2,
            ["swamp"] = 6,
            ["shore"] = 2
        }
    };
}
