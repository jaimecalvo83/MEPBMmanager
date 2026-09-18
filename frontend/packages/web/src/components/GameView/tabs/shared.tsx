import { pcSizeLabel, fortLabel, nationName as trNation, type TFunc } from '../../../i18n/lang';


// ── Shared turn-0 helpers ──
export function terrainAt(hexTiles: any[], locationHex: string): string {
  const [q, r] = locationHex.split(',').map((v) => parseInt(v.trim(), 10));
  const hex = hexTiles.find((h: any) => h.q === q && h.r === r);
  return hex ? hex.terrain : '?';
}


export function pcSentence(populationCentres: any[], locationHex: string, nation: string | undefined, lang: string, t: TFunc): string | null {
  const pc = populationCentres.find((p: any) => p.locationHex === locationHex);
  if (!pc) return null;
  const fort = pc.fortification ? ` / ${fortLabel(pc.fortification, t)}` : '';
  return t('char.pcSentence', { size: pcSizeLabel(pc.size, t), fort, name: pc.name, owner: nation ? trNation(nation, lang) : t('char.us') });
}


export function Tip({ trigger, children }: { trigger: React.ReactNode; children: React.ReactNode }) {
  return (
    <span className="relative inline-block group/tip">
      <span className="cursor-help">{trigger}</span>
      <span className="hidden group-hover/tip:block absolute left-0 top-full mt-1 z-30 w-72 max-w-[80vw] rounded-none bg-gray-900 border-2 border-mepbm-gold p-3 text-left shadow-xl">
        {children}
      </span>
    </span>
  );
}


export function StatBox({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="bg-gray-900 rounded px-3 py-2 border border-gray-700">
      <div className="text-[11px] uppercase tracking-wide text-gray-500">{label}</div>
      <div className="text-lg font-bold text-white leading-tight">{value}</div>
    </div>
  );
}
