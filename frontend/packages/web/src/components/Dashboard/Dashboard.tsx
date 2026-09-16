import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuthStore } from '../../stores/authStore';
import { useGames, useCreateGame } from '../../hooks/useGames';
import { useQueryClient } from 'react-query';
import { gamesApi } from '../../api/client';
import { useLang, LanguageSwitcher } from '../../i18n/lang';

export default function Dashboard() {
  const { t } = useLang();
  const { user, logout } = useAuthStore();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { data: games, isLoading } = useGames();
  const createGame = useCreateGame();
  const [showCreate, setShowCreate] = useState(false);
  const [newGameName, setNewGameName] = useState('');
  const [newGameType, setNewGameType] = useState('2950');
  const [playerEmails, setPlayerEmails] = useState<string[]>([]);
  const [newPlayerEmail, setNewPlayerEmail] = useState('');
  const [adminEmails, setAdminEmails] = useState<string[]>([]);

  const isTestAdmin = user?.role === 'test_admin';

  // Max admins = number of alliances - 1 (creator is already admin)
  // For 2950: 3 alliances (good, evil, neutral) => max 2 admins
  // For pruebas: same logic
  const getMaxAdmins = () => {
    // TODO: Get actual alliance count from game type
    return 2;
  };

  const handleAddPlayer = () => {
    if (!newPlayerEmail.trim()) return;
    if (playerEmails.includes(newPlayerEmail)) return;
    setPlayerEmails([...playerEmails, newPlayerEmail]);
    setNewPlayerEmail('');
  };

  const handleRemovePlayer = (email: string) => {
    setPlayerEmails(playerEmails.filter(e => e !== email));
    setAdminEmails(adminEmails.filter(e => e !== email));
  };

  const handleToggleAdmin = (email: string) => {
    if (adminEmails.includes(email)) {
      setAdminEmails(adminEmails.filter(e => e !== email));
    } else {
      // Only allow if under limit
      if (adminEmails.length < getMaxAdmins()) {
        setAdminEmails([...adminEmails, email]);
      }
    }
  };

const handleCreate = async () => {
    if (!newGameName.trim()) return;
    await createGame.mutateAsync({
      name: newGameName,
      gameTypeCode: newGameType || '2950',
      playerEmails: playerEmails,
      adminEmails: adminEmails,
    });
    setNewGameName('');
    setPlayerEmails([]);
    setAdminEmails([]);
    setShowCreate(false);
  };

  const deleteGame = async (gameId: string) => {
    await gamesApi.delete(gameId);
    queryClient.invalidateQueries('games');
  };

  return (
    <div className="min-h-screen bg-gray-900">
      <header className="bg-gray-800 border-b border-gray-700 px-6 py-4 flex justify-between items-center">
        <h1 className="text-xl font-bold text-mepbm-gold">MEPBMmanager</h1>
        <div className="flex items-center gap-4">
          <span className="text-gray-400">{user?.username}</span>
          <span className="text-xs px-2 py-0.5 rounded bg-gray-700 text-gray-300">
            {user?.role}
          </span>
          <LanguageSwitcher small />
          <button onClick={logout} className="text-sm text-gray-500 hover:text-red-400 transition">
            {t('auth.logout')}
          </button>
        </div>
      </header>

      <main className="max-w-7xl mx-auto p-6">
        <div className="flex justify-between items-center mb-6">
          <h2 className="text-2xl font-bold text-mepbm-gold">{t('dash.myGames')}</h2>
          <button
            onClick={() => setShowCreate(!showCreate)}
            className="px-4 py-2 bg-mepbm-gold text-gray-900 font-bold rounded hover:bg-yellow-400 transition"
          >
            {t('dash.createGame')}
          </button>
        </div>

        {showCreate && (
          <div className="bg-gray-800 p-4 rounded-lg mb-6 border border-gray-700">
            <div className="space-y-4">
              <div className="flex gap-4">
                <input
                  type="text"
                  placeholder={t('dash.gameName')}
                  value={newGameName}
                  onChange={(e) => setNewGameName(e.target.value)}
                  className="flex-1 p-3 bg-gray-700 rounded border border-gray-600 focus:border-mepbm-gold focus:outline-none"
                />
                <select
                  value={newGameType}
                  onChange={(e) => setNewGameType(e.target.value)}
                  className="p-3 bg-gray-700 rounded border border-gray-600 focus:border-mepbm-gold focus:outline-none"
                >
                  <option value="2950">2950</option>
                  {isTestAdmin && <option value="pruebas">pruebas</option>}
                </select>
              </div>

              <div>
                <label className="block text-sm text-gray-400 mb-2">
                  {t('dash.addPlayers')}
                </label>
                <div className="flex gap-2 mb-2">
                  <input
                    type="email"
                    placeholder="player@example.com"
                    value={newPlayerEmail}
                    onChange={(e) => setNewPlayerEmail(e.target.value)}
                    onKeyDown={(e) => e.key === 'Enter' && handleAddPlayer()}
                    className="flex-1 p-3 bg-gray-700 rounded border border-gray-600 focus:border-mepbm-gold focus:outline-none"
                  />
                  <button
                    onClick={handleAddPlayer}
                    disabled={!newPlayerEmail.trim()}
                    className="px-4 py-3 bg-green-600 text-white rounded hover:bg-green-500 transition disabled:opacity-50"
                  >
                    {t('common.add')}
                  </button>
                </div>
                
                {playerEmails.length > 0 && (
                  <div className="bg-gray-700 rounded border border-gray-600 p-2">
                    {playerEmails.map((email) => {
                      const isAdmin = adminEmails.includes(email);
                      const canBeAdmin = adminEmails.length < getMaxAdmins() || isAdmin;
                      return (
                        <div
                          key={email}
                          className="flex items-center justify-between p-2 hover:bg-gray-600 rounded"
                        >
                          <div className="flex items-center gap-3">
                            <label className={`flex items-center gap-2 cursor-pointer ${!canBeAdmin ? 'opacity-50' : ''}`}>
                              <input
                                type="checkbox"
                                checked={isAdmin}
                                onChange={() => handleToggleAdmin(email)}
                                disabled={!canBeAdmin}
                                className="rounded"
                              />
                              <span className="text-xs text-mepbm-gold">{t('dash.admin')}</span>
                            </label>
                            <span className="text-sm text-gray-300">{email}</span>
                          </div>
                          <button
                            onClick={() => handleRemovePlayer(email)}
                            className="text-red-400 hover:text-red-300 text-sm"
                          >
                            {t('common.remove')}
                          </button>
                        </div>
                      );
                    })}
                  </div>
                )}
                
                {playerEmails.length > 0 && (
                  <p className="text-xs text-gray-500 mt-1">
                    {t('dash.counts', { n: playerEmails.length, m: adminEmails.length, max: getMaxAdmins() })}
                    {adminEmails.length >= getMaxAdmins() && (
                      <span className="text-yellow-400 ml-2">{t('dash.maxReached')}</span>
                    )}
                  </p>
                )}

                {adminEmails.length > 0 && (
                  <div className="mt-3 p-2 bg-gray-600 rounded">
                    <p className="text-xs text-mepbm-gold font-semibold mb-1">{t('dash.admins')}</p>
                    <div className="flex flex-wrap gap-2">
                      {adminEmails.map(email => (
                        <span key={email} className="text-xs bg-mepbm-gold text-gray-900 px-2 py-1 rounded">{email}</span>
                      ))}
                    </div>
                  </div>
                )}
              </div>

              <div className="flex gap-3">
                <button
                  onClick={handleCreate}
                  disabled={createGame.isLoading || !newGameName.trim() || playerEmails.length === 0}
                  className="px-6 py-3 bg-green-600 text-white font-bold rounded hover:bg-green-500 transition disabled:opacity-50"
                >
                  {createGame.isLoading ? t('dash.creating') : t('dash.createGame')}
                </button>
                <button
                  onClick={() => {
setShowCreate(false);
                    setPlayerEmails([]);
                    setAdminEmails([]);
                  }}
                  className="px-6 py-3 bg-gray-700 text-gray-300 rounded hover:bg-gray-600 transition"
                >
                  {t('common.cancel')}
                </button>
              </div>
            </div>
          </div>
        )}

        {isLoading ? (
          <div className="text-center text-gray-400 py-12">{t('dash.loading')}</div>
        ) : !games || games.length === 0 ? (
          <div className="text-center py-12">
            <p className="text-gray-400 text-lg mb-4">{t('dash.noGames')}</p>
            <p className="text-gray-500">{t('dash.noGamesSub')}</p>
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
                  <p>{t('dash.scenario')} {game.gameTypeCode}</p>
                  <p>
                    {t('dash.status')}{' '}
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
                  <p>{t('dash.turn')} {game.currentTurn || '-'}</p>
                  <p>{t('dash.players')} {game.playerCount}</p>
                </div>
                {game.status === 'setup' && !game.isPlayer && (
                  <button
                    onClick={async (e) => {
                      e.stopPropagation();
                      await gamesApi.join(game.id);
                      navigate(`/game/${game.id}`);
                    }}
                    className="mt-4 w-full px-4 py-2 bg-blue-600 text-white text-sm rounded hover:bg-blue-500 transition"
                  >
                    {t('dash.joinGame')}
                  </button>
                )}
                {game.status === 'setup' && game.isPlayer && (
                  <button
                    onClick={(e) => {
                      e.stopPropagation();
                      navigate(`/game/${game.id}`);
                    }}
                    className="mt-4 w-full px-4 py-2 bg-green-600 text-white text-sm rounded hover:bg-green-500 transition"
                  >
                    {t('dash.enterGame')}
                  </button>
                )}
                {game.status === 'setup' && (
                  <button
                    onClick={async (e) => {
                      e.stopPropagation();
                      if (confirm(t('dash.confirmDelete'))) {
                        await deleteGame(game.id);
                      }
                    }}
                    className="mt-4 w-full px-4 py-2 bg-red-600 text-white text-sm rounded hover:bg-red-500 transition"
                  >
                    {t('dash.deleteGame')}
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