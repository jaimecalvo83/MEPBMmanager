using MEPBMmanager.Api.Services;
using MEPBMmanager.Domain.Constants;
using MEPBMmanager.Domain.Entities;

namespace MEPBMmanager.Api.Orders;

/// <summary>Who can issue which order right now (cheap state checks).</summary>
public sealed partial class OrderEstimateService
{
    public (bool Ok, string Reason) CheckEligible(Character ch, OrderDefinition def,
        Nation? nation, bool commandsNavy, string? lang = null)
    {
        if (ch.IsDead || ch.IsKidnapped) return (false, OrderTexts.Get(lang, "err.character-cannot-act"));
        if (def.Code == 100) return (false, OrderTexts.Get(lang, "reason.automatic-order"));
        // Prerrequisitos de estado: van antes que las restricciones de tipo,
        // aplican a todas las órdenes.
        var need0 = def.Code switch
        {
            205 => ch.Artifacts.Any(a => a.HeldByCharacterId == ch.Id && TurnProcessor.CombatArtifactTypes.Contains(a.Type ?? "")) ? "" : OrderTexts.Get(lang, "reason.no-combat-artifact-held"),
            360 or 792 => ch.Artifacts.Any(a => a.HeldByCharacterId == ch.Id) ? "" : OrderTexts.Get(lang, "reason.no-artifact-held"),
            750 or 760 => ch.CompanyId != null ? "" : OrderTexts.Get(lang, "reason.not-in-a-company"),
            790 => ch.ArmyId != null ? "" : OrderTexts.Get(lang, "reason.not-in-an-army"),
            120 => HasSpellType(ch, SpellType.Heal) ? "" : OrderTexts.Get(lang, "reason.no-healing-spell-known"),
            225 => HasSpellType(ch, SpellType.Combat) ? "" : OrderTexts.Get(lang, "reason.no-combat-spell-known"),
            330 => HasSpellType(ch, SpellType.Conjuring) ? "" : OrderTexts.Get(lang, "reason.no-conjuring-spell-known"),
            825 => HasSpellType(ch, SpellType.Movement) ? "" : OrderTexts.Get(lang, "reason.no-movement-spell-known"),
            940 => HasSpellType(ch, SpellType.Lore) ? "" : OrderTexts.Get(lang, "reason.no-lore-spell-known"),
            _ => ""
        };
        if (need0 != "") return (false, need0);
        var armyNeeded = new HashSet<int> { 230, 235, 240, 250, 255, 260, 340, 345, 347, 349, 351, 353, 355, 370, 375, 400, 404, 408, 412, 416, 420, 425, 430, 435, 440, 444, 448, 765, 775, 780, 840, 850, 860 };
        if (armyNeeded.Contains(def.Code) && ch.ArmyId == null)
            return (false, OrderTexts.Get(lang, "reason.not-in-an-army"));
        var navyNeeded = new HashSet<int> { 270, 275, 280, 830, 794, 798 };
        if (navyNeeded.Contains(def.Code) && (nation == null || nation.Navies.Count == 0))
            return (false, OrderTexts.Get(lang, "reason.no-navy-available"));
        if (def.Code == 830 && !commandsNavy)
            return (false, OrderTexts.Get(lang, "reason.must-command-a-navy"));
        var capHex = nation?.PopulationCentres.FirstOrDefault(p => p.IsCapital)?.LocationHex;
        var atCapital = capHex != null && capHex == ch.LocationHex;
        if ((def.Code is 175 or 180 or 185 or 280 or 300 or 325 or 660 or 725 or 728 or 731 or 734 or 737) && !atCapital)
            return (false, OrderTexts.Get(lang, "reason.must-be-at-your-own-capital"));
        if (def.Code == 950 && ch.LocationHex != capHex)
            return (false, OrderTexts.Get(lang, "reason.must-be-at-your-current-capital"));
        if ((def.Code is 520 or 530 or 535 or 550 or 705 or 710)
            && (nation == null || !nation.PopulationCentres.Any(p => p.LocationHex == ch.LocationHex && p.NationId == nation.Id)))
            return (false, OrderTexts.Get(lang, "reason.must-be-at-one-of-your-population-centres"));
        var r = def.Restrictions;
        if (r.Length == 0 || r.Contains("without")) return (true, "");
        var type = (ch.Type ?? "").ToLower();
        foreach (var t in r)
        {
            switch (t)
            {
                case "c": if (type == "commander" || ch.CommandSkill > 0) return (true, ""); break;
                case "a": if (type == "agent" || ch.AgentSkill > 0) return (true, ""); break;
                case "e": if (type == "emissary" || ch.EmissarySkill > 0) return (true, ""); break;
                case "m": if (type == "mage" || ch.MageSkill > 0) return (true, ""); break;
                case "com":
                    if (ch.ArmyId != null || commandsNavy) return (true, "");
                    break;
                case "company": if (ch.CompanyId != null) return (true, ""); break;
                case "cap": if (capHex != null && capHex == ch.LocationHex) return (true, ""); break;
                case "fa": return (false, OrderTexts.Get(lang, "reason.fourth-age-only"));
            }
        }
        return (false, OrderTexts.Get(lang, "reason.requires") + string.Join("/", r));
    }

    private static bool HasSpellType(Character ch, SpellType type) =>
        ch.Spells.Any(s => s.IsKnown && !s.IsLost && SpellCatalog.Get(s.SpellId)?.Type == type);
}
