using MEPBMmanager.Api.Services;
using MEPBMmanager.Domain.Entities;
using MEPBMmanager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MEPBMmanager.Tests;

public class TurnIntegrationTests
{
    private sealed record Seed(
        MepbmDbContext Db, Game Game, Nation N1, Nation N2,
        PopulationCentre Capital, Character Cmd, Character Emi, Character Mage,
        Character Boss, Army Army, Army Foe, Turn Turn);

    private static MepbmDbContext NewDb()
    {
        var opts = new DbContextOptionsBuilder<MepbmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new MepbmDbContext(opts);
    }

    private static Character NewChar(string id, string nationId, string name, string hex,
        int cmd = 10, int agt = 10, int ems = 10, int mag = 10, string? armyId = null)
    {
        return new Character
        {
            Id = id, NationId = nationId, Name = name, Type = "commander",
            LocationHex = hex, CommandSkill = cmd, AgentSkill = agt,
            EmissarySkill = ems, MageSkill = mag, Health = 100, MaxHealth = 100,
            ArmyId = armyId, Spells = new List<Spell>(), Artifacts = new List<Artifact>()
        };
    }

    private static Seed NewWorld(bool withFoeArmy = false, bool withFoeChar = false)
    {
        var db = NewDb();
        var game = new Game { Id = "g1", Name = "Test Game", Status = "active", CurrentTurn = 1 };
        var n1 = new Nation
        {
            Id = "n1", GameId = "g1", Name = "Test Nation", Allegiance = "neutral",
            Gold = 50000, Food = 5000, Timber = 5000, Leather = 2000, Bronze = 2000,
            Steel = 2000, Mithril = 100, Mounts = 2000, TaxRate = 30,
            Armies = new List<Army>(), Characters = new List<Character>(),
            PopulationCentres = new List<PopulationCentre>(), Navies = new List<Navy>(),
            Relations = new List<NationRelation>()
        };
        var n2 = new Nation
        {
            Id = "n2", GameId = "g1", Name = "Foe Nation", Allegiance = "dark_servants",
            Gold = 10000, Food = 1000, Armies = new List<Army>(), Characters = new List<Character>(),
            PopulationCentres = new List<PopulationCentre>(), Navies = new List<Navy>(),
            Relations = new List<NationRelation>()
        };
        var cap = new PopulationCentre
        {
            Id = "pc1", NationId = "n1", Name = "Capitol", Size = "town",
            LocationHex = "10,10", IsCapital = true, Loyalty = 80, Production = 200, Stores = 500
        };
        var army = new Army
        {
            Id = "a1", NationId = "n1", Name = "First Army", LocationHex = "10,10",
            HeavyInfantry = 100, MenAtArms = 200, HITraining = 50, MAATraining = 20,
            Training = 30, Morale = 50, Food = 2000
        };
        var cmd = NewChar("c-cmd", "n1", "Commander", "10,10", cmd: 30, armyId: "a1");
        var emi = NewChar("c-emi", "n1", "Emissary", "10,10", ems: 90);
        var mage = NewChar("c-mage", "n1", "Mage", "10,10", mag: 20);
        var boss = NewChar("c-boss", "n1", "NewBoss", "10,10", cmd: 20);
        n1.Characters.Add(cmd);
        n1.Characters.Add(emi);
        n1.Characters.Add(mage);
        n1.Characters.Add(boss);
        n1.Armies.Add(army);
        n1.PopulationCentres.Add(cap);
        Army? foe = null;
        if (withFoeArmy)
        {
            foe = new Army
            {
                Id = "a2", NationId = "n2", Name = "Foe Army", LocationHex = "10,10",
                MenAtArms = 100, Training = 20, Morale = 40, Food = 500
            };
            n2.Armies.Add(foe);
        }
        if (withFoeChar)
        {
            n2.Characters.Add(NewChar("c-foe", "n2", "Foe Agent", "10,10", agt: 20, ems: 20));
        }
        game.Nations.Add(n1);
        game.Nations.Add(n2);
        game.HexTiles.Add(new HexTile { Id = "h1", GameId = "g1", Q = 10, R = 10, Terrain = "plains" });
        game.HexTiles.Add(new HexTile { Id = "h2", GameId = "g1", Q = 11, R = 10, Terrain = "plains" });
        game.HexTiles.Add(new HexTile { Id = "h3", GameId = "g1", Q = 10, R = 11, Terrain = "plains" });
        var turn = new Turn
        {
            Id = "t1", GameId = "g1", Number = 1, Status = "orders_open",
            Season = "summer", Deadline = DateTime.UtcNow.AddDays(7), Game = game
        };
        game.Turns.Add(turn);
        db.Games.Add(game);
        db.SaveChanges();
        return new Seed(db, game, n1, n2, cap, cmd, emi, mage, boss, army, foe!, turn);
    }

