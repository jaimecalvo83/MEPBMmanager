import { useLang, nationName as trNation } from '../../../i18n/lang';


// ═══════════════════════════════════════════
// STANDINGS TAB (victory points per allegiance)
// ═══════════════════════════════════════════
export default function StandingsTab({ allNations }: { allNations: any[] }) {
  const { t, lang } = useLang();
  if (allNations.length === 0) {
    return <div className="text-gray-400">{t('stand.none')}</div>;
  }

  const groups: Array<{ key: string; title: string; color: string }> = [
    { key: 'free_peoples', title: t('game.freeP'), color: 'text-green-400' },
    { key: 'dark_servants', title: t('game.darkP'), color: 'text-red-400' },
    { key: 'neutral', title: t('game.neut'), color: 'text-gray-400' },
  ];

  return (
    <div className="space-y-4">
      <div className="bg-gray-800 rounded-lg p-4 border border-gray-700 text-sm text-gray-300">
        {t('stand.intro')}
        {' '}{t('stand.elimNote')}
      </div>
      {groups.map((g) => {
        const rows = allNations
          .filter((n: any) => n.allegiance === g.key)
          .sort((a: any, b: any) => (b.victoryPoints ?? 0) - (a.victoryPoints ?? 0));
        if (rows.length === 0) return null;
        return (
          <div key={g.key} className="bg-gray-800 rounded-lg border border-gray-700 overflow-hidden">
            <div className={`px-4 py-2 text-sm font-bold uppercase tracking-wider ${g.color}`}>{g.title}</div>
            <table className="w-full">
              <tbody className="divide-y divide-gray-700">
                {rows.map((n: any, i: number) => (
                  <tr key={n.id} className="hover:bg-gray-750">
                    <td className="px-4 py-2 text-sm text-gray-400 w-12">
                      {i === 0 ? '🥇' : i === 1 ? '🥈' : i === 2 ? '🥉' : `${i + 1}º`}
                    </td>
                    <td className="px-4 py-2">
                      <span className="text-sm font-medium text-white flex items-center gap-2">
                        <span className="w-3 h-3 rounded-full inline-block" style={{ backgroundColor: n.color }} />
                        {trNation(n.name, lang)}
                        {n.isEliminated && (
                          <span className="text-xs px-2 py-0.5 rounded bg-red-900 text-red-300">{t('stand.eliminated')}</span>
                        )}
                      </span>
                    </td>
                    <td className="px-4 py-2 text-sm text-right text-mepbm-gold font-bold">{n.victoryPoints ?? 0} {t('stand.vp')}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        );
      })}
    </div>
  );
}
