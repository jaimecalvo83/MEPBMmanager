import { Allegiance } from '../types/common';

export interface NationDefinition {
  id: number;
  name: string;
  allegiance: Allegiance;
  module: ('1650' | '2950')[];
  color: string;
}

export const NATIONS_1650: NationDefinition[] = [
  // FREE PEOPLES
  { id: 1, name: 'The Woodmen', allegiance: 'free_peoples', module: ['1650', '2950'], color: '#228B22' },
  { id: 2, name: 'Northmen', allegiance: 'free_peoples', module: ['1650', '2950'], color: '#4169E1' },
  { id: 4, name: 'Arthedain', allegiance: 'free_peoples', module: ['1650'], color: '#FFD700' },
  { id: 5, name: 'Cardolan', allegiance: 'free_peoples', module: ['1650'], color: '#CD853F' },
  { id: 6, name: 'Northern Gondor', allegiance: 'free_peoples', module: ['1650', '2950'], color: '#808080' },
  { id: 7, name: 'Southern Gondor', allegiance: 'free_peoples', module: ['1650', '2950'], color: '#A9A9A9' },
  { id: 8, name: 'Dwarves', allegiance: 'free_peoples', module: ['1650', '2950'], color: '#B8860B' },
  { id: 9, name: 'Sinda Elves', allegiance: 'free_peoples', module: ['1650', '2950'], color: '#9370DB' },
  { id: 10, name: 'Noldo Elves', allegiance: 'free_peoples', module: ['1650', '2950'], color: '#BA55D3' },
  { id: 3, name: 'Éothraim', allegiance: 'free_peoples', module: ['1650'], color: '#006400' },

  // DARK SERVANTS
  { id: 11, name: 'Witch-king', allegiance: 'dark_servants', module: ['1650', '2950'], color: '#8B0000' },
  { id: 12, name: 'Dragon Lord', allegiance: 'dark_servants', module: ['1650', '2950'], color: '#FF4500' },
  { id: 13, name: 'Dog Lord', allegiance: 'dark_servants', module: ['1650', '2950'], color: '#8B4513' },
  { id: 14, name: 'Cloud Lord', allegiance: 'dark_servants', module: ['1650', '2950'], color: '#708090' },
  { id: 15, name: 'Blind Sorcerer', allegiance: 'dark_servants', module: ['1650', '2950'], color: '#2F4F4F' },
  { id: 16, name: 'Ice King', allegiance: 'dark_servants', module: ['1650', '2950'], color: '#00CED1' },
  { id: 17, name: 'Quiet Avenger', allegiance: 'dark_servants', module: ['1650', '2950'], color: '#483D8B' },
  { id: 18, name: 'Fire King', allegiance: 'dark_servants', module: ['1650', '2950'], color: '#FF6347' },
  { id: 19, name: 'Long Rider', allegiance: 'dark_servants', module: ['1650', '2950'], color: '#556B2F' },
  { id: 20, name: 'Dark Lieutenants', allegiance: 'dark_servants', module: ['1650', '2950'], color: '#696969' },

  // NEUTRALS
  { id: 21, name: 'Corsairs', allegiance: 'neutral', module: ['1650', '2950'], color: '#000000' },
  { id: 22, name: 'Haradwaith', allegiance: 'neutral', module: ['1650'], color: '#D2691E' },
  { id: 23, name: 'Dunlendings', allegiance: 'neutral', module: ['1650', '2950'], color: '#9ACD32' },
  { id: 24, name: 'Rhudaur', allegiance: 'neutral', module: ['1650'], color: '#BDB76B' },
  { id: 25, name: 'Easterlings', allegiance: 'neutral', module: ['1650'], color: '#DAA520' },
];

export const NATIONS_2950_EXTRA: NationDefinition[] = [
  { id: 101, name: 'Dúnadan Rangers', allegiance: 'free_peoples', module: ['2950'], color: '#2E8B57' },
  { id: 102, name: 'Riders of Rohan', allegiance: 'free_peoples', module: ['2950'], color: '#00FF00' },
  { id: 103, name: 'Silvan Elves', allegiance: 'free_peoples', module: ['2950'], color: '#DDA0DD' },
  { id: 104, name: 'White Wizard', allegiance: 'neutral', module: ['2950'], color: '#FFFAFA' },
  { id: 105, name: 'Khand Easterlings', allegiance: 'neutral', module: ['2950'], color: '#B8860B' },
  { id: 106, name: 'Rhûn Easterlings', allegiance: 'neutral', module: ['2950'], color: '#CD853F' },
];

export const ALL_NATIONS = [...NATIONS_1650, ...NATIONS_2950_EXTRA];
