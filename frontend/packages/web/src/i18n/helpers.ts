import type { TFunc } from './LangContext';

export function sideLabel(code: string, t: TFunc): string {
  if (code === 'free_peoples') return t('game.freeP');
  if (code === 'dark_servants') return t('game.darkP');
  if (code === 'neutral') return t('game.neut');
  return code;
}

export function charTypeLabel(code: string | undefined, t: TFunc): string {
  switch ((code ?? '').toLowerCase()) {
    case 'commander': return t('data.typeCommander');
    case 'agent': return t('data.typeAgent');
    case 'emissary': return t('data.typeEmissary');
    case 'mage': return t('data.typeMage');
    default: return code ?? '';
  }
}

export function pcSizeLabel(size: string | undefined, t: TFunc): string {
  switch ((size ?? '').toLowerCase()) {
    case 'camp': return t('data.sizeCamp');
    case 'village': return t('data.sizeVillage');
    case 'town': return t('data.sizeTown');
    case 'major town': return t('data.sizeMajorTown');
    case 'city': return t('data.sizeCity');
    case 'fortress': return t('data.sizeFortress');
    case 'citadel': return t('data.sizeCitadel');
    default: return size ?? '';
  }
}

export function fortLabel(fort: string | undefined, t: TFunc): string {
  switch ((fort ?? '').toLowerCase()) {
    case 'tower': return t('data.fortTower');
    case 'fort': return t('data.fortFort');
    case 'castle': return t('data.fortCastle');
    case 'keep': return t('data.fortKeep');
    case 'citadel': return t('data.fortCitadel');
    case 'palisade': return t('data.fortPalisade');
    case 'stone walls': return t('data.fortStoneWalls');
    case 'walls': return t('data.fortWalls');
    case 'fortress': return t('data.fortFortress');
    case 'citadel walls': return t('data.fortCitadelWalls');
    default: return fort ?? '';
  }
}

export function seasonLabel(s: string | undefined, t: TFunc): string {
  switch ((s ?? '').toLowerCase()) {
    case 'spring': return t('data.seasonSpring');
    case 'summer': return t('data.seasonSummer');
    case 'autumn': case 'fall': return t('data.seasonAutumn');
    case 'winter': return t('data.seasonWinter');
    default: return s ?? '';
  }
}

export function statusLabel(s: string | undefined, t: TFunc): string {
  switch ((s ?? '').toLowerCase()) {
    case 'active': return t('data.stActive');
    case 'setup': return t('data.stSetup');
    case 'orders_open': return t('data.stOrdersOpen');
    case 'processing': return t('data.stProcessing');
    case 'completed': return t('data.stCompleted');
    case 'pending': return t('data.stPending');
    case 'validated': return t('data.stValidated');
    case 'executed': return t('data.stExecuted');
    case 'failed': return t('data.stFailed');
    default: return s ?? '';
  }
}

export function terrainLabel(s: string | undefined, t: TFunc): string {
  switch ((s ?? '').toLowerCase()) {
    case 'plains': return t('data.terrPlains');
    case 'forest': return t('data.terrForest');
    case 'mountains': return t('data.terrMountains');
    case 'rough': return t('data.terrRough');
    case 'desert': return t('data.terrDesert');
    case 'swamp': return t('data.terrSwamp');
    case 'shore': return t('data.terrShore');
    case 'coastal': return t('data.terrCoastal');
    case 'water': return t('data.terrWater');
    case 'ocean': return t('data.terrOcean');
    case 'hills': return t('data.terrHills');
    case 'river': return t('data.terrRiver');
    case 'coast': return t('data.terrCoast');
    case 'marsh': return t('data.terrMarsh');
    case 'sea': return t('data.terrSea');
    default: return s ?? '';
  }
}

export function difficultyLabel(v: string | undefined, t: TFunc): string {
  switch ((v ?? '').toLowerCase()) {
    case 'automatic': return t('ord.diffAutomatic');
    case 'easy': return t('ord.diffEasy');
    case 'average': return t('ord.diffAverage');
    case 'hard': return t('ord.diffHard');
    case 'varies': return t('ord.diffVaries');
    default: return v ?? '';
  }
}

