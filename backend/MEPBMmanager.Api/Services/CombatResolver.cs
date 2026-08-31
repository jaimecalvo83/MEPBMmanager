using MEPBMmanager.Domain.Entities;

namespace MEPBMmanager.Api.Services;

public class CombatResolver
{
    private readonly Random _rng = new();

    // ══════════════════════════════════════════════════════════════
    //  COMBATE ENTRE EJÉRCITOS
    // ══════════════════════════════════════════════════════════════
    // Valores base de tropas (E-7): Strength, Constitution
    private static readonly (int Str, int Con)[] TroopBase =
        { (16, 16), (8, 8), (10, 10), (5, 5), (6, 2), (2, 2) }; // HC, LC, HI, LI, Archers, Men-at-Arms

    private static int[] TroopCounts(Army a)
        => new[] { a.HeavyCavalry, a.LightCavalry, a.HeavyInfantry, a.LightInfantry, a.Archers, a.MenAtArms };

    private static readonly string[] BestTactic = { "ch", "su", "fl", "hr", "am", "hr" };
    private static readonly string[] WorstTactic = { "am", "am", "su", "ch", "fl", "ch" };
    private static readonly Dictionary<string, string> BeatsTactic = new()
        { { "ch", "fl" }, { "fl", "su" }, { "su", "am" }, { "am", "hr" }, { "hr", "st" }, { "st", "ch" } };

    private static readonly Dictionary<string, int[]> TerrainMod = new()
    {
        { "plains",    new[] { 100, 95, 85, 80, 80, 90 } },
        { "hills",     new[] { 80, 85, 100, 95, 100, 90 } },
        { "forest",    new[] { 70, 80, 95, 100, 100, 85 } },
        { "mountains", new[] { 60, 70, 100, 90, 90, 85 } },
        { "swamp",     new[] { 60, 70, 85, 90, 80, 80 } },
        { "marsh",     new[] { 60, 70, 85, 90, 80, 80 } },
        { "desert",    new[] { 90, 95, 75, 70, 75, 80 } },
        { "rough",     new[] { 75, 80, 95, 90, 90, 85 } },
        { "river",     new[] { 80, 85, 80, 80, 85, 85 } },
        { "coast",     new[] { 95, 95, 80, 80, 85, 90 } },
        { "sea",       new[] { 80, 80, 80, 80, 80, 80 } }
    };

    // Valor defensivo de un centro de población según su tamaño (tabla D, "Defensive Value").
    private static readonly Dictionary<string, int> PcSizeDefValue = new()
    {
        { "camp", 2000 },
        { "village", 4000 },
        { "town", 6000 },
        { "major town", 8000 },
        { "city", 10000 }
    };

    // Valor defensivo de las fortificaciones (tabla D, "Fortification Costs").
    private static readonly Dictionary<string, int> FortDefValue = new()
    {
        { "tower", 2000 },
        { "fort", 6000 },
        { "castle", 10000 },
        { "keep", 16000 },
        { "citadel", 24000 }
    };

    // Degradación de tamaño al ser capturado (una categoría menos).
    private static readonly Dictionary<string, string> PcSizeDowngrade = new()
    {
        { "city", "major town" },
        { "major town", "town" },
        { "town", "village" },
        { "village", "camp" },
        { "camp", "camp" }
    };

    // Hechizos de combate (orden 225). Daño al ejército; en navíos se divide por 100.
    // (tabla resumen M - Spells). offensivos suman fuerza; defensivos la restan al rival.
    private static readonly Dictionary<int, (bool Offensive, int Min, int Max)> CombatSpells = new()
    {
        // Ofensivos
        { 202, (true, 150, 150) }, { 204, (true, 50, 250) }, { 206, (true, 100, 200) },
        { 232, (true, 1000, 1000) }, { 234, (true, 500, 1500) }, { 236, (true, 800, 1200) },
        { 240, (true, 1000, 2000) }, { 208, (true, 250, 250) }, { 210, (true, 100, 400) },
        { 212, (true, 200, 300) }, { 220, (true, 600, 600) }, { 222, (true, 300, 900) },
        { 224, (true, 450, 750) }, { 242, (true, 1250, 2250) }, { 214, (true, 400, 400) },
        { 216, (true, 200, 600) }, { 218, (true, 300, 500) }, { 226, (true, 800, 800) },
        { 228, (true, 400, 1200) }, { 230, (true, 600, 1000) }, { 238, (true, 750, 1750) },
        // Defensivos
        { 102, (false, 500, 500) }, { 106, (false, 1000, 1000) }, { 112, (false, 1750, 1750) },
        { 114, (false, 1500, 2000) }, { 104, (false, 100, 100) }, { 108, (false, 1250, 1250) },
        { 110, (false, 1500, 1500) }, { 116, (false, 1000, 2000) }
    };

    // Efecto de un hechizo de combate (ya dividido por 100 si es navío).
    public (double Offensive, double Defensive) GetSpellEffect(int spellNumber, bool navy)
    {
        if (!CombatSpells.TryGetValue(spellNumber, out var s)) return (0, 0);
        int roll = _rng.Next(s.Min, s.Max + 1);
        double val = navy ? roll / 100.0 : roll;
        return s.Offensive ? (val, 0) : (0, val);
    }

