namespace MEPBMmanager.Domain.Enums;

public enum TroopType
{
    Hc,  // Heavy Cavalry
    Lc,  // Light Cavalry
    Hi,  // Heavy Infantry
    Li,  // Light Infantry
    Ar,  // Archers
    Ma   // Men-at-Arms
}

public enum MaterialRank
{
    None,
    Wood,
    Leather,
    Bronze,
    Steel,
    Mithril
}

public enum ArmyDirection
{
    H,
    Ne,
    E,
    Se,
    Sw,
    W,
    Nw
}

public enum Tactics
{
    Ch,  // Charge
    Fl,  // Flank
    St,  // Standoff
    Su,  // Surround
    Hr,  // Harass
    Am   // Ambush
}
