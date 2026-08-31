export type { Game, Nation, NationState, Player, NationRelation } from '@MEPBMmanager/shared';
export type { Character, Company, Artifact } from '@MEPBMmanager/shared';
export type { Army, Navy } from '@MEPBMmanager/shared';
export type { PopulationCentre, PCSize, FortificationType } from '@MEPBMmanager/shared';
export type { Order, OrderCode, OrderStatus, OrderDefinition } from '@MEPBMmanager/shared';

export interface GameSummary {
  id: string;
  name: string;
  gameTypeCode: string;
  status: string;
  currentTurn: number;
  playerCount: number;
}

export interface GameState {
  game: {
    id: string;
    name: string;
    status: string;
    currentTurn: number;
  };
  nation: {
    id: string;
    name: string;
    gold: number;
    food: number;
    taxRate: number;
    allegiance: string;
  } | null;
  characters: Array<{
    id: string;
    name: string;
    type: string;
    commandSkill: number;
    agentSkill: number;
    emissarySkill: number;
    mageSkill: number;
    locationHex: string;
    health: number;
  }>;
  armies: Array<{
    id: string;
    name: string;
    locationHex: string;
    heavyCavalry: number;
    lightCavalry: number;
    heavyInfantry: number;
    lightInfantry: number;
    archers: number;
    menAtArms: number;
    morale: number;
  }>;
  populationCentres: Array<{
    id: string;
    name: string;
    size: string;
    loyalty: number;
    isCapital: boolean;
    locationHex: string;
  }>;
  hexTiles: Array<{
    q: number;
    r: number;
    terrain: string;
  }>;
}

export interface NationListItem {
  id: string;
  name: string;
  allegiance: string;
  color: string;
  taken: boolean;
}

export interface OrderListItem {
  id: string;
  characterId: string;
  code: number;
  parameters: Record<string, unknown>;
  status: string;
  character: {
    id: string;
    name: string;
    type: string;
  };
}

export interface MessageItem {
  id: string;
  subject: string;
  content: string;
  isRead: boolean;
  createdAt: string;
  sender: { name: string; color: string };
}
