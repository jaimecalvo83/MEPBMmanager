using MEPBMmanager.Api.Services;
using MEPBMmanager.Domain.Entities;

namespace MEPBMmanager.Tests;

public class DuelTests
{
    private static Character MakeChar(string id, int cmd = 10, int agt = 10, int ems = 10,
        int mag = 10, int health = 100)
    {
        return new Character
        {
            Id = id,
            NationId = "n1",
            Name = "Duelist " + id,
            LocationHex = "10,10",
            CommandSkill = cmd,
            AgentSkill = agt,
            EmissarySkill = ems,
            MageSkill = mag,
            Health = health,
            MaxHealth = health,
            Artifacts = new List<Artifact>()
        };
    }

    [Fact]
    public void ChallengeRank_CommandOnly()
    {
        var r = new CombatResolver();
        Assert.Equal(40, r.CalculateChallengeRank(MakeChar("a", cmd: 40, agt: 0, ems: 0, mag: 0)));
    }

    [Fact]
    public void ChallengeRank_MixedSkills()
    {
        var r = new CombatResolver();
        // cmd 20, mag 20, agt 40 -> 30, ems 40 -> 20: highest 30 + (20+20+20)/4 = 45
        Assert.Equal(45, r.CalculateChallengeRank(MakeChar("a", cmd: 20, agt: 40, ems: 40, mag: 20)));
    }

    [Fact]
    public void ChallengeRank_CombatArtifactBonus()
    {
        var r = new CombatResolver();
        var c = MakeChar("a", cmd: 40, agt: 0, ems: 0, mag: 0);
        c.Artifacts.Add(new Artifact { Id = "art1", Name = "Sword", Type = "Sword", Bonus = 100 });
        Assert.Equal(42, r.CalculateChallengeRank(c));
    }

    [Fact]
    public void Duel_StrongerAlwaysWins()
    {
        var r = new CombatResolver();
        var strong = MakeChar("s", cmd: 500, health: 1000);
        var weak = MakeChar("w", cmd: 0, agt: 0, ems: 0, mag: 0, health: 10);

        var res = r.ResolveDuel(strong, weak);

        Assert.Equal("attacker", res.Winner);
        Assert.True(weak.Health < 10);
        Assert.False(string.IsNullOrWhiteSpace(res.Message));
    }

    [Fact]
    public void Duel_StructureAndDamage()
    {
        var r = new CombatResolver();
        var a = MakeChar("a", cmd: 30, health: 20);
        var b = MakeChar("b", cmd: 30, health: 20);

        var res = r.ResolveDuel(a, b);

        Assert.True(res.Winner == "attacker" || res.Winner == "defender");
        Assert.True(res.Damage >= 0);
        Assert.True(a.Health < 20 || b.Health < 20);
        Assert.False(string.IsNullOrWhiteSpace(res.Message));
    }

    [Fact]
    public void Duel_LoserDiesAtZeroHealth()
    {
        var r = new CombatResolver();
        var a = MakeChar("a", cmd: 500, health: 1000);
        var b = MakeChar("b", health: 10);

        r.ResolveDuel(a, b);

        Assert.True(b.IsDead);
        Assert.False(a.IsDead);
    }
}
