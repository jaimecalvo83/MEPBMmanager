import { useEffect, useMemo, useState } from 'react';
import { useOrders, useSubmitOrder, useCancelOrder, useValidateOrders, useEligibleOrders, useOrderEstimate, OrderFieldSpec } from '../../hooks/useOrders';
import { ORDER_DEFINITIONS } from '@MEPBMmanager/shared';
import { ORDER_SCHEMAS } from './orderSchemas';
import { useQueryClient } from 'react-query';

interface Character {
  id: string;
  name: string;
  type: string;
  commandSkill: number;
  agentSkill: number;
  emissarySkill: number;
  mageSkill: number;
  locationHex: string;
}

interface Army {
  id: string;
  name: string;
  locationHex: string;
}

interface Navy {
  id: string;
  locationHex: string;
  warships: number;
  transports: number;
}

interface OrdersPanelProps {
  gameId: string;
  characters: Character[];
  armies: Army[];
  navies: Navy[];
}

function useDebouncedValue<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const t = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(t);
  }, [value, delayMs]);
  return debounced;
}

function parseParams(raw: unknown): Record<string, unknown> {
  if (!raw) return {};
  if (typeof raw === 'object') return raw as Record<string, unknown>;
  try {
    return JSON.parse(String(raw));
  } catch {
    return {};
  }
}

