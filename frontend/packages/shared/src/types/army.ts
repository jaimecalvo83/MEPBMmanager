export type TroopType = 'hc' | 'lc' | 'hi' | 'li' | 'ar' | 'ma';

export type MaterialRank = 'none' | 'wood' | 'leather' | 'bronze' | 'steel' | 'mithril';

export type ArmyDirection = 'h' | 'ne' | 'e' | 'se' | 'sw' | 'w' | 'nw';

export type Tactics = 'ch' | 'fl' | 'st' | 'su' | 'hr' | 'am';

export interface Army {
  id: string;
  nationId: string;
  name: string;
  locationHex: string;
  commanderId?: string;
  heavyCavalry: number;
  lightCavalry: number;
  heavyInfantry: number;
  lightInfantry: number;
  archers: number;
  menAtArms: number;
  weaponRank: number;
  armourRank: number;
  morale: number;
  training: number;
  food: number;
  warMachines: number;
  isOnManoeuvres: boolean;
  size: number;
}

export interface Navy {
  id: string;
  nationId: string;
  warships: number;
  transports: number;
  locationHex: string;
  commanderId?: string;
  strength: number;
}

export const TROOP_COSTS: Record<TroopType, number> = {
  hc: 3000,
  lc: 1500,
  hi: 2000,
  li: 1000,
  ar: 1000,
  ma: 500,
};

export const MATERIAL_RANKS: Record<MaterialRank, number> = {
  none: 0,
  wood: 10,
  leather: 20,
  bronze: 40,
  steel: 60,
  mithril: 100,
};

export const FOOD_CONSUMPTION: Record<TroopType, number> = {
  hc: 2,
  lc: 3,
  hi: 1,
  li: 1,
  ar: 1,
  ma: 1,
};
