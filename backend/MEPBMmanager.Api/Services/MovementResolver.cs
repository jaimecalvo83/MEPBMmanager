using MEPBMmanager.Domain.Entities;

namespace MEPBMmanager.Api.Services;

// Resolución de movimiento fiel al reglamento (cap. E – Armies / Movement / Encounters).
//  • Presupuesto de 14 puntos de movimiento por turno.
//  • Coste por terreno según tabla del libro (infantería/mixta vs caballería sola).
//  • Sin comida: +1/3 redondeado al alza. Evasivo: x2 (tras sin comida).
//  • Montaña→montaña sólo con carretera (datos de carretera ausentes => se prohibe).
//  • Parada forzosa al entrar en hex de nación no tolerante/amistosa (salvo evasivo).
//  • Encuentros: probabilidad por hex recorrido; se resuelven con órdenes 285/290.
// NOTA: carreteras, ríos y puentes no están modelados en HexTile (sólo Terrain), por lo
// que se usan los costes "normal" (sin carretera) y se ignoran puentes; la estructura
// está preparada para leerlos si se añaden columnas al mapa.
public class MovementResolver
{
    private readonly Random _rng;

    // (infantería/mixta normal, con carretera, caballería sola normal, con carretera)
    private static readonly Dictionary<string, (int Inf, int InfRoad, int Cav, int CavRoad)> ArmyCost = new()
    {
        ["shore"] = (3, 2, 2, 1),
        ["plains"] = (3, 2, 2, 1),
        ["rough"] = (5, 3, 3, 1),
        ["forest"] = (5, 3, 5, 2),
        ["desert"] = (4, 2, 2, 1),
        ["swamp"] = (6, 3, 5, 2),
        ["mountains"] = (12, 6, 12, 3),
    };

    // Probabilidad base de encuentro por terreno (simplificado del cap. Encuentros).
    private static readonly Dictionary<string, int> EncounterChance = new()
    {
        ["plains"] = 2, ["shore"] = 2, ["desert"] = 4,
        ["rough"] = 6, ["forest"] = 6, ["swamp"] = 8, ["mountains"] = 8,
    };

    private const int Budget = 14;

    public MovementResolver(Random? rng = null) => _rng = rng ?? new Random();

    public record MoveResult(
        string FinalHex,
        int PointsSpent,
        int PointsBudget,
        bool ReachedDestination,
        List<string> Path,
        List<Encounter> Encounters,
        string Message);

    // ── Coste de entrar en un hex ─────────────────────────────────────────────
    private int EnterCost(string terrain, bool cavalryOnly, bool evasive, bool unfed)
    {
        if (!ArmyCost.TryGetValue(terrain, out var c))
            c = ArmyCost["plains"]; // terreno desconocido => llanura
        int baseCost = cavalryOnly ? c.Cav : c.Inf;

        if (unfed)
            baseCost = (int)Math.Ceiling(baseCost * 4.0 / 3);
        if (evasive)
            baseCost *= 2;

        return baseCost;
    }

    private static (int Q, int R) Parse(string hex) => hex.Split(',') is var p && p.Length == 2
        ? (int.Parse(p[0]), int.Parse(p[1])) : (0, 0);

    private static readonly (int Dq, int Dr)[] Neighbors =
        [(1, 0), (1, -1), (0, -1), (-1, 0), (-1, 1), (0, 1)];

    private bool IsNavigable(string terrain) =>
        terrain == "coastal waters" || terrain == "coastal" || terrain == "sea" ||
        terrain == "ocean" || terrain == "shore" || terrain == "major river" || terrain == "minor river";

