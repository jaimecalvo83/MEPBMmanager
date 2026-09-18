using System.Text.Json;
using MEPBMmanager.Api.Services;
using MEPBMmanager.Domain.Constants;
using MEPBMmanager.Domain.Entities;
using MEPBMmanager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MEPBMmanager.Api.Orders;

/// <summary>Validation rules, one small method per order family.</summary>
public sealed partial class OrderEstimateService
{
    public void ValidateEstimateParams(int code, EstimateScope scope, IReadOnlyList<OrderFieldSpecDto> required)
    {
        ValidateRequiredFields(required, scope);
        ValidateForceMembership(code, scope);
        ValidateCharacterTargets(code, scope);
        ValidateNavyAndCapital(code, scope);
        ValidateHeldArtifacts(code, scope);
        ValidateArmyTransfers(code, scope);
        ValidateArtifactTransfers(code, scope);
        ValidateUpgrades(code, scope);
        ValidateInfluence(code, scope);
        ValidateConstruction(code, scope);
        ValidateRansomAndSabotage(code, scope);
        ValidateTheft(code, scope);
        ValidateArtifactCounts(code, scope);
        ValidateResearch(code, scope);
        ValidateNaming(code, scope);
        ValidateCompanies(code, scope);
        ValidateCommand(code, scope);
        ValidateScouting(code, scope);
        ValidateLore(code, scope);
        ValidateOwnership(code, scope);
        ValidateRing(code, scope);
        ValidateAllegiance(code, scope);
        ValidateSpellCasting(code, scope);
        ValidateDiplomacy(code, scope);
        ValidateMarket(code, scope);
        ValidateTerrainWorks(code, scope);
    }

    private static void ValidateRequiredFields(IReadOnlyList<OrderFieldSpecDto> required, EstimateScope scope)
    {
        foreach (var field in required.Where(f => f.Required))
        {
            if (!scope.Parameters.TryGetValue(field.Key, out var el) || el.ValueKind == JsonValueKind.Null
                || (el.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(el.GetString()))
                || (el.ValueKind == JsonValueKind.Array && !el.EnumerateArray().Any()))
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.missing-info") + field.Label);
        }
    }

    private static void ValidateForceMembership(int code, EstimateScope scope)
    {
        if (code is 750 or 760 && scope.Character.CompanyId == null)
            scope.Errors.Add(OrderTexts.Get(scope.Lang, "reason.not-in-a-company"));
        if (code == 790 && scope.Character.ArmyId == null)
            scope.Errors.Add(OrderTexts.Get(scope.Lang, "reason.not-in-an-army"));
    }

    private static void ValidateCharacterTargets(int code, EstimateScope scope)
    {
        if (code is 210 or 615 or 620)
        {
            var target = scope.FindCharacter(scope.Text("targetId"));
            if (target == null)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.target-character-not-found"));
            else
            {
                if (target.LocationHex != scope.EffectiveLocation)
                    scope.Errors.Add(OrderTexts.Format(scope.Lang, "err.target-not-in-the-same-hex-at-t-locationhex", target.LocationHex));
                if (code == 615 && target.NationId == scope.Nation.Id)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.target-must-be-of-a-different-nation"));
                if (code == 615 && target.IsKidnapped)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.target-is-a-hostage"));
                if (code == 620 && (target.IsDead || target.IsKidnapped))
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.target-cannot-be-kidnapped"));
                if (code == 620 && target.NationId == scope.Nation.Id)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.target-must-be-of-a-different-nation"));
            }
        }
        if (code is 625 or 630 or 635 or 640 or 645 or 650 or 655)
        {
            var target = scope.FindCharacter(scope.Text("targetId"));
            if (target == null)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.target-not-found"));
            else
            {
                if (!target.IsKidnapped)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.target-is-not-a-hostage"));
                if (target.LocationHex != scope.EffectiveLocation)
                    scope.Errors.Add(OrderTexts.Format(scope.Lang, "err.target-not-in-the-same-hex-at-t-locationhex", target.LocationHex));
            }
        }
        if (code == 363)
        {
            var target = scope.FindCharacter(scope.Text("targetId"));
            if (scope.Text("targetId") != null && target == null)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.target-not-found"));
            else if (target != null)
            {
                if (!target.IsKidnapped)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.target-is-not-a-hostage"));
                if (target.LocationHex != scope.EffectiveLocation)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.target-must-be-at-the-same-location"));
            }
        }
        if (code == 610)
        {
            var target = scope.FindCharacter(scope.Text("targetId"));
            if (scope.Text("targetId") != null && (target == null || target.IsDead))
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.target-not-found"));
            else if (target != null)
            {
                if (target.Id == scope.Character.Id)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.cannot-guard-yourself"));
                if (target.LocationHex != scope.EffectiveLocation)
                    scope.Errors.Add(OrderTexts.Format(scope.Lang, "err.target-not-in-the-same-hex-at-t-locationhex", target.LocationHex));
            }
        }
    }

