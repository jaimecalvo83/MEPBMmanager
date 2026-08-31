import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuthStore } from '../../stores/authStore';
import { useGames, useCreateGame } from '../../hooks/useGames';
import NationPicker from '../NationPicker/NationPicker';

export default function Dashboard() {
  const { user, logout } = useAuthStore();
  const navigate = useNavigate();
  const { data: games, isLoading } = useGames();
  const createGame = useCreateGame();
  const [showCreate, setShowCreate] = useState(false);
  const [newGameName, setNewGameName] = useState('');
  const [newGameType, setNewGameType] = useState('1650');
  const [joinGameId, setJoinGameId] = useState<string | null>(null);

  const handleCreate = async () => {
    if (!newGameName.trim()) return;
    await createGame.mutateAsync({ name: newGameName, gameTypeCode: newGameType || '1650' });
    setNewGameName('');
    setShowCreate(false);
  };

  return (
    <div className="min-h-screen bg-gray-900">
      {joinGameId && (
        <NationPicker
          gameId={joinGameId}
          onClose={() => setJoinGameId(null)}
          onJoined={() => {
            setJoinGameId(null);
            navigate(`/game/${joinGameId}`);
          }}
        />
      )}
      <header className="bg-gray-800 border-b border-gray-700 px-6 py-4 flex justify-between items-center">
        <h1 className="text-xl font-bold text-mepbm-gold">MEPBMmanager</h1>
        <div className="flex items-center gap-4">
          <span className="text-gray-400">{user?.username}</span>
          <button onClick={logout} className="text-sm text-gray-500 hover:text-red-400 transition">
            Logout
          </button>
        </div>
      </header>

      <main className="max-w-7xl mx-auto p-6">
        <div className="flex justify-between items-center mb-6">
          <h2 className="text-2xl font-bold text-mepbm-gold">My Games</h2>
          <button
            onClick={() => setShowCreate(!showCreate)}
            className="px-4 py-2 bg-mepbm-gold text-gray-900 font-bold rounded hover:bg-yellow-400 transition"
          >
            Create Game
          </button>
        </div>

        {showCreate && (
          <div className="bg-gray-800 p-4 rounded-lg mb-6 border border-gray-700">
            <div className="flex gap-4">
              <input
                type="text"
                placeholder="Game name"
                value={newGameName}
                onChange={(e) => setNewGameName(e.target.value)}
                className="flex-1 p-3 bg-gray-700 rounded border border-gray-600 focus:border-mepbm-gold focus:outline-none"
              />
              <select
                value={newGameType}
                onChange={(e) => setNewGameType(e.target.value)}
                className="p-3 bg-gray-700 rounded border border-gray-600 focus:border-mepbm-gold focus:outline-none"
              >
                <option value="1650">1650</option>
                <option value="2950">2950</option>
                <option value="pruebas">pruebas</option>
              </select>
              <button
                onClick={handleCreate}
                disabled={createGame.isLoading}
                className="px-6 py-3 bg-green-600 text-white font-bold rounded hover:bg-green-500 transition disabled:opacity-50"
              >
                {createGame.isLoading ? 'Creating...' : 'Create'}
              </button>
              <button
                onClick={() => setShowCreate(false)}
                className="px-6 py-3 bg-gray-700 text-gray-300 rounded hover:bg-gray-600 transition"
              >
                Cancel
              </button>
            </div>
          </div>
        )}

        {isLoading ? (
          <div className="text-center text-gray-400 py-12">Loading games...</div>
        ) : !games || games.length === 0 ? (
          <div className="text-center py-12">
            <p className="text-gray-400 text-lg mb-4">No games yet</p>
            <p className="text-gray-500">Create a new game or join an existing one</p>
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {games.map((game) => (
              <div
                key={game.id}
                className="bg-gray-800 rounded-lg p-6 border border-gray-700 hover:border-mepbm-gold transition cursor-pointer"
                onClick={() => navigate(`/game/${game.id}`)}
              >
                <h3 className="text-lg font-semibold text-white mb-2">{game.name}</h3>
                <div className="space-y-1 text-sm text-gray-400">
                  <p>Scenario: {game.gameTypeCode}</p>
                  <p>
                    Status:{' '}
                    <span
                      className={`font-medium ${
                        game.status === 'active'
                          ? 'text-green-400'
                          : game.status === 'setup'
                          ? 'text-yellow-400'
                          : 'text-gray-500'
                      }`}
                    >
                      {game.status}
                    </span>
                  </p>
                  <p>Turn: {game.currentTurn || '-'}</p>
                  <p>Players: {game.playerCount}</p>
                </div>
                {game.status === 'setup' && (
                  <button
                    onClick={(e) => {
                      e.stopPropagation();
                      setJoinGameId(game.id);
                    }}
                    className="mt-4 w-full px-4 py-2 bg-blue-600 text-white text-sm rounded hover:bg-blue-500 transition"
                  >
                    Join Game
                  </button>
                )}
              </div>
            ))}
          </div>
        )}
      </main>
    </div>
  );
}
