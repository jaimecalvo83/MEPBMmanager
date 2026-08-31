import { HexCoordinates, Allegiance, RelationLevel } from './common';

export type NationModule = '1650' | '2950' | 'fourth_age';

export interface Nation {
  id: string;
  gameId: string;
  name: string;
  module: NationModule;
  allegiance: Allegiance;
  color: string;
  isEliminated: boolean;
  eliminatedAt?: Date;
}

export interface NationState {
  nationId: string;
  gold: number;
  food: number;
  timber: number;
  leather: number;
  bronze: number;
  steel: number;
  mithril: number;
  mounts: number;
  taxRate: number;
  capitalId?: string;
}

export interface NationRelation {
  id: string;
  nationId: string;
  targetNationId: string;
  level: RelationLevel;
}

export interface Player {
  id: string;
  userId: string;
  gameId: string;
  nationId: string;
  isReady: boolean;
  joinedAt: Date;
}
