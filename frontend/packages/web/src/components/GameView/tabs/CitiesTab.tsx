import { terrainAt } from './shared';
import { useLang, terrainLabel, pcSizeLabel, fortLabel } from '../../../i18n/lang';


// ═══════════════════════════════════════════
// CITIES TAB (turn-0 style cards)
// ═══════════════════════════════════════════
const CITY_RESOURCES = [
  { key: 'gold', label: 'Gold', color: 'text-yellow-400' },
  { key: 'food', label: 'Food', color: 'text-green-400' },
  { key: 'timber', label: 'Timber', color: 'text-amber-600' },
  { key: 'leather', label: 'Leather', color: 'text-orange-400' },
  { key: 'bronze', label: 'Bronze', color: 'text-gray-300' },
  { key: 'steel', label: 'Steel', color: 'text-blue-300' },
  { key: 'mithril', label: 'Mithril', color: 'text-purple-400' },
  { key: 'mounts', label: 'Mounts', color: 'text-emerald-400' },
];


function pcGoldIncome(pc: any, taxRate: number): number {
  const base = ({ city: 100, 'major town': 75, town: 50, village: 25 } as Record<string, number>)[pc.size?.toLowerCase()] ?? 0;
  return Math.max(0, pc.production) + base * taxRate;
}


function pcResources(pc: any, taxRate: number): Record<string, { production: number; stores: number }> {
  return {
    gold: { production: pcGoldIncome(pc, taxRate), stores: 0 },
    food: { production: 0, stores: 0 },
    timber: { production: 0, stores: pc.stores },
    leather: { production: 0, stores: 0 },
    bronze: { production: 0, stores: 0 },
    steel: { production: 0, stores: 0 },
    mithril: { production: 0, stores: 0 },
    mounts: { production: 0, stores: 0 },
  };
}


export default function CitiesTab({ populationCentres, hexTiles, taxRate }: { populationCentres: any[]; hexTiles: any[]; taxRate?: number }) {
  const { t } = useLang();
  if (populationCentres.length === 0) {
    return <div className="text-gray-400">{t('city.none')}</div>;
  }

  const docks = (pc: any) => pc.hasPort ? t('city.port') : pc.hasHarbour ? t('city.harbour') : t('city.none2');

  return (
    <div className="space-y-4">
      {populationCentres.map((pc: any) => {
        const rate = taxRate ?? 30;
        const resources = pcResources(pc, rate);
        return (
          <div key={pc.id} className="bg-gray-800 rounded-lg p-5 border border-gray-700">
            <div className="flex items-center gap-2 flex-wrap">
              <h3 className="text-lg font-bold text-white">
                {pc.isCapital ? '★ ' : '▢ '}{pc.name}{pc.isCapital ? ` ${t('map.capitalSuffix')}` : ''}
              </h3>
            </div>
            <p className="text-sm text-gray-400 mt-1">
              {t('city.loc', { h: pc.locationHex, t: terrainLabel(terrainAt(hexTiles, pc.locationHex), t) })}
            </p>
            <dl className="grid grid-cols-2 md:grid-cols-4 gap-x-4 gap-y-1 mt-3 text-sm">
              <div><dt className="inline text-gray-500">{t('city.size')} </dt><dd className="inline text-gray-200">{pcSizeLabel(pc.size, t)}</dd></div>
              <div><dt className="inline text-gray-500">{t('city.fort')} </dt><dd className="inline text-gray-200">{pc.fortification ? fortLabel(pc.fortification, t) : t('city.none2')}</dd></div>
              <div><dt className="inline text-gray-500">{t('city.loyalty')} </dt><dd className="inline text-gray-200">{pc.loyalty}</dd></div>
              <div><dt className="inline text-gray-500">{t('city.docks')} </dt><dd className="inline text-gray-200">{docks(pc)}</dd></div>
              <div><dt className="inline text-gray-500">{t('city.hidden')} </dt><dd className="inline text-gray-200">{pc.isHidden ? t('city.yes') : t('city.no')}</dd></div>
              <div><dt className="inline text-gray-500">{t('city.sieged')} </dt><dd className="inline text-gray-200">{pc.isSieged ? t('city.yes') : t('city.no')}</dd></div>
              <div><dt className="inline text-gray-500">{t('city.tax')} </dt><dd className="inline text-gray-200">{rate}%</dd></div>
              <div><dt className="inline text-gray-500">{t('city.mined')} </dt><dd className="inline text-gray-200">{Math.max(0, pc.production)}</dd></div>
            </dl>
            <div className="mt-4 border-t border-gray-700 pt-3">
              <h4 className="text-sm font-bold text-gray-300 mb-2">{t('city.resTitle')}</h4>
              <div className="grid grid-cols-2 sm:grid-cols-4 gap-2 text-xs">
                {CITY_RESOURCES.map((r) => (
                  <div key={r.key} className="bg-gray-900 rounded p-2 border border-gray-800">
                    <div className={`font-semibold ${r.color}`}>{t(`game.res${r.key[0].toUpperCase()}${r.key.slice(1)}` as any)}</div>
                    <div className="text-gray-300 mt-1">{t('city.prod')} <span className="text-gray-100">{resources[r.key].production}</span></div>
                    <div className="text-gray-300">{t('city.stores')} <span className="text-gray-100">{resources[r.key].stores}</span></div>
                  </div>
                ))}
              </div>
            </div>
          </div>
        );
      })}
    </div>
  );
}
