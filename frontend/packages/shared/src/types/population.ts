export type PCSize = 'village' | 'town' | 'city';

export type FortificationType = 'tower' | 'fort' | 'castle' | 'keep' | 'citadel';

export interface PopulationCentre {
  id: string;
  nationId?: string;
  name: string;
  size: PCSize;
  locationHex: string;
  loyalty: number;
  production: number;
  stores: number;
  isCapital: boolean;
  isHidden: boolean;
  isSieged: boolean;
  hasHarbour: boolean;
  hasPort: boolean;
  fortification?: FortificationType;
}

export const PC_SIZE_VALUES: Record<PCSize, number> = {
  village: 100,
  town: 200,
  city: 400,
};

export const FORTIFICATION_VALUES: Record<FortificationType, number> = {
  tower: 50,
  fort: 100,
  castle: 200,
  keep: 350,
  citadel: 500,
};
