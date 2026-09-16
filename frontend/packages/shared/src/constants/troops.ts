import { TroopType, MaterialRank } from '../types/army';

export const TROOP_NAMES: Record<TroopType, string> = {
  hc: 'Heavy Cavalry',
  lc: 'Light Cavalry',
  hi: 'Heavy Infantry',
  li: 'Light Infantry',
  ar: 'Archers',
  ma: 'Men-at-Arms',
};

export const TROOP_COST_PER_100: Record<TroopType, number> = {
  hc: 3000,
  lc: 1500,
  hi: 2000,
  li: 1000,
  ar: 1000,
  ma: 500,
};

export const FOOD_PER_TROOP: Record<TroopType, number> = {
  hc: 2,
  lc: 2,
  hi: 1,
  li: 1,
  ar: 1,
  ma: 1,
};

export const MAINTENANCE_PER_100: Record<TroopType, number> = {
  hc: 600,
  lc: 300,
  hi: 400,
  li: 200,
  ar: 200,
  ma: 100,
};

export const MATERIAL_RANK_VALUES: Record<MaterialRank, number> = {
  none: 0,
  wood: 10,
  leather: 10,
  bronze: 30,
  steel: 60,
  mithril: 100,
};

export const MATERIAL_NAMES: Record<MaterialRank, string> = {
  none: 'None',
  wood: 'Wood',
  leather: 'Leather',
  bronze: 'Bronze',
  steel: 'Steel',
  mithril: 'Mithril',
};

export const ARMY_SIZE_MIN = 100;
export const ARMY_SIZE_MAX = 10000;

export const MOVEMENT_COSTS: Record<string, Record<string, number>> = {
  fed: { plains: 1, forest: 2, mountains: 3, rough: 2, desert: 1, swamp: 3, shore: 1 },
  unfed: { plains: 2, forest: 4, mountains: 6, rough: 4, desert: 2, swamp: 6, shore: 2 },
};
