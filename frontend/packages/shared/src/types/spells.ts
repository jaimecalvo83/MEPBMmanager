export type SpellCategory = 'healing' | 'defensive' | 'offensive' | 'movement' | 'lore' | 'conjuring';

export type SpellId = number;

export interface SpellDefinition {
  id: SpellId;
  name: string;
  category: SpellCategory;
  minCastingRank: number;
  description: string;
}

export interface Spell {
  id: string;
  characterId: string;
  spellId: SpellId;
  isKnown: boolean;
  isLost: boolean;
}

export const SPELL_CATEGORIES: Record<SpellCategory, string> = {
  healing: 'Healing Spells',
  defensive: 'Defensive Spells',
  offensive: 'Offensive Spells',
  movement: 'Movement Spells',
  lore: 'Lore Spells',
  conjuring: 'Conjuring Spells',
};
