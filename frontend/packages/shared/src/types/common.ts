export type HexCoordinates = { q: number; r: number };

export type TerrainType =
  | 'plains'
  | 'forest'
  | 'mountains'
  | 'rough'
  | 'desert'
  | 'swamp'
  | 'shore'
  | 'water';

export type Season = 'spring' | 'summer' | 'autumn' | 'winter';

export type GameModule = '1650' | '2950' | 'fourth_age' | 'bofa';

export type GameStatus = 'setup' | 'active' | 'paused' | 'finished';

export type TurnStatus = 'pending' | 'orders_open' | 'processing' | 'completed';

export type Allegiance = 'free_peoples' | 'dark_servants' | 'neutral';

export type RelationLevel = -2 | -1 | 0 | 1 | 2;

export interface Game {
  id: string;
  name: string;
  module: GameModule;
  status: GameStatus;
  currentTurn: number;
  maxTurns: number;
  turnIntervalDays: number;
  createdAt: Date;
  startedAt?: Date;
}

export interface Turn {
  id: string;
  gameId: string;
  number: number;
  status: TurnStatus;
  season: Season;
  deadline: Date;
  processedAt?: Date;
}

export interface GameEvent {
  id: string;
  gameId: string;
  turnId: string;
  type: string;
  data: Record<string, unknown>;
  createdAt: Date;
}