    // ── Dijkstra sobre la rejilla de hexágonos ───────────────────────────────
    private List<HexTile>? ShortestPath(List<HexTile> hexes, HexTile start, HexTile goal,
        bool navy, bool cavalryOnly, bool evasive, bool unfed)
    {
        var byKey = new Dictionary<(int, int), HexTile>();
        foreach (var h in hexes) byKey[(h.Q, h.R)] = h;

        var dist = new Dictionary<(int, int), int>();
        var prev = new Dictionary<(int, int), (int, int)>();
        var visited = new HashSet<(int, int)>();
        var startKey = (start.Q, start.R);
        var goalKey = (goal.Q, goal.R);
        dist[startKey] = 0;

        while (true)
        {
            (int, int)? u = null;
            int best = int.MaxValue;
            foreach (var kv in dist)
                if (!visited.Contains(kv.Key) && kv.Value < best) { best = kv.Value; u = kv.Key; }
            if (u == null) break;
            if (u == goalKey) break;
            visited.Add(u.Value);

            foreach (var (dq, dr) in Neighbors)
            {
                var nk = (u.Value.Item1 + dq, u.Value.Item2 + dr);
                if (!byKey.TryGetValue(nk, out var nb)) continue;
                if (navy && !IsNavigable(nb.Terrain)) continue;
                if (!navy && nb.Terrain == "mountains" && byKey[u.Value].Terrain == "mountains")
                    continue; // montaña→montaña sólo con carretera (ausente)
                int w = navy ? 1 : EnterCost(nb.Terrain, cavalryOnly, evasive, unfed);
                int nd = dist[u.Value] + w;
                if (!dist.ContainsKey(nk) || nd < dist[nk]) { dist[nk] = nd; prev[nk] = u.Value; }
            }
        }

        if (!prev.ContainsKey(goalKey) && goalKey != startKey) return null;
        var path = new List<HexTile>();
        var cur = goalKey;
        while (cur != startKey) { path.Add(byKey[cur]); if (!prev.TryGetValue(cur, out cur)) return null; }
        path.Add(start);
        path.Reverse();
        return path;
    }

    // Camina el camino acumulando coste hasta agotar el presupuesto; aplica parada
    // forzosa ante ejército/PC enemiga no tolerada (salvo evasivo).
    private MoveResult Walk(Army? army, Character? character, string moverNationId, string destHex, Game game,
        List<HexTile> hexes, bool navy, bool evasive, bool forceMarch, bool unfed, bool cavalryOnly)
    {
        var startHex = Parse(army?.LocationHex ?? character!.LocationHex!);
        var goalHex = Parse(destHex);
        var start = hexes.FirstOrDefault(h => h.Q == startHex.Q && h.R == startHex.R);
        var goal = hexes.FirstOrDefault(h => h.Q == goalHex.Q && h.R == goalHex.R);
        if (start == null || goal == null)
            return new(null!, 0, Budget, false, new(), new(), "Invalid start or destination hex") { FinalHex = army?.LocationHex ?? character!.LocationHex! };

        var path = ShortestPath(hexes, start, goal, navy, cavalryOnly, evasive, unfed);
        if (path == null)
            return new(army?.LocationHex ?? character!.LocationHex!, 0, Budget, false, new(), new(),
                navy ? "No sea route to destination" : "No route to destination") { FinalHex = army?.LocationHex ?? character!.LocationHex! };

        var visitedHexes = new List<string>();
        var encounters = new List<Encounter>();
        int spent = 0;
        string final = start.LocationHex;

        for (int i = 1; i < path.Count; i++)
        {
            var h = path[i];
            int cost = navy ? 1 : EnterCost(h.Terrain, cavalryOnly, evasive, unfed);
            if (spent + cost > Budget) break; // sin presupuesto para este hex

            // Parada forzosa ante ejército/PC enemiga no tolerada (salvo evasivo o force march)
            if (!navy && !evasive && !forceMarch && BlocksMovement(game, h, moverNationId))
            {
                final = h.LocationHex;
                spent += cost;
                visitedHexes.Add(final);
                GenerateEncounter(encounters, game, h, character, army);
                break;
            }

            spent += cost;
            final = h.LocationHex;
            visitedHexes.Add(final);
            GenerateEncounter(encounters, game, h, character, army);
        }

        bool reached = final == goal.LocationHex;
        return new MoveResult(final, spent, Budget, reached, visitedHexes, encounters,
            reached ? $"Reached {final} ({spent}/{Budget} MP)" : $"Moved to {final} ({spent}/{Budget} MP), destination unreached");
    }

