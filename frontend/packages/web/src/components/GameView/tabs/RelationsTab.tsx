import { useLang, sideLabel, nationName as trNation, type TFunc } from '../../../i18n/lang';


// ═══════════════════════════════════════════
// RELATIONS TAB
// ═══════════════════════════════════════════
function relLevels(t: TFunc) {
  return [
    { value: 2, label: t('rel.ally') },
    { value: 1, label: t('rel.tolerant') },
    { value: 0, label: t('rel.neutral') },
    { value: -1, label: t('rel.hostile') },
    { value: -2, label: t('rel.enemy') },
  ];
}


function relationBadge(level: number, t: TFunc) {
  const color =
    level >= 2 ? 'bg-green-600 text-white'
    : level === 1 ? 'bg-blue-600 text-white'
    : level === 0 ? 'bg-gray-600 text-white'
    : level === -1 ? 'bg-orange-600 text-white'
    : 'bg-red-600 text-white';
  const label = relLevels(t).find((l) => l.value === level)?.label ?? `${level}`;
  return <span className={`px-2 py-1 rounded text-xs ${color}`}>{label} ({level})</span>;
}


export default function RelationsTab({ nationId, nationName, allNations, relations }: {
  nationId?: string;
  nationName?: string;
  allNations: any[];
  relations: any[];
}) {
  const { t, lang } = useLang();

  if (!nationId) {
    return <div className="text-gray-400">{t('rel.selectNation')}</div>;
  }

  const relByTarget = new Map<string, number>();
  for (const r of relations) relByTarget.set(r.targetNationId, r.level);
  const others = (allNations || []).filter((n: any) => n.id !== nationId);

  return (
    <div className="space-y-4">
      <div className="bg-gray-800 rounded-lg p-4 border border-gray-700 text-sm text-gray-300">
        {t('rel.introA')}<span className="font-bold text-white">{trNation(nationName, lang)}</span>{t('rel.introB')}
        {' '}{t('rel.viaOrders')}
      </div>
      <div className="bg-gray-800 rounded-lg border border-gray-700 overflow-hidden">
        <table className="w-full">
          <thead>
            <tr className="border-b border-gray-700 bg-gray-750">
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-400 uppercase">{t('rel.thNation')}</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-400 uppercase">{t('rel.thSide')}</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-400 uppercase">{t('rel.thRelation')}</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-700">
            {others.map((n: any) => {
              const level = relByTarget.has(n.id) ? relByTarget.get(n.id)! : 0;
              return (
                <tr key={n.id} className="hover:bg-gray-750">
                  <td className="px-4 py-3">
                    <span className="text-sm font-medium text-white flex items-center gap-2">
                      <span className="w-3 h-3 rounded-full inline-block" style={{ backgroundColor: n.color }} />
                      {trNation(n.name, lang)}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-sm text-gray-300">{sideLabel(n.allegiance, t)}</td>
                  <td className="px-4 py-3 text-sm">{relationBadge(level, t)}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