    private static void ValidateNavyAndCapital(int code, EstimateScope scope)
    {
        if (code == 275 && !scope.Nation.Navies.Any())
            scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.no-navy"));
        if (code == 280)
        {
            if (!scope.AtCapital)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "reason.must-be-at-your-own-capital"));
            if (!scope.Nation.Navies.Any())
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.no-navy"));
        }
        if (code is 300 or 325 && !scope.AtCapital)
            scope.Errors.Add(OrderTexts.Get(scope.Lang, "reason.must-be-at-your-own-capital"));
    }

    private static void ValidateHeldArtifacts(int code, EstimateScope scope)
    {
        if (code is not (205 or 805 or 935 or 945)) return;
        var wantedTypes = code switch
        {
            205 => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Sword", "Weapon", "Bow", "Mace", "Scimitar", "Hammer", "Lance", "Club", "Flail", "Axe", "Bola", "Spear" },
            805 => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Boots" },
            935 => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Mirror", "Orb", "Sphere" },
            _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Cloak", "Robes" },
        };
        var kind = code == 205 ? "combat" : code == 805 ? "movement" : code == 935 ? "scrying" : "hiding";
        var artifactId = scope.Text("artifactId");
        var artifact = scope.FindArtifact(artifactId);
        if (artifactId != null && artifact == null)
            scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.artifact-not-found"));
        else if (artifact != null)
        {
            if (artifact.HeldByCharacterId != scope.Character.Id)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.artifact-not-held-by-character"));
            if (!wantedTypes.Contains(artifact.Type ?? ""))
                scope.Errors.Add(OrderTexts.Format(scope.Lang, "err.a-name-is-not-a-kind-artifact", artifact.Name, kind));
            if (!scope.ArtifactUsableByNation(artifact))
                scope.Errors.Add(OrderTexts.Format(scope.Lang, "err.a-name-alignment-does-not-match-your-allegiance", artifact.Name));
        }
    }

    private static void ValidateArmyTransfers(int code, EstimateScope scope)
    {
        if (code is not (347 or 349 or 351 or 353 or 355)) return;
        var source = scope.SourceArmy();
        if (source == null)
        {
            scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.no-source-army"));
            return;
        }
        var destId = scope.Text("targetArmyId") ?? scope.Text("destArmyId");
        var dest = scope.Game.Nations.SelectMany(x => x.Armies).FirstOrDefault(a => a.Id == destId);
        if (dest == null)
            scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.dest-army-not-found"));
        else
        {
            if (dest.LocationHex != source.LocationHex)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.both-armies-must-be-in-the-same-hex"));
            if (!scope.IsSameOrFriendlyNation(scope.Nation.Id, dest.NationId))
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.dest-army-must-be-of-the-same-or-a-friendly-nati"));
        }
    }

    private static void ValidateArtifactTransfers(int code, EstimateScope scope)
    {
        if (code == 360)
        {
            if (scope.IdList("artifactId").Count == 0)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.missing-artifactid-s"));
            var target = scope.FindCharacter(scope.Text("targetId"));
            if (scope.Text("targetId") != null && (target == null || target.IsKidnapped))
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.target-not-found-or-is-a-hostage"));
            else if (target != null && target.LocationHex != scope.EffectiveLocation)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.both-characters-must-be-at-the-same-location"));
        }
    }

    private static void ValidateArtifactCounts(int code, EstimateScope scope)
    {
        if (code is 792 or 796)
        {
            var count = scope.IdList("artifactId").Count;
            if (count < 1 || count > 6)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.give-1-6-artifactid-s"));
        }
        if (code == 700)
        {
            var count = scope.IdList("spellId").Count;
            if (count < 1 || count > 6)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.give-1-6-spellid-s-to-forget"));
        }
    }

    private static void ValidateUpgrades(int code, EstimateScope scope)
    {
        if (code is 370 or 375)
        {
            var army = scope.SourceArmy();
            if (army == null)
            {
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.no-army-specified"));
                return;
            }
            var material = (scope.Text("material") ?? "").ToLower();
            if (material != "leather" && material != "bronze" && material != "steel" && material != "mithril")
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.material-must-be-leather-bronze-steel-or-mithril"));
            var wanted = TroopCodes.Where(p => scope.Number(p.Short) > 0).Select(p => p.Full).ToList();
            if (wanted.Count == 0)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.specify-troops-per-type-hc-lc-hi-li-ar-ma"));
            var missing = wanted.Where(t => EstimateScope.CountTroopsOfType(army, t) <= 0).ToList();
            if (missing.Count > 0)
                scope.Errors.Add(OrderTexts.Format(scope.Lang, "err.no-troops-in-army", string.Join(", ", missing), army.Name));
        }
        if (code is 400 or 404 or 408 or 412)
        {
            var weapons = (scope.Text("weapons") ?? "").ToLower();
            if (weapons != "bronze" && weapons != "steel" && weapons != "mithril")
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.weapon-material-must-be-bronze-steel-or-mithril"));
        }
        if (code == 425)
        {
            if (scope.SourceArmy() == null)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.no-army"));
            else if (TroopCodes.Sum(p => scope.Number(p.Short)) <= 0)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.no-troops-retired-amounts-per-type-hc-lc-hi-li-a"));
        }
        if (code == 444 && new[] { "leather", "bronze", "steel", "mithril" }.All(m => m != (scope.Text("material") ?? "").ToLower()))
            scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.material-must-be-leather-bronze-steel-or-mithril"));
        if (code == 448 && new[] { "bronze", "steel", "mithril" }.All(m => m != (scope.Text("material") ?? "").ToLower()))
            scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.material-must-be-bronze-steel-or-mithril"));
    }

    private static void ValidateInfluence(int code, EstimateScope scope)
    {
        if (code == 520)
        {
            if (scope.OwnPopulationAt(scope.EffectiveLocation) == null)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "reason.must-be-at-one-of-your-population-centres"));
            if (scope.Character.EmissarySkill <= 0)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.needs-emissary-skill"));
        }
        if (code == 525)
        {
            if (scope.Character.EmissarySkill <= 0)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.needs-emissary-skill"));
            var pc = scope.PopulationAt(scope.EffectiveLocation);
            if (pc == null)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.no-population-centre-here"));
            else
            {
                if (pc.NationId == scope.Nation.Id)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.use-520-on-your-own-centres"));
                if (pc.IsHidden)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.no-visible-population-centre-here"));
                var back = scope.Game.Nations.FirstOrDefault(x => x.Id == pc.NationId)?.Relations
                    .FirstOrDefault(r => r.TargetNationId == scope.Nation.Id)?.Level <= -1;
                if (back) scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.enemy-forces-present"));
            }
        }
    }

    private static void ValidateConstruction(int code, EstimateScope scope)
    {
        if (code is 530 or 535 or 550)
        {
            var pc = scope.OwnPopulationAt(scope.EffectiveLocation);
            if (pc == null)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "reason.must-be-at-one-of-your-population-centres"));
            else
            {
                if (code == 530 && (!pc.HasHarbour || pc.HasPort))
                    scope.Errors.Add(OrderTexts.Format(scope.Lang, "err.pc-name-needs-a-harbour-not-yet-a-port", pc.Name));
                if (code == 530 && pc.Size != "major town" && pc.Size != "city")
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.only-major-towns-and-cities-can-have-ports"));
                if (code == 535 && (pc.HasHarbour || pc.HasPort))
                    scope.Errors.Add(OrderTexts.Format(scope.Lang, "err.pc-name-already-has-a-harbour-or-port", pc.Name));
                if (code == 535 && pc.Size != "town" && pc.Size != "major town" && pc.Size != "city")
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.only-towns-and-larger-can-have-harbours"));
                if (code == 550 && (pc.Size == "city" || pc.Size == "citadel"))
                    scope.Errors.Add(OrderTexts.Format(scope.Lang, "err.pc-name-cannot-be-improved-further", pc.Name));
            }
            if (scope.HasEnemyForces(scope.EffectiveLocation))
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.enemy-forces-present"));
        }
        if (code is 552 or 555)
        {
            if (!scope.IsLandHex(scope.EffectiveLocation))
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.camps-need-a-land-hex"));
            if (scope.OwnPopulationAt(scope.EffectiveLocation) != null)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.already-have-a-population-centre-at-this-hex"));
            if (scope.HasEnemyForces(scope.EffectiveLocation))
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.enemy-forces-present"));
        }
        if (code == 565)
        {
            var pc = scope.PopulationAt(scope.Text("hex") ?? scope.EffectiveLocation);
            if (pc == null)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.no-population-centre-at-location"));
            else
            {
                if (pc.NationId != scope.Nation.Id)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.can-only-reduce-your-own-population-centres"));
                if (pc.Size.ToLower() == "camp")
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.camps-cannot-be-reduced-further-abandon-them"));
            }
        }
        if (code == 605)
        {
            var pc = scope.PopulationAt(scope.Text("hex") ?? scope.EffectiveLocation);
            if (pc != null && pc.NationId != scope.Nation.Id && pc.IsHidden)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.no-visible-population-centre-here"));
        }
        if (code == 494)
        {
            var pc = scope.Text("pcId") != null
                ? scope.Nation.PopulationCentres.FirstOrDefault(p => p.Id == scope.Text("pcId"))
                : scope.OwnPopulationAt(scope.Text("hex") ?? scope.EffectiveLocation);
            if (pc == null)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.must-target-one-of-your-population-centres"));
        }
        if (code == 560 && !scope.Nation.PopulationCentres.Any(p => p.LocationHex == scope.EffectiveLocation && p.Size == "camp"))
            scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.no-camp-of-yours-here"));
    }

    private static void ValidateRansomAndSabotage(int code, EstimateScope scope)
    {
        if (code == 660)
        {
            if (!scope.AtCapital)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "reason.must-be-at-your-own-capital"));
            var target = scope.FindCharacter(scope.Text("targetId"));
            if (scope.Text("targetId") != null && (target == null || !target.IsKidnapped))
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.target-not-found-or-not-kidnapped"));
        }
        if (code is 670 or 675 or 680)
        {
            var pc = scope.PopulationAt(scope.Text("hex") ?? scope.EffectiveLocation);
            if (pc == null)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.no-population-centre"));
            else
            {
                if (pc.NationId == scope.Nation.Id)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.target-must-be-of-a-different-nation"));
                if (pc.IsHidden)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.no-visible-population-centre-here"));
                if (code == 670 && string.IsNullOrEmpty(pc.Fortification))
                    scope.Errors.Add(OrderTexts.Format(scope.Lang, "err.pc-name-has-no-fortifications", pc.Name));
                if (code == 675 && !pc.HasHarbour && !pc.HasPort)
                    scope.Errors.Add(OrderTexts.Format(scope.Lang, "err.pc-name-has-no-harbour-or-port", pc.Name));
                if (code == 680)
                {
                    var store = (scope.Text("store") ?? "timber").ToLower();
                    if (store != "timber" && store != "food" && store != "mounts" && store != "leather" && store != "bronze" && store != "steel" && store != "mithril")
                        scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.store-must-be-timber-food-mounts-leather-bronze"));
                }
            }
        }
    }

    private static void ValidateTheft(int code, EstimateScope scope)
    {
        if (code == 685)
        {
            var artifactId = scope.Text("artifactId");
            var artifact = scope.FindArtifact(artifactId);
            if (artifact == null)
            {
                if (artifactId != null)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.artifact-not-found"));
                return;
            }
            if (artifact.HeldByCharacterId == scope.Character.Id)
            {
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.you-already-hold-it"));
                return;
            }
            if (artifact.HeldByCharacterId != null)
            {
                var holder = scope.FindCharacter(artifact.HeldByCharacterId);
                if (holder == null)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.holder-not-found"));
                else
                {
                    if (holder.NationId == scope.Nation.Id)
                        scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.target-must-belong-to-another-nation"));
                    if (holder.LocationHex != scope.EffectiveLocation)
                        scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.artifact-must-be-at-the-same-hex"));
                }
                return;
            }
            if (artifact.LocationHex != scope.EffectiveLocation)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.artifact-must-be-at-the-same-hex"));
            else
            {
                var pc = scope.PopulationAt(artifact.LocationHex);
                if (pc != null && (pc.IsHidden || pc.NationId == scope.Nation.Id))
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.artifact-must-lie-in-a-visible-foreign-populatio"));
            }
        }
        if (code == 690)
        {
            var victim = scope.Game.Nations.FirstOrDefault(x => x.Id == scope.Text("nationId"));
            if (victim == null)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.victim-nation-not-found"));
            else
            {
                if (victim.Id == scope.Nation.Id)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.target-must-be-of-a-different-nation"));
                var pc = scope.Game.Nations.SelectMany(x => x.PopulationCentres)
                    .FirstOrDefault(x => x.LocationHex == scope.EffectiveLocation && x.NationId == victim.Id);
                if (pc == null || pc.IsHidden)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.need-a-visible-foreign-population-centre-at-your"));
            }
        }
    }

    private static void ValidateResearch(int code, EstimateScope scope)
    {
        if (code == 705)
        {
            if (scope.OwnPopulationAt(scope.EffectiveLocation) == null)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.must-be-at-one-of-your-population-centres-to-res"));
            if (scope.Character.Spells.Count(s => s.IsKnown && !s.IsLost) >= 15)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.already-knows-15-spells"));
            if (scope.Parameters.TryGetValue("spellId", out var spellEl) && spellEl.ValueKind == JsonValueKind.Number)
            {
                var spell = SpellCatalog.Get(spellEl.GetInt32());
                if (spell == null)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.unknown-spell"));
                else if (spell.IsLost && !NationAbilities.CanLearnLostSpell(scope.Nation.Name, spell.Id))
                    scope.Errors.Add(OrderTexts.Format(scope.Lang, "err.lost-spell-sd-name-is-not-available-to-your-nati", spell.Name));
            }
        }
        if (code == 710 && scope.OwnPopulationAt(scope.EffectiveLocation) == null)
            scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.must-be-at-one-of-your-population-centres-to-tra"));
    }

    private static void ValidateNaming(int code, EstimateScope scope)
    {
        if (code is 725 or 728 or 731 or 734 or 737)
        {
            var name = (scope.Text("name") ?? "").Trim();
            if (name.Length < 5 || name.Length > 17)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.name-must-be-5-17-letters"));
            if (!scope.AtCapital)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "reason.must-be-at-your-own-capital"));
        }
        if (code == 740 && scope.Character.IsKidnapped)
            scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.cannot-retire-a-hostage"));
    }

    private static void ValidateCompanies(int code, EstimateScope scope)
    {
        if (code == 745)
        {
            if (scope.CommandsForce(scope.Character))
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.character-already-commands-a-force"));
            if (!scope.IsLandHex(scope.EffectiveLocation))
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.must-be-on-land"));
        }
        if (code == 755)
        {
            var commanderId = scope.Text("commanderId");
            var commander = scope.FindCharacter(commanderId);
            if (commanderId != null && (commander == null || commander.CompanyId == null))
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.commander-has-no-company"));
            else if (commander?.CompanyId != null)
            {
                var company = scope.FindCompany(commander.CompanyId);
                if (company == null)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.company-not-found"));
                else
                {
                    if (scope.CountCompanyMembers(company.Id) >= 9)
                        scope.Errors.Add(OrderTexts.Format(scope.Lang, "err.company-comp-name-is-full-9-members", company.Name));
                    if (company.NationId != scope.Nation.Id && !scope.IsSameOrFriendlyNation(scope.Nation.Id, company.NationId))
                        scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.company-must-be-of-the-same-or-a-friendly-nation"));
                }
            }
        }
        if (code == 755 && scope.Text("companyId") != null && !scope.OwnCompanyExists(scope.Text("companyId")))
            scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.company-not-found"));
    }

    private static void ValidateCommand(int code, EstimateScope scope)
    {
        if (code == 765)
        {
            var army = scope.SourceArmy();
            if (army == null)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.no-army-to-split"));
            var commanderId = scope.Text("commanderId");
            var commander = scope.FindCharacter(commanderId);
            if (commanderId != null && (commander == null || commander.IsDead))
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.new-commander-not-found"));
            else if (commander != null)
            {
                if (commander.NationId != scope.Nation.Id)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.new-commander-must-be-of-the-same-nation"));
                if (commander.CommandSkill <= 0)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.new-commander-needs-command-skill"));
                if (commander.ArmyId != null || commander.CompanyId != null
                    || scope.Game.Nations.SelectMany(x => x.Navies).Any(v => v.CommanderId == commander.Id))
                    scope.Errors.Add(OrderTexts.Format(scope.Lang, "err.c-name-already-commands-a-force", commander.Name));
                if (army != null && commander.LocationHex != army.LocationHex)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.new-commander-must-be-at-the-same-hex"));
            }
        }
        if (code == 770)
        {
            if (scope.CommandsForce(scope.Character))
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.character-already-commands-a-force"));
            var pc = scope.OwnPopulationAt(scope.EffectiveLocation);
            if (pc == null || pc.IsSieged)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.must-be-at-one-of-your-non-sieged-population-cen"));
            var troopType = scope.Text("troopType") ?? "MenAtArms";
            var shorts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                { { "hc", "HeavyCavalry" }, { "lc", "LightCavalry" }, { "hi", "HeavyInfantry" }, { "li", "LightInfantry" }, { "ar", "Archers" }, { "ma", "MenAtArms" } };
            if (shorts.TryGetValue(troopType, out var full)) troopType = full;
            if (troopType != "HeavyCavalry" && troopType != "LightCavalry" && troopType != "HeavyInfantry" && troopType != "LightInfantry" && troopType != "Archers" && troopType != "MenAtArms")
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.troop-type-must-be-hc-lc-hi-li-ar-or-ma"));
            var weapons = (scope.Text("weapons") ?? "bronze").ToLower();
            if (weapons != "bronze" && weapons != "steel" && weapons != "mithril")
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.weapon-material-must-be-bronze-steel-or-mithril"));
            var armour = (scope.Text("armour") ?? "leather").ToLower();
            if (armour != "leather" && armour != "bronze" && armour != "steel" && armour != "mithril")
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.armour-material-must-be-leather-bronze-steel-or"));
        }
        if (code == 775)
        {
            var army = scope.Text("armyId") != null
                ? scope.Nation.Armies.FirstOrDefault(a => a.Id == scope.Text("armyId"))
                : scope.Nation.Armies.FirstOrDefault(a => a.Id == scope.Character.ArmyId);
            if (army == null)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.no-army-to-disband"));
        }
        if (code == 780)
        {
            var army = scope.Text("armyId") != null
                ? scope.Nation.Armies.FirstOrDefault(a => a.Id == scope.Text("armyId"))
                : scope.Nation.Armies.FirstOrDefault(a => a.Id == scope.Character.ArmyId);
            if (army == null)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.no-army-to-transfer-command-of"));
            var target = scope.FindCharacter(scope.Text("targetId"));
            if (scope.Text("targetId") != null && target == null)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.target-commander-not-found"));
            else if (target != null)
            {
                if (target.NationId != scope.Nation.Id)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.new-commander-must-be-of-the-same-nation"));
                if (target.CommandSkill <= 0)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.new-commander-needs-command-skill"));
            }
        }
        if (code == 785)
        {
            if (scope.CommandsForce(scope.Character))
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.character-already-commands-a-force"));
            var commanderId = scope.Text("commanderId");
            var boss = scope.FindCharacter(commanderId);
            if (commanderId != null && (boss == null || boss.IsDead))
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.commander-not-found"));
            else if (boss != null)
            {
                if (boss.NationId != scope.Nation.Id)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.can-only-join-a-force-of-your-own-nation"));
                var forceArmy = scope.Game.Nations.SelectMany(x => x.Armies).FirstOrDefault(a => a.CommanderId == boss.Id);
                var forceNavy = forceArmy == null ? scope.Game.Nations.SelectMany(x => x.Navies).FirstOrDefault(v => v.CommanderId == boss.Id) : null;
                if (forceArmy == null && forceNavy == null)
                    scope.Errors.Add(OrderTexts.Format(scope.Lang, "err.boss-name-commands-no-force", boss.Name));
                else if ((forceArmy?.LocationHex ?? forceNavy!.LocationHex) != scope.EffectiveLocation)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.force-to-join-must-be-at-the-same-hex"));
            }
        }
        if (code == 905)
        {
            var commanderId = scope.Text("commanderId");
            var boss = scope.FindCharacter(commanderId);
            if (commanderId != null && (boss == null || boss.IsDead))
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.commander-not-found"));
            else if (boss != null)
            {
                var commandsArmy = scope.Game.Nations.SelectMany(x => x.Armies).Any(a => a.CommanderId == boss.Id);
                var commandsNavy = scope.Game.Nations.SelectMany(x => x.Navies).Any(v => v.CommanderId == boss.Id);
                if (!commandsArmy && !commandsNavy)
                    scope.Errors.Add(OrderTexts.Format(scope.Lang, "err.boss-name-commands-no-force", boss.Name));
            }
        }
    }

    private static void ValidateScouting(int code, EstimateScope scope)
    {
        if (code is 910 or 915 or 925 or 930 && !scope.IsLandHex(scope.EffectiveLocation))
            scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.must-be-on-land"));
    }

    private static void ValidateLore(int code, EstimateScope scope)
    {
        if (code != 940) return;
        if (scope.Text("targetId") is { } targetId && scope.FindCharacter(targetId) == null)
            scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.target-character-not-found"));
        if (scope.Text("nationId") is { } nationId && !scope.Game.Nations.Any(x => x.Id == nationId))
            scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.nation-not-found"));
        if (scope.Text("artifactId") is { } artifactId && !scope.ArtifactExists(artifactId))
            scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.artifact-not-found"));
        if (scope.Text("commanderId") is { } commanderId)
        {
            var boss = scope.FindCharacter(commanderId);
            if (boss == null || boss.IsDead)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.commander-not-found"));
            else if (!scope.Game.Nations.SelectMany(x => x.Armies).Any(a => a.CommanderId == boss.Id)
                && !scope.Game.Nations.SelectMany(x => x.Navies).Any(v => v.CommanderId == boss.Id))
                scope.Errors.Add(OrderTexts.Format(scope.Lang, "err.boss-name-commands-no-force", boss.Name));
        }
        if (scope.Text("allegiance") is { } allegiance && allegiance != "free_peoples" && allegiance != "dark_servants" && allegiance != "neutral")
            scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.allegiance-must-be-free-peoples-dark-servants-or"));
    }

    private static void ValidateOwnership(int code, EstimateScope scope)
    {
        if (code == 949)
        {
            if (scope.Character.EmissarySkill <= 0)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.needs-emissary-skill"));
            var pc = scope.OwnPopulationAt(scope.EffectiveLocation);
            if (pc == null)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.no-population-centre"));
            else
            {
                if (pc.IsHidden)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.population-centre-is-hidden"));
                if (pc.IsCapital)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.cannot-transfer-the-capital"));
            }
            var target = scope.FindCharacter(scope.Text("targetId"));
            if (scope.Text("targetId") != null && (target == null || target.IsDead))
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.target-not-found"));
            else if (target != null)
            {
                if (target.IsKidnapped)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.target-is-a-hostage"));
                if (target.EmissarySkill <= 0)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.target-needs-emissary-skill"));
                if (target.LocationHex != scope.EffectiveLocation)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.target-must-be-at-the-same-location"));
                if (target.NationId == scope.Nation.Id)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.target-must-be-of-another-nation"));
                var fwd = scope.Nation.Relations.FirstOrDefault(r => r.TargetNationId == target.NationId);
                if ((fwd?.Level ?? 0) <= -1)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.nations-are-enemies"));
            }
        }
        if (code == 950)
        {
            if (scope.EffectiveLocation != scope.CapitalHex)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "reason.must-be-at-your-current-capital"));
            PopulationCentre? pc = scope.Text("pcId") != null
                ? scope.Game.Nations.SelectMany(x => x.PopulationCentres).FirstOrDefault(p => p.Id == scope.Text("pcId"))
                : scope.PopulationAt(scope.Text("hex") ?? scope.EffectiveLocation);
            if (pc == null)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.no-population-centre-found"));
            else
            {
                if (pc.NationId != scope.Nation.Id)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.new-capital-must-be-owned"));
                if (pc.IsSieged)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.capitals-must-not-be-sieged"));
                var current = scope.Nation.PopulationCentres.FirstOrDefault(x => x.IsCapital);
                if (current != null && current.IsSieged)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.capitals-must-not-be-sieged"));
                if (pc.Size != "major town" && pc.Size != "city")
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.new-capital-must-be-a-major-town-or-city"));
            }
        }
    }

    private static void ValidateRing(int code, EstimateScope scope)
    {
        if (code != 990) return;
        if (scope.Nation.Allegiance == "neutral")
            scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.neutral-nations-cannot-wield-the-one-ring"));
        if (scope.CommandsForce(scope.Character))
            scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.bearer-must-travel-alone-no-army-company-or-navy"));
        if ((scope.EffectiveLocation ?? "") != "34,23")
            scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.the-bearer-must-be-at-mount-doom-34-23"));
        var ring = scope.FindArtifact(scope.Character.Artifacts
            .FirstOrDefault(a => a.Id == "14" || (a.Name ?? "").Contains("One Ring", StringComparison.OrdinalIgnoreCase))?.Id);
        if (ring == null)
            scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.bearer-must-possess-artifact-14-the-one-ring"));
    }

    private static void ValidateAllegiance(int code, EstimateScope scope)
    {
        if (code is 180 or 185 && !scope.Game.Nations.Any(x => x.Id == scope.Text("nationId")))
            scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.nation-not-found"));
        if (code == 175)
        {
            var allegiance = (scope.Text("allegiance") ?? "").ToLower();
            if (allegiance != "free_peoples" && allegiance != "dark_servants" && allegiance != "neutral")
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.allegiance-must-be-free-peoples-dark-servants-or"));
        }
        if (code is 690 && !scope.Game.Nations.Any(x => x.Id == scope.Text("nationId")))
            scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.victim-nation-not-found"));
    }

    private static void ValidateSpellCasting(int code, EstimateScope scope)
    {
        if (code is 120 or 330 or 940 or 225 or 825)
        {
            var wanted = code == 120 ? SpellType.Heal : code == 330 ? SpellType.Conjuring
                : code == 940 ? SpellType.Lore : code == 825 ? SpellType.Movement : SpellType.Combat;
            var spellId = scope.Number("spellId", -1);
            var spell = SpellCatalog.Get(spellId);
            if (spell == null || spell.Type != wanted
                || !scope.Character.Spells.Any(s => s.SpellId == spellId && s.IsKnown && !s.IsLost))
                scope.Errors.Add(OrderTexts.Format(scope.Lang, "err.valid-spell", OrderTexts.SpellTypeName(scope.Lang, wanted)));
        }
        if (code is 700 or 705 && scope.Parameters.TryGetValue("spellId", out var spellEl)
            && spellEl.ValueKind == JsonValueKind.Number)
        {
            var spell = SpellCatalog.Get(spellEl.GetInt32());
            if (spell == null)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.unknown-spell"));
            else if (code == 705 && spell.IsLost && !NationAbilities.CanLearnLostSpell(scope.Nation.Name, spell.Id))
                scope.Errors.Add(OrderTexts.Format(scope.Lang, "err.lost-spell-sd-name-is-not-available-to-your-nati", spell.Name));
        }
        if (code == 330)
        {
            var spellId = scope.Number("spellId", -1);
            var ch = scope.Character;
            if (spellId is 502 or 504)
            {
                var target = scope.FindCharacter(scope.Text("targetId"));
                if (target == null || target.IsDead)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.target-character-not-found"));
                else if (target.NationId == scope.Nation.Id)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.conjuring-target-must-be-of-a-different-nation"));
                else if (target.LocationHex != ch.LocationHex)
                    scope.Errors.Add(OrderTexts.Format(scope.Lang, "err.conjuring-target-must-be-in-the-same-hex-t", ch.LocationHex));
            }
            else if (spellId == 506)
            {
                var target = scope.FindCharacter(scope.Text("targetId"));
                if (target == null || target.IsDead)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.target-character-not-found"));
                else if (target.NationId == scope.Nation.Id)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.conjuring-target-must-be-of-a-different-nation"));
                else if (!IsSameOrAdjacentHex(ch.LocationHex, target.LocationHex))
                    scope.Errors.Add(OrderTexts.Format(scope.Lang, "err.conjuring-target-must-be-in-same-or-adjacent-hex-t", ch.LocationHex));
            }
            else if (spellId == 508)
            {
                var pc = scope.Game.Nations.First(n => n.Id == scope.Nation.Id).PopulationCentres
                    .FirstOrDefault(p => p.LocationHex == ch.LocationHex);
                if (pc == null)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.conjuring-mounts-must-be-at-own-population-centre"));
            }
            else if (spellId == 510)
            {
                var atOwnPC = scope.Nation.PopulationCentres.Any(p => p.LocationHex == ch.LocationHex);
                var withArmy = scope.Game.Nations.SelectMany(x => x.Armies).Any(a => a.NationId == scope.Nation.Id && a.LocationHex == ch.LocationHex);
                if (!atOwnPC && !withArmy)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.conjuring-food-must-be-at-own-pc-or-with-army-navy"));
            }
            else if (spellId == 512)
            {
                var withArmy = scope.Game.Nations.SelectMany(x => x.Armies).Any(a => a.NationId == scope.Nation.Id && a.LocationHex == ch.LocationHex);
                if (!withArmy)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.conjuring-hordes-must-be-with-army-or-navy"));
                if (!NationAbilities.CanLearnLostSpell(scope.Nation.Name, 512))
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.conjuring-hordes-dark-servants-only"));
            }
        }
    }

    private static void ValidateDiplomacy(int code, EstimateScope scope)
    {
        if (code == 500)
        {
            if (scope.Character.EmissarySkill <= 0)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.needs-emissary-skill"));
            var target = scope.FindCharacter(scope.Text("targetId"));
            if (scope.Text("targetId") != null && (target == null || target.IsDead))
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.target-character-not-found"));
            else if (target != null)
            {
                if (target.EmissarySkill <= 0 && target.AgentSkill <= 0)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.target-must-have-emissary-or-agent-skill"));
                if (target.LocationHex != scope.EffectiveLocation)
                    scope.Errors.Add(OrderTexts.Format(scope.Lang, "err.target-not-in-the-same-hex-at-t-locationhex", target.LocationHex));
                if (target.NationId == scope.Nation.Id)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.cannot-plant-a-double-agent-in-your-own-nation"));
            }
        }
        if (code == 505)
        {
            var target = scope.FindCharacter(scope.Text("targetId"));
            if (scope.Text("targetId") != null && target == null)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.target-not-found"));
            else if (target != null)
            {
                if (target.NationId == scope.Nation.Id)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.cannot-bribe-your-own-character"));
                if (target.IsKidnapped)
                    scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.cannot-bribe-a-hostage"));
                if (target.LocationHex != scope.EffectiveLocation)
                    scope.Errors.Add(OrderTexts.Format(scope.Lang, "err.target-not-in-the-same-hex-at-t-locationhex", target.LocationHex));
            }
            if (scope.Character.EmissarySkill <= 0)
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.needs-emissary-skill"));
        }
    }

    private static void ValidateMarket(int code, EstimateScope scope)
    {
        if (code is 310 or 315 or 320 or 325)
        {
            if (!TurnProcessor.IsMarketProductName(scope.Text("product"), out _))
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.product-must-be-timber-leather-bronze-steel-mith"));
        }
        if (code is 947 or 948)
        {
            if (!TurnProcessor.IsMarketProductName(scope.Text("resource"), out _))
                scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.resource-must-be-timber-leather-bronze-steel-mit"));
        }
    }

    private static void ValidateTerrainWorks(int code, EstimateScope scope)
    {
        if (code is not (475 or 490 or 665)) return;
        var tile = scope.TileAt(scope.Text("hex") ?? scope.EffectiveLocation ?? "");
        if (tile == null)
            scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.no-such-hex"));
        else if (code == 475 && !tile.HasBridge)
            scope.Errors.Add(OrderTexts.Format(scope.Lang, "err.no-bridge-at-tile-q-tile-r", tile.Q, tile.R));
        else if (code == 665 && !tile.HasBridge)
            scope.Errors.Add(OrderTexts.Format(scope.Lang, "err.no-bridge-at-tile-q-tile-r", tile.Q, tile.R));
        else if (code == 490 && !tile.HasMajorRiver && !tile.HasMinorRiver)
            scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.no-river-here"));
        else if (code == 490 && (tile.HasBridge || tile.HasFord))
            scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.a-ford-or-bridge-already-exists-here"));
    }

    private static Dictionary<string, System.Text.Json.JsonElement> ParseStoredParams(string? json)
    {
        var d = new Dictionary<string, System.Text.Json.JsonElement>();
        if (string.IsNullOrWhiteSpace(json)) return d;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
                foreach (var p in doc.RootElement.EnumerateObject()) d[p.Name] = p.Value.Clone();
        }
        catch { }
        return d;
    }

    private async Task<List<Order>> PendingNationOrders(string gameId, string nationId)
    {
        var turn = await _db.Turns
            .Where(t => t.GameId == gameId && t.Status == "orders_open")
            .OrderByDescending(t => t.Number)
            .FirstOrDefaultAsync();
        if (turn == null) return new List<Order>();
        return await _db.Orders
            .Where(o => o.GameId == gameId && o.TurnId == turn.Id && o.NationId == nationId && o.Status == "pending")
            .ToListAsync();
    }

    private PendingUsage SummarizePendingUsage(List<Order> pending, Nation nation, Game game)
    {
        var used = new PendingUsage();
        var chars = game.Nations.SelectMany(n => n.Characters).ToDictionary(c => c.Id);
        foreach (var o in pending)
        {
            var stored = ParseStoredParams(o.Parameters);
            int Amount(string k, int def = 0)
            {
                if (stored.TryGetValue(k, out var el) && el.ValueKind == System.Text.Json.JsonValueKind.Number && el.TryGetInt32(out var n2)) return n2;
                return def;
            }
            string? Text(string k) => stored.TryGetValue(k, out var el) && el.ValueKind == System.Text.Json.JsonValueKind.String ? el.GetString() : null;
            if (o.Code is >= 400 and <= 420)
            {
                var amount = Amount("amount");
                if (amount <= 0) continue;
                chars.TryGetValue(o.CharacterId, out var recruiter);
                var army = PendingOrderArmy(nation, o.ArmyId, recruiter?.ArmyId);
                var pc = army == null ? null : nation.PopulationCentres.FirstOrDefault(x => x.LocationHex == army.LocationHex);
                if (pc != null) used.RecruitsByPc[pc.Id] = used.RecruitsByPc.GetValueOrDefault(pc.Id) + amount;
            }
            else if (o.Code is 310 or 315)
            {
                var prod = (Text("product") ?? "").ToLower();
                if (prod != "") used.BuyByProduct[prod] = used.BuyByProduct.GetValueOrDefault(prod) + Amount("amount");
            }
            else if (o.Code is 320)
            {
                var prod = (Text("product") ?? "").ToLower();
                if (TurnProcessor.IsMarketProductName(prod, out _) && Amount("amount") > 0)
                {
                    var (_, baseSell) = TurnProcessor.MarketRate(prod);
                    used.SellGoldUsed += Amount("amount") * NationAbilities.MarketSellPrice(nation.Name, baseSell);
                }
            }
            else if (o.Code is 325)
            {
                var prod = (Text("product") ?? "").ToLower();
                if (TurnProcessor.IsMarketProductName(prod, out _))
                {
                    var (_, baseSellAll) = TurnProcessor.MarketRate(prod);
                    var sell = NationAbilities.MarketSellPrice(nation.Name, baseSellAll);
                    var pct = Amount("percentage", 100);
                    var stock = EstimateScope.StockLevel(nation, prod);
                    used.SellGoldUsed += stock * pct / 100 * sell;
                }
            }
            else if (o.Code == 494)
            {
                var pc = Text("pcId") != null
                    ? nation.PopulationCentres.FirstOrDefault(x => x.Id == Text("pcId"))
                    : nation.PopulationCentres.FirstOrDefault(x => x.LocationHex == (Text("hex") ?? chars.GetValueOrDefault(o.CharacterId)?.LocationHex));
                if (pc != null) used.FortifiedPcs.Add(pc.Id);
            }
            else if (o.Code == 500 && Text("targetId") is { } targetCharId
                && chars.TryGetValue(targetCharId, out var targetChar))
                used.DoubleAgentNations.Add(targetChar.NationId);
            else if (o.Code == 210 && Text("targetId") is { } tg) used.ChallengedTargets.Add(tg);
            else if (o.Code == 505 && Text("targetId") is { } tb) used.BribedTargets.Add(tb);
            else if ((o.Code == 360 || o.Code == 792) && Text("artifactId") is { } ar) used.MovedArtifacts.Add(ar);
        }
        return used;
    }

    private static Army? PendingOrderArmy(Nation nation, string? orderArmyId, string? characterArmyId)
    {
        if (orderArmyId != null) return nation.Armies.FirstOrDefault(a => a.Id == orderArmyId);
        if (characterArmyId != null) return nation.Armies.FirstOrDefault(a => a.Id == characterArmyId);
        return nation.Armies.FirstOrDefault();
    }

    public void CrossOrderConflicts(int code, EstimateScope scope)
    {
        var used = scope.Used ?? new PendingUsage();
        var pending = scope.Pending ?? new List<Order>();
        if (code == 494)
        {
            var pc = scope.Text("pcId") != null
                ? scope.Nation.PopulationCentres.FirstOrDefault(p => p.Id == scope.Text("pcId"))
                : scope.Nation.PopulationCentres.FirstOrDefault(p => p.LocationHex == (scope.Text("hex") ?? scope.EffectiveLocation));
            if (pc != null && used.FortifiedPcs.Contains(pc.Id))
                scope.Errors.Add(OrderTexts.Format(scope.Lang, "err.pc-name-is-already-fortified-by-another-order-th", pc.Name));
        }
        if (code == 500 && scope.Text("targetId") is { } targetCharId
            && scope.FindCharacter(targetCharId) is { } targetChar
            && used.DoubleAgentNations.Contains(targetChar.NationId))
            scope.Errors.Add(OrderTexts.Get(scope.Lang, "err.another-order-already-recruits-a-double-agent-th"));
        if (code == 210 && scope.Text("targetId") is { } challengedId && used.ChallengedTargets.Contains(challengedId))
        {
            var otherMax = 0;
            foreach (var o in pending.Where(o => o.Code == 210))
            {
                var stored = ParseStoredParams(o.Parameters);
                if (stored.TryGetValue("targetId", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.String && t.GetString() == challengedId)
                {
                    var other = scope.FindCharacter(o.CharacterId);
                    if (other != null) otherMax = Math.Max(otherMax, other.ChallengeRank);
                }
            }
            if (scope.Character.ChallengeRank < otherMax)
                scope.Errors.Add(OrderTexts.Format(scope.Lang, "err.target-already-challenged-by-higher-challenge-ra", otherMax, scope.Character.ChallengeRank));
            else
                scope.Warnings.Add(OrderTexts.Get(scope.Lang, "warn.target-already-challenged-by-another-character-o"));
        }
        if (code == 505 && scope.Text("targetId") is { } bribedId && used.BribedTargets.Contains(bribedId))
            scope.Warnings.Add(OrderTexts.Get(scope.Lang, "warn.target-already-bribed-by-another-character-first"));
        if ((code == 360 || code == 792) && scope.Text("artifactId") is { } movedId && used.MovedArtifacts.Contains(movedId))
            scope.Warnings.Add(OrderTexts.Get(scope.Lang, "warn.artifact-already-moved-by-another-pending-order"));
        if ((code is 320 or 325) && used.SellGoldUsed > 0)
            scope.Warnings.Add(OrderTexts.Format(scope.Lang, "warn.used-sellgoldused-gold-of-sell-cap-already-used", used.SellGoldUsed));
        if (code is 400 or 404 or 408 or 412 or 416 or 420)
        {
            var army = scope.SourceArmy();
            var pc = army == null ? null : scope.OwnPopulationAt(army.LocationHex);
            if (pc != null)
            {
                var taken = used.RecruitsByPc.GetValueOrDefault(pc.Id);
                var left = TurnProcessor.RecruitCapacity(pc.Size) - taken;
                var want = scope.Number("amount", left);
                if (want > left)
                    scope.Errors.Add(OrderTexts.Format(scope.Lang, "err.only-math-max-0-left-recruits-left-at-pc-name-ot", Math.Max(0, left), pc.Name, taken));
            }
        }
    }

    private static bool IsSameOrAdjacentHex(string hexA, string hexB)
    {
        if (hexA == hexB) return true;
        var a = hexA.Split(',');
        var b = hexB.Split(',');
        if (a.Length != 2 || b.Length != 2) return false;
        if (!int.TryParse(a[0], out var aq) || !int.TryParse(a[1], out var ar)) return false;
        if (!int.TryParse(b[0], out var bq) || !int.TryParse(b[1], out var br)) return false;
        var dq = Math.Abs(aq - bq);
        var dr = Math.Abs(ar - br);
        var ds = Math.Abs((-aq - ar) - (-bq - br));
        return Math.Max(dq, Math.Max(dr, ds)) == 1;
    }
}
