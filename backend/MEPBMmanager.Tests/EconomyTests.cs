using MEPBMmanager.Api.Services;
using MEPBMmanager.Domain.Entities;

namespace MEPBMmanager.Tests;

public class EconomyTests
{
    private static Nation MakeNation()
    {
        return new Nation
        {
            Id = "n1",
            GameId = "g1",
            Name = "Test Nation",
            Allegiance = "neutral",
            Armies = new List<Army>(),
            Characters = new List<Character>(),
            PopulationCentres = new List<PopulationCentre>(),
            Navies = new List<Navy>(),
            Relations = new List<NationRelation>()
        };
    }

    private static Army MakeArmy(int hc = 0, int lc = 0, int hi = 0, int li = 0, int ar = 0, int ma = 0)
    {
        return new Army
        {
            Id = "a1",
            NationId = "n1",
            Name = "Test Army",
            LocationHex = "10,10",
            HeavyCavalry = hc,
            LightCavalry = lc,
            HeavyInfantry = hi,
            LightInfantry = li,
            Archers = ar,
            MenAtArms = ma
        };
    }

    [Theory]
    [InlineData(100, 0, 0, 0, 0, 0, 200)]   // HC eats 2
    [InlineData(0, 100, 0, 0, 0, 0, 200)]   // LC eats 2
    [InlineData(0, 0, 100, 0, 0, 0, 100)]   // HI eats 1
    [InlineData(0, 0, 0, 100, 0, 0, 100)]
    [InlineData(0, 0, 0, 0, 100, 0, 100)]
    [InlineData(0, 0, 0, 0, 0, 100, 100)]
    [InlineData(100, 200, 300, 400, 500, 600, 200 + 400 + 300 + 400 + 500 + 600)]
    public void FoodCost_PerType(int hc, int lc, int hi, int li, int ar, int ma, int expected)
    {
        Assert.Equal(expected, TurnProcessor.GetArmyFoodCost(MakeArmy(hc, lc, hi, li, ar, ma)));
    }

    [Fact]
    public void Upkeep_GoldPerTroop()
    {
        var n = MakeNation();
        n.Armies.Add(MakeArmy(hc: 100, lc: 100, hi: 100, li: 100, ar: 100, ma: 100));
        // Wiki: HC6 LC3 HI4 LI2 AR2 MA1
        Assert.Equal(600 + 300 + 400 + 200 + 200 + 100, TurnProcessor.NationUpkeep(n));
    }

    [Theory]
    [InlineData("camp", 100)]
    [InlineData("village", 200)]
    [InlineData("town", 300)]
    [InlineData("major town", 400)]
    [InlineData("city", 500)]
    public void RecruitCapacity_BySize(string size, int expected)
    {
        Assert.Equal(expected, TurnProcessor.RecruitCapacity(size));
    }

    [Theory]
    [InlineData("city", 40, 4000)]
    [InlineData("town", 30, 1500)]
    [InlineData("village", 20, 500)]
    [InlineData("camp", 50, 0)]
    public void PcTax_SizeTimesRate(string size, int rate, int expected)
    {
        Assert.Equal(expected, TurnProcessor.GetPCTax(size, rate));
    }

    [Theory]
    [InlineData("tower", 1)]
    [InlineData("fort", 2)]
    [InlineData("castle", 3)]
    [InlineData("keep", 4)]
    [InlineData("citadel", 5)]
    [InlineData("none", 0)]
    public void FortLevel_ByName(string fort, int expected)
    {
        Assert.Equal(expected, TurnProcessor.FortLevel(fort));
    }

    [Theory]
    [InlineData(400, 200)]
    [InlineData(404, 120)]
    [InlineData(408, 150)]
    [InlineData(412, 80)]
    [InlineData(416, 100)]
    [InlineData(420, 60)]
    public void RecruitCostPerUnit(int code, int expected)
    {
        Assert.Equal(expected, TurnProcessor.RecruitCostPerUnit[code]);
    }
}