    private static Order NewOrder(Seed s, Character ch, int code, string pars, Army? army = null)
    {
        var nation = ch.NationId == s.N1.Id ? s.N1 : s.N2;
        return new Order
        {
            Id = Guid.NewGuid().ToString(),
            GameId = s.Game.Id,
            TurnId = s.Turn.Id,
            NationId = nation.Id,
            Nation = nation,
            CharacterId = ch.Id,
            Character = ch,
            ArmyId = army?.Id,
            Army = army,
            Code = code,
            Parameters = pars,
            Status = "pending"
        };
    }

    private static async Task<Order> RunSingle(Seed s, Order order)
    {
        s.Db.Orders.Add(order);
        await s.Db.SaveChangesAsync();
        var tp = new TurnProcessor(s.Db, new CombatResolver());
        await tp.ProcessTurnAsync(s.Game.Id);
        return s.Db.Orders.First(o => o.Id == order.Id);
    }

    [Fact]
    public async Task Turn_RecruitAddsTroopsChargesGoldAndCaps()
    {
        var s = NewWorld();
        // Town caps at 300; request 500.
        var o = await RunSingle(s, NewOrder(s, s.Cmd, 408, """{"amount":500,"weapons":"bronze","armour":"leather"}""", s.Army));

        Assert.Equal("resolved", o.Status);
        Assert.Equal(400, s.Army.HeavyInfantry); // 100 + capped 300
        Assert.Contains("300", o.Result ?? "");
    }

    [Fact]
    public async Task Turn_RecruitAveragesTypeTraining()
    {
        var s = NewWorld();
        // 100 HI @ 50 + 100 recruits @ max(10, cmd 30)=30 -> (5000+3000)/200 = 40
        var o = await RunSingle(s, NewOrder(s, s.Cmd, 408, """{"amount":100,"weapons":"bronze","armour":"leather"}""", s.Army));

        Assert.Equal("resolved", o.Status);
        Assert.Equal(200, s.Army.HeavyInfantry);
        Assert.Equal(40, s.Army.HITraining);
    }

    [Fact]
    public async Task Turn_MoveArmyRelocates()
    {
        var s = NewWorld();
        var o = await RunSingle(s, NewOrder(s, s.Cmd, 850, """{"destination":"11,10"}""", s.Army));

        Assert.Equal("resolved", o.Status);
        Assert.Equal("11,10", s.Army.LocationHex);
    }

    [Fact]
    public async Task Turn_AttackCausesCasualties()
    {
        var s = NewWorld(withFoeArmy: true);
        var o = await RunSingle(s, NewOrder(s, s.Cmd, 230, """{}""", s.Army));

        Assert.Equal("resolved", o.Status);
        Assert.True(s.Foe.HeavyInfantry + s.Foe.LightInfantry + s.Foe.MenAtArms
            + s.Foe.HeavyCavalry + s.Foe.LightCavalry + s.Foe.Archers < 100);
    }

    [Fact]
    public async Task Turn_NameCommanderCreatesCharacter()
    {
        var s = NewWorld();
        var before = s.N1.Characters.Count;
        var o = await RunSingle(s, NewOrder(s, s.Emi, 728, """{"name":"Validname"}"""));

        Assert.Equal("resolved", o.Status);
        Assert.Equal(before + 1, s.N1.Characters.Count);
        var created = s.N1.Characters.First(c => c.Name == "Validname");
        Assert.Equal(s.Capital.LocationHex, created.LocationHex);
        Assert.Contains("5000", o.Result ?? "");
    }

