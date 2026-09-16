using MEPBMmanager.Api.Services;
using MEPBMmanager.Domain.Constants;

namespace MEPBMmanager.Tests;

public class CatalogTests
{
    [Fact]
    public void Spells_All71Present()
    {
        Assert.Equal(71, SpellCatalog.All.Count);
        Assert.Equal(71, SpellCatalog.All.Select(s => s.Id).Distinct().Count());
    }

    [Theory]
    [InlineData(214, "Call Winds")]
    [InlineData(102, "Barriers")]
    [InlineData(402, "Perceive Allegiance")]
    [InlineData(314, "Teleport")]
    public void SpellCatalog_GetById(int id, string expectedName)
    {
        Assert.Equal(expectedName, SpellCatalog.Get(id)?.Name);
    }

    [Fact]
    public void SpellCatalog_GetUnknown_ReturnsNull()
    {
        Assert.Null(SpellCatalog.Get(9999));
    }

    [Fact]
    public void SpellCatalog_LostLists()
    {
        foreach (var id in new[] { 244, 246, 248, 502, 504, 508, 510, 512 })
            Assert.True(SpellCatalog.Get(id)?.IsLost == true, $"spell {id} should be lost");
        Assert.False(SpellCatalog.Get(314)?.IsLost == true);
        Assert.False(SpellCatalog.Get(214)?.IsLost == true);
    }

    [Fact]
    public void SpellCatalog_WikiFieldsPresent()
    {
        var def = SpellCatalog.Get(214);
        Assert.NotNull(def);
        Assert.False(string.IsNullOrWhiteSpace(def.WikiCollege));
        Assert.False(string.IsNullOrWhiteSpace(def.Difficulty));
        Assert.False(string.IsNullOrWhiteSpace(def.CastOrder));
        Assert.False(string.IsNullOrWhiteSpace(def.Effect));
        Assert.Equal(40, def.MinCastingRank);
    }

    [Fact]
    public void SpellDefinitionsEs_CoversAll()
    {
        Assert.Equal(71, SpellDefinitionsEs.ById.Count);
        foreach (var s in SpellDefinitions.Spells)
            Assert.True(SpellDefinitionsEs.ById.ContainsKey(s.Id), $"missing ES for {s.Id}");
        Assert.Equal(71, SpellDefinitionsEs.NamesEs.Count);
        Assert.Equal(71, SpellDefinitionsEs.CollegesEs.Count);
        Assert.Equal(71, SpellDefinitionsEs.CastOrdersEs.Count);
    }

    [Fact]
    public void Artifacts_All213Present()
    {
        Assert.Equal(213, ArtifactCatalog2950.All.Length);
        Assert.Equal(213, ArtifactCatalog2950.All.Select(a => a.Id).Distinct().Count());
    }

    [Theory]
    [InlineData("Bracers of Chennacatt", 66)]
    [InlineData("bracers of chennacatt", 66)]
    [InlineData("  The One Ring  ", 14)]
    public void ArtifactCatalog_FindByName(string name, int expectedId)
    {
        Assert.Equal(expectedId, ArtifactCatalog2950.Find(name)?.Id);
    }

    [Fact]
    public void ArtifactCatalog_FindUnknown_ReturnsNull()
    {
        Assert.Null(ArtifactCatalog2950.Find("No Such Artifact"));
        Assert.Null(ArtifactCatalog2950.Find(null));
    }

    [Fact]
    public void ArtifactCatalogEs_CoversAll()
    {
        Assert.Equal(213, ArtifactCatalog2950Es.ById.Count);
        Assert.Equal(213, ArtifactCatalog2950Es.NamesEs.Count);
        foreach (var a in ArtifactCatalog2950.All)
        {
            Assert.True(ArtifactCatalog2950Es.ById.ContainsKey(a.Id), $"missing ES for {a.Id}");
            Assert.True(ArtifactCatalog2950Es.NamesEs.ContainsKey(a.Id), $"missing ES name for {a.Id}");
        }
        Assert.Equal("Command +10", ArtifactCatalog2950.All.First(a => a.Id == 66).Primary);
        Assert.Equal("Mando +10", ArtifactCatalog2950Es.ById[66].Primary);
        Assert.Equal("Brazales de Chennacatt", ArtifactCatalog2950Es.NamesEs[66]);
    }

    [Fact]
    public void ArtifactCatalogEs_FindByName()
    {
        Assert.Equal("Brazales de Chennacatt",
            ArtifactCatalog2950Es.NameEsByName("Bracers of Chennacatt"));
        Assert.Null(ArtifactCatalog2950Es.NameEsByName("No Such Artifact"));
    }

    [Fact]
    public void OrderDefinitions_CodesUnique()
    {
        var codes = OrderDefinitions.Orders.Select(o => o.Code).ToList();
        Assert.True(codes.Count > 100);
        Assert.Equal(codes.Count, codes.Distinct().Count());
    }

    [Fact]
    public void OrderDefinitions_KeyCodesExist()
    {
        foreach (var code in new[] { 175, 180, 205, 210, 370, 400, 500, 505, 660, 725, 770, 775, 940, 990 })
            Assert.Contains(OrderDefinitions.Orders, o => o.Code == code);
    }
}
