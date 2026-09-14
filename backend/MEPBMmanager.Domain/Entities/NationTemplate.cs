namespace MEPBMmanager.Domain.Entities;

public class NationTemplate
{
    public string Id { get; set; } = string.Empty;
    public string GameTypeId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Allegiance { get; set; } = string.Empty;   // free_peoples / dark_servants / neutral
    public string Color { get; set; } = string.Empty;
    public string StartHex { get; set; } = "0,0";

    // ── Recursos iniciales ──
    public int StartingGold { get; set; } = 10000;
    public int StartingFood { get; set; } = 5000;
    public int StartingTimber { get; set; } = 2000;
    public int StartingLeather { get; set; } = 1000;
    public int StartingBronze { get; set; } = 500;
    public int StartingSteel { get; set; } = 200;
    public int StartingMithril { get; set; } = 0;
    public int StartingMounts { get; set; } = 500;
    public int TaxRate { get; set; } = 30;

    // ── Ejército inicial ──
    public int StartingHeavyCavalry { get; set; }
    public int StartingLightCavalry { get; set; }
    public int StartingHeavyInfantry { get; set; }
    public int StartingLightInfantry { get; set; }
    public int StartingArchers { get; set; }
    public int StartingMenAtArms { get; set; }
    public int StartingMorale { get; set; } = 30;
    public int StartingTraining { get; set; } = 10;

    // ── Calidad de armas/armaduras por tropa ──
    public int HCWeaponRank { get; set; }
    public int HCArmourRank { get; set; }
    public int LCWeaponRank { get; set; }
    public int LCArmourRank { get; set; }
    public int HIWeaponRank { get; set; }
    public int HIArmourRank { get; set; }
    public int LIWeaponRank { get; set; }
    public int LIArmourRank { get; set; }
    public int ArcherWeaponRank { get; set; }
    public int ArcherArmourRank { get; set; }
    public int MAAWeaponRank { get; set; }
    public int MAAArmourRank { get; set; }

    // ── Poblaciones iniciales ──
    public string CapitalName { get; set; } = "Capital";
    public string CapitalSize { get; set; } = "town";
    public string CapitalFortification { get; set; } = "";
    public bool CapitalHasHarbour { get; set; }
    public bool CapitalHasPort { get; set; }
    public string BorderTownName { get; set; } = "Border Town";
    public string BorderTownSize { get; set; } = "village";
    public string BorderTownFortification { get; set; } = "";

    // ── Nombres de personajes iniciales ──
    public string Character1Name { get; set; } = "Ruler";
    public string Character2Name { get; set; } = "Commander";
    public string Character3Name { get; set; } = "Marshal";
    public string Character4Name { get; set; } = "Spymaster";
    public string Character5Name { get; set; } = "Emissary";
    public string Character6Name { get; set; } = "Sage";

    // ── Reglas de alianza / admin ──
    public bool RequiresAdmin { get; set; }          // esta nación SIEMPRE debe tener un admin de alianza
    public bool FixedAllegiance { get; set; } = true; // no puede cambiar de alianza

    public GameType GameType { get; set; } = null!;
}
