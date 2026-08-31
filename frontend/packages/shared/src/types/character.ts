export type SkillType = 'command' | 'agent' | 'emissary' | 'mage';

export type CharacterType = 'commander' | 'agent' | 'emissary' | 'mage' | 'champion';

export interface Character {
  id: string;
  nationId: string;
  name: string;
  type: CharacterType;
  isChampion: boolean;
  commandSkill: number;
  agentSkill: number;
  emissarySkill: number;
  mageSkill: number;
  health: number;
  maxHealth: number;
  stealth: number;
  challengeRank: number;
  locationHex: string;
  isDead: boolean;
  isKidnapped: boolean;
  heldByNationId?: string;
  companyId?: string;
  armyId?: string;
}

export interface Company {
  id: string;
  nationId: string;
  name: string;
  locationHex: string;
  characters: string[];
}

export interface Artifact {
  id: string;
  nationId?: string;
  name: string;
  type: 'combat' | 'movement' | 'scrying' | 'hiding';
  alignment: 'good' | 'evil' | 'none';
  bonus: number;
  locationHex?: string;
  heldByCharacterId?: string;
  isAtCapital: boolean;
}
