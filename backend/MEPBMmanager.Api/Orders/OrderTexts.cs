using MEPBMmanager.Api.Services;
using MEPBMmanager.Domain.Constants;
using MEPBMmanager.Domain.Entities;

namespace MEPBMmanager.Api.Orders;

/// <summary>
/// Single home for every user-facing order string (EN/ES).
/// Rules and forms reference these keys; translators edit only this file.
/// Conventions: err.* endpoint/validation errors, reason.* eligibility,
/// label.* form field labels, opt.* dropdown options, warn.* warnings,
/// cost.* resource names.
/// </summary>
public static class OrderTexts
{
    public static string Norm(string? lang) => lang == "es" ? "es" : "en";

    public static string Get(string? lang, string key)
    {
        if (Strings.TryGetValue(key, out var s)) return lang == "es" ? s.Es : s.En;
        return key;
    }

    public static string Format(string? lang, string key, params object?[] args)
    {
        try { return string.Format(Get(lang, key), args); }
        catch { return Get(lang, key); }
    }

    public static string CostKey(string? lang, string key) => key.ToLower() switch
    {
        "gold" => Get(lang, "cost.gold"),
        "food" => Get(lang, "cost.food"),
        "timber" => Get(lang, "cost.timber"),
        "leather" => Get(lang, "cost.leather"),
        "bronze" => Get(lang, "cost.bronze"),
        "steel" => Get(lang, "cost.steel"),
        "mithril" => Get(lang, "cost.mithril"),
        "mounts" => Get(lang, "cost.mounts"),
        _ => key
    };

    public static string SpellTypeName(string? lang, SpellType type) => type switch
    {
        SpellType.Heal => lang == "es" ? "curaci\u00f3n" : "heal",
        SpellType.Combat => lang == "es" ? "combate" : "combat",
        SpellType.Conjuring => lang == "es" ? "invocaci\u00f3n" : "conjuring",
        SpellType.Movement => lang == "es" ? "movimiento" : "movement",
        SpellType.Lore => lang == "es" ? "saber" : "lore",
        _ => lang == "es" ? "hechizo" : "spell"
    };

    public static string AllegianceName(string? lang, string? al) => (al ?? "").ToLower() switch
    {
        "free_peoples" or "free" => lang == "es" ? "Pueblos Libres" : "Free Peoples",
        "dark_servants" or "dark" => lang == "es" ? "Sirvientes Oscuros" : "Dark Servants",
        "neutral" => lang == "es" ? "Neutral" : "Neutral",
        _ => al ?? "?"
    };

    public static string CharTypeName(string? lang, string? t) => (t ?? "").ToLower() switch
    {
        "commander" => lang == "es" ? "comandante" : "commander",
        "agent" => lang == "es" ? "agente" : "agent",
        "emissary" => lang == "es" ? "emisario" : "emissary",
        "mage" => lang == "es" ? "mago" : "mage",
        _ => t ?? "?"
    };

    public static string ProductName(string? lang, string p) => p.ToLower() switch
    {
        "timber" => lang == "es" ? "madera" : "timber",
        "leather" => lang == "es" ? "cuero" : "leather",
        "bronze" => lang == "es" ? "bronce" : "bronze",
        "steel" => lang == "es" ? "acero" : "steel",
        "mithril" => lang == "es" ? "mitril" : "mithril",
        "mounts" => lang == "es" ? "monturas" : "mounts",
        "food" => lang == "es" ? "comida" : "food",
        _ => p
    };

