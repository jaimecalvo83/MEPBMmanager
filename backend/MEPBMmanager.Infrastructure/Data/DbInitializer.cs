using MEPBMmanager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MEPBMmanager.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(MepbmDbContext db)
    {
        await SeedGameTypes(db);
        await SeedNationTemplates(db);
    }

    private static async Task SeedGameTypes(MepbmDbContext db)
    {
        var types = new (string Code, string Name, string Desc)[]
        {
            ("1650", "Third Age 1650", "Escenario clásico de la Segunda Edad tardía / guerras del norte"),
            ("2950", "Third Age 2950", "Guerra del Anillo inminente"),
            ("pruebas", "Pruebas", "Escenario mínimo para testing")
        };
        foreach (var t in types)
        {
            if (await db.GameTypes.AnyAsync(g => g.Code == t.Code)) continue;
            db.GameTypes.Add(new GameType
            {
                Id = Guid.NewGuid().ToString(),
                Code = t.Code,
                Name = t.Name,
                Description = t.Desc
            });
        }
        await db.SaveChangesAsync();
    }

    private static async Task SeedNationTemplates(MepbmDbContext db)
    {
        // ── 1650: sin cambios (mantenemos las 25 naciones existentes) ──
        var defs1650 = new (string Name, string Allegiance, string Color)[]
        {
            ("The Woodmen", "free_peoples", "#228B22"),
            ("Northmen", "free_peoples", "#4169E1"),
            ("Arthedain", "free_peoples", "#FFD700"),
            ("Cardolan", "free_peoples", "#CD853F"),
            ("Northern Gondor", "free_peoples", "#808080"),
            ("Southern Gondor", "free_peoples", "#A9A9A9"),
            ("Dwarves", "free_peoples", "#B8860B"),
            ("Sinda Elves", "free_peoples", "#9370DB"),
            ("Noldo Elves", "free_peoples", "#BA55D3"),
            ("Éothraim", "free_peoples", "#006400"),
            ("Witch-king", "dark_servants", "#8B0000"),
            ("Dragon Lord", "dark_servants", "#FF4500"),
            ("Dog Lord", "dark_servants", "#8B4513"),
            ("Cloud Lord", "dark_servants", "#708090"),
            ("Blind Sorcerer", "dark_servants", "#2F4F4F"),
            ("Ice King", "dark_servants", "#00CED1"),
            ("Quiet Avenger", "dark_servants", "#483D8B"),
            ("Fire King", "dark_servants", "#FF6347"),
            ("Long Rider", "dark_servants", "#556B2F"),
            ("Dark Lieutenants", "dark_servants", "#696969"),
            ("Corsairs", "neutral", "#000000"),
            ("Haradwaith", "neutral", "#D2691E"),
            ("Dunlendings", "neutral", "#9ACD32"),
            ("Rhudaur", "neutral", "#BDB76B"),
            ("Easterlings", "neutral", "#DAA520")
        };

        var gt1650 = await db.GameTypes.FirstAsync(g => g.Code == "1650");
        if (!await db.NationTemplates.AnyAsync(n => n.GameTypeId == gt1650.Id))
        {
            for (int i = 0; i < defs1650.Length; i++)
            {
                var (name, alleg, color) = defs1650[i];
                db.NationTemplates.Add(new NationTemplate
                {
                    Id = Guid.NewGuid().ToString(),
                    GameTypeId = gt1650.Id,
                    Name = name,
                    Allegiance = alleg,
                    Color = color,
                    StartHex = ComputeStartHex(i, defs1650.Length, alleg)
                });
            }
        }

        // ── 2950: naciones enriquecidas ──
        var gt2950 = await db.GameTypes.FirstAsync(g => g.Code == "2950");
        if (!await db.NationTemplates.AnyAsync(n => n.GameTypeId == gt2950.Id))
        {
            var n2950 = Build2950Nations();
            for (int i = 0; i < n2950.Length; i++)
            {
                n2950[i].GameTypeId = gt2950.Id;
                n2950[i].StartHex = ComputeStartHex(i, n2950.Length, n2950[i].Allegiance);
                db.NationTemplates.Add(n2950[i]);
            }
        }

        // ── pruebas: mínimo ──
        var gtPruebas = await db.GameTypes.FirstAsync(g => g.Code == "pruebas");
        if (!await db.NationTemplates.AnyAsync(n => n.GameTypeId == gtPruebas.Id))
        {
            db.NationTemplates.Add(new NationTemplate
            {
                Id = Guid.NewGuid().ToString(),
                GameTypeId = gtPruebas.Id,
                Name = "Test Realm A",
                Allegiance = "free_peoples",
                Color = "#228B22",
                StartHex = ComputeStartHex(0, 2, "free_peoples"),
                StartingGold = 10000, StartingFood = 5000,
                StartingHeavyInfantry = 1000, StartingLightInfantry = 2000,
                CapitalName = "Test Capital", CapitalSize = "town"
            });
            db.NationTemplates.Add(new NationTemplate
            {
                Id = Guid.NewGuid().ToString(),
                GameTypeId = gtPruebas.Id,
                Name = "Test Realm B",
                Allegiance = "dark_servants",
                Color = "#8B0000",
                StartHex = ComputeStartHex(1, 2, "dark_servants"),
                StartingGold = 10000, StartingFood = 5000,
                StartingHeavyInfantry = 1000, StartingLightInfantry = 2000,
                CapitalName = "Test Capital", CapitalSize = "town"
            });
        }

        await db.SaveChangesAsync();
    }

    private static NationTemplate[] Build2950Nations()
    {
        // FREE PEOPLES (6)
        var minasTirith = NT("Minas Tirith", "free_peoples", "#FFD700", requiresAdmin: true,
            gold: 15000, food: 8000, timber: 1500, leather: 1000, bronze: 800, steel: 500, mithril: 50, mounts: 300,
            hc: 500, lc: 1000, hi: 3000, li: 2000, arc: 2000, maa: 1500, wr: 60, ar: 60,
            capital: "Minas Tirith", capSize: "city", border: "Osgiliath", borderSize: "town");

        var rohan = NT("Rohan", "free_peoples", "#006400", requiresAdmin: true,
            gold: 12000, food: 7000, timber: 2000, leather: 1500, bronze: 400, steel: 200, mounts: 1500,
            hc: 2000, lc: 3000, hi: 500, li: 1000, arc: 1000, maa: 500, wr: 40, ar: 40,
            capital: "Edoras", capSize: "town", border: "Fangorn", borderSize: "village");

        var lorien = NT("Lórien", "free_peoples", "#BA55D3", requiresAdmin: true,
            gold: 8000, food: 4000, timber: 3000, leather: 500, bronze: 200, steel: 100, mithril: 200, mounts: 200,
            hc: 200, lc: 500, hi: 1000, li: 2000, arc: 3000, maa: 500, wr: 80, ar: 70,
            capital: "Caras Galadhon", capSize: "village", border: "Lothlórien", borderSize: "village");

        var erebor = NT("Erebor", "free_peoples", "#B8860B", requiresAdmin: true,
            gold: 14000, food: 5000, timber: 1000, leather: 800, bronze: 600, steel: 600, mithril: 100, mounts: 200,
            hc: 300, lc: 500, hi: 3000, li: 1500, arc: 1000, maa: 2000, wr: 70, ar: 70,
            capital: "Erebor", capSize: "fortress", border: "Dale", borderSize: "town");

        var dale = NT("Dale", "free_peoples", "#4169E1",
            gold: 11000, food: 6000, timber: 2000, leather: 1000, bronze: 500, steel: 300, mounts: 400,
            hc: 500, lc: 1000, hi: 1500, li: 2000, arc: 1500, maa: 1000, wr: 50, ar: 50,
            capital: "Dale", capSize: "town", border: "Windlames", borderSize: "village");

        var mirkwood = NT("Mirkwood", "free_peoples", "#228B22",
            gold: 7000, food: 5000, timber: 4000, leather: 800, bronze: 300, steel: 100, mithril: 50, mounts: 300,
            hc: 100, lc: 300, hi: 800, li: 3000, arc: 4000, maa: 500, wr: 60, ar: 50,
            capital: "Thranduil's Halls", capSize: "village", border: "Woodland Realm", borderSize: "village");

        // DARK SERVANTS (6)
        var mordor = NT("Mordor", "dark_servants", "#8B0000", requiresAdmin: true,
            gold: 18000, food: 10000, timber: 1000, leather: 2000, bronze: 1000, steel: 800, mithril: 100, mounts: 800,
            hc: 1000, lc: 2000, hi: 5000, li: 4000, arc: 3000, maa: 3000, wr: 50, ar: 50,
            capital: "Barad-dûr", capSize: "citadel", border: "Minas Morgul", borderSize: "fortress");

        var isengard = NT("Isengard", "dark_servants", "#708090", requiresAdmin: true,
            gold: 10000, food: 6000, timber: 1500, leather: 1000, bronze: 500, steel: 400, mounts: 500,
            hc: 500, lc: 1000, hi: 2000, li: 2000, arc: 1500, maa: 2000, wr: 60, ar: 50,
            capital: "Isengard", capSize: "fortress", border: "Fangorn Border", borderSize: "village");

        var minasMorgul = NT("Minas Morgul", "dark_servants", "#2F4F4F",
            gold: 9000, food: 5000, timber: 800, leather: 800, bronze: 400, steel: 300, mounts: 400,
            hc: 400, lc: 800, hi: 2000, li: 2000, arc: 1500, maa: 1500, wr: 45, ar: 45,
            capital: "Minas Morgul", capSize: "fortress", border: "Cirith Ungol", borderSize: "village");

        var harad = NT("Harad", "dark_servants", "#D2691E",
            gold: 8000, food: 6000, timber: 1500, leather: 1500, bronze: 600, steel: 200, mounts: 1000,
            hc: 1500, lc: 2000, hi: 1000, li: 1500, arc: 1000, maa: 500, wr: 30, ar: 30,
            capital: "Harad Capital", capSize: "town", border: "Harad Border", borderSize: "village");

        var umbar = NT("Umbar", "dark_servants", "#000000",
            gold: 9000, food: 5000, timber: 1000, leather: 800, bronze: 500, steel: 300, mounts: 500,
            hc: 500, lc: 1000, hi: 1500, li: 1500, arc: 1000, maa: 1000, wr: 40, ar: 40,
            capital: "Umbar", capSize: "town", hasPort: true, border: "Corsair Port", borderSize: "village");

        var rhun = NT("Rhûn", "dark_servants", "#556B2F",
            gold: 7000, food: 6000, timber: 2000, leather: 1000, bronze: 500, steel: 200, mounts: 800,
            hc: 1000, lc: 2000, hi: 1000, li: 1500, arc: 800, maa: 500, wr: 25, ar: 25,
            capital: "Rhûn Capital", capSize: "town", border: "Rhûn Border", borderSize: "village");

        // NEUTRALES (8)
        var dunland = NT("Dunland", "neutral", "#9ACD32",
            gold: 4000, food: 4000, timber: 2000, leather: 800, bronze: 300, steel: 100, mounts: 300,
            hc: 200, lc: 500, hi: 1000, li: 2000, arc: 500, maa: 500, wr: 20, ar: 20,
            capital: "Dunland Camp", capSize: "village", border: "Wulf's Camp", borderSize: "camp");

        var enedwaith = NT("Enedwaith", "neutral", "#BDB76B",
            gold: 3000, food: 3000, timber: 2500, leather: 600, bronze: 200, steel: 50, mounts: 200,
            hc: 100, lc: 300, hi: 500, li: 1500, arc: 500, maa: 300, wr: 15, ar: 15,
            capital: "Enedwaith Camp", capSize: "camp", border: "Tharbad", borderSize: "village");

        var dunadan = NT("Dúnadan Rangers", "free_peoples", "#2E8B57",
            gold: 6000, food: 4000, timber: 2000, leather: 800, bronze: 300, steel: 200, mithril: 10, mounts: 400,
            hc: 300, lc: 800, hi: 1000, li: 2000, arc: 2000, maa: 500, wr: 55, ar: 50,
            capital: "Bree", capSize: "village", border: "Weathertop", borderSize: "camp");

        var ridersRohan = NT("Riders of Rohan", "free_peoples", "#00FF00", requiresAdmin: true,
            gold: 8000, food: 5000, timber: 1500, leather: 1200, bronze: 300, steel: 150, mounts: 2000,
            hc: 3000, lc: 2000, hi: 300, li: 500, arc: 500, maa: 200, wr: 35, ar: 35,
            capital: "Aldburg", capSize: "town", border: "Westfold", borderSize: "village");

        var silvan = NT("Silvan Elves", "free_peoples", "#DDA0DD",
            gold: 5000, food: 3000, timber: 4000, leather: 400, bronze: 100, steel: 50, mithril: 100, mounts: 100,
            hc: 50, lc: 200, hi: 500, li: 2000, arc: 3000, maa: 300, wr: 75, ar: 65,
            capital: "Mirkwood Palace", capSize: "village", border: "Greenwood", borderSize: "camp");

        var whiteWizard = NT("White Wizard", "neutral", "#FFFAFA",
            gold: 6000, food: 4000, timber: 2000, leather: 500, bronze: 200, steel: 100, mithril: 300, mounts: 200,
            hc: 100, lc: 300, hi: 800, li: 1500, arc: 2000, maa: 500, wr: 70, ar: 60,
            capital: "Isengard Outpost", capSize: "camp", border: "Fangorn Watch", borderSize: "camp");

        var khand = NT("Khand Easterlings", "neutral", "#B8860B",
            gold: 4000, food: 5000, timber: 1500, leather: 800, bronze: 400, steel: 100, mounts: 600,
            hc: 800, lc: 1500, hi: 500, li: 1000, arc: 500, maa: 300, wr: 20, ar: 20,
            capital: "Khand Camp", capSize: "village", border: "Variag Camp", borderSize: "camp");

        var rhunEasterlings = NT("Rhûn Easterlings", "neutral", "#CD853F",
            gold: 5000, food: 5000, timber: 2000, leather: 1000, bronze: 400, steel: 150, mounts: 700,
            hc: 1000, lc: 1500, hi: 600, li: 1000, arc: 600, maa: 400, wr: 22, ar: 22,
            capital: "Rhûn Camp", capSize: "village", border: "Sea of Rhûn", borderSize: "camp");

        return [minasTirith, rohan, lorien, erebor, dale, mirkwood,
                mordor, isengard, minasMorgul, harad, umbar, rhun,
                dunland, enedwaith, dunadan, ridersRohan, silvan, whiteWizard, khand, rhunEasterlings];
    }

    private static NationTemplate NT(
        string name, string alleg, string color, bool requiresAdmin = false,
        int gold = 10000, int food = 5000, int timber = 2000, int leather = 1000,
        int bronze = 500, int steel = 200, int mithril = 0, int mounts = 500,
        int hc = 0, int lc = 0, int hi = 0, int li = 0, int arc = 0, int maa = 0,
        int wr = 10, int ar = 10,
        string capital = "Capital", string capSize = "town",
        bool hasHarbour = false, bool hasPort = false,
        string border = "Border Town", string borderSize = "village")
    {
        return new NationTemplate
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Allegiance = alleg,
            Color = color,
            RequiresAdmin = requiresAdmin,
            StartingGold = gold,
            StartingFood = food,
            StartingTimber = timber,
            StartingLeather = leather,
            StartingBronze = bronze,
            StartingSteel = steel,
            StartingMithril = mithril,
            StartingMounts = mounts,
            StartingHeavyCavalry = hc,
            StartingLightCavalry = lc,
            StartingHeavyInfantry = hi,
            StartingLightInfantry = li,
            StartingArchers = arc,
            StartingMenAtArms = maa,
            StartingWeaponRank = wr,
            StartingArmourRank = ar,
            CapitalName = capital,
            CapitalSize = capSize,
            CapitalHasHarbour = hasHarbour,
            CapitalHasPort = hasPort,
            BorderTownName = border,
            BorderTownSize = borderSize
        };
    }

    private static string ComputeStartHex(int i, int count, string allegiance)
    {
        double angle = 2 * Math.PI * i / count;
        int radius = allegiance == "neutral" ? 4 : 16;
        int q = (int)Math.Round(radius * Math.Cos(angle));
        int r = (int)Math.Round(radius * Math.Sin(angle));
        int size = 20;
        int rMin = Math.Max(-size, -q - size);
        int rMax = Math.Min(size, -q + size);
        r = Math.Clamp(r, rMin, rMax);
        return $"{q},{r}";
    }
}
