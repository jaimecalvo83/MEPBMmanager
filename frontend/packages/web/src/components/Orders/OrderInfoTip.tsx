import { useEffect, useState } from 'react';
import { ORDER_DEFINITIONS } from '@MEPBMmanager/shared';
import type { OrderRestriction } from '@MEPBMmanager/shared';
import { ORDER_SCHEMAS } from './orderSchemas';
import { useOrderEstimate, type OrderFieldSpec } from '../../hooks/useOrders';
import { useLang, type TFunc } from '../../i18n/lang';

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

function skillType(r: OrderRestriction, t: TFunc): string | null {
  switch (r) {
    case 'c': return t('ord.typeCommand');
    case 'a': return t('ord.typeAgent');
    case 'e': return t('ord.typeEmissary');
    case 'm': return t('ord.typeMage');
    default: return null;
  }
}

function restrictionPrereq(r: OrderRestriction, t: TFunc): string | null {
  switch (r) {
    case 'com': return t('ord.rForceCommander');
    case 'company': return t('ord.rCompany');
    case 'cap': return t('ord.rCapital');
    case 'fa': return t('ord.rFourthAge');
    case 'k': return t('ord.rKingdom');
    default: return null;
  }
}

function spellPrereq(code: number, t: TFunc): string | null {
  switch (code) {
    case 120: return t('ord.pHeal');
    case 225: return t('ord.pCombatSpell');
    case 330: return t('ord.pConjuring');
    case 825: return t('ord.pMovement');
    case 940: return t('ord.pLore');
    default: return null;
  }
}

function orderType(code: number, t: TFunc): string {
  const def = ORDER_DEFINITIONS.find((d) => d.code === code);
  const skills = [...new Set((def?.restrictions ?? []).map((r) => skillType(r, t)).filter((s): s is string => s != null))];
  if (skills.length === 0) return t('ord.typeGeneral');
  return skills.join(' / ');
}

function orderPrereqs(code: number, t: TFunc): string[] {
  const def = ORDER_DEFINITIONS.find((d) => d.code === code);
  const out: string[] = [];
  const push = (s: string | null) => { if (s && !out.includes(s)) out.push(s); };
  for (const r of def?.restrictions ?? []) push(restrictionPrereq(r, t));
  if (CAPITAL_ORDERS.has(code)) push(t('ord.pCapital'));
  if (code === 950) push(t('ord.pCurrentCapital'));
  if (ARMY_ORDERS.has(code)) push(t('ord.pArmy'));
  if (code === 830) push(t('ord.pNavy'));
  if (NAVY_NATION_ORDERS.has(code)) push(t('ord.pNavyNation'));
  if (COMPANY_ORDERS.has(code)) push(t('ord.pCompany'));
  if (OWN_PC_ORDERS.has(code)) push(t('ord.pOwnPc'));
  if (LAND_ORDERS.has(code)) push(t('ord.pLand'));
  if (code === 205) push(t('ord.pCombatArt'));
  if (code === 360 || code === 792) push(t('ord.pHeldArt'));
  push(spellPrereq(code, t));
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
  const { t } = useLang();
  const def = ORDER_DEFINITIONS.find((d) => d.code === code);
  const schema = ORDER_SCHEMAS[code];
  if (!def) return <span className="text-mepbm-gold">[{code}]</span>;
  const prereqs = orderPrereqs(code, t);
  return (
    <span>
      <span className="block text-mepbm-gold font-bold text-sm">[{def.code}] {def.name}</span>
      <Section title={t('ord.tipType')}>{orderType(code, t)}</Section>
      <Section title={t('ord.tipDifficulty')}>{def.difficulty}{def.skillIncrease ? t('ord.skillPlus') : ''}</Section>
      {prereqs.length > 0 && (
        <Section title={t('ord.tipPrereq')}>
          {prereqs.map((p) => (
            <span key={p} className="block text-xs">• {p}</span>
          ))}
        </Section>
      )}
      {requires && requires.length > 0 && (
        <Section title={t('ord.tipReqInfo')}>
          {requires.map((f) => (
            <span key={f.key} className="block text-xs">• {f.label}{f.required ? ' *' : ''}</span>
          ))}
        </Section>
      )}
      <Section title={t('ord.tipDesc')}>
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
