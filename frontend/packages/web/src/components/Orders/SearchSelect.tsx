import { useEffect, useRef, useState } from 'react';

export interface SearchOption {
  value: string;
  label: string;
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
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
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
              {selected ? selected.label : placeholder ?? 'Select…'}
            </span>
          </button>
          {selected && (
            <button
              type="button"
              onClick={() => pick('')}
              title="Clear"
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
            placeholder={placeholder ?? 'Type to search…'}
            className="w-full p-2 bg-gray-700 rounded border border-mepbm-gold focus:outline-none"
          />
          <div className="absolute z-20 mt-1 w-full max-h-64 overflow-y-auto bg-gray-800 rounded border border-gray-600 shadow-lg">
            {filtered.length === 0 && (
              <div className="px-3 py-2 text-sm text-gray-500">No matches</div>
            )}
            {filtered.map((o) => (
              <button
                type="button"
                key={o.value}
                onMouseDown={(e) => e.preventDefault()}
                onClick={() => pick(o.value)}
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
        </div>
      )}
      {open && (
        <div className="fixed inset-0 z-10" onClick={() => setOpen(false)} />
      )}
    </div>
  );
}
