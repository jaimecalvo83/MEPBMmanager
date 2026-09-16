import { ORDER_DEFINITIONS } from '@MEPBMmanager/shared';
import { ORDER_SCHEMAS } from './orderSchemas';
import type { OrderFieldSpec } from '../../hooks/useOrders';

// Square tooltip on the gold order name with all order information.
export function OrderInfoTip({ code, requires, costs, expectedGold, maxAmount }: {
  code: number;
  requires?: OrderFieldSpec[] | null;
  costs?: Record<string, number> | null;
  expectedGold?: number | null;
  maxAmount?: number | null;
}) {
  const def = ORDER_DEFINITIONS.find((d) => d.code === code);
  const schema = ORDER_SCHEMAS[code];
  if (!def) return <span className="text-mepbm-gold">[{code}]</span>;
  const costText = costs
    ? Object.entries(costs).filter(([, v]) => (v as number) !== 0).map(([k, v]) => `${k}: ${v}`).join(' · ')
    : '';
  return (
    <span className="relative inline-block group/tip">
      <span className="text-mepbm-gold cursor-help">[{def.code}] {def.name}</span>
      <span className="hidden group-hover/tip:block absolute left-0 top-full mt-1 z-30 w-80 max-w-[80vw] max-h-96 overflow-y-auto rounded-none bg-gray-900 border-2 border-mepbm-gold p-3 text-left shadow-xl">
        <span className="block text-mepbm-gold font-bold text-sm">[{def.code}] {def.name}</span>
        <span className="block text-xs text-gray-400 mt-0.5">{def.abbreviation} · {def.difficulty}{def.skillIncrease ? ' · +skill' : ''}</span>
        <span className="block text-sm text-gray-200 mt-2">{def.description}</span>
        {schema?.help && <span className="block text-xs text-gray-400 italic mt-1">{schema.help}</span>}
        {requires && requires.length > 0 && (
          <span className="block mt-2">
            <span className="block text-xs font-bold text-gray-300">Required information:</span>
            {requires.map((f) => (
              <span key={f.key} className="block text-xs text-gray-300">• {f.label}{f.required ? ' *' : ''}</span>
            ))}
          </span>
        )}
        {(costText !== '' || expectedGold != null || maxAmount != null) && (
          <span className="block mt-2 text-xs text-yellow-300">
            Cost: {costText}{expectedGold != null ? ` · expected +${expectedGold} gold` : ''}{maxAmount != null ? ` (max ${maxAmount})` : ''}
          </span>
        )}
      </span>
    </span>
  );
}
