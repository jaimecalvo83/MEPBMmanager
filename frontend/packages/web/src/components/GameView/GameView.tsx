import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useAuthStore } from '../../stores/authStore';
import { useGameState, useProcessTurn } from '../../hooks/useGameState';
import { gamesApi } from '../../api/client';
import HexMap from '../Map/HexMap';
import OrdersPanel from '../Orders/OrdersPanel';
import MessagesPanel from '../Messages/MessagesPanel';
import NationPicker from '../NationPicker/NationPicker';
import { useQuery } from 'react-query';

export default function GameView() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { logout } = useAuthStore();
  const { data: gameState, isLoading, error } = useGameState(id!);
  const processTurn = useProcessTurn(id!);
  const [selectedHex, setSelectedHex] = useState<{ q: number; r: number } | null>(null);
  const [activeTab, setActiveTab] = useState<'map' | 'orders' | 'messages'>('map');
  const [showPicker, setShowPicker] = useState(false);

  const isForbidden = !!error && (error as any)?.response?.status === 403;

  const { data: notPlayerGame } = useQuery(
    ['game-not-player', id],
    async () => {
      const { data } = await gamesApi.get(id!);
      return data as { id: string; name: string; status: string };
    },
    { enabled: isForbidden, retry: false }
  );

  const handleProcessTurn = async () => {
    if (!id) return;
    try {
      await processTurn.mutateAsync();
    } catch {
      // error handled by mutation state
    }
  };

  if (isLoading) {
    return (
      <div className="min-h-screen bg-gray-900 flex items-center justify-center">
        <div className="text-gray-400 text-lg">Loading game...</div>
      </div>
    );
  }

  if (isForbidden && notPlayerGame) {
    return (
      <div className="min-h-screen bg-gray-900 flex items-center justify-center">
        {showPicker && notPlayerGame.status === 'setup' && (
          <NationPicker
            gameId={notPlayerGame.id}
            onClose={() => setShowPicker(false)}
            onJoined={() => {
              setShowPicker(false);
              window.location.reload();
            }}
          />
        )}
        <div className="text-center">
          <h2 className="text-2xl font-bold text-mepbm-gold mb-2">{notPlayerGame.name}</h2>
          {notPlayerGame.status === 'setup' ? (
            <>
              <p className="text-gray-400 mb-6">You are not a player in this game yet.</p>
              <button
                onClick={() => setShowPicker(true)}
                className="px-6 py-3 bg-blue-600 text-white text-lg rounded hover:bg-blue-500 transition"
              >
                Choose Nation & Join
              </button>
            </>
          ) : (
            <p className="text-gray-400 mb-6">You are not a player in this game.</p>
          )}
          <div className="mt-6">
            <button
              onClick={() => navigate('/')}
              className="px-4 py-2 bg-gray-700 text-gray-300 rounded hover:bg-gray-600 transition"
            >
              Back to Games
            </button>
          </div>
        </div>
      </div>
    );
  }

  if (!gameState) {
    return (
      <div className="min-h-screen bg-gray-900 flex items-center justify-center">
        <div className="text-center">
          <p className="text-gray-400 mb-4">Game not found</p>
          <button
            onClick={() => navigate('/')}
            className="px-4 py-2 bg-mepbm-gold text-gray-900 rounded hover:bg-yellow-400"
          >
            Back to Games
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gray-900">
      <header className="bg-gray-800 border-b border-gray-700 px-6 py-3">
        <div className="flex justify-between items-center">
          <div className="flex items-center gap-4">
            <button
              onClick={() => navigate('/')}
              className="text-gray-400 hover:text-white"
            >
              ← Back
            </button>
            <h1 className="text-xl font-bold text-mepbm-gold">{gameState.game.name}</h1>
            <span className="text-sm text-gray-400">Turn {gameState.game.currentTurn}</span>
            <button
              onClick={handleProcessTurn}
              disabled={processTurn.isLoading}
              className="px-3 py-1.5 bg-red-600 text-white text-sm font-bold rounded hover:bg-red-500 transition disabled:opacity-50"
            >
              {processTurn.isLoading ? 'Processing…' : 'Process Turn'}
            </button>
            {processTurn.isError && (
              <span className="text-sm text-red-400">
                {(processTurn.error as any)?.response?.data?.error || 'Failed to process turn'}
              </span>
            )}
          </div>
          <div className="flex items-center gap-4">
            <div className="text-sm text-gray-400">
              {gameState.nation ? (
                <>
                  <span className="text-mepbm-gold">{gameState.nation.name}</span>
                  {' | '}
                  Gold: {gameState.nation.gold} | Food: {gameState.nation.food} | Tax: {gameState.nation.taxRate}%
                </>
              ) : (
                <span className="text-mepbm-gold">No nation assigned yet</span>
              )}
            </div>
            <button onClick={logout} className="text-sm text-gray-500 hover:text-red-400">
              Logout
            </button>
          </div>
        </div>
      </header>

      <div className="flex">
        <nav className="w-48 bg-gray-800 min-h-[calc(100vh-52px)] p-4 border-r border-gray-700">
          <div className="space-y-2">
            {(['map', 'orders', 'messages'] as const).map((tab) => (
              <button
                key={tab}
                onClick={() => setActiveTab(tab)}
                className={`w-full text-left px-3 py-2 rounded transition ${
                  activeTab === tab
                    ? 'bg-mepbm-gold text-gray-900 font-bold'
                    : 'text-gray-400 hover:bg-gray-700'
                }`}
              >
                {tab.charAt(0).toUpperCase() + tab.slice(1)}
              </button>
            ))}
          </div>

          <div className="mt-8 space-y-4">
            <div>
              <h3 className="text-xs text-gray-500 uppercase mb-2">Characters</h3>
              {gameState.characters.map((char) => (
                <div key={char.id} className="text-xs text-gray-400 py-1">
                  {char.name} <span className="text-gray-600">({char.type})</span>
                </div>
              ))}
            </div>

            <div>
              <h3 className="text-xs text-gray-500 uppercase mb-2">Armies</h3>
              {gameState.armies.map((army) => (
                <div key={army.id} className="text-xs text-gray-400 py-1">
                  {army.name} <span className="text-gray-600">(M:{army.morale})</span>
                </div>
              ))}
            </div>

            <div>
              <h3 className="text-xs text-gray-500 uppercase mb-2">Population Centres</h3>
              {gameState.populationCentres.map((pc) => (
                <div key={pc.id} className="text-xs text-gray-400 py-1">
                  {pc.name} <span className="text-gray-600">({pc.size})</span>
                  {pc.isCapital && <span className="text-mepbm-gold ml-1">★</span>}
                </div>
              ))}
            </div>
          </div>
        </nav>

        <main className="flex-1 p-4">
          {activeTab === 'map' && (
            <HexMap
              hexes={gameState.hexTiles}
              armies={gameState.armies}
              characters={gameState.characters}
              populationCentres={gameState.populationCentres}
              onHexClick={(q, r) => setSelectedHex({ q, r })}
              selectedHex={selectedHex}
            />
          )}

          {activeTab === 'orders' && (
            <OrdersPanel
              gameId={gameState.game.id}
              characters={gameState.characters}
            />
          )}

          {activeTab === 'messages' && (
            <MessagesPanel gameId={gameState.game.id} />
          )}
        </main>
      </div>
    </div>
  );
}
