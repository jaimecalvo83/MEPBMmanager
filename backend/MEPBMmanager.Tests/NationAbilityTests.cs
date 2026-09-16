using MEPBMmanager.Domain.Constants;

namespace MEPBMmanager.Tests;

public class NationAbilityTests
{
    [Fact]
    public void RecruitTraining_DefaultIs10()
    {
        Assert.Equal(10, NationAbilities.RecruitTrainingFor(null, "HeavyInfantry"));
        Assert.Equal(10, NationAbilities.RecruitTrainingFor("No Such Nation", "Archers"));
    }

    [Fact]
    public void HasForNation_UnknownAbilityIsFalse()
    {
        Assert.False(NationAbilities.HasForNation("Dwarves", "NO_SUCH_ABILITY"));
        Assert.False(NationAbilities.HasForNation(null, "HIRE_FREE"));
    }

    [Fact]
    public void HasForNation_KnownAbilities()
    {
        Assert.True(NationAbilities.HasForNation("White Wizard", "HIRE_FREE"));
        Assert.True(NationAbilities.HasForNation("white-wizard", "HIRE_FREE"));
    }

    [Fact]
    public void CanLearnLostSpell_Restricted()
    {
        // 508 Conjure Mounts learnable only by listed nations
        Assert.False(NationAbilities.CanLearnLostSpell("Dwarves", 508));
    }

    [Fact]
    public void HireArmyCost_Positive()
    {
        Assert.True(NationAbilities.HireArmyCost(null) > 0);
        Assert.True(NationAbilities.HireArmyCost("Dwarves") > 0);
    }

    [Fact]
    public void DisplayNames_BothLanguages()
    {
        Assert.True(NationAbilities.DisplayNames.Count > 20);
        Assert.True(NationAbilities.DisplayNamesEn.Count >= NationAbilities.DisplayNames.Count);
    }
}
