import { useEffect, useMemo, useState } from 'react';
import { useOrders, useSubmitOrder, useCancelOrder, useValidateOrders, useEligibleOrders, useOrderEstimate, OrderFieldSpec } from '../../hooks/useOrders';
import { ORDER_DEFINITIONS } from '@MEPBMmanager/shared';
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

export default function OrdersPanel({ gameId, characters, armies, navies }: OrdersPanelProps) {
  const queryClient = useQueryClient();
  const { data: orders, isLoading } = useOrders(gameId);
  const submitOrder = useSubmitOrder(gameId);
  const cancelOrder = useCancelOrder(gameId);
  const validateOrders = useValidateOrders(gameId);
  const [selectedCharacter, setSelectedCharacter] = useState<string>('');
  const [selectedOrder, setSelectedOrder] = useState<number>(0);
  const [params, setParams] = useState<Record<string, unknown>>({});
  const [armyId, setArmyId] = useState<string>('');
  const [navyId, setNavyId] = useState<string>('');

  const { data: eligible } = useEligibleOrders(gameId, selectedCharacter || null);
  const eligibleSet = useMemo(() => new Set((eligible ?? []).filter((e) => e.ok).map((e) => e.code)), [eligible]);
  const availableOrders = useMemo(
    () => ORDER_DEFINITIONS.filter((o) => eligibleSet.has(o.code)),
    [eligibleSet]
  );

  const pendingForChar = useMemo(
    () => (orders ?? []).filter((o) => o.characterId === selectedCharacter && o.status === 'pending'),
    [orders, selectedCharacter]
  );
  const slotBlocked = pendingForChar.length >= 2;
  const afterOrder = pendingForChar.length === 1
    ? (() => {
        const first = pendingForChar[0];
        let parsed: Record<string, unknown> = {};
        try {
          parsed = typeof first.parameters === 'string' ? JSON.parse(first.parameters) : (first.parameters as Record<string, unknown>) ?? {};
        } catch { parsed = {}; }
        return { code: first.code, parameters: parsed };
      })()
    : undefined;

  const paramsKey = useDebouncedValue(JSON.stringify(params), 400);
  const afterKey = afterOrder ? JSON.stringify(afterOrder) : 'none';
  const estimate = useOrderEstimate(gameId, selectedCharacter || null, selectedOrder, paramsKey, afterKey, () => ({
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

  const handleSubmit = async () => {
    if (!selectedCharacter || !selectedOrder || slotBlocked) return;
    await submitOrder.mutateAsync({
      characterId: selectedCharacter,
      code: selectedOrder,
      parameters: JSON.parse(paramsKey),
      ...(armyId ? { armyId } : {}),
    });
    setSelectedOrder(0);
    setParams({});
    queryClient.invalidateQueries(['order-estimate', gameId]);
  };

  const applyMax = () => {
    if (estimate.data?.maxAmount != null) setParam('amount', estimate.data.maxAmount);
  };

  const renderField = (f: OrderFieldSpec) => {
    const value = params[f.key];
    if (f.kind === 'number') {
      return (
        <div key={f.key}>
          <label className="block text-sm text-gray-400 mb-1">{f.label}{f.required ? ' *' : ''}</label>
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
        </div>
      );
    }
    if (f.kind === 'select') {
      return (
        <div key={f.key}>
          <label className="block text-sm text-gray-400 mb-1">{f.label}{f.required ? ' *' : ''}</label>
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
          <label className="block text-sm text-gray-400 mb-1">{f.label}</label>
          <div className="flex flex-wrap gap-2">
            {(f.options ?? []).map((o) => (
              <label key={o.value} className={`px-2 py-1 rounded text-xs cursor-pointer border ${selected.includes(o.value) ? 'bg-mepbm-gold text-gray-900 border-mepbm-gold' : 'bg-gray-700 text-gray-300 border-gray-600'}`}>
                <input type="checkbox" className="hidden" checked={selected.includes(o.value)} onChange={() => toggle(o.value)} />
                {o.label}
              </label>
            ))}
          </div>
        </div>
      );
    }
    if (f.kind === 'flag') {
      return (
        <label key={f.key} className="flex items-center gap-2 text-sm text-gray-300">
          <input type="checkbox" checked={value === true} onChange={(e) => setParam(f.key, e.target.checked ? true : undefined)} className="rounded" />
          {f.label}
        </label>
      );
    }
    return (
      <div key={f.key}>
        <label className="block text-sm text-gray-400 mb-1">{f.label}{f.required ? ' *' : ''}</label>
        <input
          type="text"
          value={typeof value === 'string' ? value : ''}
          placeholder={f.kind === 'hex' ? (estimate.data?.effectiveLocation ?? 'Q,R') : ''}
          onChange={(e) => setParam(f.key, e.target.value || undefined)}
          className="w-full p-2 bg-gray-700 rounded border border-gray-600"
          list={f.key === 'name' && estimate.data?.suggestNames ? `names-${f.key}` : undefined}
        />
        {f.key === 'name' && estimate.data?.suggestNames && (
          <datalist id={`names-${f.key}`}>
            {estimate.data.suggestNames.map((n) => <option key={n} value={n} />)}
          </datalist>
        )}
      </div>
    );
  };

  if (isLoading) {
    return <div className="text-gray-400 p-4">Loading orders...</div>;
  }

  const est = estimate.data;
  const costEntries = est ? Object.entries(est.costs ?? {}).filter(([, v]) => (v as number) !== 0) : [];

  return (
    <div className="bg-gray-800 rounded-lg border border-gray-700 p-4">
      <h2 className="text-lg font-bold text-mepbm-gold mb-4">Orders</h2>

      <div className="space-y-4">
        <div>
          <label className="block text-sm text-gray-400 mb-1">Character</label>
          <select
            value={selectedCharacter}
            onChange={(e) => {
              setSelectedCharacter(e.target.value);
              setSelectedOrder(0);
              setParams({});
              setArmyId('');
              setNavyId('');
            }}
            className="w-full p-2 bg-gray-700 rounded border border-gray-600"
          >
            <option value="">Select character</option>
            {characters.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name} ({c.type}) - C:{c.commandSkill} A:{c.agentSkill} E:{c.emissarySkill} M:{c.mageSkill}
              </option>
            ))}
          </select>
        </div>

        {selectedCharacter && (
          <div>
            <label className="block text-sm text-gray-400 mb-1">
              Order {pendingForChar.length === 1 ? '(2nd — conditioned by the 1st)' : pendingForChar.length >= 2 ? '(blocked: 2 already)' : '(1st)'}
              {eligible && <span className="ml-2 text-xs text-gray-500">{availableOrders.length} available</span>}
            </label>
            <select
              value={selectedOrder}
              onChange={(e) => {
                setSelectedOrder(parseInt(e.target.value));
                setParams({});
              }}
              className="w-full p-2 bg-gray-700 rounded border border-gray-600"
            >
              <option value={0}>Select order</option>
              {availableOrders.map((order) => (
                <option key={order.code} value={order.code}>
                  [{order.code}] {order.abbreviation} - {order.name}
                </option>
              ))}
            </select>
            {slotBlocked && (
              <p className="text-yellow-400 text-sm mt-2">This character already has 2 orders this turn. Cancel one to change it.</p>
            )}
            {est?.movedByFirstOrder && est.effectiveLocation && (
              <p className="text-blue-300 text-sm mt-2">After the 1st order, location will be {est.effectiveLocation}. Location checks use it.</p>
            )}
          </div>
        )}

        {selectedOrder > 0 && (
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

        {selectedOrder > 0 && estimate.isLoading && (
          <p className="text-gray-500 text-sm">Loading order info…</p>
        )}

        {selectedOrder > 0 && est && (
          <div className="space-y-3 bg-gray-900 rounded p-3 border border-gray-700">
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
            {est.errors.length > 0 && (
              <div className="text-sm">
                {est.errors.map((e, i) => <p key={i} className="text-red-400">• {e}</p>)}
              </div>
            )}
          </div>
        )}

        <div className="flex gap-2">
          <button
            onClick={handleSubmit}
            disabled={!selectedCharacter || !selectedOrder || slotBlocked || submitOrder.isLoading || (est != null && !est.ok)}
            className="px-4 py-2 bg-mepbm-gold text-gray-900 font-bold rounded hover:bg-yellow-400 transition disabled:opacity-50"
          >
            {submitOrder.isLoading ? 'Submitting...' : pendingForChar.length === 1 ? 'Submit 2nd Order' : 'Submit Order'}
          </button>
          <button
            onClick={() => validateOrders.mutate()}
            disabled={validateOrders.isLoading}
            className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-500 transition disabled:opacity-50"
          >
            {validateOrders.isLoading ? 'Validating...' : 'Validate All'}
          </button>
        </div>
        {submitOrder.isError && (
          <p className="text-red-400 text-sm">{(submitOrder.error as any)?.response?.data?.error || 'Failed to submit order'}</p>
        )}
      </div>

      <div className="mt-6">
        <h3 className="text-sm font-semibold text-gray-400 mb-2">Submitted Orders ({orders?.length ?? 0})</h3>
        <div className="space-y-2 max-h-64 overflow-y-auto">
          {(orders ?? []).map((order) => (
            <div key={order.id} className="flex items-center justify-between bg-gray-700 p-2 rounded text-sm">
              <span>
                <span className="text-mepbm-gold">{order.character.name}</span>
                {' → '}
                <span className="text-gray-300">[{order.code}]</span>
                {' '}
                <span className="text-gray-400">{typeof order.parameters === 'string' ? order.parameters : JSON.stringify(order.parameters)}</span>
                {order.status !== 'pending' && <span className="ml-2 text-xs text-gray-500">({order.status})</span>}
              </span>
              {order.status === 'pending' && (
                <button
                  onClick={() => cancelOrder.mutate(order.id)}
                  disabled={cancelOrder.isLoading}
                  className="text-red-400 hover:text-red-300 text-xs disabled:opacity-50"
                >
                  Cancel
                </button>
              )}
            </div>
          ))}
          {(!orders || orders.length === 0) && (
            <p className="text-gray-500 text-sm">No orders submitted yet</p>
          )}
        </div>
      </div>
    </div>
  );
}
