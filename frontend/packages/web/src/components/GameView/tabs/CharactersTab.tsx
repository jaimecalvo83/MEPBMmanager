import { SPELL_DEFINITIONS } from '@MEPBMmanager/shared';
import { useLang, charTypeLabel, alignmentValue, artifactType, type TFunc } from '../../../i18n/lang';
import { pcSentence, Tip, StatBox } from './shared';


function ArtifactChip({ a, holder, nationName, t }: { a: any; holder: string; nationName?: string; t: TFunc }) {
  const { lang } = useLang();
  const es = lang === 'es';
  const dispName = es ? (a.nameEs ?? a.name ?? a.Name ?? '?') : (a.name ?? a.Name ?? '?');
  const primary = lang === 'es' ? (a.primaryEs ?? a.primaryBenefit ?? a.PrimaryBenefit) : (a.primaryBenefit ?? a.PrimaryBenefit);
  const secondary = lang === 'es' ? (a.secondaryEs ?? a.secondaryPower ?? a.SecondaryPower) : (a.secondaryPower ?? a.SecondaryPower);
  const type = a.type ?? a.Type;
  const bonus = a.bonus ?? a.Bonus ?? 0;
  const alignment = a.alignment ?? a.Alignment;
  const loc = a.locationHex ?? a.LocationHex;
  const wikiId = a.wikiId ?? a.WikiId;
  return (
    <Tip
      trigger={
        <span className="inline-block px-2 py-1 rounded bg-gray-700 border border-amber-600/60 text-xs text-amber-200">
          {wikiId ? `#${wikiId} ` : ''}{dispName}
        </span>
      }
    >
      <span className="block text-amber-200 font-bold text-sm">{wikiId ? `#${wikiId} ` : ''}{dispName}</span>
      {type && <span className="block text-xs text-gray-400 mt-0.5">{artifactType(type, lang)}</span>}
      {primary && <span className="block text-sm text-gray-200 mt-1">{primary}</span>}
      {secondary && secondary !== '-' && <span className="block text-sm text-gray-200">{secondary}</span>}
      <span className="block text-sm text-gray-200 mt-1">{t('char.artBonus', { x: bonus })}</span>
      {alignment && <span className="block text-sm text-gray-200">{t('char.artAlignment', { x: alignmentValue(alignment, t) })}</span>}
      {loc && <span className="block text-sm text-gray-200">{t('char.artLocation', { x: loc })}</span>}
      {nationName && <span className="block text-sm text-gray-200">{t('char.artNation', { x: nationName })}</span>}
      <span className="block text-sm text-gray-200">{t('char.artHolder', { x: holder })}</span>
    </Tip>
  );
}


function SpellChip({ s, t }: { s: any; t: TFunc }) {
  const { lang } = useLang();
  const es = lang === 'es';
  const id = s.spellId ?? s.SpellId;
  const info = SPELL_DEFINITIONS.find((d: any) => d.id === id);
  const dispName = es ? (s.nameEs ?? s.name ?? s.Name ?? info?.name ?? '?') : (s.name ?? s.Name ?? info?.name ?? '?');
  const college = es ? (s.collegeEs ?? s.wikiCollege ?? info?.category ?? s.college ?? '') : (s.wikiCollege ?? info?.category ?? s.college ?? '');
  const minRank = s.minRank ?? info?.minCastingRank;
  const difficulty = es ? (s.difficultyEs ?? s.difficulty) : s.difficulty;
  const castOrder = es ? (s.castOrderEs ?? s.castOrder) : s.castOrder;
  const prereqs = es ? (s.prerequisitesEs ?? s.prerequisites) : s.prerequisites;
  const reqInfo = es ? (s.requiredInfoEs ?? s.requiredInfo) : s.requiredInfo;
  const effect = es ? (s.effectEs ?? s.effect ?? info?.description) : (s.effect ?? info?.description);
  return (
    <Tip
      trigger={
        <span className="inline-block px-2 py-1 rounded bg-gray-700 border border-violet-500/60 text-xs text-violet-200">
          #{id} {dispName} <span className="text-gray-400">({s.rank ?? s.Rank ?? 0})</span>
        </span>
      }
    >
      <span className="block text-violet-200 font-bold text-sm">#{id} {dispName}</span>
      <span className="block text-xs text-gray-400 mt-0.5">
        {college}{difficulty ? ` · ${difficulty}` : ''}{minRank != null ? ` · ${t('char.spMinRank', { x: minRank })}` : ''} · {t('char.spRank', { x: s.rank ?? s.Rank ?? 0 })}
      </span>
      {effect && <span className="block text-sm text-gray-200 mt-1">{effect}</span>}
      {prereqs && <span className="block text-sm text-gray-200 mt-1">{t('char.spPrereq', { x: prereqs })}</span>}
      {reqInfo && <span className="block text-sm text-gray-200">{t('char.spReqInfo', { x: reqInfo })}</span>}
      {castOrder && <span className="block text-xs text-gray-400 mt-1">{castOrder}</span>}
    </Tip>
  );
}


