using MEPBMmanager.Api.Orders;
using MEPBMmanager.Domain.Entities;
using MEPBMmanager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MEPBMmanager.Tests;

/// <summary>
/// Reglas de órdenes a través del servicio (sin HTTP): elegibilidad,
/// formularios, textos y claves de coste. El controlador solo delega.
/// </summary>
public class OrderEstimateServiceTests
{
    private static MepbmDbContext NewDb()
    {
        var opts = new DbContextOptionsBuilder<MepbmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new MepbmDbContext(opts);
    }

    private static (Game Game, Nation Nation, Character Ch) NewWorld()
    {
        var game = new Game { Id = "g1", Name = "Test", Status = "active" };
        var nation = new Nation
        {
            Id = "n1", GameId = "g1", Name = "Test Nation", Allegiance = "neutral",
            Gold = 10000, Food = 1000, Timber = 1000, Leather = 500, Bronze = 500,
            Steel = 500, Mounts = 500, TaxRate = 30,
            Armies = new List<Army>(), Characters = new List<Character>(),
            PopulationCentres = new List<PopulationCentre>(), Navies = new List<Navy>(),
            Relations = new List<NationRelation>()
        };
        nation.PopulationCentres.Add(new PopulationCentre
        {
            Id = "pc1", NationId = "n1", Name = "Capitol", Size = "town",
            LocationHex = "10,10", IsCapital = true, Loyalty = 80
        });
        var ch = new Character
        {
            Id = "c1", NationId = "n1", Name = "Tester", Type = "commander",
            LocationHex = "10,10", CommandSkill = 20,
            Spells = new List<Spell>(), Artifacts = new List<Artifact>()
        };
        nation.Characters.Add(ch);
        game.Nations.Add(nation);
        return (game, nation, ch);
    }

    private static EstimateCtx Ctx(Game game, Nation nation, Character ch, string lang = "en")
    {
        return new EstimateCtx(game, nation, ch, null, null, ch.LocationHex,
            new Dictionary<string, System.Text.Json.JsonElement>(), lang);
    }

    [Fact]
    public void Eligible_ArmyOrder_WithoutArmy()
    {
        using var db = NewDb();
        var svc = new OrderEstimateService(db);
        var (game, nation, ch) = NewWorld();
        var def = Domain.Constants.OrderDefinitions.Orders.First(o => o.Code == 775);

        var (ok, reason) = svc.CheckEligible(ch, def, nation, commandsNavy: false, lang: "en");
        var (okEs, reasonEs) = svc.CheckEligible(ch, def, nation, commandsNavy: false, lang: "es");

        Assert.False(ok);
        Assert.Equal("Not in an army", reason);
        Assert.False(okEs);
        Assert.Equal("Fuera del ejército", reasonEs);
    }

    [Fact]
    public void Eligible_NavyOrder_WithoutNavy()
    {
        using var db = NewDb();
        var svc = new OrderEstimateService(db);
        var (game, nation, ch) = NewWorld();
        var def = Domain.Constants.OrderDefinitions.Orders.First(o => o.Code == 275);

        var (ok, reason) = svc.CheckEligible(ch, def, nation, commandsNavy: false);
        Assert.False(ok);
        Assert.Equal("No navy available", reason);
    }

    [Fact]
    public void Eligible_DeadCharacter_CannotAct()
    {
        using var db = NewDb();
        var svc = new OrderEstimateService(db);
        var (game, nation, ch) = NewWorld();
        ch.IsDead = true;
        var def = Domain.Constants.OrderDefinitions.Orders.First(o => o.Code == 175);

        var (ok, reason) = svc.CheckEligible(ch, def, nation, commandsNavy: false);
        Assert.False(ok);
        Assert.Equal("Character cannot act", reason);
    }

    [Fact]
    public void RequiresFor_370_AsksMaterialAndTroopTypes()
    {
        using var db = NewDb();
        var svc = new OrderEstimateService(db);
        var (game, nation, ch) = NewWorld();

        var fields = svc.RequiresFor(370, Ctx(game, nation, ch));
        var keys = fields.Select(f => f.Key).ToList();

        Assert.Contains("material", keys);
        Assert.Contains("hc", keys);
        Assert.Contains("ma", keys);
        Assert.All(fields.Where(f => f.Key == "material"), f => Assert.True(f.Required));
    }

    [Fact]
    public void RequiresFor_940LoreNationSpell_AsksNation()
    {
        using var db = NewDb();
        var svc = new OrderEstimateService(db);
        var (game, nation, ch) = NewWorld();
        ch.Spells.Add(new Spell { Id = "s1", CharacterId = "c1", SpellId = 404, IsKnown = true });
        var pars = new Dictionary<string, System.Text.Json.JsonElement>
        {
            ["spellId"] = System.Text.Json.JsonDocument.Parse("404").RootElement.Clone()
        };
        var ctx = new EstimateCtx(game, nation, ch, null, null, ch.LocationHex, pars, "en");

        var keys = svc.RequiresFor(940, ctx).Select(f => f.Key).ToList();

        Assert.Contains("spellId", keys);
        Assert.Contains("nationId", keys);
    }

    [Fact]
    public void RequiresFor_UnknownCode_Empty()
    {
        using var db = NewDb();
        var svc = new OrderEstimateService(db);
        var (game, nation, ch) = NewWorld();

        Assert.Empty(svc.RequiresFor(215, Ctx(game, nation, ch)));
    }

    [Theory]
    [InlineData("es", "reason.no-navy-available", "Sin armada disponible")]
    [InlineData("en", "reason.no-navy-available", "No navy available")]
    [InlineData("fr", "reason.no-navy-available", "No navy available")]
    public void OrderTexts_Get_Language(string lang, string key, string expected)
    {
        Assert.Equal(expected, OrderTexts.Get(lang, key));
    }

    [Fact]
    public void OrderTexts_Format_Interpolates()
    {
        Assert.Equal("No bridge at 10,11",
            OrderTexts.Format("en", "err.no-bridge-at-tile-q-tile-r", 10, 11));
        Assert.Equal("Sin puente en 10,11",
            OrderTexts.Format("es", "err.no-bridge-at-tile-q-tile-r", 10, 11));
    }

    [Fact]
    public void OrderTexts_MissingKey_FallsBackToKey()
    {
        Assert.Equal("err.nope", OrderTexts.Get("en", "err.nope"));
    }

    [Theory]
    [InlineData("es", "gold", "oro")]
    [InlineData("en", "gold", "gold")]
    [InlineData("es", "mounts", "monturas")]
    public void OrderTexts_CostKey(string lang, string key, string expected)
    {
        Assert.Equal(expected, OrderTexts.CostKey(lang, key));
    }

    [Fact]
    public void OrderTexts_Norm_DefaultsToEnglish()
    {
        Assert.Equal("en", OrderTexts.Norm(null));
        Assert.Equal("en", OrderTexts.Norm("fr"));
        Assert.Equal("es", OrderTexts.Norm("es"));
    }
}
