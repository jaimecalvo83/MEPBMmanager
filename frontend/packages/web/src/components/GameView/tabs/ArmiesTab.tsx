import { useLang, terrainLabel, charTypeLabel, type TFunc } from '../../../i18n/lang';
import { terrainAt, pcSentence, Tip, StatBox } from './shared';


// ═══════════════════════════════════════════
// ARMIES TAB (turn-0 style blocks)
// ═══════════════════════════════════════════
const TROOP_ROWS = [
  { key: 'heavyCavalry', label: 'Heavy Cavalry', w: 'hcWeaponRank', a: 'hcArmourRank', tr: 'hcTraining' },
  { key: 'lightCavalry', label: 'Light Cavalry', w: 'lcWeaponRank', a: 'lcArmourRank', tr: 'lcTraining' },
  { key: 'heavyInfantry', label: 'Heavy Infantry', w: 'hiWeaponRank', a: 'hiArmourRank', tr: 'hiTraining' },
  { key: 'lightInfantry', label: 'Light Infantry', w: 'liWeaponRank', a: 'liArmourRank', tr: 'liTraining' },
  { key: 'archers', label: 'Archers', w: 'archerWeaponRank', a: 'archerArmourRank', tr: 'archerTraining' },
  { key: 'menAtArms', label: 'Men-at-Arms', w: 'maaWeaponRank', a: 'maaArmourRank', tr: 'maaTraining' },
];


function materialName(rank: number, kind: 'weapon' | 'armour', t: TFunc): string {
  if (rank >= 100) return t('army.matMithril');
  if (rank >= 60) return t('army.matSteel');
  if (rank >= 30) return t('army.matBronze');
  if (kind === 'armour') return rank >= 10 ? t('army.matLeather') : t('army.matNone');
  return rank >= 10 ? t('army.matWood') : t('army.matNone');
}


function troopLabel(key: string, t: TFunc): string {
  switch (key) {
    case 'heavyCavalry': return t('army.trHC');
    case 'lightCavalry': return t('army.trLC');
    case 'heavyInfantry': return t('army.trHI');
    case 'lightInfantry': return t('army.trLI');
    case 'archers': return t('army.trAR');
    case 'menAtArms': return t('army.trMA');
    default: return key;
  }
}


function terrainPref(key: string, t: TFunc): string {
  switch (key) {
    case 'heavyCavalry': return t('army.terrPlains');
    case 'lightCavalry': return `${t('army.terrPlains')} / ${t('army.terrDesert')} / ${t('army.terrCoast')}`;
    case 'heavyInfantry': return `${t('army.terrHills')} / ${t('army.terrMountains')}`;
    case 'lightInfantry': return t('army.terrForest');
    case 'archers': return `${t('army.terrHills')} / ${t('army.terrForest')}`;
    case 'menAtArms': return `${t('army.terrPlains')} / ${t('army.terrHills')} / ${t('army.terrCoast')}`;
    default: return '';
  }
}


// Wiki (armies topic) + CombatResolver: Str/Con base, best/worst tactic, upkeep.
const TROOP_INFO: Record<string, { str: number; con: number; best: string; worst: string; terrain: string; gold: number; food: number }> = {
  heavyCavalry: { str: 16, con: 16, best: 'ch', worst: 'am', terrain: 'plains', gold: 6, food: 2 },
  lightCavalry: { str: 8, con: 8, best: 'su', worst: 'am', terrain: 'plains / desert / coast', gold: 3, food: 2 },
  heavyInfantry: { str: 10, con: 10, best: 'fl', worst: 'su', terrain: 'hills / mountains', gold: 4, food: 1 },
  lightInfantry: { str: 5, con: 5, best: 'hr', worst: 'ch', terrain: 'forest', gold: 2, food: 1 },
  archers: { str: 6, con: 2, best: 'am', worst: 'fl', terrain: 'hills / forest', gold: 2, food: 1 },
  menAtArms: { str: 2, con: 2, best: 'hr', worst: 'ch', terrain: 'plains / hills / coast', gold: 1, food: 1 },
};


function armyFoodCost(army: any): number {
  return TROOP_ROWS.reduce((s, t) => s + (army[t.key] || 0) * (TROOP_INFO[t.key]?.food ?? 1), 0);
}


