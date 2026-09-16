import { useEffect, useRef, useState, type ReactNode } from 'react';
import { useLang } from '../../i18n/lang';

export interface SearchOption {
  value: string;
  label: string;
  tooltip?: ReactNode;
}

export default function SearchSelect({
  value,
  options,
  onChange,
  placeholder,
}: {
  value: string;
  options: SearchOption[];
  onChange: (value: string) => void;
  placeholder?: string;
}) {
  const { t } = useLang();
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const [tip, setTip] = useState<{ x: number; y: number; node: ReactNode } | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const boxRef = useRef<HTMLDivElement>(null);

  const selected = options.find((o) => o.value === value);
  const q = query.trim().toLowerCase();
  const filtered = q === ''
    ? options
    : options.filter((o) => o.label.toLowerCase().includes(q));

  useEffect(() => {
    if (open) {
      setQuery('');
      const t = setTimeout(() => inputRef.current?.focus(), 0);
      return () => clearTimeout(t);
    }
  }, [open ]);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(false);
    };
    if (open) window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [open ]);

  const pick = (v: string) => {
    onChange(v);
    setOpen(false);
    setTip(null);
  };

  return (
    <div ref={boxRef} className="relative">
      {!open ? (
        <div className="flex gap-1">
          <button
            type="button"
            onClick={() => setOpen(true)}
            className="flex-1 p-2 bg-gray-700 rounded border border-gray-600 text-left truncate"
          >
            <span className={selected ? 'text-gray-100' : 'text-gray-500'}>
              {selected ? selected.label : placeholder ?? t('ord.selectPh')}
            </span>
          </button>
          {selected && (
            <button
              type="button"
              onClick={() => pick('')}
              title={t('ord.clearTitle')}
              className="px-2 bg-gray-700 rounded border border-gray-600 text-gray-400 hover:text-white"
            >
              ×
            </button>
          )}
        </div>
      ) : (
        <div>
          <input
            ref={inputRef}
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && filtered.length > 0) pick(filtered[0].value);
            }}
            placeholder={placeholder ?? t('ord.searchPh')}
            className="w-full p-2 bg-gray-700 rounded border border-mepbm-gold focus:outline-none"
          />
          <div
            className="absolute z-20 mt-1 w-full max-h-64 overflow-y-auto bg-gray-800 rounded border border-gray-600 shadow-lg"
            onScroll={() => setTip(null)}
          >
            {filtered.length === 0 && (
              <div className="px-3 py-2 text-sm text-gray-500">{t('ord.noMatches')}</div>
            )}
            {filtered.map((o) => (
              <button
                type="button"
                key={o.value}
                onMouseDown={(e) => e.preventDefault()}
                onClick={() => pick(o.value)}
                onMouseEnter={(e) => { if (o.tooltip) setTip({ x: e.clientX, y: e.clientY, node: o.tooltip }); }}
                onMouseMove={(e) => { if (o.tooltip) setTip({ x: e.clientX, y: e.clientY, node: o.tooltip }); }}
                onMouseLeave={() => setTip(null)}
                className={`w-full text-left px-3 py-2 text-sm transition ${
                  o.value === value
                    ? 'bg-mepbm-gold text-gray-900 font-bold'
                    : 'text-gray-200 hover:bg-gray-700'
                }`}
              >
                {o.label}
              </button>
            ))}
          </div>
          {open && tip?.node != null && (() => {
            const w = 320;
            const left = Math.max(8, Math.min(tip.x + 16, window.innerWidth - w - 8));
            const flip = tip.y > window.innerHeight - 300;
            const top = flip ? Math.max(8, tip.y - 16) : tip.y + 16;
            return (
              <div
                className="fixed z-50 w-80 max-w-[80vw] rounded-none bg-gray-900 border-2 border-mepbm-gold p-3 text-left shadow-xl pointer-events-none"
                style={{ left, top, transform: flip ? 'translateY(-100%)' : undefined }}
              >
                {tip.node}
              </div>
            );
          })()}
        </div>
      )}
      {open && (
        <div className="fixed inset-0 z-10" onClick={() => setOpen(false)} />
      )}
    </div>
  );
}