function OrderComposer({
  gameId,
  character,
  armies,
  navies,
  afterOrder,
  slotLabel,
  onSubmitted,
}: {
  gameId: string;
  character: Character;
  armies: Army[];
  navies: Navy[];
  afterOrder?: { code: number; parameters: Record<string, unknown> };
  slotLabel: string;
  onSubmitted: () => void;
}) {
  const queryClient = useQueryClient();
  const submitOrder = useSubmitOrder(gameId);
  const [orderCode, setOrderCode] = useState<number>(0);
  const [params, setParams] = useState<Record<string, unknown>>({});
  const [armyId, setArmyId] = useState<string>('');
  const [navyId, setNavyId] = useState<string>('');

  const { data: eligible } = useEligibleOrders(gameId, character.id);
  const eligibleSet = useMemo(() => new Set((eligible ?? []).filter((e) => e.ok).map((e) => e.code)), [eligible]);
  const availableOrders = useMemo(
    () => ORDER_DEFINITIONS.filter((o) => eligibleSet.has(o.code)),
    [eligibleSet]
  );

  const paramsKey = useDebouncedValue(JSON.stringify(params), 400);
  const afterKey = afterOrder ? JSON.stringify(afterOrder) : 'none';
  const estimate = useOrderEstimate(gameId, character.id, orderCode, paramsKey, afterKey, () => ({
    parameters: JSON.parse(paramsKey),
    ...(armyId ? { armyId } : {}),
    ...(navyId ? { navyId } : {}),
    ...(afterOrder ? { afterOrder } : {}),
  }));

  const setParam = (key: string, value: unknown) => {
    setParams((prev) => {
      const next = { ...prev };
      if (value === '' || value === null || value === undefined) delete next[key];
      else next[key] = value;
      return next;
    });
  };

  const applyMax = () => {
    if (estimate.data?.maxAmount != null) setParam('amount', estimate.data.maxAmount);
  };

  const handleSubmit = async () => {
    if (!orderCode) return;
    await submitOrder.mutateAsync({
      characterId: character.id,
      code: orderCode,
      parameters: JSON.parse(JSON.stringify(params)),
      ...(armyId ? { armyId } : {}),
    });
    setOrderCode(0);
    setParams({});
    queryClient.invalidateQueries(['order-estimate', gameId]);
    onSubmitted();
  };

  const schema = orderCode > 0 ? ORDER_SCHEMAS[orderCode] : undefined;

  const renderField = (f: OrderFieldSpec) => {
    const value = params[f.key];
    const override = schema?.fields?.[f.key];
    const label = override?.label ?? f.label;
    if (f.kind === 'number') {
      return (
        <div key={f.key}>
          <label className="block text-sm text-gray-400 mb-1">{label}{f.required ? ' *' : ''}</label>
          <div className="flex gap-2">
            <input
              type="number"
              min={f.min ?? undefined}
              max={f.max ?? undefined}
              value={typeof value === 'number' ? value : ''}
              onChange={(e) => setParam(f.key, e.target.value === '' ? undefined : parseInt(e.target.value, 10))}
              className="flex-1 p-2 bg-gray-700 rounded border border-gray-600"
            />
            {f.key === 'amount' && estimate.data?.maxAmount != null && (
              <button onClick={applyMax} className="px-3 py-1 bg-gray-600 text-xs rounded hover:bg-gray-500" title="Usar máximo posible">
                max {estimate.data.maxAmount}
              </button>
            )}
          </div>
          {override?.help && <p className="text-xs text-gray-500 mt-1">{override.help}</p>}
        </div>
      );
    }
    if (f.kind === 'select') {
      return (
        <div key={f.key}>
          <label className="block text-sm text-gray-400 mb-1">{label}{f.required ? ' *' : ''}</label>
          <select
            value={typeof value === 'string' ? value : ''}
            onChange={(e) => setParam(f.key, e.target.value || undefined)}
            className="w-full p-2 bg-gray-700 rounded border border-gray-600"
          >
            <option value="">Select…</option>
            {(f.options ?? []).map((o) => (
              <option key={o.value} value={o.value}>{o.label}</option>
            ))}
          </select>
          {override?.help && <p className="text-xs text-gray-500 mt-1">{override.help}</p>}
        </div>
      );
    }
    if (f.kind === 'multiselect') {
      const selected: string[] = Array.isArray(value) ? (value as string[]) : [];
      const toggle = (v: string) => {
        setParam(f.key, selected.includes(v) ? selected.filter((x) => x !== v) : [...selected, v]);
      };
      return (
        <div key={f.key}>
          <label className="block text-sm text-gray-400 mb-1">{label}</label>
          <div className="flex flex-wrap gap-2">
            {(f.options ?? []).map((o) => (
              <label key={o.value} className={`px-2 py-1 rounded text-xs cursor-pointer border ${selected.includes(o.value) ? 'bg-mepbm-gold text-gray-900 border-mepbm-gold' : 'bg-gray-700 text-gray-300 border-gray-600'}`}>
                <input type="checkbox" className="hidden" checked={selected.includes(o.value)} onChange={() => toggle(o.value)} />
                {o.label}
              </label>
            ))}
          </div>
          {override?.help && <p className="text-xs text-gray-500 mt-1">{override.help}</p>}
        </div>
      );
    }
    if (f.kind === 'flag') {
      return (
        <label key={f.key} className="flex items-center gap-2 text-sm text-gray-300">
          <input type="checkbox" checked={value === true} onChange={(e) => setParam(f.key, e.target.checked ? true : undefined)} className="rounded" />
          {label}
        </label>
      );
    }
    return (
      <div key={f.key}>
        <label className="block text-sm text-gray-400 mb-1">{label}{f.required ? ' *' : ''}</label>
        <input
          type="text"
          value={typeof value === 'string' ? value : ''}
          placeholder={override?.placeholder ?? (f.kind === 'hex' ? 'Q,R' : '')}
          onChange={(e) => setParam(f.key, e.target.value || undefined)}
          className="w-full p-2 bg-gray-700 rounded border border-gray-600"
          list={f.key === 'name' && estimate.data?.suggestNames ? `names-${character.id}-${f.key}` : undefined}
        />
        {f.key === 'name' && estimate.data?.suggestNames && (
          <datalist id={`names-${character.id}-${f.key}`}>
            {estimate.data.suggestNames.map((n) => <option key={n} value={n} />)}
          </datalist>
        )}
        {override?.help && <p className="text-xs text-gray-500 mt-1">{override.help}</p>}
      </div>
    );
  };

  const est = estimate.data;
  const costEntries = est ? Object.entries(est.costs ?? {}).filter(([, v]) => (v as number) !== 0) : [];

  return (
    <div className="bg-gray-900 rounded p-3 border border-gray-700 space-y-3">
      <div className="flex items-center justify-between">
        <span className="text-sm font-bold text-gray-300">{slotLabel}</span>
        {eligible && <span className="text-xs text-gray-500">{availableOrders.length} available</span>}
      </div>
      <select
        value={orderCode}
        onChange={(e) => {
          setOrderCode(parseInt(e.target.value));
          setParams({});
        }}
        className="w-full p-2 bg-gray-700 rounded border border-gray-600"
      >
        <option value={0}>Select order…</option>
        {availableOrders.map((order) => (
          <option key={order.code} value={order.code}>
            [{order.code}] {order.abbreviation} - {order.name}
          </option>
        ))}
      </select>
      {schema?.help && orderCode > 0 && (
        <p className="text-xs text-gray-400 italic">{schema.help}</p>
      )}

      {orderCode > 0 && (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
          <div>
            <label className="block text-sm text-gray-400 mb-1">Army (optional context)</label>
            <select value={armyId} onChange={(e) => setArmyId(e.target.value)} className="w-full p-2 bg-gray-700 rounded border border-gray-600">
              <option value="">Default</option>
              {armies.map((a) => <option key={a.id} value={a.id}>{a.name} @ {a.locationHex}</option>)}
            </select>
          </div>
          <div>
            <label className="block text-sm text-gray-400 mb-1">Navy (optional context)</label>
            <select value={navyId} onChange={(e) => setNavyId(e.target.value)} className="w-full p-2 bg-gray-700 rounded border border-gray-600">
              <option value="">Default</option>
              {navies.map((v) => <option key={v.id} value={v.id}>Navy @ {v.locationHex} ({v.warships}W/{v.transports}T)</option>)}
            </select>
          </div>
        </div>
      )}

      {orderCode > 0 && estimate.isLoading && (
        <p className="text-gray-500 text-sm">Loading order info…</p>
      )}

      {orderCode > 0 && est && (
        <div className="space-y-3">
          {est.requires.map(renderField)}
          {(costEntries.length > 0 || est.expectedGold != null) && (
            <div className="text-sm bg-gray-800 rounded p-2 border border-gray-700">
              <span className="text-gray-400">Cost: </span>
              {costEntries.map(([k, v]) => (
                <span key={k} className="mr-3 text-yellow-300">{k}: {v}</span>
              ))}
              {est.expectedGold != null && (
                <span className="mr-3 text-green-400">expected +{est.expectedGold} gold</span>
              )}
              {est.maxAmount != null && <span className="text-gray-500">(max {est.maxAmount})</span>}
            </div>
          )}
          {est.warnings && est.warnings.length > 0 && (
            <div className="text-sm">
              {est.warnings.map((w, i) => <p key={i} className="text-yellow-400">• {w}</p>)}
            </div>
          )}
          {est.errors.length > 0 && (
            <div className="text-sm">
              {est.errors.map((e, i) => <p key={i} className="text-red-400">• {e}</p>)}
            </div>
          )}
        </div>
      )}

      <div>
        <button
          onClick={handleSubmit}
          disabled={!orderCode || submitOrder.isLoading || (est != null && !est.ok)}
          className="px-4 py-2 bg-mepbm-gold text-gray-900 font-bold rounded hover:bg-yellow-400 transition disabled:opacity-50"
        >
          {submitOrder.isLoading ? 'Submitting…' : `Submit ${slotLabel}`}
        </button>
        {est?.movedByFirstOrder && est.effectiveLocation && (
          <span className="ml-3 text-xs text-blue-300">After 1st order → {est.effectiveLocation}</span>
        )}
      </div>
      {submitOrder.isError && (
        <p className="text-red-400 text-sm">{(submitOrder.error as any)?.response?.data?.error || 'Failed to submit order'}</p>
      )}
    </div>
  );
}