export default function CharactersTab({ characters, armies, populationCentres, nationName, nations }: {
  characters: any[]; armies: any[]; populationCentres: any[]; nationName?: string; nations?: any[];
}) {
  const { t, lang } = useLang();
  if (characters.length === 0) {
    return <div className="text-gray-400">{t('char.none')}</div>;
  }

  const armyById = new Map<string, any>();
  for (const a of armies) armyById.set(a.id, a);
  const nationById = new Map<string, string>();
  for (const n of nations ?? []) nationById.set(n.id, n.name);

  const typeColor: Record<string, string> = {
    commander: 'bg-blue-600',
    agent: 'bg-purple-600',
    emissary: 'bg-teal-600',
    mage: 'bg-orange-600',
  };

  return (
    <div className="space-y-5">
      {characters.map((char: any) => {
        const army = char.armyId ? armyById.get(char.armyId) : null;
        const pcLine = pcSentence(populationCentres, char.locationHex, nationName, lang, t);
        const artifacts: any[] = char.artifacts || [];
        const spells: any[] = char.spells || [];
        return (
          <div key={char.id} className="bg-gray-800 rounded-lg p-5 border border-gray-700 space-y-4">
            <div className="flex items-center gap-2 flex-wrap">
              <h3 className="text-xl font-bold text-white">{char.name}</h3>
              <span className={`text-xs px-2 py-1 rounded text-white ${typeColor[char.type] || 'bg-gray-600'}`}>
                {charTypeLabel(char.type, t)}
              </span>
              {char.isChampion && <span className="text-xs px-2 py-1 rounded bg-yellow-600 text-white">{t('char.champion')}</span>}
              {char.isDead && <span className="text-xs px-2 py-1 rounded bg-red-600 text-white">{t('char.dead')}</span>}
              {char.isKidnapped && <span className="text-xs px-2 py-1 rounded bg-orange-600 text-white">{t('char.kidnapped')}</span>}
              <span className="ml-auto text-xs px-2 py-1 rounded bg-gray-900 border border-gray-600 text-gray-300">
                @ {char.locationHex}
              </span>
            </div>

            <div>
              <div className="text-[11px] uppercase tracking-wide text-gray-500 mb-1">{t('char.skills')}</div>
              <div className="grid grid-cols-2 md:grid-cols-4 gap-2">
                <StatBox label={t('char.command')} value={char.commandSkill} />
                <StatBox label={t('char.agent')} value={char.agentSkill} />
                <StatBox label={t('char.emissary')} value={char.emissarySkill} />
                <StatBox label={t('char.mage')} value={char.mageSkill} />
              </div>
            </div>

            <div>
              <div className="text-[11px] uppercase tracking-wide text-gray-500 mb-1">{t('char.status')}</div>
              <div className="grid grid-cols-3 gap-2 max-w-md">
                <StatBox label={t('char.health')} value={`${char.health ?? '?'}${char.maxHealth ? ` / ${char.maxHealth}` : ''}`} />
                <StatBox label={t('char.stealth')} value={char.stealth ?? 0} />
                <StatBox label={t('char.challenge')} value={char.challengeRank ?? 0} />
              </div>
            </div>

            <div>
              <div className="text-[11px] uppercase tracking-wide text-gray-500 mb-1">
                {t('char.artifacts')}{artifacts.length > 0 ? ` (${artifacts.length})` : ''}
              </div>
              {artifacts.length > 0 ? (
                <div className="flex flex-wrap gap-2">
                  {artifacts.map((a: any) => (
                    <ArtifactChip
                      key={a.id ?? a.Id ?? a.name}
                      a={a}
                      holder={char.name}
                      nationName={nationById.get(a.nationId ?? a.NationId)}
                      t={t}
                    />
                  ))}
                </div>
              ) : (
                <p className="text-sm text-gray-500">—</p>
              )}
            </div>

            <div>
              <div className="text-[11px] uppercase tracking-wide text-gray-500 mb-1">
                {t('char.spells')}{spells.length > 0 ? ` (${spells.length})` : ''}
              </div>
              {spells.length > 0 ? (
                <div className="flex flex-wrap gap-2">
                  {spells.map((s: any) => <SpellChip key={s.spellId ?? s.SpellId} s={s} t={t} />)}
                </div>
              ) : (
                <p className="text-sm text-gray-500">—</p>
              )}
            </div>

            <p className="text-sm text-gray-400 border-t border-gray-700 pt-3">
              {army ? t('char.cmdArmy', { n: char.name, h: char.locationHex }) : t('char.atHex', { n: char.name, h: char.locationHex })}
              {pcLine ? ` ${pcLine}` : ''}
            </p>
          </div>
        );
      })}
    </div>
  );
}