    private static readonly Dictionary<string, string> NationEs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Woodmen"] = "Hombres del Bosque",
        ["Northmen"] = "Hombres del Norte",
        ["Riders of Rohan"] = "Jinetes de Rohan",
        ["D\u00fanadan Rangers"] = "Montaraces D\u00fanedain",
        ["Dunadan Rangers"] = "Montaraces D\u00fanedain",
        ["Silvan Elves"] = "Elfos Silvanos",
        ["Northern Gondor"] = "Gondor del Norte",
        ["Southern Gondor"] = "Gondor del Sur",
        ["Dwarves"] = "Enanos",
        ["Sinda Elves"] = "Elfos Sindar",
        ["Noldo Elves"] = "Elfos Noldor",
        ["Witch-king"] = "Rey Brujo",
        ["Witch King"] = "Rey Brujo",
        ["Dragon Lord"] = "Se\u00f1or de los Dragones",
        ["Dog Lord"] = "Se\u00f1or de los Perros",
        ["Cloud Lord"] = "Se\u00f1or de las Nubes",
        ["Blind Sorcerer"] = "Hechicero Ciego",
        ["Ice King"] = "Rey de Hielo",
        ["Quiet Avenger"] = "Vengador Silencioso",
        ["Fire King"] = "Rey del Fuego",
        ["Long Rider"] = "Jinete Largo",
        ["Dark Lieutenants"] = "Lugartenientes Oscuros",
        ["Corsairs"] = "Corsarios",
        ["Rh\u00fan Easterlings"] = "Orientales de Rh\u00fan",
        ["Rhun Easterlings"] = "Orientales de Rh\u00fan",
        ["Dunlendings"] = "Dunlendinos",
        ["White Wizard"] = "Mago Blanco",
        ["Khand Easterlings"] = "Orientales de Khand",
    };

    public static string NationDisplayName(string? lang, string name) =>
        lang == "es" && NationEs.TryGetValue(name ?? "", out var es) ? es : name;

    public static string SpellDisplayName(string? lang, int spellId, string fallback) =>
        lang == "es" && SpellDefinitionsEs.NamesEs.TryGetValue(spellId, out var n) ? n : fallback;

    public static string ArtifactDisplayName(string? lang, Artifact a) =>
        lang == "es" ? (ArtifactCatalog2950Es.NameEsByName(a.Name) ?? a.Name) : a.Name;

    private static readonly Dictionary<string, (string En, string Es)> Strings = new()
    {

        // cost
        ["cost.bronze"] = ("bronze", "bronce"),
        ["cost.food"] = ("food", "comida"),
        ["cost.gold"] = ("gold", "oro"),
        ["cost.leather"] = ("leather", "cuero"),
        ["cost.mithril"] = ("mithril", "mitril"),
        ["cost.mounts"] = ("mounts", "monturas"),
        ["cost.steel"] = ("steel", "acero"),
        ["cost.timber"] = ("timber", "madera"),

        // err
        ["err.a-ford-or-bridge-already-exists-here"] = ("A ford or bridge already exists here", "Aquí ya hay vado o puente"),
        ["err.a-name-alignment-does-not-match-your-allegiance"] = ("{0} alignment does not match your allegiance", "El alineamiento de {0} no vale para tu bando"),
        ["err.a-name-is-not-a-kind-artifact"] = ("{0} is not a {1} artifact", "{0} no es un artefacto de {1}"),
        ["err.allegiance-must-be-free-peoples-dark-servants-or"] = ("Allegiance must be free_peoples, dark_servants or neutral", "Lealtad: free_peoples, dark_servants o neutral"),
        ["err.already-have-a-population-centre-at-this-hex"] = ("Already have a population centre at this hex", "Ya tienes un centro en este hex"),
        ["err.already-knows-15-spells"] = ("Already knows 15 spells", "Ya conoce 15 hechizos"),
        ["err.another-order-already-recruits-a-double-agent-th"] = ("Another order already recruits a double agent there", "Otra orden ya recluta un agente doble allí"),
        ["err.armour-material-must-be-leather-bronze-steel-or"] = ("Armour material must be leather, bronze, steel or mithril", "Armadura de cuero, bronce, acero o mitril"),
        ["err.artifact-must-be-at-the-same-hex"] = ("Artifact must be at the same hex", "El artefacto debe estar en el mismo hex"),
        ["err.artifact-must-lie-in-a-visible-foreign-populatio"] = ("Artifact must lie in a visible foreign population centre", "El artefacto debe estar en un centro extranjero visible"),
        ["err.artifact-not-found"] = ("Artifact not found", "Artefacto no encontrado"),
        ["err.artifact-not-held-by-character"] = ("Artifact not held by character", "El personaje no lleva el artefacto"),
        ["err.bearer-must-possess-artifact-14-the-one-ring"] = ("Bearer must possess artifact #14 The One Ring", "El portador debe llevar el artefacto #14 El Anillo Único"),
        ["err.bearer-must-travel-alone-no-army-company-or-navy"] = ("Bearer must travel alone (no army, company or navy)", "El portador debe viajar solo (sin ejército, compañía ni armada)"),
        ["err.boss-name-commands-no-force"] = ("{0} commands no force", "{0} no manda ninguna fuerza"),
        ["err.both-armies-must-be-in-the-same-hex"] = ("Both armies must be in the same hex", "Ambos ejércitos en el mismo hex"),
        ["err.both-characters-must-be-at-the-same-location"] = ("Both characters must be at the same location", "Ambos personajes en el mismo lugar"),
        ["err.c-name-already-commands-a-force"] = ("{0} already commands a force", "{0} ya manda una fuerza"),
        ["err.camps-cannot-be-reduced-further-abandon-them"] = ("Camps cannot be reduced further (abandon them)", "Los campamentos no se reducen (abandónalos)"),
        ["err.camps-need-a-land-hex"] = ("Camps need a land hex", "El campamento requiere tierra"),
        ["err.can-only-join-a-force-of-your-own-nation"] = ("Can only join a force of your own nation", "Solo puedes unirte a una fuerza de tu nación"),
        ["err.can-only-reduce-your-own-population-centres"] = ("Can only reduce your own population centres", "Solo puedes reducir tus centros"),
        ["err.cannot-bribe-a-hostage"] = ("Cannot bribe a hostage", "No puedes sobornar a un rehén"),
        ["err.cannot-bribe-your-own-character"] = ("Cannot bribe your own character", "No puedes sobornar a tu personaje"),
        ["err.cannot-guard-yourself"] = ("Cannot guard yourself", "No puedes protegerte a ti mismo"),
        ["err.cannot-plant-a-double-agent-in-your-own-nation"] = ("Cannot plant a double agent in your own nation", "No puedes plantar un agente doble en tu nación"),
        ["err.cannot-retire-a-hostage"] = ("Cannot retire a hostage", "No puedes retirar un rehén"),
        ["err.cannot-transfer-the-capital"] = ("Cannot transfer the capital", "No puedes transferir la capital"),
        ["err.capital-and-new-capital-must-not-be-under-siege"] = ("Capital and new capital must not be under siege", "Ni la capital ni la nueva deben estar asediadas"),
        ["err.character-already-commands-a-force"] = ("Character already commands a force", "El personaje ya manda una fuerza"),
        ["err.character-already-has-2-orders-this-turn"] = ("Character already has 2 orders this turn", "El personaje ya tiene 2 órdenes este turno"),
        ["err.character-cannot-act"] = ("Character cannot act", "El personaje no puede actuar"),
        ["err.character-is-dead"] = ("Character is dead", "El personaje está muerto"),
        ["err.character-is-kidnapped"] = ("Character is kidnapped", "El personaje está secuestrado"),
        ["err.character-not-found"] = ("Character not found", "Personaje no encontrado"),
        ["err.commander-has-no-company"] = ("Commander has no company", "El comandante no tiene compañía"),
        ["err.commander-not-found"] = ("Commander not found", "Comandante no encontrado"),
        ["err.company-comp-name-is-full-9-members"] = ("Company '{0}' is full (9 members)", "La compañía '{0}' está llena (9 miembros)"),
        ["err.company-must-be-of-the-same-or-a-friendly-nation"] = ("Company must be of the same or a friendly nation", "La compañía debe ser de tu nación o amiga"),
        ["err.company-not-found"] = ("Company not found", "Compañía no encontrada"),
        ["err.dest-army-must-be-of-the-same-or-a-friendly-nati"] = ("Dest army must be of the same or a friendly nation", "El destino debe ser de tu nación o amiga"),
        ["err.dest-army-not-found"] = ("Dest army not found", "Ejército destino no encontrado"),
        ["err.enemy-forces-present"] = ("Enemy forces present", "Hay fuerzas enemigas"),
        ["err.force-to-join-must-be-at-the-same-hex"] = ("Force to join must be at the same hex", "La fuerza debe estar en el mismo hex"),
        ["err.game-not-found"] = ("Game not found", "Partida no encontrada"),
        ["err.give-1-6-artifactid-s"] = ("Give 1-6 artifactId(s)", "Indica 1-6 artefactos"),
        ["err.give-1-6-spellid-s-to-forget"] = ("Give 1-6 spellId(s) to forget", "Indica 1-6 hechizos a olvidar"),
        ["err.holder-not-found"] = ("Holder not found", "Portador no encontrado"),
        ["err.valid-spell"] = ("Info must contain a valid known {0} spell", "Info debe traer un hechizo válido conocido de {0}"),
        ["err.insufficient-resources-or-capacity-to-execute-ma"] = ("Insufficient resources or capacity to execute (max 0)", "Recursos o capacidad insuficientes (máx 0)"),
        ["err.invalid-order-code"] = ("Invalid order code", "Código de orden inválido"),
        ["err.lost-spell-sd-name-is-not-available-to-your-nati"] = ("Lost spell {0} is not available to your nation", "El hechizo perdido {0} no está disponible para tu nación"),
        ["err.material-must-be-bronze-steel-or-mithril"] = ("Material must be bronze, steel or mithril", "Material: bronce, acero o mitril"),
        ["err.material-must-be-leather-bronze-steel-or-mithril"] = ("Material must be leather, bronze, steel or mithril", "Material: cuero, bronce, acero o mitril"),
        ["err.missing-artifactid-s"] = ("Missing artifactId(s)", "Faltan artefactos"),
        ["err.missing-info"] = ("Missing info: ", "Falta info: "),
        ["err.must-be-at-one-of-your-non-sieged-population-cen"] = ("Must be at one of your non-sieged population centres", "Debes estar en un centro propio no asediado"),
        ["err.must-be-at-one-of-your-population-centres-to-res"] = ("Must be at one of your population centres to research", "Debes estar en un centro propio para investigar"),
        ["err.must-be-at-one-of-your-population-centres-to-tra"] = ("Must be at one of your population centres to train", "Debes estar en un centro propio para entrenar"),
        ["err.must-be-on-land"] = ("Must be on land", "Debes estar en tierra"),
        ["err.must-target-one-of-your-population-centres"] = ("Must target one of your population centres", "Debes apuntar a un centro propio"),
        ["err.name-must-be-5-17-letters"] = ("Name must be 5-17 letters", "El nombre debe tener 5-17 letras"),
        ["err.nation-not-found"] = ("Nation not found", "Nación no encontrada"),
        ["err.nations-are-enemies"] = ("Nations are enemies", "Las naciones son enemigas"),
        ["err.need-a-visible-foreign-population-centre-at-your"] = ("Need a visible foreign population centre at your hex", "Necesitas un centro extranjero visible en tu hex"),
        ["err.needs-emissary-skill"] = ("Needs emissary skill", "Requiere emisario"),
        ["err.neutral-nations-cannot-wield-the-one-ring"] = ("Neutral nations cannot wield the One Ring", "Las naciones neutrales no pueden blandir el Anillo Único"),
        ["err.new-capital-must-be-a-major-town-or-city"] = ("New capital must be a major town or city", "La nueva capital debe ser villa grande o ciudad"),
        ["err.new-capital-must-be-owned"] = ("New capital must be owned by your nation", "La nueva capital debe ser de tu nación"),
        ["err.capitals-must-not-be-sieged"] = ("Capital and new capital must not be under siege", "Las capitales no deben estar asediadas"),
        ["err.new-commander-must-be-at-the-same-hex"] = ("New commander must be at the same hex", "El nuevo comandante debe estar en el mismo hex"),
        ["err.new-commander-must-be-of-the-same-nation"] = ("New commander must be of the same nation", "El nuevo comandante debe ser de tu nación"),
        ["err.new-commander-needs-command-skill"] = ("New commander needs command skill", "El nuevo comandante necesita mando"),
        ["err.new-commander-not-found"] = ("New commander not found", "Nuevo comandante no encontrado"),
        ["err.no-army"] = ("No army", "Sin ejército"),
        ["err.no-army-specified"] = ("No army specified", "Sin ejército"),
        ["err.no-army-to-disband"] = ("No army to disband", "Sin ejército que disolver"),
        ["err.no-army-to-split"] = ("No army to split", "Sin ejército que dividir"),
        ["err.no-army-to-transfer-command-of"] = ("No army to transfer command of", "Sin ejército que transferir"),
        ["err.no-bridge-at-tile-q-tile-r"] = ("No bridge at {0},{1}", "Sin puente en {0},{1}"),
        ["err.no-camp-of-yours-here"] = ("No camp of yours here", "Aquí no hay campamento tuyo"),
        ["err.no-navy"] = ("No navy", "Sin armada"),
        ["err.no-population-centre"] = ("No population centre", "Sin centro de población"),
        ["err.no-population-centre-at-location"] = ("No population centre at location", "Sin centro de población aquí"),
        ["err.no-population-centre-here"] = ("No population centre here", "Aquí no hay centro de población"),
        ["err.no-river-here"] = ("No river here", "Aquí no hay río"),
        ["err.no-source-army"] = ("No source army", "Sin ejército origen"),
        ["err.no-such-hex"] = ("No such hex", "Ese hex no existe"),
        ["err.no-troops-retired-amounts-per-type-hc-lc-hi-li-a"] = ("No troops retired (amounts per type: hc/lc/hi/li/ar/ma)", "Sin tropas licenciadas (cantidades por tipo)"),
        ["err.no-troops-in-army"] = ("No {0} troops in {1}", "Sin tropas {0} en {1}"),
        ["err.no-turn-accepting-orders"] = ("No turn accepting orders", "Ningún turno acepta órdenes"),
        ["err.no-visible-population-centre-here"] = ("No visible population centre here", "Aquí no hay centro visible"),
        ["err.not-a-player-in-this-game"] = ("Not a player in this game", "No juegas en esta partida"),
        ["err.only-major-towns-and-cities-can-have-ports"] = ("Only major towns and cities can have ports", "Solo villas grandes y ciudades tienen puerto"),
        ["err.only-math-max-0-left-recruits-left-at-pc-name-ot"] = ("Only {0} recruits left at {1} (other orders use {2})", "Solo quedan {0} reclutas en {1} (otras órdenes usan {2})"),
        ["err.only-towns-and-larger-can-have-harbours"] = ("Only towns and larger can have harbours", "Solo villas o más tienen fondeadero"),
        ["err.order-not-found-or-cannot-be-cancelled"] = ("Order not found or cannot be cancelled", "Orden no encontrada o no cancelable"),
        ["err.pc-name-already-has-a-harbour-or-port"] = ("{0} already has a harbour or port", "{0} ya tiene fondeadero o puerto"),
        ["err.pc-name-cannot-be-improved-further"] = ("{0} cannot be improved further", "{0} no se puede mejorar más"),
        ["err.pc-name-has-no-fortifications"] = ("{0} has no fortifications", "{0} no tiene fortificaciones"),
        ["err.pc-name-has-no-harbour-or-port"] = ("{0} has no harbour or port", "{0} no tiene fondeadero ni puerto"),
        ["err.pc-name-is-already-fortified-by-another-order-th"] = ("{0} is already fortified by another order this turn", "{0} ya lo fortifica otra orden este turno"),
        ["err.pc-name-needs-a-harbour-not-yet-a-port"] = ("{0} needs a harbour (not yet a port)", "{0} necesita fondeadero (aún no puerto)"),
        ["err.population-centre-is-hidden"] = ("Population centre is hidden", "El centro está oculto"),
        ["err.product-must-be-timber-leather-bronze-steel-mith"] = ("Product must be timber, leather, bronze, steel, mithril, mounts or food", "Producto: madera, cuero, bronce, acero, mitril, monturas o comida"),
        ["err.resource-must-be-timber-leather-bronze-steel-mit"] = ("Resource must be timber, leather, bronze, steel, mithril, mounts or food", "Recurso: madera, cuero, bronce, acero, mitril, monturas o comida"),
        ["err.specify-troops-per-type-hc-lc-hi-li-ar-ma"] = ("Specify troops per type (hc/lc/hi/li/ar/ma)", "Indica tropas por tipo (hc/lc/hi/li/ar/ma)"),
        ["err.store-must-be-timber-food-mounts-leather-bronze"] = ("Store must be timber, food, mounts, leather, bronze, steel or mithril (not gold)", "Reserva de madera, comida, monturas, cuero, bronce, acero o mitril (no oro)"),
        ["err.target-already-challenged-by-higher-challenge-ra"] = ("Target already challenged by higher challenge rank ({0} vs yours {1})", "El objetivo ya lo desafía un rango mayor ({0} vs tu {1})"),
        ["err.target-cannot-be-kidnapped"] = ("Target cannot be kidnapped", "El objetivo no puede ser secuestrado"),
        ["err.target-character-not-found"] = ("Target character not found", "Personaje objetivo no encontrado"),
        ["err.target-commander-not-found"] = ("Target commander not found", "Comandante objetivo no encontrado"),
        ["err.target-is-a-hostage"] = ("Target is a hostage", "El objetivo es un rehén"),
        ["err.target-is-not-a-hostage"] = ("Target is not a hostage", "El objetivo no es un rehén"),
        ["err.target-must-be-at-the-same-location"] = ("Target must be at the same location", "El objetivo debe estar en el mismo lugar"),
        ["err.target-must-be-of-a-different-nation"] = ("Target must be of a different nation", "El objetivo debe ser de otra nación"),
        ["err.target-must-be-of-another-nation"] = ("Target must be of another nation", "El objetivo debe ser de otra nación"),
        ["err.target-must-belong-to-another-nation"] = ("Target must belong to another nation", "El objetivo debe ser de otra nación"),
        ["err.target-must-have-emissary-or-agent-skill"] = ("Target must have emissary or agent skill", "El objetivo debe tener emisario o agente"),
        ["err.target-needs-emissary-skill"] = ("Target needs emissary skill", "El objetivo necesita emisario"),
        ["err.target-not-found"] = ("Target not found", "Objetivo no encontrado"),
        ["err.target-not-found-or-is-a-hostage"] = ("Target not found or is a hostage", "Objetivo no encontrado o es rehén"),
        ["err.target-not-found-or-not-kidnapped"] = ("Target not found or not kidnapped", "Objetivo no encontrado o no secuestrado"),
        ["err.target-not-in-the-same-hex-at-t-locationhex"] = ("Target not in the same hex (at {0})", "El objetivo no está en el mismo hex (en {0})"),
        ["err.the-bearer-must-be-at-mount-doom-34-23"] = ("The bearer must be at Mount Doom (34,23)", "El portador debe estar en el Monte del Destino (34,23)"),
        ["err.troop-type-must-be-hc-lc-hi-li-ar-or-ma"] = ("Troop type must be hc, lc, hi, li, ar or ma", "Tipo de tropa: hc, lc, hi, li, ar o ma"),
        ["err.unknown-spell"] = ("Unknown spell", "Hechizo desconocido"),
        ["err.use-520-on-your-own-centres"] = ("Use 520 on your own centres", "Usa 520 en tus centros"),
        ["err.victim-nation-not-found"] = ("Victim nation not found", "Nación víctima no encontrada"),
        ["err.weapon-material-must-be-bronze-steel-or-mithril"] = ("Weapon material must be bronze, steel or mithril", "Armas de bronce, acero o mitril"),
        ["err.you-already-hold-it"] = ("You already hold it", "Ya lo tienes"),

        // label
        ["label.agent-0-30"] = ("Agent 0-30", "Agente 0-30"),
        ["label.allegiance"] = ("Allegiance", "Lealtad"),
        ["label.amount"] = ("Amount", "Cantidad"),
        ["label.armour"] = ("Armour", "Armadura"),
        ["label.armour-material"] = ("Armour material", "Armadura"),
        ["label.army-name"] = ("Army name", "Nombre de ejército"),
        ["label.artifact"] = ("Artifact", "Artefacto"),
        ["label.artifact-optional"] = ("Artifact (optional)", "Artefacto (opcional)"),
        ["label.artifacts"] = ("Artifacts", "Artefactos"),
        ["label.artifacts-1-6"] = ("Artifacts (1-6)", "Artefactos (1-6)"),
        ["label.bid-price-empty-market"] = ("Bid price (empty = market)", "Puja (vacío = mercado)"),
        ["label.bribe-gold-min-500"] = ("Bribe gold (min 500)", "Soborno en oro (mín 500)"),
        ["label.camp-name-empty-nation-pool"] = ("Camp name (empty = nation pool)", "Nombre campamento (vacío = reserva)"),
        ["label.command-0-30"] = ("Command 0-30", "Mando 0-30"),
        ["label.company-commander"] = ("Company commander", "Comandante de compañía"),
        ["label.company-name"] = ("Company name", "Nombre de compañía"),
        ["label.destination-army"] = ("Destination army", "Ejército destino"),
        ["label.destination-hex"] = ("Destination hex", "Hex destino"),
        ["label.destination-hex-empty-stay"] = ("Destination hex (empty = stay)", "Hex destino (vacío = quedarse)"),
        ["label.emissary-0-30"] = ("Emissary 0-30", "Emisario 0-30"),
        ["label.evasive"] = ("Evasive", "Evasivo"),
        ["label.follow"] = ("Follow", "Seguir"),
        ["label.food-units"] = ("Food units", "Comida"),
        ["label.force-commander"] = ("Force commander", "Comandante de fuerza"),
        ["label.fort-level-empty-next"] = ("Fort level (empty = next)", "Nivel de fuerte (vacío = siguiente)"),
        ["label.gold-amount"] = ("Gold amount", "Oro"),
        ["label.heavy-cavalry"] = ("Heavy cavalry", "Caballería pesada"),
        ["label.heavy-infantry"] = ("Heavy infantry", "Infantería pesada"),
        ["label.hex-empty-current-location"] = ("Hex (empty = current location)", "Hex (vacío = actual)"),
        ["label.hex-empty-force-location"] = ("Hex (empty = force location)", "Hex (vacío = fuerza)"),
        ["label.hex-empty-here"] = ("Hex (empty = here)", "Hex (vacío = aquí)"),
        ["label.hex-to-scry-empty-here"] = ("Hex to scry (empty = here)", "Hex a espiar (vacío = aquí)"),
        ["label.hostage"] = ("Hostage", "Rehén"),
        ["label.light-cavalry"] = ("Light cavalry", "Caballería ligera"),
        ["label.light-infantry"] = ("Light infantry", "Infantería ligera"),
        ["label.mage-0-30"] = ("Mage 0-30", "Mago 0-30"),
        ["label.material"] = ("Material", "Material"),
        ["label.men-at-arms"] = ("Men-at-arms", "Hombres de armas"),
        ["label.movement-artifact"] = ("Movement artifact", "Artefacto de movimiento"),
        ["label.name-5-17-letters-capitalized"] = ("Name (5-17 letters, capitalized)", "Nombre (5-17 letras, mayúscula)"),
        ["label.nation"] = ("Nation", "Nación"),
        ["label.new-capital-major-town-city"] = ("New capital (major town/city)", "Nueva capital (villa grande/ciudad)"),
        ["label.new-commander"] = ("New commander", "Nuevo comandante"),
        ["label.new-tax-rate"] = ("New tax rate", "Nueva tasa"),
        ["label.percentage-of-stock"] = ("Percentage of stock", "Porcentaje de reserva"),
        ["label.product"] = ("Product", "Producto"),
        ["label.rank-points"] = ("Rank points", "Puntos de rango"),
        ["label.ransom-gold-empty-1000"] = ("Ransom gold (empty = 1000)", "Rescate en oro (vacío = 1000)"),
        ["label.receiving-emissary-other-nation-same-hex"] = ("Receiving emissary (other nation, same hex)", "Emisario receptor (otra nación, mismo hex)"),
        ["label.resource"] = ("Resource", "Recurso"),
        ["label.spell"] = ("Spell", "Hechizo"),
        ["label.spell-empty-random-research"] = ("Spell (empty = random research)", "Hechizo (vacío = aleatorio)"),
        ["label.spells-to-forget-1-6"] = ("Spells to forget (1-6)", "Hechizos a olvidar (1-6)"),
        ["label.tactic"] = ("Tactic", "Táctica"),
        ["label.target"] = ("Target", "Objetivo"),
        ["label.target-character"] = ("Target character", "Personaje objetivo"),
        ["label.target-character-emissary-agent-same-hex"] = ("Target character (emissary/agent, same hex)", "Objetivo (emisario/agente, mismo hex)"),
        ["label.target-character-same-hex-empty-self"] = ("Target character (same hex, empty = self)", "Objetivo (mismo hex, vacío = uno mismo)"),
        ["label.target-nation"] = ("Target nation", "Nación objetivo"),
        ["label.to-character-same-hex"] = ("To character (same hex)", "A personaje (mismo hex)"),
        ["label.transports-empty-all"] = ("Transports (empty = all)", "Transportes (vacío = todos)"),
        ["label.transports-to-pick-up"] = ("Transports to pick up", "Transportes a recoger"),
        ["label.troop-type"] = ("Troop type", "Tipo de tropa"),
        ["label.troops"] = ("Troops", "Tropas"),
        ["label.victim-nation"] = ("Victim nation", "Nación víctima"),
        ["label.warships-empty-all"] = ("Warships (empty = all)", "Buques (vacío = todos)"),
        ["label.weapon-material-bronze-steel-mithril"] = ("Weapon material (bronze/steel/mithril)", "Armas (bronce/acero/mitril)"),
        ["label.weapons"] = ("Weapons", "Armas"),

        // opt
        ["opt.1-tower"] = ("1 Tower", "1 Torre"),
        ["opt.2-fort"] = ("2 Fort", "2 Fuerte"),
        ["opt.3-castle"] = ("3 Castle", "3 Castillo"),
        ["opt.4-keep"] = ("4 Keep", "4 Torreón"),
        ["opt.5-citadel"] = ("5 Citadel", "5 Ciudadela"),
        ["opt.agent"] = ("agent", "agente"),
        ["opt.archers"] = ("Archers", "Arqueros"),
        ["opt.bronze-30"] = ("Bronze (30)", "Bronce (30)"),
        ["opt.commander"] = ("commander", "comandante"),
        ["opt.dark-servants"] = ("Dark Servants", "Sirvientes Oscuros"),
        ["opt.emissary"] = ("emissary", "emisario"),
        ["opt.free-peoples"] = ("Free Peoples", "Pueblos Libres"),
        ["opt.heavy-cavalry"] = ("Heavy Cavalry", "Caballería pesada"),
        ["opt.heavy-infantry"] = ("Heavy Infantry", "Infantería pesada"),
        ["opt.held"] = ("(held)", "(en mano)"),
        ["opt.leather-10"] = ("Leather (10)", "Cuero (10)"),
        ["opt.light-cavalry"] = ("Light Cavalry", "Caballería ligera"),
        ["opt.light-infantry"] = ("Light Infantry", "Infantería ligera"),
        ["opt.lost"] = (" [lost]", " [perdido]"),
        ["opt.mage"] = ("mage", "mago"),
        ["opt.men-at-arms"] = ("Men-at-Arms", "Hombres de armas"),
        ["opt.mithril-100"] = ("Mithril (100)", "Mitril (100)"),
        ["opt.neutral"] = ("Neutral", "Neutral"),
        ["opt.steel-60"] = ("Steel (60)", "Acero (60)"),

        // reason
        ["reason.automatic-order"] = ("Automatic order", "Orden automática"),
        ["reason.fourth-age-only"] = ("Fourth Age only", "Solo Cuarta Edad"),
        ["reason.must-be-at-one-of-your-population-centres"] = ("Must be at one of your population centres", "Debes estar en un centro propio"),
        ["reason.must-be-at-your-current-capital"] = ("Must be at your current capital", "Debes estar en tu capital actual"),
        ["reason.must-be-at-your-own-capital"] = ("Must be at your own capital", "Debes estar en tu capital"),
        ["reason.must-command-a-navy"] = ("Must command a navy", "Debes mandar una armada"),
        ["reason.no-artifact-held"] = ("No artifact held", "Sin artefacto en mano"),
        ["reason.no-combat-artifact-held"] = ("No combat artifact held", "Sin artefacto de combate en mano"),
        ["reason.no-combat-spell-known"] = ("No combat spell known", "Sin hechizo de combate"),
        ["reason.no-conjuring-spell-known"] = ("No conjuring spell known", "Sin hechizo de invocación"),
        ["reason.no-healing-spell-known"] = ("No healing spell known", "Sin hechizo de curación"),
        ["reason.no-lore-spell-known"] = ("No lore spell known", "Sin hechizo de saber"),
        ["reason.no-movement-spell-known"] = ("No movement spell known", "Sin hechizo de movimiento"),
        ["reason.no-navy-available"] = ("No navy available", "Sin armada disponible"),
        ["reason.not-in-a-company"] = ("Not in a company", "Fuera de compañía"),
        ["reason.not-in-an-army"] = ("Not in an army", "Fuera del ejército"),
        ["reason.requires"] = ("Requires ", "Requiere "),

        // warn
        ["warn.artifact-already-moved-by-another-pending-order"] = ("Artifact already moved by another pending order", "Otra orden pendiente ya mueve el artefacto"),
        ["warn.target-already-bribed-by-another-character-first"] = ("Target already bribed by another character (first attempt decides)", "Otro personaje ya soborna al objetivo (decide el primer intento)"),
        ["warn.target-already-challenged-by-another-character-o"] = ("Target already challenged by another character (only the highest CR fights)", "Otro personaje ya desafía al objetivo (solo lucha el CR mayor)"),
        ["warn.used-sellgoldused-gold-of-sell-cap-already-used"] = ("{0} gold of sell cap already used by other orders", "{0} de oro del tope ya usados por otras órdenes"),
    };
}