function armyGoldCost(army: any): number {
  return TROOP_ROWS.reduce((s, t) => s + (army[t.key] || 0) * (TROOP_INFO[t.key]?.gold ?? 0), 0);
}


function armyTroopTotal(army: any): number {
  return TROOP_ROWS.reduce((s, t) => s + (army[t.key] || 0), 0);
}


function ArmyCharChip({ c, isCommander, t }: { c: any; isCommander: boolean; t: TFunc }) {
  return (
    <Tip
      trigger={
        <span className={`inline-block px-2 py-1 rounded text-xs border ${isCommander ? 'bg-gray-700 border-mepbm-gold text-mepbm-gold' : 'bg-gray-700 border-gray-600 text-gray-200'}`}>
          {c.name}{isCommander ? ' ★' : ''}
        </span>
      }
    >
      <span className="block text-white font-bold text-sm">{c.name}{isCommander ? t('army.commanderMark') : ''}</span>
      <span className="block text-xs text-gray-400 mt-0.5">{charTypeLabel(c.type, t)}</span>
      <span className="block text-sm text-gray-200 mt-1">
        {t('army.cmdSkills', { c: c.commandSkill ?? 0, a: c.agentSkill ?? 0, e: c.emissarySkill ?? 0, m: c.mageSkill ?? 0 })}
      </span>
      <span className="block text-sm text-gray-200">{t('army.health', { h: `${c.health ?? '?'}${c.maxHealth ? ` / ${c.maxHealth}` : ''}` })}</span>
    </Tip>
  );
}


