import { useState } from 'react';
import { useOrders, useSubmitOrder, useCancelOrder, useValidateOrders } from '../../hooks/useOrders';
import { ORDER_DEFINITIONS } from '@MEPBMmanager/shared';

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

interface OrdersPanelProps {
  gameId: string;
  characters: Character[];
}

export default function OrdersPanel({ gameId, characters }: OrdersPanelProps) {
  const { data: orders, isLoading } = useOrders(gameId);
  const submitOrder = useSubmitOrder(gameId);
  const cancelOrder = useCancelOrder(gameId);
  const validateOrders = useValidateOrders(gameId);
  const [selectedCharacter, setSelectedCharacter] = useState<string>('');
  const [selectedOrder, setSelectedOrder] = useState<number>(0);
  const [parameters, setParameters] = useState<Record<string, string>>({});

  const getAvailableOrders = () => {
    if (!selectedCharacter) return [];
    const char = characters.find((c) => c.id === selectedCharacter);
    if (!char) return [];

    return ORDER_DEFINITIONS.filter((order) => {
      if (order.restrictions.includes('c') && char.commandSkill <= 10) return false;
      if (order.restrictions.includes('a') && char.agentSkill <= 10) return false;
      if (order.restrictions.includes('e') && char.emissarySkill <= 10) return false;
      if (order.restrictions.includes('m') && char.mageSkill <= 10) return false;
      return true;
    });
  };

  const handleSubmit = async () => {
    if (!selectedCharacter || !selectedOrder) return;
    await submitOrder.mutateAsync({
      characterId: selectedCharacter,
      code: selectedOrder,
      parameters,
    });
    setSelectedOrder(0);
    setParameters({});
  };

  const handleCancel = async (orderId: string) => {
    await cancelOrder.mutateAsync(orderId);
  };

  const handleValidate = async () => {
    await validateOrders.mutateAsync();
  };

  if (isLoading) {
    return <div className="text-gray-400 p-4">Loading orders...</div>;
  }

  return (
    <div className="bg-gray-800 rounded-lg border border-gray-700 p-4">
      <h2 className="text-lg font-bold text-mepbm-gold mb-4">Orders</h2>

      <div className="space-y-4">
        <div>
          <label className="block text-sm text-gray-400 mb-1">Character</label>
          <select
            value={selectedCharacter}
            onChange={(e) => setSelectedCharacter(e.target.value)}
            className="w-full p-2 bg-gray-700 rounded border border-gray-600"
          >
            <option value="">Select character</option>
            {characters.map((char) => (
              <option key={char.id} value={char.id}>
                {char.name} ({char.type}) - C:{char.commandSkill} A:{char.agentSkill} E:{char.emissarySkill} M:{char.mageSkill}
              </option>
            ))}
          </select>
        </div>

        {selectedCharacter && (
          <div>
            <label className="block text-sm text-gray-400 mb-1">Order</label>
            <select
              value={selectedOrder}
              onChange={(e) => {
                setSelectedOrder(parseInt(e.target.value));
                setParameters({});
              }}
              className="w-full p-2 bg-gray-700 rounded border border-gray-600"
            >
              <option value={0}>Select order</option>
              {getAvailableOrders().map((order) => (
                <option key={order.code} value={order.code}>
                  [{order.code}] {order.abbreviation} - {order.name}
                </option>
              ))}
            </select>
          </div>
        )}

        {selectedOrder > 0 && (
          <div>
            <label className="block text-sm text-gray-400 mb-1">Parameters</label>
            <input
              type="text"
              placeholder="e.g., hex: 0,1 or rate: 50"
              value={JSON.stringify(parameters)}
              onChange={(e) => {
                try {
                  setParameters(JSON.parse(e.target.value));
                } catch {}
              }}
              className="w-full p-2 bg-gray-700 rounded border border-gray-600 font-mono text-sm"
            />
          </div>
        )}

        <div className="flex gap-2">
          <button
            onClick={handleSubmit}
            disabled={!selectedCharacter || !selectedOrder || submitOrder.isLoading}
            className="px-4 py-2 bg-mepbm-gold text-gray-900 font-bold rounded hover:bg-yellow-400 transition disabled:opacity-50"
          >
            {submitOrder.isLoading ? 'Submitting...' : 'Submit Order'}
          </button>
          <button
            onClick={handleValidate}
            disabled={validateOrders.isLoading}
            className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-500 transition disabled:opacity-50"
          >
            {validateOrders.isLoading ? 'Validating...' : 'Validate All'}
          </button>
        </div>
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
                <span className="text-gray-400">{JSON.stringify(order.parameters)}</span>
              </span>
              {order.status === 'pending' && (
                <button
                  onClick={() => handleCancel(order.id)}
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