    // Combate ejército vs ejército, ecuación G-2 del reglamento.
    public BattleResult ResolveArmyBattle(Army attacker, Army defender, Game game,
        string? tacticA = null, string? tacticD = null, string terrain = "plains",
        double atkSpellOff = 0, double atkSpellDef = 0, double defSpellOff = 0, double defSpellDef = 0)
    {
        var result = new BattleResult
        {
            AttackerName = attacker.Name,
            DefenderName = defender.Name,
            Terrain = terrain
        };

        int atkInit = GetTotalTroops(attacker);
        int defInit = GetTotalTroops(defender);

        // Artefactos de combate: cada personaje usa el más potente; suman a la fuerza.
        int atkArtifact = ArmyArtifactBonus(attacker, game);
        int defArtifact = ArmyArtifactBonus(defender, game);

        int round = 0;
        while (GetTotalTroops(attacker) > 0 && GetTotalTroops(defender) > 0 && round < 60)
        {
            round++;
            int atkConst = ComputeConstitution(attacker, game);
            int defConst = ComputeConstitution(defender, game);
            int atkStr = ComputeStrength(attacker, defender, game, tacticA, tacticD, terrain);
            int defStr = ComputeStrength(defender, attacker, game, tacticD, tacticA, terrain);

            // Hechizos y artefactos de combate: ofensivos suman a la fuerza; defensivos restan la del rival.
            double atkEff = Math.Max(0, atkStr + atkSpellOff + atkArtifact - defSpellDef);
            double defEff = Math.Max(0, defStr + defSpellOff + defArtifact - atkSpellDef);

            double defLoss = defConst > 0 ? Math.Min(100.0, atkEff / defConst * 100) : 100;
            double atkLoss = atkConst > 0 ? Math.Min(100.0, defEff / atkConst * 100) : 100;
            ApplyConstitutionLoss(defender, defLoss);
            ApplyConstitutionLoss(attacker, atkLoss);
        }

        bool atkAlive = GetTotalTroops(attacker) > 0;
        bool defAlive = GetTotalTroops(defender) > 0;
        if (atkAlive && !defAlive) result.Winner = "attacker";
        else if (defAlive && !atkAlive) result.Winner = "defender";
        else result.Winner = "draw";

        // Desbande si el vencedor queda con menos de 100 tropas
        if (result.Winner == "attacker" && GetTotalTroops(attacker) < 100) ZeroArmy(attacker);
        if (result.Winner == "defender" && GetTotalTroops(defender) < 100) ZeroArmy(defender);

        result.AttackerCasualties = atkInit - GetTotalTroops(attacker);
        result.DefenderCasualties = defInit - GetTotalTroops(defender);
        result.AttackerPower = ComputeStrength(attacker, defender, game, tacticA, tacticD, terrain);
        result.DefenderPower = ComputeStrength(defender, attacker, game, tacticD, tacticA, terrain);

        // Moral
        if (result.Winner == "attacker")
        {
            attacker.Morale = Math.Min(100, attacker.Morale + 10);
            defender.Morale = Math.Max(0, defender.Morale - 20);
        }
        else if (result.Winner == "defender")
        {
            defender.Morale = Math.Min(100, defender.Morale + 10);
            attacker.Morale = Math.Max(0, attacker.Morale - 20);
        }
        else
        {
            attacker.Morale = Math.Max(0, attacker.Morale - 5);
            defender.Morale = Math.Max(0, defender.Morale - 5);
        }

        // Personajes: el comandante vencedor siempre sobrevive; otros pueden caer
        ResolveBattleCharacters(attacker, result.Winner == "attacker", attacker.CommanderId);
        ResolveBattleCharacters(defender, result.Winner == "defender", defender.CommanderId);

        result.Message = result.Winner switch
        {
            "attacker" => $"Attacker {attacker.Name} wins after {round} rounds ({atkInit - GetTotalTroops(attacker)} casualties vs {defInit - GetTotalTroops(defender)}); defender destroyed",
            "defender" => $"Defender {defender.Name} wins after {round} rounds ({defInit - GetTotalTroops(defender)} casualties vs {atkInit - GetTotalTroops(attacker)}); attacker destroyed",
            _ => $"Mutual destruction after {round} rounds (no survivors)"
        };

        if (atkSpellOff != 0 || atkSpellDef != 0 || defSpellOff != 0 || defSpellDef != 0 ||
            atkArtifact != 0 || defArtifact != 0)
            result.Message += $" [spells a+{atkSpellOff:F0}/a-{atkSpellDef:F0} d+{defSpellOff:F0}/d-{defSpellDef:F0}]" +
                              $" [artifacts a+{atkArtifact}/d+{defArtifact}]";
        return result;
    }

    private int ComputeConstitution(Army army, Game game)
    {
        var counts = TroopCounts(army);
        int con = 0;
        for (int i = 0; i < 6; i++)
            con += (int)(counts[i] * TroopBase[i].Con * (1 + army.ArmourRank / 100.0));
        // Bonificación por fortificaciones de un centro del propio país en el hex
        var pc = game.Nations.SelectMany(n => n.PopulationCentres)
            .FirstOrDefault(p => p.NationId == army.NationId && p.LocationHex == army.LocationHex);
        if (pc != null && !string.IsNullOrEmpty(pc.Fortification))
            con += 200 * FortificationLevel(pc.Fortification);
        return con;
    }

