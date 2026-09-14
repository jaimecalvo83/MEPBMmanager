using MEPBMmanager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MEPBMmanager.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(MepbmDbContext db)
    {
        await SeedRoles(db);
        await SeedUsers(db);
        await SeedGameTypes(db);
        await SeedNationTemplates(db);
    }

    private static async Task SeedRoles(MepbmDbContext db)
    {
        var roles = new (string Id, string Name, string Description)[]
        {
            ("game_user", "Game User", "Regular player in production games"),
            ("test_user", "Test User", "Regular player in test games"),
            ("game_admin", "Game Admin", "Admin in production games"),
            ("test_admin", "Test Admin", "Full access to everything")
        };

        foreach (var (id, name, description) in roles)
        {
            if (await db.Roles.AnyAsync(r => r.Id == id)) continue;
            db.Roles.Add(new Role
            {
                Id = id,
                Name = name,
                Description = description
            });
        }
        await db.SaveChangesAsync();
    }

    private static async Task SeedUsers(MepbmDbContext db)
    {
        if (await db.Users.AnyAsync()) return;

        var users = new (string Username, string Email, string Password, string RoleId)[]
        {
            ("alice", "alice@example.com", "alicepass1", "game_user"),
            ("bob", "bob@example.com", "bobpass1", "game_user"),
            ("carol", "carol@example.com", "carolpass1", "game_user"),
            ("dave", "dave@example.com", "davepass1", "game_user"),
            ("eve", "eve@example.com", "evepass1", "game_user"),
            ("frank", "frank@example.com", "frankpass1", "game_user"),
            ("grace", "grace@example.com", "gracepass1", "game_user"),
            ("hank", "hank@example.com", "hankpass1", "game_user"),
            ("iris", "iris@example.com", "irispass1", "game_user"),
            ("jack", "jack@example.com", "jackpass1", "game_user"),
            ("admin", "admin@admin.com", "admin123", "test_admin"),
            ("tina", "tina@example.com", "tinapass1", "test_admin"),
        };

        foreach (var (username, email, password, roleId) in users)
        {
            db.Users.Add(new User
            {
                Id = Guid.NewGuid().ToString(),
                Username = username,
                Email = email,
                Password = BCrypt.Net.BCrypt.HashPassword(password),
                RoleId = roleId
            });
        }
        await db.SaveChangesAsync();
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
            var sqlParams = new List<NpgsqlParameter>();
            for (int i = 0; i < defs1650.Length; i++)
            {
                var (name, alleg, color) = defs1650[i];
                var ntId = Guid.NewGuid().ToString();
                var startHex = ComputeStartHex(i, defs1650.Length, alleg);
                var sql = "INSERT INTO \"NationTemplates\" (\"Id\",\"GameTypeId\",\"Name\",\"Allegiance\",\"Color\",\"StartHex\") VALUES (@p0,@p1,@p2,@p3,@p4,@p5)";
                await db.Database.ExecuteSqlRawAsync(sql,
                    new NpgsqlParameter("p0", ntId),
                    new NpgsqlParameter("p1", gt1650.Id),
                    new NpgsqlParameter("p2", name),
                    new NpgsqlParameter("p3", alleg),
                    new NpgsqlParameter("p4", color),
                    new NpgsqlParameter("p5", startHex));
            }
        }

        // ── 2950: naciones enriquecidas ──
        var gt2950 = await db.GameTypes.FirstAsync(g => g.Code == "2950");
        if (!await db.NationTemplates.AnyAsync(n => n.GameTypeId == gt2950.Id))
        {
            var n2950 = Build2950Nations();
            foreach (var nt in n2950)
            {
                nt.GameTypeId = gt2950.Id;
                nt.StartHex = ComputeStartHex(Array.IndexOf(n2950, nt), n2950.Length, nt.Allegiance);
                await InsertNationTemplateRaw(db, nt);
            }
        }

        // ── pruebas: mínimo ──
        var gtPruebas = await db.GameTypes.FirstAsync(g => g.Code == "pruebas");
        if (!await db.NationTemplates.AnyAsync(n => n.GameTypeId == gtPruebas.Id))
        {
            var testA = NT("Test Realm A", "free_peoples", "#228B22",
                gold: 10000, food: 5000,
                hi: 1000, li: 2000,
                hcW: 50, hcA: 50, lcW: 50, lcA: 50,
                hiW: 50, hiA: 50, liW: 50, liA: 50,
                arcW: 50, arcA: 50, maaW: 50, maaA: 50,
                capital: "Test Capital", capitalFortification: "Wooden Palisade",
                border: "Test Border", borderFortification: "Wooden Palisade",
                char1: "Aragorn", char2: "Legolas", char3: "Gimli",
                char4: "Frodo", char5: "Samwise", char6: "Gandalf");
            testA.GameTypeId = gtPruebas.Id;
            testA.StartHex = "0,0";
            await InsertNationTemplateRaw(db, testA);

            var testB = NT("Test Realm B", "dark_servants", "#8B0000",
                gold: 10000, food: 5000,
                hi: 1000, li: 2000,
                hcW: 40, hcA: 40, lcW: 40, lcA: 40,
                hiW: 40, hiA: 40, liW: 40, liA: 40,
                arcW: 40, arcA: 40, maaW: 40, maaA: 40,
                capital: "Test Capital", capitalFortification: "Wooden Palisade",
                border: "Test Border", borderFortification: "Wooden Palisade",
                char1: "Sauron", char2: "Khamûl", char3: "Lurtz",
                char4: "Uglúk", char5: "Saruman", char6: "Witch-king");
            testB.GameTypeId = gtPruebas.Id;
            testB.StartHex = "1,0";
            await InsertNationTemplateRaw(db, testB);
        }
    }

    private static NationTemplate[] Build2950Nations()
    {
        // FREE PEOPLES (6)
        var minasTirith = NT("Minas Tirith", "free_peoples", "#FFD700", requiresAdmin: true,
            gold: 15000, food: 8000, timber: 1500, leather: 1000, bronze: 800, steel: 500, mithril: 50, mounts: 300,
            hc: 500, lc: 1000, hi: 3000, li: 2000, arc: 2000, maa: 1500,
            hcW: 65, hcA: 55, lcW: 70, lcA: 45, hiW: 55, hiA: 70, liW: 60, liA: 50, arcW: 65, arcA: 40, maaW: 60, maaA: 60,
            capital: "Minas Tirith", capSize: "city", capitalFortification: "Walls",
            border: "Osgiliath", borderSize: "town", borderFortification: "Wooden Palisade",
            char1: "Beregond", char2: "Faramir", char3: "Hirluin", char4: "Mablung", char5: "Longoth", char6: "Eldamir");

        var rohan = NT("Rohan", "free_peoples", "#006400", requiresAdmin: true,
            gold: 12000, food: 7000, timber: 2000, leather: 1500, bronze: 400, steel: 200, mounts: 1500,
            hc: 2000, lc: 3000, hi: 500, li: 1000, arc: 1000, maa: 500,
            hcW: 50, hcA: 35, lcW: 55, lcA: 25, hiW: 35, hiA: 45, liW: 40, liA: 35, arcW: 45, arcA: 30, maaW: 40, maaA: 40,
            capital: "Edoras", capSize: "town", capitalFortification: "Wooden Palisade",
            border: "Fangorn", borderSize: "village", borderFortification: "Earthen Ramparts",
            char1: "Éomer", char2: "Éowyn", char3: "Gamling", char4: "Háma", char5: "Dernhelm", char6: "Wulfgar");

        var lorien = NT("Lórien", "free_peoples", "#BA55D3", requiresAdmin: true,
            gold: 8000, food: 4000, timber: 3000, leather: 500, bronze: 200, steel: 100, mithril: 200, mounts: 200,
            hc: 200, lc: 500, hi: 1000, li: 2000, arc: 3000, maa: 500,
            hcW: 85, hcA: 70, lcW: 90, lcA: 60, hiW: 75, hiA: 80, liW: 80, liA: 65, arcW: 90, arcA: 55, maaW: 80, maaA: 75,
            capital: "Caras Galadhon", capSize: "village", capitalFortification: "Earthen Ramparts",
            border: "Lothlórien", borderSize: "village", borderFortification: "Earthen Ramparts",
            char1: "Haldir", char2: "Rúmil", char3: "Orophin", char4: "Galadriel", char5: "Celeborn", char6: "Tinúviel");

        var erebor = NT("Erebor", "free_peoples", "#B8860B", requiresAdmin: true,
            gold: 14000, food: 5000, timber: 1000, leather: 800, bronze: 600, steel: 600, mithril: 100, mounts: 200,
            hc: 300, lc: 500, hi: 3000, li: 1500, arc: 1000, maa: 2000,
            hcW: 70, hcA: 80, lcW: 65, lcA: 70, hiW: 65, hiA: 90, liW: 60, liA: 75, arcW: 65, arcA: 60, maaW: 70, maaA: 85,
            capital: "Erebor", capSize: "fortress", capitalFortification: "Stone Walls",
            border: "Dale", borderSize: "town", borderFortification: "Wooden Palisade",
            char1: "Dáin", char2: "Thorin III", char3: "Balin", char4: "Dwalin", char5: "Glóin", char6: "Bofur");

        var dale = NT("Dale", "free_peoples", "#4169E1",
            gold: 11000, food: 6000, timber: 2000, leather: 1000, bronze: 500, steel: 300, mounts: 400,
            hc: 500, lc: 1000, hi: 1500, li: 2000, arc: 1500, maa: 1000,
            hcW: 55, hcA: 45, lcW: 60, lcA: 35, hiW: 45, hiA: 55, liW: 50, liA: 45, arcW: 55, arcA: 35, maaW: 50, maaA: 50,
            capital: "Dale", capSize: "town", capitalFortification: "Wooden Palisade",
            border: "Windlames", borderSize: "village", borderFortification: "Earthen Ramparts",
            char1: "Bard II", char2: "Brand", char3: "Girion", char4: "Algar", char5: "Regin", char6: "Léod");

        var mirkwood = NT("Mirkwood", "free_peoples", "#228B22",
            gold: 7000, food: 5000, timber: 4000, leather: 800, bronze: 300, steel: 100, mithril: 50, mounts: 300,
            hc: 100, lc: 300, hi: 800, li: 3000, arc: 4000, maa: 500,
            hcW: 65, hcA: 50, lcW: 70, lcA: 40, hiW: 55, hiA: 60, liW: 65, liA: 50, arcW: 75, arcA: 45, maaW: 60, maaA: 55,
            capital: "Thranduil's Halls", capSize: "village", capitalFortification: "Earthen Ramparts",
            border: "Woodland Realm", borderSize: "village", borderFortification: "Earthen Ramparts",
            char1: "Thranduil", char2: "Legolas", char3: "Tauriel", char4: "Feren", char5: "Galion", char6: "Oropher");

        // DARK SERVANTS (6)
        var mordor = NT("Mordor", "dark_servants", "#8B0000", requiresAdmin: true,
            gold: 18000, food: 10000, timber: 1000, leather: 2000, bronze: 1000, steel: 800, mithril: 100, mounts: 800,
            hc: 1000, lc: 2000, hi: 5000, li: 4000, arc: 3000, maa: 3000,
            hcW: 50, hcA: 45, lcW: 55, lcA: 35, hiW: 45, hiA: 55, liW: 50, liA: 40, arcW: 50, arcA: 35, maaW: 50, maaA: 50,
            capital: "Barad-dûr", capSize: "citadel", capitalFortification: "Citadel Walls",
            border: "Minas Morgul", borderSize: "fortress", borderFortification: "Stone Walls",
            char1: "Khamûl", char2: "Morgomir", char3: "Holvimento", char4: "Nulfoke", char5: "Sulmûl", char6: "Ashnazg");

        var isengard = NT("Isengard", "dark_servants", "#708090", requiresAdmin: true,
            gold: 10000, food: 6000, timber: 1500, leather: 1000, bronze: 500, steel: 400, mounts: 500,
            hc: 500, lc: 1000, hi: 2000, li: 2000, arc: 1500, maa: 2000,
            hcW: 60, hcA: 50, lcW: 65, lcA: 40, hiW: 55, hiA: 60, liW: 55, liA: 45, arcW: 60, arcA: 40, maaW: 60, maaA: 55,
            capital: "Isengard", capSize: "fortress", capitalFortification: "Stone Walls",
            border: "Fangorn Border", borderSize: "village", borderFortification: "Earthen Ramparts",
            char1: "Saruman", char2: "Lurtz", char3: "Uglúk", char4: "Grimbold", char5: "Vrasku", char6: "Mautherbain");

        var minasMorgul = NT("Minas Morgul", "dark_servants", "#2F4F4F",
            gold: 9000, food: 5000, timber: 800, leather: 800, bronze: 400, steel: 300, mounts: 400,
            hc: 400, lc: 800, hi: 2000, li: 2000, arc: 1500, maa: 1500,
            hcW: 45, hcA: 40, lcW: 50, lcA: 30, hiW: 40, hiA: 50, liW: 45, liA: 35, arcW: 45, arcA: 30, maaW: 45, maaA: 45,
            capital: "Minas Morgul", capSize: "fortress", capitalFortification: "Stone Walls",
            border: "Cirith Ungol", borderSize: "village", borderFortification: "Earthen Ramparts",
            char1: "Witch-king", char2: "Camulost", char3: "Dellwyth", char4: "Nernolth", char5: "Gorothrim", char6: "Faunel");

        var harad = NT("Harad", "dark_servants", "#D2691E",
            gold: 8000, food: 6000, timber: 1500, leather: 1500, bronze: 600, steel: 200, mounts: 1000,
            hc: 1500, lc: 2000, hi: 1000, li: 1500, arc: 1000, maa: 500,
            hcW: 35, hcA: 25, lcW: 40, lcA: 20, hiW: 25, hiA: 35, liW: 30, liA: 25, arcW: 30, arcA: 20, maaW: 30, maaA: 30,
            capital: "Harad Capital", capSize: "town", capitalFortification: "Wooden Palisade",
            border: "Harad Border", borderSize: "village", borderFortification: "Earthen Ramparts",
            char1: "Harthrand", char2: "Fuinur", char3: "Lhuguen", char4: "Sangahyando", char5: "Aduiar", char6: "Zimrathôn");

        var umbar = NT("Umbar", "dark_servants", "#000000",
            gold: 9000, food: 5000, timber: 1000, leather: 800, bronze: 500, steel: 300, mounts: 500,
            hc: 500, lc: 1000, hi: 1500, li: 1500, arc: 1000, maa: 1000,
            hcW: 45, hcA: 35, lcW: 50, lcA: 25, hiW: 35, hiA: 45, liW: 40, liA: 35, arcW: 40, arcA: 30, maaW: 40, maaA: 40,
            capital: "Umbar", capSize: "town", capitalFortification: "Wooden Palisade",
            hasPort: true,
            border: "Corsair Port", borderSize: "village", borderFortification: "Earthen Ramparts",
            char1: "Herumor", char2: "Lírazôn", char3: "Ciryaher", char4: "Angamaite", char5: "Tillanc", char6: "Yulmat");

        var rhun = NT("Rhûn", "dark_servants", "#556B2F",
            gold: 7000, food: 6000, timber: 2000, leather: 1000, bronze: 500, steel: 200, mounts: 800,
            hc: 1000, lc: 2000, hi: 1000, li: 1500, arc: 800, maa: 500,
            hcW: 30, hcA: 20, lcW: 35, lcA: 15, hiW: 20, hiA: 30, liW: 25, liA: 20, arcW: 25, arcA: 15, maaW: 25, maaA: 25,
            capital: "Rhûn Capital", capSize: "town", capitalFortification: "Wooden Palisade",
            border: "Rhûn Border", borderSize: "village", borderFortification: "Earthen Ramparts",
            char1: "Yara-Naeth", char2: "Zhorjakh", char3: "Dushgoi", char4: "Khamûl", char5: "Bolgoi", char6: "Rûkhor");

        // NEUTRALES (8)
        var dunland = NT("Dunland", "neutral", "#9ACD32",
            gold: 4000, food: 4000, timber: 2000, leather: 800, bronze: 300, steel: 100, mounts: 300,
            hc: 200, lc: 500, hi: 1000, li: 2000, arc: 500, maa: 500,
            hcW: 25, hcA: 15, lcW: 30, lcA: 10, hiW: 15, hiA: 25, liW: 20, liA: 15, arcW: 20, arcA: 10, maaW: 20, maaA: 20,
            capital: "Dunland Camp", capSize: "village", capitalFortification: "Earthen Ramparts",
            border: "Wulf's Camp", borderSize: "camp", borderFortification: "",
            char1: "Wulf", char2: "Faulk", char3: "Adhelm", char4: "Brytwith", char5: "Crendon", char6: "Dorfir");

        var enedwaith = NT("Enedwaith", "neutral", "#BDB76B",
            gold: 3000, food: 3000, timber: 2500, leather: 600, bronze: 200, steel: 50, mounts: 200,
            hc: 100, lc: 300, hi: 500, li: 1500, arc: 500, maa: 300,
            hcW: 20, hcA: 10, lcW: 25, lcA: 8, hiW: 10, hiA: 20, liW: 15, liA: 10, arcW: 15, arcA: 8, maaW: 15, maaA: 15,
            capital: "Enedwaith Camp", capSize: "camp", capitalFortification: "",
            border: "Tharbad", borderSize: "village", borderFortification: "Earthen Ramparts",
            char1: "Théodwyn", char2: "Aldric", char3: "Brand", char4: "Cynric", char5: "Dunbar", char6: "Eadric");

        var dunadan = NT("Dúnadan Rangers", "free_peoples", "#2E8B57",
            gold: 6000, food: 4000, timber: 2000, leather: 800, bronze: 300, steel: 200, mithril: 10, mounts: 400,
            hc: 300, lc: 800, hi: 1000, li: 2000, arc: 2000, maa: 500,
            hcW: 60, hcA: 50, lcW: 65, lcA: 40, hiW: 50, hiA: 60, liW: 55, liA: 50, arcW: 60, arcA: 40, maaW: 55, maaA: 55,
            capital: "Bree", capSize: "village", capitalFortification: "Earthen Ramparts",
            border: "Weathertop", borderSize: "camp", borderFortification: "",
            char1: "Aragorn II", char2: "Elrohir", char3: "Elladan", char4: "Ranger Captain", char5: "Hirgon", char6: "Dirhael");

        var ridersRohan = NT("Riders of Rohan", "free_peoples", "#00FF00", requiresAdmin: true,
            gold: 8000, food: 5000, timber: 1500, leather: 1200, bronze: 300, steel: 150, mounts: 2000,
            hc: 3000, lc: 2000, hi: 300, li: 500, arc: 500, maa: 200,
            hcW: 40, hcA: 30, lcW: 45, lcA: 20, hiW: 30, hiA: 40, liW: 35, liA: 30, arcW: 40, arcA: 25, maaW: 35, maaA: 35,
            capital: "Aldburg", capSize: "town", capitalFortification: "Wooden Palisade",
            border: "Westfold", borderSize: "village", borderFortification: "Earthen Ramparts",
            char1: "Théoden", char2: "Éomer", char3: "Elfhelm", char4: "Dúnhere", char5: "Grimbold", char6: "Herefara");

        var silvan = NT("Silvan Elves", "free_peoples", "#DDA0DD",
            gold: 5000, food: 3000, timber: 4000, leather: 400, bronze: 100, steel: 50, mithril: 100, mounts: 100,
            hc: 50, lc: 200, hi: 500, li: 2000, arc: 3000, maa: 300,
            hcW: 80, hcA: 60, lcW: 85, lcA: 50, hiW: 70, hiA: 75, liW: 75, liA: 60, arcW: 85, arcA: 50, maaW: 75, maaA: 65,
            capital: "Mirkwood Palace", capSize: "village", capitalFortification: "Earthen Ramparts",
            border: "Greenwood", borderSize: "camp", borderFortification: "",
            char1: "Thranduil", char2: "Legolas", char3: "Tauriel", char4: "Feren", char5: "Oropher", char6: "Galion");

        var whiteWizard = NT("White Wizard", "neutral", "#FFFAFA",
            gold: 6000, food: 4000, timber: 2000, leather: 500, bronze: 200, steel: 100, mithril: 300, mounts: 200,
            hc: 100, lc: 300, hi: 800, li: 1500, arc: 2000, maa: 500,
            hcW: 75, hcA: 55, lcW: 80, lcA: 45, hiW: 65, hiA: 70, liW: 70, liA: 55, arcW: 75, arcA: 45, maaW: 70, maaA: 60,
            capital: "Isengard Outpost", capSize: "camp", capitalFortification: "",
            border: "Fangorn Watch", borderSize: "camp", borderFortification: "",
            char1: "Gandalf", char2: "Radagast", char3: "Pallando", char4: "Alatar", char5: "Curunír", char6: "Mírandir");

        var khand = NT("Khand Easterlings", "neutral", "#B8860B",
            gold: 4000, food: 5000, timber: 1500, leather: 800, bronze: 400, steel: 100, mounts: 600,
            hc: 800, lc: 1500, hi: 500, li: 1000, arc: 500, maa: 300,
            hcW: 25, hcA: 15, lcW: 30, lcA: 10, hiW: 15, hiA: 25, liW: 20, liA: 15, arcW: 20, arcA: 10, maaW: 20, maaA: 20,
            capital: "Khand Camp", capSize: "village", capitalFortification: "Earthen Ramparts",
            border: "Variag Camp", borderSize: "camp", borderFortification: "",
            char1: "Khôrshesh", char2: "Varjakh", char3: "Gorzhûl", char4: "Murlam", char5: "Bajazm", char6: "Ushkûl");

        var rhunEasterlings = NT("Rhûn Easterlings", "neutral", "#CD853F",
            gold: 5000, food: 5000, timber: 2000, leather: 1000, bronze: 400, steel: 150, mounts: 700,
            hc: 1000, lc: 1500, hi: 600, li: 1000, arc: 600, maa: 400,
            hcW: 27, hcA: 17, lcW: 32, lcA: 12, hiW: 17, hiA: 27, liW: 22, liA: 17, arcW: 22, arcA: 12, maaW: 22, maaA: 22,
            capital: "Rhûn Camp", capSize: "village", capitalFortification: "Earthen Ramparts",
            border: "Sea of Rhûn", borderSize: "camp", borderFortification: "",
            char1: "Sauron", char2: "Khamûl", char3: "Dushgoi", char4: "Bolgoi", char5: "Rûkhor", char6: "Yara-Naeth");

        return [minasTirith, rohan, lorien, erebor, dale, mirkwood,
                mordor, isengard, minasMorgul, harad, umbar, rhun,
                dunland, enedwaith, dunadan, ridersRohan, silvan, whiteWizard, khand, rhunEasterlings];
    }

    private static NationTemplate NT(
        string name, string alleg, string color, bool requiresAdmin = false,
        int gold = 10000, int food = 5000, int timber = 2000, int leather = 1000,
        int bronze = 500, int steel = 200, int mithril = 0, int mounts = 500,
        int hc = 0, int lc = 0, int hi = 0, int li = 0, int arc = 0, int maa = 0,
        int hcW = 10, int hcA = 10,
        int lcW = 10, int lcA = 10,
        int hiW = 10, int hiA = 10,
        int liW = 10, int liA = 10,
        int arcW = 10, int arcA = 10,
        int maaW = 10, int maaA = 10,
        string capital = "Capital", string capSize = "town",
        string capitalFortification = "Wooden Palisade",
        bool hasHarbour = false, bool hasPort = false,
        string border = "Border Town", string borderSize = "village",
        string borderFortification = "Wooden Palisade",
        string char1 = "", string char2 = "", string char3 = "",
        string char4 = "", string char5 = "", string char6 = "")
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
            HCWeaponRank = hcW,
            HCArmourRank = hcA,
            LCWeaponRank = lcW,
            LCArmourRank = lcA,
            HIWeaponRank = hiW,
            HIArmourRank = hiA,
            LIWeaponRank = liW,
            LIArmourRank = liA,
            ArcherWeaponRank = arcW,
            ArcherArmourRank = arcA,
            MAAWeaponRank = maaW,
            MAAArmourRank = maaA,
            CapitalName = capital,
            CapitalSize = capSize,
            CapitalFortification = capitalFortification,
            CapitalHasHarbour = hasHarbour,
            CapitalHasPort = hasPort,
            BorderTownName = border,
            BorderTownSize = borderSize,
            BorderTownFortification = borderFortification,
            Character1Name = char1,
            Character2Name = char2,
            Character3Name = char3,
            Character4Name = char4,
            Character5Name = char5,
            Character6Name = char6
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

    private static async Task InsertNationTemplateRaw(MepbmDbContext db, NationTemplate nt)
    {
        var sql = @"INSERT INTO ""NationTemplates""
            (""Id"",""GameTypeId"",""Name"",""Allegiance"",""Color"",""StartHex"",
             ""StartingGold"",""StartingFood"",""StartingTimber"",""StartingLeather"",""StartingBronze"",""StartingSteel"",""StartingMithril"",""StartingMounts"",""TaxRate"",
             ""StartingHeavyCavalry"",""StartingLightCavalry"",""StartingHeavyInfantry"",""StartingLightInfantry"",""StartingArchers"",""StartingMenAtArms"",""StartingMorale"",""StartingTraining"",
             ""HCWeaponRank"",""HCArmourRank"",""LCWeaponRank"",""LCArmourRank"",""HIWeaponRank"",""HIArmourRank"",""LIWeaponRank"",""LIArmourRank"",""ArcherWeaponRank"",""ArcherArmourRank"",""MAAWeaponRank"",""MAAArmourRank"",
             ""CapitalName"",""CapitalSize"",""CapitalFortification"",""CapitalHasHarbour"",""CapitalHasPort"",
             ""BorderTownName"",""BorderTownSize"",""BorderTownFortification"",
             ""Character1Name"",""Character2Name"",""Character3Name"",""Character4Name"",""Character5Name"",""Character6Name"",
             ""RequiresAdmin"",""FixedAllegiance"")
            VALUES
            (@id,@gtId,@name,@alleg,@color,@hex,
             @gold,@food,@timber,@leather,@bronze,@steel,@mithril,@mounts,@tax,
             @hc,@lc,@hi,@li,@arc,@maa,@morale,@training,
             @hcW,@hcA,@lcW,@lcA,@hiW,@hiA,@liW,@liA,@arcW,@arcA,@maaW,@maaA,
             @capName,@capSize,@capFort,@capHarb,@capPort,
             @borderName,@borderSize,@borderFort,
             @c1,@c2,@c3,@c4,@c5,@c6,
             @reqAdmin,@fixedAlleg)";
        await db.Database.ExecuteSqlRawAsync(sql,
            new NpgsqlParameter("id", nt.Id),
            new NpgsqlParameter("gtId", nt.GameTypeId),
            new NpgsqlParameter("name", nt.Name),
            new NpgsqlParameter("alleg", nt.Allegiance),
            new NpgsqlParameter("color", nt.Color),
            new NpgsqlParameter("hex", nt.StartHex),
            new NpgsqlParameter("gold", nt.StartingGold),
            new NpgsqlParameter("food", nt.StartingFood),
            new NpgsqlParameter("timber", nt.StartingTimber),
            new NpgsqlParameter("leather", nt.StartingLeather),
            new NpgsqlParameter("bronze", nt.StartingBronze),
            new NpgsqlParameter("steel", nt.StartingSteel),
            new NpgsqlParameter("mithril", nt.StartingMithril),
            new NpgsqlParameter("mounts", nt.StartingMounts),
            new NpgsqlParameter("tax", nt.TaxRate),
            new NpgsqlParameter("hc", nt.StartingHeavyCavalry),
            new NpgsqlParameter("lc", nt.StartingLightCavalry),
            new NpgsqlParameter("hi", nt.StartingHeavyInfantry),
            new NpgsqlParameter("li", nt.StartingLightInfantry),
            new NpgsqlParameter("arc", nt.StartingArchers),
            new NpgsqlParameter("maa", nt.StartingMenAtArms),
            new NpgsqlParameter("morale", nt.StartingMorale),
            new NpgsqlParameter("training", nt.StartingTraining),
            new NpgsqlParameter("hcW", nt.HCWeaponRank),
            new NpgsqlParameter("hcA", nt.HCArmourRank),
            new NpgsqlParameter("lcW", nt.LCWeaponRank),
            new NpgsqlParameter("lcA", nt.LCArmourRank),
            new NpgsqlParameter("hiW", nt.HIWeaponRank),
            new NpgsqlParameter("hiA", nt.HIArmourRank),
            new NpgsqlParameter("liW", nt.LIWeaponRank),
            new NpgsqlParameter("liA", nt.LIArmourRank),
            new NpgsqlParameter("arcW", nt.ArcherWeaponRank),
            new NpgsqlParameter("arcA", nt.ArcherArmourRank),
            new NpgsqlParameter("maaW", nt.MAAWeaponRank),
            new NpgsqlParameter("maaA", nt.MAAArmourRank),
            new NpgsqlParameter("capName", nt.CapitalName),
            new NpgsqlParameter("capSize", nt.CapitalSize),
            new NpgsqlParameter("capFort", nt.CapitalFortification),
            new NpgsqlParameter("capHarb", nt.CapitalHasHarbour),
            new NpgsqlParameter("capPort", nt.CapitalHasPort),
            new NpgsqlParameter("borderName", nt.BorderTownName),
            new NpgsqlParameter("borderSize", nt.BorderTownSize),
            new NpgsqlParameter("borderFort", nt.BorderTownFortification),
            new NpgsqlParameter("c1", nt.Character1Name),
            new NpgsqlParameter("c2", nt.Character2Name),
            new NpgsqlParameter("c3", nt.Character3Name),
            new NpgsqlParameter("c4", nt.Character4Name),
            new NpgsqlParameter("c5", nt.Character5Name),
            new NpgsqlParameter("c6", nt.Character6Name),
            new NpgsqlParameter("reqAdmin", nt.RequiresAdmin),
            new NpgsqlParameter("fixedAlleg", nt.FixedAllegiance));
    }
}
