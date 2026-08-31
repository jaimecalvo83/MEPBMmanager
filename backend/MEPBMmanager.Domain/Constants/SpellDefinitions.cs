using MEPBMmanager.Domain.Enums;

namespace MEPBMmanager.Domain.Constants;

public record SpellDefinition(int Id, string Name, SpellCategory Category, int MinCastingRank, string Description);

public static class SpellDefinitions
{
    public static readonly SpellDefinition[] Spells =
    [
        // HEALING SPELLS
        new(2, "Minor Heal", SpellCategory.Healing, 10, "Heal minor wounds"),
        new(4, "Major Heal", SpellCategory.Healing, 30, "Heal major wounds"),
        new(6, "Greater Heal", SpellCategory.Healing, 50, "Heal severe wounds"),
        new(8, "Heal True", SpellCategory.Healing, 70, "Heal all wounds completely"),

        // DEFENSIVE SPELLS
        new(102, "Barriers", SpellCategory.Defensive, 10, "Create magical barriers"),
        new(104, "Resistances", SpellCategory.Defensive, 20, "Grant magical resistances"),
        new(106, "Deflections", SpellCategory.Defensive, 30, "Deflect incoming attacks"),
        new(108, "Blessings", SpellCategory.Defensive, 40, "Bless troops for protection"),
        new(110, "Protections", SpellCategory.Defensive, 50, "Grant magical protections"),
        new(112, "Shields", SpellCategory.Defensive, 60, "Create magical shields"),
        new(114, "Barrier Walls", SpellCategory.Defensive, 70, "Create walls of barriers"),
        new(116, "Force Walls", SpellCategory.Defensive, 80, "Create walls of force"),

        // OFFENSIVE SPELLS
        new(202, "Call Fire", SpellCategory.Offensive, 10, "Call forth fire"),
        new(204, "Wild Flames", SpellCategory.Offensive, 15, "Unleash wild flames"),
        new(206, "Wall of Fire", SpellCategory.Offensive, 20, "Create a wall of fire"),
        new(208, "Words of Pain", SpellCategory.Offensive, 25, "Speak words of pain"),
        new(210, "Words of Calm", SpellCategory.Offensive, 30, "Speak words of calm (defensive)"),
        new(212, "Words of Paralysis", SpellCategory.Offensive, 35, "Paralyze enemies"),
        new(214, "Call Winds", SpellCategory.Offensive, 40, "Call forth winds"),
        new(216, "Wild Winds", SpellCategory.Offensive, 45, "Unleash wild winds"),
        new(218, "Wall of Wind", SpellCategory.Offensive, 50, "Create a wall of wind"),
        new(220, "Words of Agony", SpellCategory.Offensive, 55, "Cause agony"),
        new(222, "Words of Stun", SpellCategory.Offensive, 60, "Stun enemies"),
        new(224, "Words of Command", SpellCategory.Offensive, 65, "Issue magical commands"),
        new(226, "Chill Bolts", SpellCategory.Offensive, 70, "Launch chill bolts"),
        new(228, "Frost Balls", SpellCategory.Offensive, 75, "Launch frost balls"),
        new(230, "Wind Storms", SpellCategory.Offensive, 80, "Create wind storms"),
        new(232, "Fire Bolts", SpellCategory.Offensive, 85, "Launch fire bolts"),
        new(234, "Fire Balls", SpellCategory.Offensive, 90, "Launch fire balls"),
        new(236, "Fire Storms", SpellCategory.Offensive, 95, "Create fire storms"),
        new(238, "Summon Wind Spirits", SpellCategory.Offensive, 60, "Summon wind spirits"),
        new(240, "Summon Fire Spirits", SpellCategory.Offensive, 70, "Summon fire spirits"),
        new(242, "Words of Death", SpellCategory.Offensive, 80, "Speak words of death"),
        new(244, "Fearful Hearts", SpellCategory.Offensive, 85, "Cause fear in hearts"),
        new(246, "Summon Storms", SpellCategory.Offensive, 90, "Summon storms"),
        new(248, "Fanaticism", SpellCategory.Offensive, 95, "Inspire fanaticism"),

        // MOVEMENT SPELLS
        new(302, "Long Stride", SpellCategory.Movement, 10, "Increase movement range"),
        new(304, "Fast Stride", SpellCategory.Movement, 20, "Greatly increase movement"),
        new(306, "Path Mastery", SpellCategory.Movement, 30, "Master all paths"),
        new(308, "Capital Return", SpellCategory.Movement, 40, "Return to capital"),
        new(310, "Major Return", SpellCategory.Movement, 50, "Return to major town"),
        new(312, "Return True", SpellCategory.Movement, 60, "Return to any friendly PC"),
        new(314, "Teleport", SpellCategory.Movement, 80, "Teleport to any location"),

        // LORE SPELLS
        new(402, "Perceive Allegiance", SpellCategory.Lore, 10, "Perceive nation allegiance"),
        new(404, "Perceive Relations", SpellCategory.Lore, 15, "Perceive nation relations"),
        new(406, "Divine Army", SpellCategory.Lore, 20, "Divine information about armies"),
        new(408, "Perceive Nationality", SpellCategory.Lore, 25, "Perceive character nationality"),
        new(410, "Divine Allegiance Forces", SpellCategory.Lore, 30, "Divine allegiance forces"),
        new(412, "Research Artifact", SpellCategory.Lore, 35, "Research an artifact"),
        new(413, "Scry Population Centre", SpellCategory.Lore, 40, "Scry a population centre"),
        new(414, "Scry Hex", SpellCategory.Lore, 45, "Scry a hex"),
        new(415, "Scry Area", SpellCategory.Lore, 50, "Scry an area"),
        new(416, "Reveal Production", SpellCategory.Lore, 55, "Reveal PC production"),
        new(417, "Divine Characters With Forces", SpellCategory.Lore, 60, "Divine characters with forces"),
        new(418, "Locate Artifact", SpellCategory.Lore, 65, "Locate an artifact"),
        new(419, "Divine Nation Forces", SpellCategory.Lore, 70, "Divine nation forces"),
        new(420, "Reveal Character", SpellCategory.Lore, 75, "Reveal a hidden character"),
        new(422, "Perceive Power", SpellCategory.Lore, 80, "Perceive magical power"),
        new(424, "Perceive Mission", SpellCategory.Lore, 85, "Perceive character mission"),
        new(426, "Divine Army True", SpellCategory.Lore, 90, "Divine army information perfectly"),
        new(428, "Locate Artifact True", SpellCategory.Lore, 95, "Locate artifact perfectly"),
        new(430, "Reveal Character True", SpellCategory.Lore, 100, "Reveal character perfectly"),
        new(432, "Perceive Secrets", SpellCategory.Lore, 60, "Perceive hidden secrets"),
        new(434, "Reveal Population Centre", SpellCategory.Lore, 70, "Reveal hidden PC"),
        new(436, "Scry Character", SpellCategory.Lore, 50, "Scry a character"),

        // CONJURING SPELLS
        new(502, "Weakness", SpellCategory.Conjuring, 40, "Conjure weakness (lost spell)"),
        new(504, "Sickness", SpellCategory.Conjuring, 50, "Conjure sickness (lost spell)"),
        new(506, "Curses", SpellCategory.Conjuring, 60, "Conjure curses (lost spell)"),
        new(508, "Conjure Mounts", SpellCategory.Conjuring, 70, "Conjure mounts (lost spell)"),
        new(510, "Conjure Food", SpellCategory.Conjuring, 80, "Conjure food (lost spell)"),
        new(512, "Conjure Hordes", SpellCategory.Conjuring, 90, "Conjure hordes (lost spell, DS only)"),
    ];
}