    private int ComputeStrength(Army army, Army opponent, Game game, string? tactic, string? oppTactic, string terrain)
        => ComputeStrength(army, opponent?.NationId, game, tactic, oppTactic, terrain);

    private int ComputeStrength(Army army, string? opponentNationId, Game game, string? tactic, string? oppTactic, string terrain)
    {
        var counts = TroopCounts(army);
        int total = 0;
        for (int i = 0; i < 6; i++)
        {
            if (counts[i] <= 0) continue;
            int modPct = ModifierPercent(army, game, i, tactic, terrain);
            total += (int)(counts[i] * TroopBase[i].Str * (1 + modPct / 100.0));
        }
        total += army.WarMachines * 50; // máquinas de guerra
        total = (int)(total * TacticVsTacticMultiplier(tactic, oppTactic));
        total = (int)(total * RelationMultiplier(game, army.NationId, opponentNationId));
        return total;
    }

    private int ModifierPercent(Army army, Game game, int troopIdx, string? tactic, string terrain)
    {
        int training = army.Training;
        int weapon = army.WeaponRank;
        int terrainMod = TerrainMod.TryGetValue(terrain, out var arr) ? arr[troopIdx] : 80;
        int tacticMod = TroopTacticModifier(troopIdx, tactic);
        int cmd = CommanderChallengeRank(army, game);
        int climate = 100;  // modificador de clima de la nación (neutral por defecto)
        int terrNation = 100; // modificador de terreno de la nación (neutral por defecto)
        int morale = army.Morale;
        return (training + weapon + terrainMod + tacticMod + cmd + climate + terrNation + morale) / 8;
    }

    private int TroopTacticModifier(int troopIdx, string? tactic)
    {
        if (tactic == null) return 100;
        if (tactic == BestTactic[troopIdx]) return 115;
        if (tactic == WorstTactic[troopIdx]) return 90;
        return 100;
    }

    private double TacticVsTacticMultiplier(string? a, string? d)
    {
        if (a == null || d == null) return 1.0;
        if (BeatsTactic.TryGetValue(a, out var beats) && beats == d) return 1.10;
        if (BeatsTactic.TryGetValue(d, out var beats2) && beats2 == a) return 0.90;
        return 1.0;
    }

    private double RelationMultiplier(Game game, string nationId, string targetNationId)
    {
        var rel = game.Nations.SelectMany(n => n.Relations)
            .FirstOrDefault(r => r.NationId == nationId && r.TargetNationId == targetNationId);
        if (rel == null) return 1.0;
        return rel.Level switch
        {
            <= -75 => 1.25,
            <= -25 => 1.10,
            < 25 => 1.0,
            < 75 => 0.90,
            _ => 0.75
        };
    }

    private int CommanderChallengeRank(Army army, Game game)
    {
        if (string.IsNullOrEmpty(army.CommanderId)) return 0;
        var c = game.Nations.SelectMany(n => n.Characters).FirstOrDefault(ch => ch.Id == army.CommanderId);
        return c?.ChallengeRank ?? 0;
    }

    private static int FortificationLevel(string fort)
        => fort.ToLower() switch { "tower" => 1, "fort" => 2, "castle" => 3, "keep" => 4, "citadel" => 5, _ => 0 };

    private static void ApplyConstitutionLoss(Army army, double pct)
    {
        if (pct <= 0) return;
        double keep = 1 - pct / 100.0;
        army.HeavyCavalry = (int)(army.HeavyCavalry * keep);
        army.LightCavalry = (int)(army.LightCavalry * keep);
        army.HeavyInfantry = (int)(army.HeavyInfantry * keep);
        army.LightInfantry = (int)(army.LightInfantry * keep);
        army.Archers = (int)(army.Archers * keep);
        army.MenAtArms = (int)(army.MenAtArms * keep);
    }

    private static void ZeroArmy(Army army)
    {
        army.HeavyCavalry = army.LightCavalry = army.HeavyInfantry = 0;
        army.LightInfantry = army.Archers = army.MenAtArms = 0;
        army.WarMachines = 0;
    }