export default function ArmiesTab({ armies, characters, populationCentres, hexTiles, nationName }: {
  armies: any[]; characters: any[]; populationCentres: any[]; hexTiles: any[]; nationName?: string;
}) {
  const { t, lang } = useLang();
  if (armies.length === 0) {
    return <div className="text-gray-400">{t('army.none')}</div>;
  }

  const charById = new Map<string, any>();
  for (const c of characters) charById.set(c.id, c);

  return (
    <div className="space-y-5">
      {armies.map((army: any) => {
        const commander = army.commanderId ? charById.get(army.commanderId) : null;
        const pcLine = pcSentence(populationCentres, army.locationHex, nationName, lang, t);
        const members = characters.filter((c: any) => c.armyId === army.id);
        const total = armyTroopTotal(army);
        const eats = armyFoodCost(army);
        const upkeep = armyGoldCost(army);
        const food = army.food ?? 0;
        const turns = eats > 0 ? Math.floor(food / eats) : null;
        const rows = TROOP_ROWS.filter((t) => (army[t.key] || 0) > 0);
        const sparesW = army.spareWeapons ?? 0;
        const sparesWMat = army.spareWeaponsMaterial ?? 'none';
        const sparesA = army.spareArmour ?? 0;
        const sparesAMat = army.spareArmourMaterial ?? 'none';
        return (
          <div key={army.id} className="bg-gray-800 rounded-lg p-5 border border-gray-700 space-y-4">
            <div className="flex items-center gap-2 flex-wrap">
              <h3 className="text-xl font-bold text-white">{army.name}</h3>
              <span className="text-xs px-2 py-1 rounded bg-gray-900 border border-gray-600 text-gray-300">
                @ {army.locationHex} · {terrainLabel(terrainAt(hexTiles, army.locationHex), t)}
              </span>
              <span className="ml-auto text-xs text-gray-400">{t('army.troopsWord', { n: total })}</span>
            </div>

            <div>
              <div className="text-[11px] uppercase tracking-wide text-gray-500 mb-1">{t('army.cmdUpkeep')}</div>
              <div className="grid grid-cols-2 md:grid-cols-4 gap-2">
                <StatBox label={t('army.morale')} value={army.morale ?? 0} />
                <StatBox label={t('army.trAvg')} value={army.training ?? 0} />
                <StatBox label={t('army.foodTurn')} value={`${eats} ${t('army.troopsSuffix', { n: total })}`} />
                <StatBox label={t('army.goldTurn')} value={upkeep} />
              </div>
            </div>

            <div>
              <div className="text-[11px] uppercase tracking-wide text-gray-500 mb-1">{t('army.train')}</div>
              <div className="grid grid-cols-2 md:grid-cols-4 gap-2">
                <StatBox label={t('army.food')} value={`${food}${turns != null ? ` ${t('army.turnsSuffix', { n: turns })}` : ''}`} />
                <StatBox label={t('army.wm')} value={army.warMachines ?? 0} />
                <StatBox label={t('army.sw')} value={sparesW > 0 ? `${sparesW} ${sparesWMat}` : '—'} />
                <StatBox label={t('army.sa')} value={sparesA > 0 ? `${sparesA} ${sparesAMat}` : '—'} />
              </div>
            </div>

            {rows.length > 0 && (
              <div>
                <div className="text-[11px] uppercase tracking-wide text-gray-500 mb-1">{t('army.troopsTitle')}</div>
                <table className="w-full text-sm">
                  <thead>
                    <tr className="text-left text-xs text-gray-500 uppercase">
                      <th className="py-1 pr-3">{t('army.thType')}</th>
                      <th className="py-1 pr-3 text-right">#</th>
                      <th className="py-1 pr-3">{t('army.thWeapon')}</th>
                      <th className="py-1 pr-3">{t('army.thArmour')}</th>
                      <th className="py-1 pr-3">{t('army.thTraining')}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-700">
                    {rows.map((row) => {
                      const count = army[row.key] || 0;
                      const w = army[row.w] ?? 0;
                      const a = army[row.a] ?? 0;
                      const tr = army[row.tr] ?? army.training ?? 0;
                      const info = TROOP_INFO[row.key];
                      const shareFood = count * (info?.food ?? 1);
                      const shareGold = count * (info?.gold ?? 0);
                      return (
                        <tr key={row.key} className="text-gray-200">
                          <td className="py-1 pr-3">
                            <Tip
                              trigger={<span className="cursor-help">{troopLabel(row.key, t)}</span>}
                            >
                              <span className="block text-white font-bold text-sm">{troopLabel(row.key, t)} × {count}</span>
                              <span className="block text-sm text-gray-200 mt-1">{t('army.tipStr', { a: info?.str ?? 0, b: info?.con ?? 0 })}</span>
                              <span className="block text-sm text-gray-200">{t('army.tipUpkeep', { g: shareGold, f: shareFood, pg: info?.gold ?? 0, pf: info?.food ?? 0 })}</span>
                              <span className="block text-sm text-gray-200">{t('army.tipTactic', { b: info?.best ?? '', w: info?.worst ?? '' })}</span>
                              <span className="block text-sm text-gray-200">{t('army.tipTerrain', { t: terrainPref(row.key, t) })}</span>
                              <span className="block text-sm text-gray-200 mt-1">{t('army.tipWeapons', { m: materialName(w, 'weapon', t), r: w })}</span>
                              <span className="block text-sm text-gray-200">{t('army.tipArmour', { m: materialName(a, 'armour', t), r: a })}</span>
                              <span className="block text-sm text-gray-200">{t('army.tipTraining', { t: tr })}</span>
                            </Tip>
                          </td>
                          <td className="py-1 pr-3 text-right font-mono">{count}</td>
                          <td className="py-1 pr-3">{materialName(w, 'weapon', t)} <span className="text-gray-500 font-mono">({w})</span></td>
                          <td className="py-1 pr-3">{materialName(a, 'armour', t)} <span className="text-gray-500 font-mono">({a})</span></td>
                          <td className="py-1 pr-3 font-mono">{tr}</td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}

            <div>
              <div className="text-[11px] uppercase tracking-wide text-gray-500 mb-1">
                {t('army.charsTitle')}{members.length > 0 ? ` (${members.length})` : ''}
              </div>
              {members.length > 0 ? (
                <div className="flex flex-wrap gap-2">
                  {members.map((c: any) => (
                    <ArmyCharChip key={c.id} c={c} isCommander={c.id === army.commanderId} t={t} />
                  ))}
                </div>
              ) : (
                <p className="text-sm text-gray-500">—</p>
              )}
              {!commander && <p className="text-xs text-gray-500 mt-1">{t('army.noCommander')}</p>}
            </div>

            {pcLine && <p className="text-sm text-gray-400 border-t border-gray-700 pt-3">{pcLine}</p>}
          </div>
        );
      })}
    </div>
  );
}
