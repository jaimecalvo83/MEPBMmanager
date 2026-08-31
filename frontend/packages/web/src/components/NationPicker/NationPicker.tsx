import { useState } from 'react';
import { useNations } from '../../hooks/useNations';
import { useQueryClient } from 'react-query';
import { gamesApi } from '../../api/client';

interface NationPickerProps {
  gameId: string;
  onClose: () => void;
  onJoined: () => void;
}

const NationPicker: React.FC<NationPickerProps> = ({ gameId, onClose, onJoined }) => {
  const { data: nations, isLoading } = useNations(gameId);
  const queryClient = useQueryClient();
  const [selected, setSelected] = useState<string | null>(null);
  const [joining, setJoining] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const available = nations?.filter((n) => !n.taken) ?? [];

  const handleJoin = async () => {
    if (!selected) return;
    setJoining(true);
    setError(null);
    try {
      await gamesApi.join(gameId, selected);
      await queryClient.invalidateQueries(['game', gameId]);
      onJoined();
    } catch (err: unknown) {
      const axiosError = err as { response?: { data?: { error?: string } } };
      setError(axiosError.response?.data?.error || 'Failed to join game');
      setJoining(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-black/60 flex items-center justify-center z-50">
      <div className="bg-gray-800 rounded-lg p-6 w-full max-w-lg border border-gray-700">
        <div className="flex justify-between items-center mb-4">
          <h2 className="text-xl font-bold text-mepbm-gold">Choose Your Nation</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-white text-xl leading-none">
            &times;
          </button>
        </div>

        {isLoading ? (
          <div className="text-center text-gray-400 py-8">Loading nations...</div>
        ) : (
          <div className="max-h-80 overflow-y-auto space-y-2 mb-4">
            {available.length === 0 ? (
              <p className="text-gray-400 text-center py-6">No nations available</p>
            ) : (
              available.map((nation) => (
                <button
                  key={nation.id}
                  onClick={() => setSelected(nation.id)}
                  className={`w-full text-left px-4 py-3 rounded border transition ${
                    selected === nation.id
                      ? 'bg-mepbm-gold text-gray-900 border-mepbm-gold font-bold'
                      : 'bg-gray-700 border-gray-600 text-gray-200 hover:border-mepbm-gold'
                  }`}
                >
                  <div className="flex items-center gap-3">
                    <span
                      className="inline-block w-4 h-4 rounded-full"
                      style={{ backgroundColor: nation.color || '#888' }}
                    />
                    <span>{nation.name}</span>
                    {nation.allegiance && (
                      <span className="text-xs opacity-70">({nation.allegiance})</span>
                    )}
                  </div>
                </button>
              ))
            )}
          </div>
        )}

        {error && <p className="text-red-400 text-sm mb-3">{error}</p>}

        <div className="flex gap-3">
          <button
            onClick={handleJoin}
            disabled={!selected || joining}
            className="flex-1 px-4 py-2 bg-blue-600 text-white font-bold rounded hover:bg-blue-500 transition disabled:opacity-50"
          >
            {joining ? 'Joining...' : 'Join Game'}
          </button>
          <button
            onClick={onClose}
            className="px-4 py-2 bg-gray-700 text-gray-300 rounded hover:bg-gray-600 transition"
          >
            Cancel
          </button>
        </div>
      </div>
    </div>
  );
};

export default NationPicker;
