import { useEffect, useState } from 'react';
import { ORDER_DEFINITIONS } from '@MEPBMmanager/shared';
import type { OrderRestriction } from '@MEPBMmanager/shared';
import { ORDER_SCHEMAS } from './orderSchemas';
import { useOrderEstimate, type OrderFieldSpec } from '../../hooks/useOrders';

// ── Static prerequisites (mirror backend CheckEligible + resolve) ──
const CAPITAL_ORDERS = new Set([175, 180, 185, 280, 300, 325, 660, 725, 728, 731, 734, 737]);
const ARMY_ORDERS = new Set([
  230, 235, 240, 250, 255, 260, 340, 345, 347, 349, 351, 353, 355, 370, 375,
  400, 404, 408, 412, 416, 420, 425, 430, 435, 440, 444, 448, 765, 775, 780, 840, 850, 860,
]);
const NAVY_NATION_ORDERS = new Set([270, 275, 280, 794, 798]);
const COMPANY_ORDERS = new Set([750, 760]);
const OWN_PC_ORDERS = new Set([520, 530, 535, 550, 705, 710]);
const LAND_ORDERS = new Set([745, 552, 555, 910, 915, 925, 930]);

const SKILL_TYPE: Partial<Record<OrderRestriction, string>> = {
  c: 'Command',
  a: 'Agent',
  e: 'Emissary',
  m: 'Mage',
};

const RESTRICTION_PREREQ: Partial<Record<OrderRestriction, string>> = {
  com: 'Force commander',
  company: 'In a company',
  cap: 'At capital',
  fa: 'Fourth Age only',
  k: 'Kingdom',
};

const SPELL_PREREQ: Record<number, string> = {
  120: 'Known healing spell',
  225: 'Known combat spell',
  330: 'Known conjuring spell',
  825: 'Known movement spell',
  940: 'Known lore spell',
};

function orderType(code: number): string {
  const def = ORDER_DEFINITIONS.find((d) => d.code === code);
  const skills = (def?.restrictions ?? []).filter((r): r is keyof typeof SKILL_TYPE => r in SKILL_TYPE);
  if (skills.length === 0) return 'General';
  return [...new Set(skills.map((s) => SKILL_TYPE[s]))].join(' / ');
}

function orderPrereqs(code: number): string[] {
  const def = ORDER_DEFINITIONS.find((d) => d.code === code);
  const out: string[] = [];
  for (const r of def?.restrictions ?? []) {
    const t = RESTRICTION_PREREQ[r];
    if (t && !out.includes(t)) out.push(t);
  }
  if (CAPITAL_ORDERS.has(code) && !out.includes('At capital')) out.push('At your capital');
  if (code === 950) out.push('At your current capital');
  if (ARMY_ORDERS.has(code)) out.push('In an army');
  if (code === 830) out.push('Command a navy');
  if (NAVY_NATION_ORDERS.has(code)) out.push('Nation owns a navy');
  if (COMPANY_ORDERS.has(code) && !out.includes('In a company')) out.push('In a company');
  if (OWN_PC_ORDERS.has(code)) out.push('At one of your population centres');
  if (LAND_ORDERS.has(code)) out.push('On land');
  if (code === 205) out.push('Held combat artifact');
  if (code === 360 || code === 792) out.push('Held artifact');
  if (SPELL_PREREQ[code]) out.push(SPELL_PREREQ[code]);
  return out;
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <span className="block mt-2">
      <span className="block text-xs font-bold text-gray-400 uppercase">{title}</span>
      <span className="block text-sm text-gray-200 mt-0.5">{children}</span>
    </span>
  );
}

// Shared 6-section body: title / type / difficulty / prerequisites / required info / description.
export function OrderTipBody({ code, requires }: { code: number; requires?: OrderFieldSpec[] | null }) {
  const def = ORDER_DEFINITIONS.find((d) => d.code === code);
  const schema = ORDER_SCHEMAS[code];
  if (!def) return <span className="text-mepbm-gold">[{code}]</span>;
  const prereqs = orderPrereqs(code);
  return (
    <span>
      <span className="block text-mepbm-gold font-bold text-sm">[{def.code}] {def.name}</span>
      <Section title="Tipo">{orderType(code)}</Section>
      <Section title="Dificultad">{def.difficulty}{def.skillIncrease ? ' · +skill' : ''}</Section>
      {prereqs.length > 0 && (
        <Section title="Prerequisitos">
          {prereqs.map((p) => (
            <span key={p} className="block text-xs">• {p}</span>
          ))}
        </Section>
      )}
      {requires && requires.length > 0 && (
        <Section title="Info requerida">
          {requires.map((f) => (
            <span key={f.key} className="block text-xs">• {f.label}{f.required ? ' *' : ''}</span>
          ))}
        </Section>
      )}
      <Section title="Descripción">
        {def.description}
        {schema?.help && <span className="block text-xs text-gray-400 italic mt-1">{schema.help}</span>}
      </Section>
    </span>
  );
}

// Hover box on the gold order name (live required info).
export function OrderInfoTip({ code, requires }: {
  code: number;
  requires?: OrderFieldSpec[] | null;
}) {
  return (
    <span className="relative inline-block group/tip">
      <span className="text-mepbm-gold cursor-help">[{code}] {ORDER_DEFINITIONS.find((d) => d.code === code)?.name ?? ''}</span>
      <span className="hidden group-hover/tip:block absolute left-0 top-full mt-1 z-30 w-80 max-w-[80vw] rounded-none bg-gray-900 border-2 border-mepbm-gold p-3 text-left shadow-xl">
        <OrderTipBody code={code} requires={requires} />
      </span>
    </span>
  );
}

// Browsing tooltip for the dropdown: fetches live required info after a short dwell.
export function OrderDropdownTip({ gameId, characterId, code }: {
  gameId: string;
  characterId: string;
  code: number;
}) {
  const [dwell, setDwell] = useState(false);
  useEffect(() => {
    const t = setTimeout(() => setDwell(true), 250);
    return () => clearTimeout(t);
  }, []);
  const est = useOrderEstimate(gameId, characterId, code, '{}', 'none', () => ({ parameters: {} }), dwell);
  return <OrderTipBody code={code} requires={dwell ? est.data?.requires : undefined} />;
}
