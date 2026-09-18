import { useState } from 'react';
import { useQuery } from 'react-query';
import { gamesApi } from '../../../api/client';
import { orderName } from '../../Orders/OrderInfoTip';
import { useLang, nationName as trNation, sideLabel, seasonLabel, statusLabel, type TFunc } from '../../../i18n/lang';


// ═══════════════════════════════════════════
// REPORTS TAB (turn results)
// ═══════════════════════════════════════════
function sectionTitle(s: any, tr: TFunc, lang: string): string {
  switch (s.key) {
    case 'summary': return tr('rep.secSummary');
    case 'nation': return `${tr('rep.secNation')}: ${trNation(s.nation ?? '', lang)}${s.allegiance ? ` (${sideLabel(s.allegiance, tr)})` : ''}`;
    case 'economy': return tr('rep.secEconomy');
    case 'famine': return tr('rep.secFamine');
    case 'movement': return tr('rep.secMovement');
    case 'combat': return tr('rep.secCombat');
    case 'recruitment': return tr('rep.secRecruitment');
    case 'econ': return tr('rep.secEcon');
    case 'magic': return tr('rep.secMagic');
    case 'other': return tr('rep.secOther');
    case 'hold': return tr('rep.secHold');
    default: return s.title ?? '';
  }
}


function sectionCat(category: string, tr: TFunc): string {
  switch (category) {
    case 'resources': return tr('rep.catResources');
    case 'tax': return tr('rep.catTax');
    case 'armies': return tr('rep.catArmies');
    case 'pcs': return tr('rep.catPcs');
    case 'chars': return tr('rep.catChars');
    default: return category;
  }
}


function ReportOrderEntry({ e, lang }: { e: any; lang: string }) {
  const ok = e.success === true;
  const hasResult = e.success === true || e.success === false;
  return (
    <div className="bg-gray-900 rounded px-3 py-2 border border-gray-700">
      <div className="flex items-center gap-2 flex-wrap">
        {hasResult && (
          <span className={`text-xs font-bold px-1.5 py-0.5 rounded ${ok ? 'bg-green-700 text-green-100' : 'bg-red-700 text-red-100'}`}>
            {ok ? '✓' : '✗'}
          </span>
        )}
        <span className="text-sm font-bold text-white">{e.character ?? '?'}</span>
        {e.code > 0 && (
          <span className="text-xs text-mepbm-gold">[{e.code}] {orderName(e.code, lang)}</span>
        )}
      </div>
      {e.message ? <p className="text-sm text-gray-300 mt-1">{e.message}</p> : null}
    </div>
  );
}


export default function ReportsTab({ gameId, turns }: { gameId: string; turns: any[] }) {
  const { t: tr, lang } = useLang();
  const [selectedTurnId, setSelectedTurnId] = useState<string | null>(turns[0]?.id ?? null);
  const activeTurnId = turns.some((t: any) => t.id === selectedTurnId) ? selectedTurnId : turns[0]?.id ?? null;

  const { data, isLoading, isError } = useQuery(
    ['turn-report', gameId, activeTurnId, lang],
    async () => {
      const { data } = await gamesApi.getTurnReport(gameId, activeTurnId!);
      return data as { turn: any; sections: Array<{ key?: string; title: string; nation?: string; allegiance?: string; entries: any[] }> };
    },
    { enabled: !!gameId && !!activeTurnId, retry: false }
  );

  if (turns.length === 0) {
    return <div className="text-gray-400">{tr('rep.noTurns')}</div>;
  }

  return (
    <div className="space-y-4">
      <div className="flex gap-2 flex-wrap">
        {turns.map((t: any) => (
          <button
            key={t.id}
            onClick={() => setSelectedTurnId(t.id)}
            className={`px-4 py-2 rounded text-sm font-semibold transition ${
              t.id === activeTurnId
                ? 'bg-mepbm-gold text-gray-900'
                : 'bg-gray-800 text-gray-300 hover:bg-gray-700 border border-gray-700'
            }`}
          >
            {tr('rep.turnBtn', { n: t.number, s: seasonLabel(t.season, tr), st: statusLabel(t.status, tr) })}
          </button>
        ))}
      </div>

      {isLoading && <div className="text-gray-400">{tr('rep.loading')}</div>}
      {isError && <div className="text-red-400">{tr('rep.loadFail')}</div>}
      {data && (
        <div className="space-y-4">
          {data.sections.length === 0 && (
            <div className="text-gray-400">{tr('rep.noResults')}</div>
          )}
          {data.sections.map((s, i) => (
            <div key={i} className="bg-gray-800 rounded-lg p-4 border border-gray-700">
              <h3 className="text-md font-bold text-mepbm-gold mb-2">{sectionTitle(s, tr, lang)}</h3>
              {s.key === 'economy' ? (
                <table className="w-full text-sm">
                  <thead>
                    <tr className="text-left text-xs text-gray-500 uppercase">
                      <th className="py-1 pr-3">{tr('rep.thNation')}</th>
                      <th className="py-1 pr-3 text-right">{tr('rep.thGold')}</th>
                      <th className="py-1 pr-3 text-right">{tr('rep.thFood')}</th>
                      <th className="py-1 pr-3 text-right">{tr('rep.thTax')}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-700">
                    {(s.entries || []).map((e: any, j: number) => (
                      <tr key={j} className="text-gray-200">
                        <td className="py-1 pr-3">{trNation(e.nation ?? '', lang)}</td>
                        <td className="py-1 pr-3 text-right font-mono">{e.gold ?? 0}</td>
                        <td className="py-1 pr-3 text-right font-mono">{e.food ?? 0}</td>
                        <td className="py-1 pr-3 text-right font-mono">{e.tax ?? 0}%</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              ) : s.key === 'summary' || s.key === 'nation' ? (
                <dl className="space-y-2">
                  {(s.entries || []).map((e: any, j: number) => (
                    <div key={j} className="text-sm">
                      <dt className="inline text-gray-500">{sectionCat(e.category, tr)}: </dt>
                      <dd className="inline text-gray-200">{e.detail}</dd>
                    </div>
                  ))}
                </dl>
              ) : s.key === 'famine' ? (
                <div className="space-y-2">
                  {(s.entries || []).map((e: any, j: number) => (
                    <div key={j} className="bg-red-900/30 rounded px-3 py-2 border border-red-800 text-sm">
                      <span className="font-bold text-red-300">{trNation(e.nation ?? '', lang)} · {e.army}</span>
                      {e.message ? <p className="text-gray-300 mt-1">{e.message}</p> : null}
                    </div>
                  ))}
                </div>
              ) : (
                <div className="space-y-2">
                  {(s.entries || []).map((e: any, j: number) => (
                    <ReportOrderEntry key={j} e={e} lang={lang} />
                  ))}
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
