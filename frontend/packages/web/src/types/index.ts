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
  isPlayer: boolean;
}

export interface GameState {
  game: {
    id: string;
    name: string;
    gameTypeCode: string;
    status: string;
    currentTurn: number;
    maxTurns: number;
    turnIntervalDays: number;
  };
  player: {
    id: string;
    userId: string;
    gameId: string;
    nationId: string | null;
    isReady: boolean;
    joinedAt: string;
  } | null;
  isGameAdmin: boolean;
  turns: Array<{
    id: string;
    number: number;
    status: string;
    season: string;
    deadline: string;
    processedAt: string | null;
  }>;
  nation: {
    id: string;
    name: string;
    gold: number;
    food: number;
    timber: number;
    leather: number;
    bronze: number;
    steel: number;
    mithril: number;
    mounts: number;
    taxRate: number;
    allegiance: string;
    color: string;
    victoryPoints: number;
    warshipStrength: number;
    abilities?: Array<{
      id: string;
      name: string;
    }>;
  } | null;
  nations: Array<{
    id: string;
    name: string;
    allegiance: string;
    color: string;
    gold: number;
    food: number;
    taxRate: number;
    victoryPoints: number;
  }>;
  allNations: Array<{
    id: string;
    name: string;
    allegiance: string;
    color: string;
    victoryPoints: number;
    isEliminated: boolean;
  }>;
  relations: Array<{
    id: string;
    targetNationId: string;
    level: number;
  }>;
  characters: Array<{
    id: string;
    nationId: string;
    name: string;
    type: string;
    isChampion: boolean;
    commandSkill: number;
    agentSkill: number;
    emissarySkill: number;
    mageSkill: number;
    locationHex: string;
    health: number;
    armyId: string | null;
    artifacts?: Array<{
      id: string;
      name: string;
    }>;
    spells?: Array<{
      spellId: number;
      name: string;
      rank: number;
    }>;
  }>;
  armies: Array<{
    id: string;
    nationId: string;
    name: string;
    locationHex: string;
    commanderId: string | null;
    heavyCavalry: number;
    lightCavalry: number;
    heavyInfantry: number;
    lightInfantry: number;
    archers: number;
    menAtArms: number;
    hcWeaponRank: number;
    hcArmourRank: number;
    lcWeaponRank: number;
    lcArmourRank: number;
    hiWeaponRank: number;
    hiArmourRank: number;
    liWeaponRank: number;
    liArmourRank: number;
    archerWeaponRank: number;
    archerArmourRank: number;
    maaWeaponRank: number;
    maaArmourRank: number;
    morale: number;
    training: number;
    food: number;
    warMachines: number;
  }>;
  populationCentres: Array<{
    id: string;
    nationId: string;
    name: string;
    size: string;
    fortification: string | null;
    loyalty: number;
    isCapital: boolean;
    locationHex: string;
  }>;
  hexTiles: Array<{
    q: number;
    r: number;
    terrain: string;
    hasBridge?: boolean;
    hasFord?: boolean;
    hasMajorRiver?: boolean;
    hasMinorRiver?: boolean;
    hasRoad?: boolean;
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
