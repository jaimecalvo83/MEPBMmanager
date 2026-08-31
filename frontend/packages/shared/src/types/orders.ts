export type OrderCode = number;

export type OrderRestriction =
  | 'c'       // command skill
  | 'a'       // agent skill
  | 'e'       // emissary skill
  | 'm'       // mage skill
  | 'com'     // army/navy commander
  | 'company' // company commander
  | 'without' // not in command
  | 'cap'     // at capital
  | 'fa'      // fourth age only
  | 'k';      // north/south kingdom

export type OrderDifficulty = 'automatic' | 'easy' | 'average' | 'hard' | 'varies';

export interface OrderDefinition {
  code: OrderCode;
  abbreviation: string;
  name: string;
  restrictions: OrderRestriction[];
  difficulty: OrderDifficulty;
  skillIncrease: boolean;
  description: string;
}

export interface Order {
  id: string;
  gameId: string;
  turnId: string;
  characterId: string;
  code: OrderCode;
  parameters: Record<string, string | number>;
  status: 'pending' | 'validated' | 'executed' | 'failed';
  result?: string;
  submittedAt: Date;
}

export type OrderStatus = 'pending' | 'validated' | 'executed' | 'failed';