    private bool BlocksMovement(Game game, HexTile hex, string moverNationId)
    {
        foreach (var n in game.Nations)
        {
            if (n.Id == moverNationId) continue;
            if (IsTolerated(n.Id, moverNationId, game)) continue;
            if (n.Armies.Any(a => a.LocationHex == hex.LocationHex)) return true;
            if (n.PopulationCentres.Any(p => p.LocationHex == hex.LocationHex && !string.IsNullOrEmpty(p.Fortification))) return true;
        }
        return false;
    }

    // Relación tolerante/amistosa: Level > 0 (reglamento: friendly/tolerant no fuerza parada).
    private bool IsTolerated(string nationId, string targetId, Game game)
    {
        var rel = game.Nations.FirstOrDefault(n => n.Id == nationId)?.Relations
            .FirstOrDefault(r => r.TargetNationId == targetId);
        return (rel?.Level ?? 0) > 0;
    }

    private void GenerateEncounter(List<Encounter> outList, Game game, HexTile hex,
        Character? character, Army? army)
    {
        if (!EncounterChance.TryGetValue(hex.Terrain, out int chance)) chance = 2;
        if (_rng.Next(0, 100) >= chance) return;
        var type = _rng.Next(0, 5) switch
        {
            0 => "creature", 1 => "npc", 2 => "character", 3 => "artifact", _ => "lore"
        };
        outList.Add(new Encounter
        {
            Id = Guid.NewGuid().ToString(),
            GameId = game.Id,
            LocationHex = hex.LocationHex,
            CharacterId = character?.Id,
            ArmyId = army?.Id,
            Type = type,
            Description = $"A {type} encounter in {hex.LocationHex} ({hex.Terrain}).",
        });
    }

    // ── Públicos ──────────────────────────────────────────────────────────────
    public MoveResult MoveArmy(Army army, Character? commander, string destHex, Game game,
        List<HexTile> hexes, bool evasive, bool forceMarch)
    {
        bool cavalryOnly = (army.HeavyCavalry + army.LightCavalry) > 0 &&
                           (army.HeavyInfantry + army.LightInfantry + army.Archers + army.MenAtArms) == 0;
        bool unfed = army.Food <= 0;
        var r = Walk(army, null, army.NationId, destHex, game, hexes, navy: false, evasive, forceMarch, unfed, cavalryOnly);
        army.LocationHex = r.FinalHex;
        if (commander != null) commander.LocationHex = r.FinalHex;
        if (unfed) army.Morale = Math.Max(0, army.Morale - _rng.Next(1, 6));
        return r;
    }

    public MoveResult MoveCharacter(Character ch, string destHex, Game game, List<HexTile> hexes, bool evasive)
    {
        var r = Walk(null, ch, ch.NationId, destHex, game, hexes, navy: false, evasive, forceMarch: false, unfed: false, cavalryOnly: false);
        ch.LocationHex = r.FinalHex;
        return r;
    }

    public MoveResult MoveCompany(Company co, string destHex, Game game, List<HexTile> hexes)
    {
        var r = Walk(null, null, co.NationId, destHex, game, hexes, navy: false, evasive: false, forceMarch: false, unfed: false, cavalryOnly: false);
        co.LocationHex = r.FinalHex;
        return r;
    }

    public MoveResult MoveNavy(Navy navy, string destHex, Game game, List<HexTile> hexes)
    {
        var r = Walk(null, null, navy.NationId, destHex, game, hexes, navy: true, evasive: false, forceMarch: false, unfed: false, cavalryOnly: false);
        navy.LocationHex = r.FinalHex;
        return r;
    }
}