    private void ResolveBattleCharacters(Army army, bool victorious, string? commanderId)
    {
        foreach (var c in army.Characters.Where(c => !c.IsDead).ToList())
        {
            if (victorious)
            {
                if (c.Id == commanderId) continue; // el comandante vencedor siempre sobrevive
                if (_rng.Next(0, 100) < 10) { c.IsDead = true; c.ArmyId = null; }
            }
            else
            {
                if (_rng.Next(0, 100) < 30) { c.IsDead = true; c.ArmyId = null; }
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  ASIEDO DE POBLACIÓN
    // ══════════════════════════════════════════════════════════════
    public SiegeResult ResolveSiege(Army attacker, PopulationCentre defender)
    {
        var result = new SiegeResult
        {
            AttackerName = attacker.Name,
            DefenderName = defender.Name
        };

        var attackerPower = CalculateArmyPower(attacker);
        var fortLevel = defender.Fortification switch
        {
            "fortress" => 4,
            "castle" => 3,
            "walls" => 2,
            "palisade" => 1,
            _ => 0
        };
        var defenderPower = fortLevel * 50 + 200;

        var attackerRoll = _rng.Next(1, 7);
        var defenderRoll = _rng.Next(1, 7);

        var attackerTotal = attackerPower + attackerRoll;
        var defenderTotal = defenderPower + defenderRoll;

        result.AttackerTotal = attackerTotal;
        result.DefenderTotal = defenderTotal;

        if (attackerTotal > defenderTotal)
        {
            result.Success = true;
            result.Message = $"Siege successful! ({attackerTotal} vs {defenderTotal})";

            // Reducir fortificaciones
            var currentLevel = defender.Fortification switch
            {
                "fortress" => 4,
                "castle" => 3,
                "walls" => 2,
                "palisade" => 1,
                _ => 0
            };
            if (currentLevel > 1)
                defender.Fortification = currentLevel switch { 4 => "castle", 3 => "walls", 2 => "palisade", _ => null };
            else
                defender.Fortification = null;

            // Bajas del asaltante
            var casualties = CalculateSiegeCasualties(attacker);
            ApplyCasualties(attacker, casualties);
            result.Casualties = casualties;

            attacker.Morale = Math.Min(100, attacker.Morale + 15);
        }
        else
        {
            result.Success = false;
            result.Message = $"Siege repelled! ({attackerTotal} vs {defenderTotal})";

            // Bajas del asaltante (mayores)
            var casualties = CalculateSiegeCasualties(attacker, 1.5);
            ApplyCasualties(attacker, casualties);
            result.Casualties = casualties;

            attacker.Morale = Math.Max(0, attacker.Morale - 15);
        }

        return result;
    }

    // ══════════════════════════════════════════════════════════════
    //  COMBATE NAVAL (G-4)
    //  Cada barco tiene constitución 3. Fuerza del barco: 1 para
    //  transportes, 2-5 para naves de guerra según la expertise naval
    //  de la nación. Los barcos de guerra se destruyen antes que los
    //  transportes. Factores: rango de mando, táctica, relaciones,
    //  artefactos y hechizos (÷100 en naval).
    // ══════════════════════════════════════════════════════════════
    // Expertise naval (fuerza de nave de guerra 2-5) por nación.
    // Valores aproximados del reglamento; ajustables con la tabla exacta.
    private static readonly Dictionary<string, int> WarshipStrengthByNation = new()
    {
        { "gondor", 5 }, { "dol amroth", 5 }, { "umbar", 5 }, { "corsairs of umbar", 5 },
        { "rohan", 4 }, { "dale", 4 }, { "ardor", 4 }, { "elf", 4 }, { "lothlorien", 4 },
        { "mordor", 3 }, { "isengard", 3 }, { "dunland", 3 }, { "harad", 3 }, { "rhun", 3 },
        { "angmar", 2 }, { "withered heath", 2 }, { "orcs", 2 }
    };

    private static int WarshipStrength(string nationName)
        => WarshipStrengthByNation.TryGetValue((nationName ?? "").ToLower(), out var s) ? s : 3;

    private static int NavyConstitution(Navy navy)
        => 3 * (navy.Warships + navy.Transports);

    private int NavyStrength(Navy navy, string? opponentNationId, Game game, string? tactic, string? oppTactic)
    {
        int ws = WarshipStrength(game.Nations.FirstOrDefault(n => n.Id == navy.NationId)?.Name ?? "");
        int baseStr = navy.Warships * ws + navy.Transports * 1;
        int cmd = CommanderChallengeRankNavy(navy, game);
        int modPct = (cmd + 100 + 100) / 8; // cmd + clima(100) + terreno nación(100); artefactos/hechizos pendientes
        double str = baseStr * (1 + modPct / 100.0);
        str *= TacticVsTacticMultiplier(tactic, oppTactic);
        str *= RelationMultiplier(game, navy.NationId, opponentNationId);
        return (int)str;
    }

    private int CommanderChallengeRankNavy(Navy navy, Game game)
    {
        if (string.IsNullOrEmpty(navy.CommanderId)) return 0;
        var c = game.Nations.SelectMany(n => n.Characters).FirstOrDefault(ch => ch.Id == navy.CommanderId);
        return c?.ChallengeRank ?? 0;
    }

    // Bonus de artefactos de combate: cada personaje usa sólo el más potente a la vez.
    private static int ArtifactBonus(Character c)
    {
        if (c.Artifacts == null) return 0;
        int best = 0;
        foreach (var a in c.Artifacts)
            if ((a.Type ?? "").ToLower() == "combat" && a.Bonus > best) best = a.Bonus;
        return best;
    }

    private static int ArmyArtifactBonus(Army army, Game game)
    {
        int sum = 0;
        foreach (var c in game.Nations.SelectMany(n => n.Characters)
                     .Where(c => (c.ArmyId == army.Id || c.Id == army.CommanderId) && !c.IsDead))
            sum += ArtifactBonus(c);
        return sum;
    }

    private double NavyArtifactBonus(Navy navy, Game game)
    {
        if (string.IsNullOrEmpty(navy.CommanderId)) return 0;
        var c = game.Nations.SelectMany(n => n.Characters).FirstOrDefault(ch => ch.Id == navy.CommanderId);
        if (c == null) return 0;
        return ArtifactBonus(c) / 100.0; // en navíos el efecto se divide por 100
    }

    private static void ApplyNavyLoss(Navy navy, double pct)
    {
        if (pct <= 0) return;
        int total = navy.Warships + navy.Transports;
        if (total <= 0) return;
        int remove = (int)Math.Ceiling(total * pct / 100.0);
        // Los barcos de guerra se destruyen antes que los transportes.
        int fromWarships = Math.Min(navy.Warships, remove);
        navy.Warships -= fromWarships;
        int remaining = remove - fromWarships;
        navy.Transports = Math.Max(0, navy.Transports - remaining);
    }

    private static void ZeroNavy(Navy navy)
    {
        navy.Warships = 0;
        navy.Transports = 0;
    }

    public BattleResult ResolveNavyBattle(Navy attacker, Navy defender, Game game,
        string? tacticA = null, string? tacticD = null,
        double atkSpellOff = 0, double atkSpellDef = 0, double defSpellOff = 0, double defSpellDef = 0)
    {
        var result = new BattleResult
        {
            AttackerName = attacker.Nation?.Name ?? "Navy",
            DefenderName = defender.Nation?.Name ?? "Navy",
            Terrain = "sea"
        };

        int atkInit = attacker.Warships + attacker.Transports;
        int defInit = defender.Warships + defender.Transports;

        // Artefactos de combate en navíos: ÷100 (el comandante usa el más potente).
        double atkArtifactN = NavyArtifactBonus(attacker, game);
        double defArtifactN = NavyArtifactBonus(defender, game);

        int round = 0;
        while ((attacker.Warships + attacker.Transports) > 0 &&
               (defender.Warships + defender.Transports) > 0 && round < 60)
        {
            round++;
            int atkConst = NavyConstitution(attacker);
            int defConst = NavyConstitution(defender);
            int atkStr = NavyStrength(attacker, defender.NationId, game, tacticA, tacticD);
            int defStr = NavyStrength(defender, attacker.NationId, game, tacticD, tacticA);

            // Hechizos y artefactos de combate (ya ÷100 para navíos): ofensivos suman, defensivos restan.
            double atkEff = Math.Max(0, atkStr + atkSpellOff + atkArtifactN - defSpellDef);
            double defEff = Math.Max(0, defStr + defSpellOff + defArtifactN - atkSpellDef);

            double defLoss = defConst > 0 ? Math.Min(100.0, atkEff / defConst * 100) : 100;
            double atkLoss = atkConst > 0 ? Math.Min(100.0, defEff / atkConst * 100) : 100;
            ApplyNavyLoss(defender, defLoss);
            ApplyNavyLoss(attacker, atkLoss);
        }

        bool atkAlive = (attacker.Warships + attacker.Transports) > 0;
        bool defAlive = (defender.Warships + defender.Transports) > 0;
        if (atkAlive && !defAlive) result.Winner = "attacker";
        else if (defAlive && !atkAlive) result.Winner = "defender";
        else result.Winner = "draw";

        result.AttackerCasualties = atkInit - (attacker.Warships + attacker.Transports);
        result.DefenderCasualties = defInit - (defender.Warships + defender.Transports);
        result.AttackerPower = NavyStrength(attacker, defender.NationId, game, tacticA, tacticD);
        result.DefenderPower = NavyStrength(defender, attacker.NationId, game, tacticD, tacticA);

        if (result.Winner == "attacker")
        {
            attacker.Strength = Math.Min(100, attacker.Strength + 5);
            defender.Strength = Math.Max(0, defender.Strength - 10);
        }
        else if (result.Winner == "defender")
        {
            defender.Strength = Math.Min(100, defender.Strength + 5);
            attacker.Strength = Math.Max(0, attacker.Strength - 10);
        }

        // Personajes: comandante vencedor sobrevive; otros pueden caer.
        ResolveNavyCharacters(attacker, game, result.Winner == "attacker", attacker.CommanderId);
        ResolveNavyCharacters(defender, game, result.Winner == "defender", defender.CommanderId);

        result.Message = result.Winner switch
        {
            "attacker" => $"{result.AttackerName} navy wins after {round} rounds ({result.AttackerCasualties} ships lost vs {result.DefenderCasualties}); {result.DefenderName} navy destroyed",
            "defender" => $"{result.DefenderName} navy wins after {round} rounds ({result.DefenderCasualties} ships lost vs {result.AttackerCasualties}); {result.AttackerName} navy destroyed",
            _ => $"Naval draw after {round} rounds ({result.AttackerCasualties}/{result.DefenderCasualties} ships lost)"
        };

        if (atkSpellOff != 0 || atkSpellDef != 0 || defSpellOff != 0 || defSpellDef != 0 ||
            atkArtifactN != 0 || defArtifactN != 0)
            result.Message += $" [spells a+{atkSpellOff:F0}/a-{atkSpellDef:F0} d+{defSpellOff:F0}/d-{defSpellDef:F0}]" +
                              $" [artifacts a+{atkArtifactN:F2}/d+{defArtifactN:F2}]";

        return result;
    }

    private void ResolveNavyCharacters(Navy navy, Game game, bool victorious, string? commanderId)
    {
        // Sólo el comandante está vinculado a la armada en el modelo actual.
        if (string.IsNullOrEmpty(commanderId)) return;
        bool destroyed = (navy.Warships + navy.Transports) <= 0;
        var c = game.Nations.SelectMany(n => n.Characters).FirstOrDefault(ch => ch.Id == commanderId);
        if (c == null || c.IsDead) return;
        if (victorious) return;                       // el comandante vencedor siempre sobrevive
        if (destroyed || _rng.Next(0, 100) < 30) { c.IsDead = true; c.ArmyId = null; }
    }

    // ══════════════════════════════════════════════════════════════
    //  ASALTO A CENTRO DE POBLACIÓN (G-3)
    //  Ecuación del reglamento: las máquinas de guerra reducen las
    //  fortificaciones (200 puntos c/u); el valor defensivo del centro
    //  (tamaño + fortificaciones) inflige daño al ejército; el asalto
    //  triunfa si la fuerza del ejército >= valor defensivo del centro.
    //  mode = "capture" (degrada tamaño y cambia dueño) o "destroy"
    //  (convierte el centro en ruinas sin dueño).
    // ══════════════════════════════════════════════════════════════
    public BattleResult ResolvePopulationCentreAssault(Army army, PopulationCentre pc, Game game,
        string mode, string? tactic = null, string terrain = "plains",
        double atkSpellOff = 0, double atkSpellDef = 0)
    {
        var result = new BattleResult
        {
            AttackerName = army.Name,
            DefenderName = pc.Name,
            Terrain = terrain
        };

        int armyInit = GetTotalTroops(army);

        // Paso 1: máquinas de guerra reducen las fortificaciones (200 puntos por máquina).
        int fortValue = FortDefValue.TryGetValue((pc.Fortification ?? "").ToLower(), out var fv) ? fv : 0;
        int fortDamage = army.WarMachines * 200;
        if (fortDamage >= fortValue)
        {
            pc.Fortification = null;
            fortValue = 0;
        }
        else if (fortValue > 0)
        {
            int remaining = fortValue - fortDamage;
            pc.Fortification = remaining >= 16000 ? "keep"
                              : remaining >= 10000 ? "castle"
                              : remaining >= 6000 ? "fort"
                              : remaining >= 2000 ? "tower"
                              : null;
            fortValue = remaining;
        }

        // Valor defensivo total del centro (tamaño + fortificaciones restantes).
        int sizeValue = PcSizeDefValue.TryGetValue((pc.Size ?? "camp").ToLower(), out var sv) ? sv : 2000;
        int pcDefValue = sizeValue + fortValue;

        // Paso 2-3: constitución y fuerza del ejército asaltante.
        int armyConst = ComputeConstitution(army, game);
        int armyStr = ComputeStrength(army, pc.NationId, game, tactic, null, terrain) + (int)atkSpellOff + ArmyArtifactBonus(army, game);

        // El centro inflige daño = su valor defensivo como % de la constitución del ejército
        // (los hechizos defensivos del asaltante reducen ese daño).
        double loss = armyConst > 0
            ? Math.Min(100.0, Math.Max(0, pcDefValue - atkSpellDef) / armyConst * 100)
            : 100;
        ApplyConstitutionLoss(army, loss);

        int armyFinal = GetTotalTroops(army);
        int armyCasualties = armyInit - armyFinal;
        bool armyDestroyed = armyFinal <= 0;

        string message;
        if (armyDestroyed)
        {
            result.Winner = "defender";
            result.Success = false;
            message = $"Assault failed: army destroyed (lost {armyCasualties}); {pc.Name} undamaged";
            foreach (var c in army.Characters.Where(c => !c.IsDead).ToList())
            {
                c.IsDead = true;
                c.ArmyId = null;
            }
        }
        else if (armyStr >= pcDefValue)
        {
            result.Winner = "attacker";
            result.Success = true;
            if (mode == "destroy")
            {
                pc.NationId = null;
                pc.Size = "ruins";
                message = $"Destroyed {pc.Name}: now unowned ruins (lost {armyCasualties} troops)";
            }
            else
            {
                pc.NationId = army.NationId;
                pc.Size = PcSizeDowngrade.TryGetValue((pc.Size ?? "camp").ToLower(), out var down) ? down : "camp";
                message = $"Captured {pc.Name}: now size {pc.Size} (lost {armyCasualties} troops)";
            }
            army.Morale = Math.Min(100, army.Morale + 10);
            ResolveBattleCharacters(army, true, army.CommanderId);
            pc.IsSieged = true; // el centro sufre las consecuencias del asedio
        }
        else
        {
            result.Winner = "defender";
            result.Success = false;
            message = $"Assault failed: strength {armyStr} < defensive value {pcDefValue} (lost {armyCasualties}); {pc.Name} undamaged";
            army.Morale = Math.Max(0, army.Morale - 10);
            ResolveBattleCharacters(army, false, army.CommanderId);
        }

        if (armyFinal < 100) ZeroArmy(army);

        result.AttackerCasualties = armyCasualties;
        result.DefenderCasualties = 0;
        result.AttackerPower = armyStr;
        result.DefenderPower = pcDefValue;
        result.Message = message;
        return result;
    }

    // ══════════════════════════════════════════════════════════════
    //  DUELO ENTRE PERSONAJES
    // ══════════════════════════════════════════════════════════════
    // Combate personal según reglamento (G-1 / C-4):
    //  - Challenge Rank = 100% de la mayor habilidad (mando/mago) + 25% de cada otra
    //    (agente 75%, emisario 50%) + bono de artefactos de combate (bonus/50).
    //  - Cada ronda: 1d100 + CR (1-5 resta 1d100; 96-100 suma 1d100).
    //    El perdedor de la ronda pierde 1d(margen) de salud. Se repite hasta la muerte.
    public DuelResult ResolveDuel(Character attacker, Character defender)
    {
        var result = new DuelResult
        {
            AttackerName = attacker.Name,
            DefenderName = defender.Name
        };

        var aCR = CalculateChallengeRank(attacker);
        var dCR = CalculateChallengeRank(defender);
        result.AttackerTotal = aCR;
        result.DefenderTotal = dCR;

        var aHealth = attacker.Health;
        var dHealth = defender.Health;
        var rounds = 0;

        while (aHealth > 0 && dHealth > 0)
        {
            rounds++;
            var aTotal = RollChallenge(aCR);
            var dTotal = RollChallenge(dCR);

            if (aTotal > dTotal)
            {
                var margin = aTotal - dTotal;
                dHealth -= _rng.Next(1, margin + 1);
            }
            else if (dTotal > aTotal)
            {
                var margin = dTotal - aTotal;
                aHealth -= _rng.Next(1, margin + 1);
            }
            // empate: sin daño esta ronda
        }

        var attackerWon = aHealth > 0;
        var winner = attackerWon ? attacker : defender;
        var loser = attackerWon ? defender : attacker;
        var loserStartHealth = attackerWon ? defender.Health : attacker.Health;
        var dmg = Math.Max(0, loserStartHealth - (attackerWon ? dHealth : aHealth));

        attacker.Health = Math.Max(0, aHealth);
        defender.Health = Math.Max(0, dHealth);

        result.Winner = attackerWon ? "attacker" : "defender";
        result.Damage = dmg;

        // Recompensa: +1..15 a la mejor habilidad del vencedor; se recalcula su CR
        var gain = _rng.Next(1, 16);
        IncreaseBestSkill(winner, gain);
        winner.ChallengeRank = CalculateChallengeRank(winner);

        if (loser.Health <= 0) loser.IsDead = true;

        // Moral de ejércitos: vencedor +gain, perdedor -gain (si son comandantes)
        ApplyCommanderMorale(winner, gain);
        ApplyCommanderMorale(loser, -gain);

        result.Message = attackerWon
            ? $"{attacker.Name} defeats {defender.Name} (CR {aCR} vs {dCR}, {rounds} rounds); {defender.Name} {(defender.IsDead ? "slain" : "injured")}, -{dmg} health, +{gain} best skill"
            : $"{defender.Name} defeats {attacker.Name} (CR {dCR} vs {aCR}, {rounds} rounds); {attacker.Name} {(attacker.IsDead ? "slain" : "injured")}, -{dmg} health, +{gain} best skill";

        return result;
    }

    // Challenge Rank (reglamento C-4): 100% de la habilidad de mayor challenge
    // rating + 25% de cada otra + bono de artefactos de combate (bonus/50).
    public int CalculateChallengeRank(Character c)
    {
        var cmd = c.CommandSkill;                             // challenge rating 100%
        var mag = c.MageSkill;                                // challenge rating 100%
        var agt = (int)Math.Floor(0.75 * c.AgentSkill);       // challenge rating 75%
        var ems = (int)Math.Floor(0.50 * c.EmissarySkill);    // challenge rating 50%

        var ratings = new[] { cmd, mag, agt, ems };
        var highest = ratings.Max();
        var others = ratings.Sum() - highest;

        var cr = highest + (int)Math.Floor(0.25 * others);

        foreach (var a in c.Artifacts)
            if (IsCombatArtifact(a.Type) && a.Bonus > 0)
                cr += a.Bonus / 50;

        return cr;
    }

    // Tirada de desafío (reglamento G-1 paso 2): 1d100 + CR;
    // 1-5 resta 1d100; 96-100 suma 1d100.
    private int RollChallenge(int challengeRank)
    {
        var r = _rng.Next(1, 101);
        var total = challengeRank + r;
        if (r >= 1 && r <= 5)
            total -= _rng.Next(1, 101);
        else if (r >= 96 && r <= 100)
            total += _rng.Next(1, 101);
        return total;
    }

    public static void IncreaseBestSkill(Character c, int amount)
    {
        var cmdCR = c.CommandSkill;
        var magCR = c.MageSkill;
        var agtCR = (int)Math.Floor(0.75 * c.AgentSkill);
        var emsCR = (int)Math.Floor(0.50 * c.EmissarySkill);

        if (cmdCR >= magCR && cmdCR >= agtCR && cmdCR >= emsCR)
            c.CommandSkill = Math.Min(100, c.CommandSkill + amount);
        else if (magCR >= agtCR && magCR >= emsCR)
            c.MageSkill = Math.Min(100, c.MageSkill + amount);
        else if (agtCR >= emsCR)
            c.AgentSkill = Math.Min(100, c.AgentSkill + amount);
        else
            c.EmissarySkill = Math.Min(100, c.EmissarySkill + amount);
    }

    public static void ApplyCommanderMorale(Character c, int delta)
    {
        if (c.Army != null)
            c.Army.Morale = Math.Max(0, Math.Min(100, c.Army.Morale + delta));
    }

    private static bool IsCombatArtifact(string type)
    {
        return type switch
        {
            "combat" or "weapon" or "armor" or "armour" or "amulet"
                or "sword" or "shield" or "ring" or "helm" or "staff" => true,
            _ => false
        };
    }

    // ══════════════════════════════════════════════════════════════
    //  CÁLCULOS
    // ══════════════════════════════════════════════════════════════
    public int CalculateArmyPower(Army army)
    {
        // Poder base por tipo de tropa
        var cavalryPower = (army.HeavyCavalry * 3) + (army.LightCavalry * 2);
        var infantryPower = (army.HeavyInfantry * 2) + (army.LightInfantry * 1);
        var rangedPower = army.Archers * 2;
        var supportPower = army.MenAtArms * 1;

        // Bonus por máquinas de guerra
        var warMachineBonus = army.WarMachines * 50;

        return cavalryPower + infantryPower + rangedPower + supportPower + warMachineBonus;
    }

    public int GetTotalTroops(Army army)
    {
        return army.HeavyCavalry + army.LightCavalry
             + army.HeavyInfantry + army.LightInfantry
             + army.Archers + army.MenAtArms;
    }

    private (int AttackBonus, int DefenseBonus) GetTerrainBonus(string terrain)
    {
        return terrain switch
        {
            "mountains" => (AttackBonus: -20, DefenseBonus: 40),
            "forest" => (AttackBonus: -10, DefenseBonus: 20),
            "swamp" => (AttackBonus: -30, DefenseBonus: 10),
            "rough" => (AttackBonus: -15, DefenseBonus: 15),
            "plains" => (AttackBonus: 10, DefenseBonus: 0),
            "river" => (AttackBonus: -25, DefenseBonus: 5),
            _ => (AttackBonus: 0, DefenseBonus: 0)
        };
    }

    private int CalculateCasualties(Army army, double ratio, bool isMajorLoser)
    {
        var totalTroops = GetTotalTroops(army);
        if (totalTroops == 0) return 0;

        var basePercent = isMajorLoser ? 0.3 : 0.1;
        var casualtyPercent = Math.Min(0.8, basePercent + ratio * 0.2);
        return (int)(totalTroops * casualtyPercent);
    }

    private int CalculateSiegeCasualties(Army army, double multiplier = 1.0)
    {
        var totalTroops = GetTotalTroops(army);
        if (totalTroops == 0) return 0;

        var casualtyPercent = Math.Min(0.6, 0.2 * multiplier);
        return (int)(totalTroops * casualtyPercent);
    }

    private void ApplyCasualties(Army army, int casualties)
    {
        if (casualties <= 0) return;

        var totalTroops = GetTotalTroops(army);
        if (totalTroops == 0) return;

        var ratio = (double)casualties / totalTroops;

        army.HeavyCavalry = Math.Max(0, army.HeavyCavalry - (int)(army.HeavyCavalry * ratio));
        army.LightCavalry = Math.Max(0, army.LightCavalry - (int)(army.LightCavalry * ratio));
        army.HeavyInfantry = Math.Max(0, army.HeavyInfantry - (int)(army.HeavyInfantry * ratio));
        army.LightInfantry = Math.Max(0, army.LightInfantry - (int)(army.LightInfantry * ratio));
        army.Archers = Math.Max(0, army.Archers - (int)(army.Archers * ratio));
        army.MenAtArms = Math.Max(0, army.MenAtArms - (int)(army.MenAtArms * ratio));
    }
}

// ══════════════════════════════════════════════════════════════
//  MODELOS DE RESULTADO
// ══════════════════════════════════════════════════════════════

public class BattleResult
{
    public string AttackerName { get; set; } = string.Empty;
    public string DefenderName { get; set; } = string.Empty;
    public string Terrain { get; set; } = "plains";
    public string Winner { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int AttackerPower { get; set; }
    public int DefenderPower { get; set; }
    public int AttackerRoll { get; set; }
    public int DefenderRoll { get; set; }
    public int AttackerTotal { get; set; }
    public int DefenderTotal { get; set; }
    public int AttackerCasualties { get; set; }
    public int DefenderCasualties { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class SiegeResult
{
    public string AttackerName { get; set; } = string.Empty;
    public string DefenderName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int AttackerTotal { get; set; }
    public int DefenderTotal { get; set; }
    public int Casualties { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class DuelResult
{
    public string AttackerName { get; set; } = string.Empty;
    public string DefenderName { get; set; } = string.Empty;
    public string Winner { get; set; } = string.Empty;
    public int AttackerTotal { get; set; }
    public int DefenderTotal { get; set; }
    public int Damage { get; set; }
    public string Message { get; set; } = string.Empty;
}
