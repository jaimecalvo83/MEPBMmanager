// Compatibility shim: prefer importing from './dict-en', './dict-es',
// './helpers' or './LangContext' directly.
export { en, type DictKey } from './dict-en';
export { es } from './dict-es';
export type { Lang, TFunc } from './LangContext';
export { LangProvider, useLang, LanguageSwitcher } from './LangContext';
export {
  sideLabel, nationName, charTypeLabel, pcSizeLabel, fortLabel, seasonLabel,
  statusLabel, terrainLabel, difficultyLabel, roleLabel, alignmentValue, artifactType,
} from './helpers';
