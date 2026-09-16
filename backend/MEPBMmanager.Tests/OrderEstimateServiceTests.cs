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

    private static EstimateScope Scope(MepbmDbContext db, Game game, Nation nation, Character ch, string lang = "en")
    {
        var ctx = new EstimateCtx(game, nation, ch, ch.LocationHex,
            new Dictionary<string, System.Text.Json.JsonElement>(), lang);
        return new EstimateScope(db, ctx);
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

        var fields = svc.RequiresFor(370, Scope(db, game, nation, ch).Ctx);
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
        var ctx = new EstimateCtx(game, nation, ch, ch.LocationHex, pars, "en");

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

        Assert.Empty(svc.RequiresFor(215, Scope(db, game, nation, ch).Ctx));
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

    private static EstimateScope ValidateScope(MepbmDbContext db, Game game, Nation nation, Character ch, string jsonParams, string lang = "en")
    {
        var pars = System.Text.Json.JsonDocument.Parse(jsonParams).RootElement.EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.Clone());
        return new EstimateScope(db, new EstimateCtx(game, nation, ch, ch.LocationHex, pars, lang));
    }

    private static List<string> Validate(OrderEstimateService svc, int code, EstimateScope scope)
    {
        svc.ValidateEstimateParams(code, scope, new List<OrderFieldSpecDto>());
        return scope.Errors;
    }

    [Fact]
    public void Validate_755_MissingCommander_NoCrashNoError()
    {
        using var db = NewDb();
        var svc = new OrderEstimateService(db);
        var (game, nation, ch) = NewWorld();

        var errors = Validate(svc, 755, ValidateScope(db, game, nation, ch, "{}"));

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_785_MissingCommander_NoCrashNoError()
    {
        using var db = NewDb();
        var svc = new OrderEstimateService(db);
        var (game, nation, ch) = NewWorld();

        var errors = Validate(svc, 785, ValidateScope(db, game, nation, ch, "{}"));

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_685_MissingArtifact_NoCrashNoError()
    {
        using var db = NewDb();
        var svc = new OrderEstimateService(db);
        var (game, nation, ch) = NewWorld();

        var errors = Validate(svc, 685, ValidateScope(db, game, nation, ch, "{}"));

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_370_WithoutTroopTypes_ReportsIt()
    {
        using var db = NewDb();
        var svc = new OrderEstimateService(db);
        var (game, nation, ch) = NewWorld();
        ch.ArmyId = "a1";
        nation.Armies.Add(new Army { Id = "a1", NationId = "n1", Name = "A", LocationHex = "10,10", HeavyInfantry = 10 });

        var errors = Validate(svc, 370, ValidateScope(db, game, nation, ch, """{"material":"bronze"}"""));

        Assert.Contains(errors, e => e.Contains("hc/lc/hi/li/ar/ma"));
    }

    [Fact]
    public void Validate_725_ShortName_ReportsIt()
    {
        using var db = NewDb();
        var svc = new OrderEstimateService(db);
        var (game, nation, ch) = NewWorld();

        var errors = Validate(svc, 725, ValidateScope(db, game, nation, ch, """{"name":"Abc"}"""));

        Assert.Single(errors);
        Assert.Contains("5-17", errors[0]);
    }

    [Fact]
    public void Validate_500_OwnNation_Rejected()
    {
        using var db = NewDb();
        var svc = new OrderEstimateService(db);
        var (game, nation, ch) = NewWorld();

        var errors = Validate(svc, 500, ValidateScope(db, game, nation, ch, """{"targetId":"c1"}"""));

        Assert.Contains(errors, e => e.Contains("double agent"));
    }

    [Fact]
    public void Scope_TextNumberIdList_ReadParams()
    {
        using var db = NewDb();
        var (game, nation, ch) = NewWorld();
        var scope = ValidateScope(db, game, nation, ch, """{"a":"x","n":7,"ids":["1",2]}""");

        Assert.Equal("x", scope.Text("a"));
        Assert.Null(scope.Text("missing"));
        Assert.Equal(7, scope.Number("n"));
        Assert.Equal(0, scope.Number("missing"));
        Assert.Equal(new[] { "1", "2" }, scope.IdList("ids"));
    }
}
