import { useLang, nationName as trNation, sideLabel, seasonLabel } from '../../../i18n/lang';


// ═══════════════════════════════════════════
// NATION TAB (turn-0 style overview)
// ═══════════════════════════════════════════
export default function NationTab({ nation, populationCentres, armies, characters, currentTurn }: {
  nation: any;
  populationCentres: any[];
  armies: any[];
  characters: any[];
  currentTurn: any;
}) {
  const { t, lang } = useLang();
  if (!nation) {
    return <div className="text-gray-400">{t('nation.none')}</div>;
  }
  const capital = populationCentres.find((p: any) => p.isCapital);
  const abilities: any[] = nation.abilities || [];
  const stats = [
    { label: t('nation.cities'), value: populationCentres.length },
    { label: t('nation.armies'), value: armies.length },
    { label: t('nation.characters'), value: characters.length },
    { label: t('nation.taxRate'), value: nation.taxRate != null ? `${nation.taxRate}%` : '—' },
    { label: t('nation.vp'), value: nation.victoryPoints ?? 0 },
    { label: t('nation.ws'), value: nation.warshipStrength ?? 0 },
  ];

  return (
    <div className="space-y-4">
      <div className="bg-gray-800 rounded-lg p-6 border border-gray-700">
        <div className="flex items-center gap-3">
          <div className="w-6 h-6 rounded-full" style={{ backgroundColor: nation.color }} />
          <div>
            <h2 className="text-2xl font-bold text-white">{trNation(nation.name, lang)}</h2>
            <p className="text-sm text-gray-400">
              {sideLabel(nation.allegiance, t)}
              {currentTurn ? ` · ${t('game.turnShort', { n: currentTurn.number, s: currentTurn.season ? ` · ${seasonLabel(currentTurn.season, t)}` : '' })}` : ''}
            </p>
          </div>
        </div>
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3 mt-4">
          {stats.map((s) => (
            <div key={s.label} className="bg-gray-750 rounded-lg p-3 border border-gray-700">
              <div className="text-xs text-gray-400 uppercase">{s.label}</div>
              <div className="text-lg font-bold text-mepbm-gold">{s.value}</div>
            </div>
          ))}
        </div>
        {capital && (
          <p className="mt-3 text-sm text-gray-300">
            {t('nation.capitalLine', { x: capital.name, h: capital.locationHex })}
          </p>
        )}
      </div>
      <div className="bg-gray-800 rounded-lg p-6 border border-gray-700">
        <h3 className="text-sm font-bold text-mepbm-gold uppercase tracking-wider mb-3">{t('nation.abilities')}</h3>
        {abilities.length === 0 ? (
          <p className="text-sm text-gray-400">—</p>
        ) : (
          <ul className="list-disc list-inside space-y-1">
            {abilities.map((a: any) => (
              <li key={a.id} className="text-sm text-gray-200">
                {lang === 'es' ? a.name : (a.nameEn ?? a.name)}
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}
