namespace MEPBMmanager.Domain.Enums;

public enum TerrainType
{
    Plains,
    Forest,
    Mountains,
    Rough,
    Desert,
    Swamp,
    Shore,
    Water
}

public enum Season
{
    Spring,
    Summer,
    Autumn,
    Winter
}

public enum GameModule
{
    Module1650,
    Module2950,
    FourthAge,
    Bofa
}

public enum GameStatus
{
    Setup,
    Active,
    Paused,
    Finished
}

public enum TurnStatus
{
    Pending,
    OrdersOpen,
    Processing,
    Completed
}

public enum Allegiance
{
    FreePeoples,
    DarkServants,
    Neutral
}

public enum RelationLevel
{
    Hostile = -2,
    Unfriendly = -1,
    Neutral2 = 0,
    Friendly = 1,
    Allied = 2
}