export function alignmentValue(v: string | undefined, t: TFunc): string {
  switch ((v ?? '').toLowerCase()) {
    case '': case 'none': return t('data.alNone');
    case 'neutral': return t('data.alNeutral');
    case 'good': return t('data.alGood');
    case 'evil': return t('data.alEvil');
    default: return v ?? '';
  }
}

const NATION_ES: Record<string, string> = {
  'woodmen': 'Hombres del Bosque',
  'northmen': 'Hombres del Norte',
  'riders of rohan': 'Jinetes de Rohan',
  'dúnadan rangers': 'Montaraces Dúnedain',
  'dunadan rangers': 'Montaraces Dúnedain',
  'silvan elves': 'Elfos Silvanos',
  'northern gondor': 'Gondor del Norte',
  'southern gondor': 'Gondor del Sur',
  'dwarves': 'Enanos',
  'sinda elves': 'Elfos Sindar',
  'noldo elves': 'Elfos Noldor',
  'witch-king': 'Rey Brujo',
  'witch king': 'Rey Brujo',
  'dragon lord': 'Señor de los Dragones',
  'dog lord': 'Señor de los Perros',
  'cloud lord': 'Señor de las Nubes',
  'blind sorcerer': 'Hechicero Ciego',
  'ice king': 'Rey de Hielo',
  'quiet avenger': 'Vengador Silencioso',
  'fire king': 'Rey del Fuego',
  'long rider': 'Jinete Largo',
  'dark lieutenants': 'Lugartenientes Oscuros',
  'corsairs': 'Corsarios',
  'rhûn easterlings': 'Orientales de Rhûn',
  'rhun easterlings': 'Orientales de Rhûn',
  'dunlendings': 'Dunlendinos',
  'white wizard': 'Mago Blanco',
  'khand easterlings': 'Orientales de Khand',
};

export function nationName(name: string | undefined, lang: string): string {
  if (!name) return '';
  if (lang !== 'es') return name;
  return NATION_ES[name.toLowerCase()] ?? name;
}

const ARTIFACT_TYPE_ES: Record<string, string> = {
  sword: 'Espada', weapon: 'Arma', bow: 'Arco', mace: 'Maza', scimitar: 'Cimitarra',
  hammer: 'Martillo', lance: 'Lanza', club: 'Garrote', flail: 'Mangual', axe: 'Hacha',
  bola: 'Bola', spear: 'Lanza', boots: 'Botas', mirror: 'Espejo', orb: 'Orbe',
  sphere: 'Esfera', cloak: 'Capa', robes: 'Túnicas', robe: 'Túnica', ring: 'Anillo',
  staff: 'Báculo', helm: 'Yelmo', armour: 'Armadura', armor: 'Armadura', shield: 'Escudo',
  plate: 'Coraza', crown: 'Corona', bracelet: 'Brazalete', bracers: 'Brazales',
  mantle: 'Manto', collar: 'Collar', rod: 'Vara', belt: 'Cinturón', talisman: 'Talismán',
  dagger: 'Daga', rapier: 'Estoque', sickle: 'Hoz', sceptre: 'Cetro', book: 'Libro',
  tablets: 'Tablillas', pectoral: 'Pectoral', gauntlets: 'Guanteletes', amulet: 'Amuleto',
  blade: 'Hoja', knife: 'Cuchillo', gown: 'Túnica', cap: 'Gorro',
};

export function artifactType(t: string | undefined, lang: string): string {
  if (!t || lang !== 'es') return t ?? '';
  return ARTIFACT_TYPE_ES[t.toLowerCase()] ?? t;
}

export function roleLabel(role: string | undefined, t: TFunc): string {
  const r = (role ?? '').toLowerCase();
  if (r === 'test_admin' || r === 'test admin') return t('data.roleTestAdmin');
  return role ?? '';
}
