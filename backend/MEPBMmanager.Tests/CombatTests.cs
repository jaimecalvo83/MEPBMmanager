using MEPBMmanager.Api.Services;
using MEPBMmanager.Domain.Entities;

namespace MEPBMmanager.Tests;

public class CombatTests
{
    private static Army MakeArmy(string id, string nationId, int hc = 0, int lc = 0, int hi = 0,
        int li = 0, int ar = 0, int ma = 0, int training = 30, int morale = 50)
    {
        return new Army
        {
            Id = id,
            NationId = nationId,
            Name = "Test " + id,
            LocationHex = "10,10",
            HeavyCavalry = hc,
            LightCavalry = lc,
            HeavyInfantry = hi,
            LightInfantry = li,
            Archers = ar,
            MenAtArms = ma,
            Training = training,
            HCTraining = training,
            LCTraining = training,
            HITraining = training,
            LITraining = training,
            ArcherTraining = training,
            MAATraining = training,
            Morale = morale
        };
    }

    private static Game MakeGame(params Nation[] nations)
    {
        return new Game { Id = "g1", Nations = nations.ToList() };
    }

    private static Nation MakeNation(string id, string name)
    {
        return new Nation
        {
            Id = id,
            GameId = "g1",
            Name = name,
            Allegiance = "neutral",
            Armies = new List<Army>(),
            Characters = new List<Character>(),
            PopulationCentres = new List<PopulationCentre>(),
            Navies = new List<Navy>(),
            Relations = new List<NationRelation>()
        };
    }

    [Fact]
    public void BigArmy_BeatsSmallArmy()
    {
        var r = new CombatResolver();
        var na = MakeNation("na", "A");
        var nb = MakeNation("nb", "B");
        var atk = MakeArmy("a1", "na", hi: 5000);
        var def = MakeArmy("a2", "nb", ma: 100);
        na.Armies.Add(atk);
        nb.Armies.Add(def);
        var game = MakeGame(na, nb);

        var res = r.ResolveArmyBattle(atk, def, game);

        Assert.NotNull(res);
        var defLeft = def.HeavyCavalry + def.LightCavalry + def.HeavyInfantry
            + def.LightInfantry + def.Archers + def.MenAtArms;
        var atkLeft = atk.HeavyCavalry + atk.LightCavalry + atk.HeavyInfantry
            + atk.LightInfantry + atk.Archers + atk.MenAtArms;
        Assert.Equal(0, defLeft);
        Assert.True(atkLeft > 0);
        Assert.True(res.DefenderCasualties > 0);
    }

    [Fact]
    public void Battle_ReducesTroopsOnBothSides()
    {
        var r = new CombatResolver();
        var na = MakeNation("na", "A");
        var nb = MakeNation("nb", "B");
        var atk = MakeArmy("a1", "na", hi: 1000);
        var def = MakeArmy("a2", "nb", hi: 1000);
        na.Armies.Add(atk);
        nb.Armies.Add(def);
        var game = MakeGame(na, nb);
        var before = atk.HeavyInfantry + def.HeavyInfantry;

        var res = r.ResolveArmyBattle(atk, def, game);

        Assert.NotNull(res.Message);
        Assert.True(atk.HeavyInfantry + def.HeavyInfantry < before);
    }

    [Fact]
    public void Battle_ReportsPowersAndTerrain()
    {
        var r = new CombatResolver();
        var na = MakeNation("na", "A");
        var nb = MakeNation("nb", "B");
        var atk = MakeArmy("a1", "na", hc: 500);
        var def = MakeArmy("a2", "nb", li: 500);
        na.Armies.Add(atk);
        nb.Armies.Add(def);
        var game = MakeGame(na, nb);

        var res = r.ResolveArmyBattle(atk, def, game, terrain: "plains");

        Assert.Equal("plains", res.Terrain);
        Assert.True(res.AttackerPower + res.DefenderPower > 0);
    }

    [Theory]
    [InlineData(202, 150, 150)]
    [InlineData(214, 400, 400)]
    [InlineData(242, 1250, 2250)]
    public void CombatSpell_EffectInRange(int spell, int min, int max)
    {
        var r = new CombatResolver();
        for (var i = 0; i < 20; i++)
        {
            var (off, def) = r.GetSpellEffect(spell, navy: false);
            Assert.InRange(off, min, max);
            Assert.Equal(0, def);
        }
    }

    [Theory]
    [InlineData(102, 500, 500)]
    [InlineData(104, 750, 750)]
    [InlineData(112, 1750, 1750)]
    public void DefensiveSpell_EffectInRange(int spell, int min, int max)
    {
        var r = new CombatResolver();
        for (var i = 0; i < 20; i++)
        {
            var (off, def) = r.GetSpellEffect(spell, navy: false);
            Assert.InRange(def, min, max);
            Assert.Equal(0, off);
        }
    }

    [Fact]
    public void CombatSpell_NavyDividesBy100()
    {
        var r = new CombatResolver();
        var (off, _) = r.GetSpellEffect(202, navy: true);
        Assert.Equal(1.5, off);
    }

    [Fact]
    public void UnknownSpell_NoEffect()
    {
        var r = new CombatResolver();
        Assert.Equal((0, 0), r.GetSpellEffect(9999, navy: false));
    }

    [Fact]
    public void NavyBattle_BiggerFleetWins()
    {
        var r = new CombatResolver();
        var na = MakeNation("na", "A");
        var nb = MakeNation("nb", "B");
        var atk = new Navy { Id = "v1", NationId = "na", LocationHex = "1,1", Warships = 20 };
        var def = new Navy { Id = "v2", NationId = "nb", LocationHex = "1,1", Warships = 1 };
        na.Navies.Add(atk);
        nb.Navies.Add(def);
        var game = MakeGame(na, nb);

        var res = r.ResolveNavyBattle(atk, def, game);

        Assert.NotNull(res);
        Assert.Equal(0, def.Warships + def.Transports);
        Assert.True(atk.Warships + atk.Transports > 0);
    }
}