function CharacterOrderCard({
  gameId,
  character,
  armies,
  navies,
  pending,
  onChanged,
}: {
  gameId: string;
  character: Character;
  armies: Army[];
  navies: Navy[];
  pending: Array<{ id: string; code: number; parameters: unknown; status: string }>;
  onChanged: () => void;
}) {
  const queryClient = useQueryClient();
  const cancelOrder = useCancelOrder(gameId);

  const afterOrder = pending.length === 1
    ? { code: pending[0].code, parameters: parseParams(pending[0].parameters) }
    : undefined;

  return (
    <div className="bg-gray-800 rounded-lg border border-gray-700 p-4">
      <div className="flex items-center justify-between flex-wrap gap-2 mb-3">
        <div>
          <span className="font-bold text-white">{character.name}</span>
          <span className="ml-2 text-xs text-gray-400">
            ({character.type}) C:{character.commandSkill} A:{character.agentSkill} E:{character.emissarySkill} M:{character.mageSkill} @ {character.locationHex}
          </span>
        </div>
        <span className="text-xs text-gray-500">{pending.length}/2 orders</span>
      </div>

      {pending.length === 0 && (
        <OrderComposer
          gameId={gameId}
          character={character}
          armies={armies}
          navies={navies}
          slotLabel="1st order"
          onSubmitted={onChanged}
        />
      )}

      {pending.length >= 1 && (
        <div className="mb-3 space-y-2">
          {pending.map((o, i) => {
            const def = ORDER_DEFINITIONS.find((d) => d.code === o.code);
            return (
              <div key={o.id} className="flex items-center justify-between bg-gray-700 px-3 py-2 rounded text-sm">
                <span>
                  <span className="text-gray-400">{i === 0 ? '1st' : '2nd'}: </span>
                  <span className="text-mepbm-gold">[{o.code}] {def?.name ?? ''}</span>
                  {' '}
                  <span className="text-gray-400">{typeof o.parameters === 'string' ? o.parameters : JSON.stringify(o.parameters)}</span>
                  {o.status !== 'pending' && <span className="ml-2 text-xs text-gray-500">({o.status})</span>}
                </span>
                {o.status === 'pending' && (
                  <button
                    onClick={async () => {
                      await cancelOrder.mutateAsync(o.id);
                      queryClient.invalidateQueries(['order-estimate', gameId]);
                      onChanged();
                    }}
                    disabled={cancelOrder.isLoading}
                    className="text-red-400 hover:text-red-300 text-xs disabled:opacity-50"
                  >
                    Cancel
                  </button>
                )}
              </div>
            );
          })}
        </div>
      )}

      {pending.length === 1 && afterOrder && (
        <OrderComposer
          gameId={gameId}
          character={character}
          armies={armies}
          navies={navies}
          afterOrder={afterOrder}
          slotLabel="2nd order (conditioned by the 1st)"
          onSubmitted={onChanged}
        />
      )}

      {pending.length >= 2 && (
        <p className="text-yellow-400 text-sm">Two orders submitted. Cancel one to change it.</p>
      )}
    </div>
  );
}

export default function OrdersPanel({ gameId, characters, armies, navies }: OrdersPanelProps) {
  const queryClient = useQueryClient();
  const { data: orders } = useOrders(gameId);
  const validateOrders = useValidateOrders(gameId);
  const [, force] = useState(0);

  return (
    <div className="bg-gray-800 rounded-lg border border-gray-700 p-4">
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-lg font-bold text-mepbm-gold">Orders</h2>
        <button
          onClick={() => validateOrders.mutate()}
          disabled={validateOrders.isLoading}
          className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-500 transition disabled:opacity-50"
        >
          {validateOrders.isLoading ? 'Validating…' : 'Validate All'}
        </button>
      </div>

      {characters.length === 0 && (
        <div className="text-gray-400">No characters visible.</div>
      )}

      <div className="space-y-4">
        {characters.map((c) => (
          <CharacterOrderCard
            key={c.id}
            gameId={gameId}
            character={c}
            armies={armies}
            navies={navies}
            pending={(orders ?? []).filter((o) => o.characterId === c.id && o.status === 'pending')}
            onChanged={() => {
              queryClient.invalidateQueries(['orders', gameId]);
              force((x) => x + 1);
            }}
          />
        ))}
      </div>
    </div>
  );
}
