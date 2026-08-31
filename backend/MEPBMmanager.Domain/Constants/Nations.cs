using MEPBMmanager.Domain.Enums;

namespace MEPBMmanager.Domain.Constants;

public record NationDefinition(int Id, string Name, Allegiance Allegiance, string[] Modules, string Color);

public static class Nations
{
    public static readonly NationDefinition[] Nations1650 =
    [
        // FREE PEOPLES
        new(1, "The Woodmen", Allegiance.FreePeoples, ["1650", "2950"], "#228B22"),
        new(2, "Northmen", Allegiance.FreePeoples, ["1650", "2950"], "#4169E1"),
        new(4, "Arthedain", Allegiance.FreePeoples, ["1650"], "#FFD700"),
        new(5, "Cardolan", Allegiance.FreePeoples, ["1650"], "#CD853F"),
        new(6, "Northern Gondor", Allegiance.FreePeoples, ["1650", "2950"], "#808080"),
        new(7, "Southern Gondor", Allegiance.FreePeoples, ["1650", "2950"], "#A9A9A9"),
        new(8, "Dwarves", Allegiance.FreePeoples, ["1650", "2950"], "#B8860B"),
        new(9, "Sinda Elves", Allegiance.FreePeoples, ["1650", "2950"], "#9370DB"),
        new(10, "Noldo Elves", Allegiance.FreePeoples, ["1650", "2950"], "#BA55D3"),
        new(3, "Éothraim", Allegiance.FreePeoples, ["1650"], "#006400"),

        // DARK SERVANTS
        new(11, "Witch-king", Allegiance.DarkServants, ["1650", "2950"], "#8B0000"),
        new(12, "Dragon Lord", Allegiance.DarkServants, ["1650", "2950"], "#FF4500"),
        new(13, "Dog Lord", Allegiance.DarkServants, ["1650", "2950"], "#8B4513"),
        new(14, "Cloud Lord", Allegiance.DarkServants, ["1650", "2950"], "#708090"),
        new(15, "Blind Sorcerer", Allegiance.DarkServants, ["1650", "2950"], "#2F4F4F"),
        new(16, "Ice King", Allegiance.DarkServants, ["1650", "2950"], "#00CED1"),
        new(17, "Quiet Avenger", Allegiance.DarkServants, ["1650", "2950"], "#483D8B"),
        new(18, "Fire King", Allegiance.DarkServants, ["1650", "2950"], "#FF6347"),
        new(19, "Long Rider", Allegiance.DarkServants, ["1650", "2950"], "#556B2F"),
        new(20, "Dark Lieutenants", Allegiance.DarkServants, ["1650", "2950"], "#696969"),

        // NEUTRALS
        new(21, "Corsairs", Allegiance.Neutral, ["1650", "2950"], "#000000"),
        new(22, "Haradwaith", Allegiance.Neutral, ["1650"], "#D2691E"),
        new(23, "Dunlendings", Allegiance.Neutral, ["1650", "2950"], "#9ACD32"),
        new(24, "Rhudaur", Allegiance.Neutral, ["1650"], "#BDB76B"),
        new(25, "Easterlings", Allegiance.Neutral, ["1650"], "#DAA520"),
    ];

    public static readonly NationDefinition[] Nations2950Extra =
    [
        new(101, "Dúnadan Rangers", Allegiance.FreePeoples, ["2950"], "#2E8B57"),
        new(102, "Riders of Rohan", Allegiance.FreePeoples, ["2950"], "#00FF00"),
        new(103, "Silvan Elves", Allegiance.FreePeoples, ["2950"], "#DDA0DD"),
        new(104, "White Wizard", Allegiance.Neutral, ["2950"], "#FFFAFA"),
        new(105, "Khand Easterlings", Allegiance.Neutral, ["2950"], "#B8860B"),
        new(106, "Rhûn Easterlings", Allegiance.Neutral, ["2950"], "#CD853F"),
    ];

    public static readonly NationDefinition[] AllNations = [.. Nations1650, .. Nations2950Extra];
}