    [Fact]
    public async Task Turn_ImprovePcUpgradesSize()
    {
        var s = NewWorld();
        var o = await RunSingle(s, NewOrder(s, s.Emi, 550, """{}"""));

        Assert.Equal("resolved", o.Status);
        Assert.Equal("major town", s.Capital.Size);
    }

    [Fact]
    public async Task Turn_ChangeTaxRate()
    {
        var s = NewWorld();
        var o = await RunSingle(s, NewOrder(s, s.Emi, 300, """{"newRate":40}"""));

        Assert.Equal("resolved", o.Status);
        Assert.Equal(40, s.N1.TaxRate);
    }

    [Fact]
    public async Task Turn_HireArmyCreatesForce()
    {
        var s = NewWorld();
        var o = await RunSingle(s, NewOrder(s, s.Emi, 770,
            """{"name":"Second","troops":100,"troopType":"MenAtArms","weapons":"bronze","armour":"leather","food":50}"""));

        Assert.Equal("resolved", o.Status);
        Assert.Equal(2, s.N1.Armies.Count);
        var hired = s.N1.Armies.First(a => a.Id != s.Army.Id);
        Assert.Equal(100, hired.MenAtArms);
        Assert.Equal("10,10", hired.LocationHex);
    }

    [Fact]
    public async Task Turn_SplitArmyHalvesTroops()
    {
        var s = NewWorld();
        var o = await RunSingle(s, NewOrder(s, s.Cmd, 765,
            $$"""{"commanderId":"{{s.Boss.Id}}"}""", s.Army));

        Assert.Equal("resolved", o.Status);
        Assert.Equal(2, s.N1.Armies.Count);
        var split = s.N1.Armies.First(a => a.Id != s.Army.Id);
        Assert.Equal(50, split.HeavyInfantry);
        Assert.Equal(50, s.Army.HeavyInfantry);
        Assert.Equal(split.Id, s.Boss.ArmyId);
    }

    [Fact]
    public async Task Turn_DisbandArmyRemovesIt()
    {
        var s = NewWorld();
        var o = await RunSingle(s, NewOrder(s, s.Cmd, 775, """{}""", s.Army));

        Assert.Equal("resolved", o.Status);
        Assert.DoesNotContain(s.N1.Armies, a => a.Id == s.Army.Id);
    }

    [Fact]
    public async Task Turn_TransferFoodMovesReserveToArmy()
    {
        var s = NewWorld();
        var o = await RunSingle(s, NewOrder(s, s.Cmd, 340, """{"amount":500}""", s.Army));

        Assert.Equal("resolved", o.Status);
        // Economy phase eats 300 first (100 HI + 200 MA), then +500 transferred.
        Assert.Equal(2200, s.Army.Food);
        Assert.Equal(4500, s.N1.Food);
    }

    [Fact]
    public async Task Turn_FoodConsumptionDrainsTrainThenNation()
    {
        var s = NewWorld();
        s.Army.HeavyInfantry = 0;
        s.Army.MenAtArms = 1000; // eats 1000
        s.Army.Food = 400;
        s.N1.Food = 5000;
        await s.Db.SaveChangesAsync();
        var tp = new TurnProcessor(s.Db, new CombatResolver());

        await tp.ProcessTurnAsync(s.Game.Id);

        Assert.Equal(0, s.Army.Food);
        Assert.Equal(4400, s.N1.Food);
    }

    [Fact]
    public async Task Turn_DoubleAgentPlantsEvent()
    {
        var s = NewWorld(withFoeChar: true);
        var o = await RunSingle(s, NewOrder(s, s.Emi, 500,
            $$"""{"targetId":"{{s.N2.Characters.First().Id}}"}"""));

        Assert.Equal("resolved", o.Status);
        Assert.True(s.Db.GameEvents.Any(e => e.GameId == s.Game.Id && e.Type == "double_agent"));
    }
}
