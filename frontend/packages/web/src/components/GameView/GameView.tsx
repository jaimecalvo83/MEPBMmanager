import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useAuthStore } from '../../stores/authStore';
import { useGameState, useProcessTurn } from '../../hooks/useGameState';
import { gamesApi } from '../../api/client';
import { useQuery, useMutation, useQueryClient } from 'react-query';
import HexMap from '../Map/HexMap';
import OrdersPanel from '../Orders/OrdersPanel';
import MessagesPanel from '../Messages/MessagesPanel';
import NationPicker from '../NationPicker/NationPicker';
import { SPELL_DEFINITIONS } from '@MEPBMmanager/shared';

interface PlayerInfo {
  id: string;
  userId: string;
  username: string;
  email: string;
  isReady: boolean;
  acceptedAt: string | null;
  wantsToPlayWith: { userId: string; username: string } | null;
}

interface AdminInfo {
  id: string;
  userId: string;
  username: string;
  email: string;
  isReady: boolean;
  acceptedAt: string | null;
}

type TabType = 'nation' | 'map' | 'cities' | 'armies' | 'characters' | 'orders' | 'messages' | 'relations' | 'reports' | 'standings';

export default function GameView() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { user } = useAuthStore();
  const queryClient = useQueryClient();
  const [wantsToPlayWithId, setWantsToPlayWithId] = useState('');
  const [newPlayerEmail, setNewPlayerEmail] = useState('');
  const [newPlayerIsAdmin, setNewPlayerIsAdmin] = useState(false);
  const [selectedHex, setSelectedHex] = useState<{ q: number; r: number } | null>(null);
  const [activeTab, setActiveTab] = useState<TabType>('nation');
  const [selectedNationId, setSelectedNationId] = useState<string | undefined>(undefined);

  const isTestAdmin = user?.roleId === 'test_admin' || user?.role === 'test_admin' || user?.role === 'Test Admin';

  // Fetch game state (with optional nation switching)
  const { data: gameState, isLoading } = useGameState(id!, selectedNationId);
  const gameStatus = gameState?.game?.status;
  const isSetup = gameStatus === 'setup';

  // ── Setup queries (only for setup phase) ──
  const { data: playersData } = useQuery(
    ['game-players', id],
    async () => {
      const { data } = await gamesApi.getPlayers(id!);
      return data as { players: PlayerInfo[] };
    },
    { enabled: !!id && isSetup }
  );

  const { data: adminsData } = useQuery(
    ['game-admins', id],
    async () => {
      const { data } = await gamesApi.getAdmins(id!);
      return data as { admins: AdminInfo[] };
    },
    { enabled: !!id && isSetup }
  );

  // ── Mutations ──
  const acceptMutation = useMutation(
    async (wantsToPlayWithUserId?: string) => {
      await gamesApi.accept(id!, wantsToPlayWithUserId);
    },
    {
      onSuccess: () => {
        queryClient.invalidateQueries(['game-players', id]);
        queryClient.invalidateQueries(['game-admins', id]);
        queryClient.invalidateQueries(['game', id]);
      }
    }
  );

  const addPlayerMutation = useMutation(
    async ({ email, isAdmin }: { email: string; isAdmin: boolean }) => {
      await gamesApi.addPlayer(id!, email, isAdmin);
    },
    {
      onSuccess: () => {
        setNewPlayerEmail('');
        setNewPlayerIsAdmin(false);
        queryClient.invalidateQueries(['game-players', id]);
        queryClient.invalidateQueries(['game-admins', id]);
      }
    }
  );

  const removePlayerMutation = useMutation(
    async (playerId: string) => {
      await gamesApi.removePlayer(id!, playerId);
    },
    {
      onSuccess: () => {
        queryClient.invalidateQueries(['game-players', id]);
        queryClient.invalidateQueries(['game-admins', id]);
      }
    }
  );

  const startGameMutation = useMutation(
    async () => {
      await gamesApi.start(id!);
    },
    {
      onSuccess: () => {
        queryClient.invalidateQueries(['game', id]);
      }
    }
  );

  const acceptAdminMutation = useMutation(
    async () => {
      await gamesApi.acceptAdmin(id!);
    },
    {
      onSuccess: () => {
        queryClient.invalidateQueries(['game-players', id]);
        queryClient.invalidateQueries(['game-admins', id]);
        queryClient.invalidateQueries(['game', id]);
      }
    }
  );

  if (isLoading) {
    return (
      <div className="min-h-screen bg-gray-900 flex items-center justify-center">
        <div className="text-gray-400 text-lg">Loading game...</div>
      </div>
    );
  }

  const currentUserId = user?.id;
  const isGameAdmin = isSetup
    ? (adminsData?.admins?.some(a => a.userId === currentUserId) || false)
    : isTestAdmin || !!gameState?.player;

  const shouldSeeAll = isTestAdmin || isGameAdmin;

  // ── SETUP VIEW ──
  if (isSetup) {
    return <SetupView
      gameState={gameState!}
      playersData={playersData}
      adminsData={adminsData}
      currentUserId={currentUserId!}
      isTestAdmin={isTestAdmin}
      isGameAdmin={isGameAdmin}
      shouldSeeAll={shouldSeeAll}
      wantsToPlayWithId={wantsToPlayWithId}
      setWantsToPlayWithId={setWantsToPlayWithId}
      newPlayerEmail={newPlayerEmail}
      setNewPlayerEmail={setNewPlayerEmail}
      newPlayerIsAdmin={newPlayerIsAdmin}
      setNewPlayerIsAdmin={setNewPlayerIsAdmin}
      acceptMutation={acceptMutation}
      addPlayerMutation={addPlayerMutation}
      removePlayerMutation={removePlayerMutation}
      startGameMutation={startGameMutation}
      acceptAdminMutation={acceptAdminMutation}
      navigate={navigate}
    />;
  }

  // ── ACTIVE GAME VIEW ──
  return <ActiveGameView
    gameState={gameState!}
    isTestAdmin={isTestAdmin}
    selectedNationId={selectedNationId}
    setSelectedNationId={setSelectedNationId}
    selectedHex={selectedHex}
    setSelectedHex={setSelectedHex}
    activeTab={activeTab}
    setActiveTab={setActiveTab}
    navigate={navigate}
  />;
}

// ═══════════════════════════════════════════
// SETUP VIEW
// ═══════════════════════════════════════════
function SetupView({
  gameState, playersData, adminsData, currentUserId, isGameAdmin, shouldSeeAll,
  wantsToPlayWithId, setWantsToPlayWithId, newPlayerEmail, setNewPlayerEmail,
  newPlayerIsAdmin, setNewPlayerIsAdmin,
  acceptMutation, addPlayerMutation, removePlayerMutation, startGameMutation, acceptAdminMutation, navigate
}: any) {
  const currentPlayer = playersData?.players?.find((p: PlayerInfo) => p.userId === currentUserId);
  const hasAccepted = currentPlayer?.isReady === true;
  const isPlayer = !!currentPlayer;
  const myAdmin = adminsData?.admins?.find((a: AdminInfo) => a.userId === currentUserId);
  const needsAdminAccept = !!myAdmin && !myAdmin.isReady;

  const adminUserIds = new Set(adminsData?.admins?.map((a: AdminInfo) => a.userId) || []);
  const playerUserIds = new Set(playersData?.players?.map((p: PlayerInfo) => p.userId) || []);

  const allParticipants = [
    ...(playersData?.players || []),
    ...(adminsData?.admins || [])
      .filter((a: AdminInfo) => !playerUserIds.has(a.userId))
      .map((a: AdminInfo) => ({
        id: a.id, userId: a.userId, username: a.username, email: a.email,
        isReady: a.isReady, acceptedAt: a.acceptedAt,
        wantsToPlayWith: null as { userId: string; username: string } | null
      }))
  ];

  const confirmedParticipants = allParticipants.filter((p: any) => p.isReady);
  const visibleParticipants = shouldSeeAll ? allParticipants : confirmedParticipants;
  const otherPlayers = playersData?.players?.filter((p: PlayerInfo) => p.userId !== currentUserId) || [];

  const getRole = (playerUserId: string): string => {
    const isAdmin = adminUserIds.has(playerUserId);
    const isP = playerUserIds.has(playerUserId);
    if (isAdmin && isP) return 'Player + Admin';
    if (isAdmin) return 'Admin';
    return 'Player';
  };

  const pendingAdmins = adminsData?.admins?.filter((a: AdminInfo) => !a.isReady) || [];
  const pendingPlayers = playersData?.players?.filter((p: PlayerInfo) => !p.isReady) || [];
  const canStart = isGameAdmin && pendingAdmins.length === 0 && pendingPlayers.length === 0 && allParticipants.length >= 1;

  const handleAddPlayer = () => {
    if (!newPlayerEmail.trim()) return;
    addPlayerMutation.mutate({ email: newPlayerEmail.trim(), isAdmin: newPlayerIsAdmin });
  };

  return (
    <div className="min-h-screen bg-gray-900">
      <div className="p-6">
        <h2 className="text-xl font-bold text-mepbm-gold mb-2">Game: {gameState?.game?.name}</h2>
        <p className="text-gray-400 mb-4">Status: {gameState.game.status} | Waiting for players...</p>

        {isGameAdmin && (
          <div className="bg-gray-800 rounded-lg p-4 mb-6 border border-blue-600">
            <h3 className="text-lg font-semibold text-white mb-2">Game Setup</h3>
            <p className="text-gray-400 mb-3 text-sm">
              {pendingAdmins.length > 0 && `${pendingAdmins.length} admin(s) pending. `}
              {pendingPlayers.length > 0 && `${pendingPlayers.length} player(s) pending. `}
              {pendingAdmins.length === 0 && pendingPlayers.length === 0 && allParticipants.length >= 1 && 'All confirmed! '}
              {allParticipants.length < 1 && 'Need at least 1 participant. '}
              {canStart ? 'Ready to start!' : 'Not ready yet.'}
            </p>
            {gameState?.game?.gameTypeCode === '2950' && (
              <p className="text-gray-400 mb-3 text-sm">
                2950 brackets (free + dark + neutral): 6-10 → 4+4+2 · 11-15 → 6+6+3 · 16-20 → 8+8+4 · 21-25 → 10+10+5.
                Surplus nations play as NPCs (max 4, neutrals first) on a cropped map and can join later.
              </p>
            )}
            <button
              onClick={() => startGameMutation.mutate()}
              disabled={!canStart || startGameMutation.isLoading}
              className="px-6 py-3 bg-blue-600 text-white font-bold rounded hover:bg-blue-500 transition disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {startGameMutation.isLoading ? 'Starting...' : 'Start Game'}
            </button>
            {startGameMutation.isError && (
              <p className="text-red-400 text-sm mt-2">
                {(startGameMutation.error as any)?.response?.data?.error || 'Failed to start game'}
              </p>
            )}
          </div>
        )}

        {isGameAdmin && (
          <div className="bg-gray-800 rounded-lg p-4 mb-6 border border-gray-600">
            <h3 className="text-lg font-semibold text-white mb-3">Add Player</h3>
            <div className="flex gap-3 items-end">
              <div className="flex-1">
                <input
                  type="email"
                  value={newPlayerEmail}
                  onChange={(e) => setNewPlayerEmail(e.target.value)}
                  placeholder="player@email.com"
                  className="w-full p-3 bg-gray-700 rounded border border-gray-600 focus:border-mepbm-gold focus:outline-none"
                  onKeyDown={(e) => e.key === 'Enter' && handleAddPlayer()}
                />
              </div>
              <label className="flex items-center gap-2 text-sm text-gray-400 whitespace-nowrap">
                <input type="checkbox" checked={newPlayerIsAdmin}
                  onChange={(e) => setNewPlayerIsAdmin(e.target.checked)} className="rounded" />
                Admin
              </label>
              <button
                onClick={handleAddPlayer}
                disabled={!newPlayerEmail.trim() || addPlayerMutation.isLoading}
                className="px-6 py-3 bg-green-600 text-white font-bold rounded hover:bg-green-500 transition disabled:opacity-50"
              >
                {addPlayerMutation.isLoading ? 'Adding...' : 'Add'}
              </button>
            </div>
            {addPlayerMutation.isError && (
              <p className="text-red-400 text-sm mt-2">
                {(addPlayerMutation.error as any)?.response?.data?.error || 'Failed to add player'}
              </p>
            )}
          </div>
        )}

        {isPlayer && !hasAccepted && (
          <div className="bg-gray-800 rounded-lg p-4 mb-6 border border-yellow-600">
            <h3 className="text-lg font-semibold text-yellow-400 mb-3">You are invited to this game</h3>
            <p className="text-gray-400 mb-3">Choose a player you want to play with (optional):</p>
            <select
              value={wantsToPlayWithId}
              onChange={(e) => setWantsToPlayWithId(e.target.value)}
              className="w-full p-3 bg-gray-700 rounded border border-gray-600 focus:border-mepbm-gold focus:outline-none mb-4"
            >
              <option value="">No preference</option>
              {otherPlayers.map((p: PlayerInfo) => (
                <option key={p.userId} value={p.userId}>{p.username} ({p.email})</option>
              ))}
            </select>
            <button
              onClick={() => acceptMutation.mutate(wantsToPlayWithId || undefined)}
              disabled={acceptMutation.isLoading}
              className="px-6 py-3 bg-green-600 text-white font-bold rounded hover:bg-green-500 transition disabled:opacity-50"
            >
              {acceptMutation.isLoading ? 'Confirming...' : 'Confirm Attendance'}
            </button>
          </div>
        )}

        {isPlayer && hasAccepted && (
          <div className="bg-gray-800 rounded-lg p-4 mb-6 border border-green-600">
            <p className="text-green-400 font-semibold">You have confirmed your attendance</p>
            {currentPlayer?.wantsToPlayWith && (
              <p className="text-gray-400 mt-1">Playing with: {currentPlayer.wantsToPlayWith.username}</p>
            )}
          </div>
        )}

        {needsAdminAccept && (
          <div className="bg-gray-800 rounded-lg p-4 mb-6 border border-purple-600">
            <h3 className="text-lg font-semibold text-purple-300 mb-2">You are invited as game admin</h3>
            <p className="text-gray-400 mb-3 text-sm">Accept the admin role so the game can be started. Nations are assigned when the game starts.</p>
            <button
              onClick={() => acceptAdminMutation.mutate()}
              disabled={acceptAdminMutation.isLoading}
              className="px-6 py-3 bg-purple-600 text-white font-bold rounded hover:bg-purple-500 transition disabled:opacity-50"
            >
              {acceptAdminMutation.isLoading ? 'Accepting...' : 'Accept Admin Role'}
            </button>
            {acceptAdminMutation.isError && (
              <p className="text-red-400 text-sm mt-2">
                {(acceptAdminMutation.error as any)?.response?.data?.error || 'Failed to accept admin role'}
              </p>
            )}
          </div>
        )}

        <div className="bg-gray-800 rounded-lg p-4 mb-6">
          <h3 className="text-lg font-semibold text-white mb-2">
            Participants ({confirmedParticipants.length} confirmed / {visibleParticipants.length} total)
          </h3>
          <table className="w-full">
            <thead>
              <tr className="border-b border-gray-700">
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-400 uppercase">Name</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-400 uppercase">Role</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-400 uppercase">Status</th>
                {shouldSeeAll && (
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-400 uppercase">Playing With</th>
                )}
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-400 uppercase">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-700">
              {visibleParticipants.map((participant: any) => {
                const role = getRole(participant.userId);
                return (
                  <tr key={participant.id}>
                    <td className="px-4 py-3">
                      <div className="text-sm font-medium text-white">{participant.username || participant.email}</div>
                      <div className="text-xs text-gray-500">{participant.email}</div>
                    </td>
                    <td className="px-4 py-3">
                      <span className={`text-xs px-2 py-1 rounded ${
                        role === 'Admin' ? 'bg-purple-600 text-white' :
                        role === 'Player + Admin' ? 'bg-blue-600 text-white' :
                        'bg-gray-600 text-white'
                      }`}>{role}</span>
                    </td>
                    <td className="px-4 py-3">
                      <span className={`text-xs px-2 py-1 rounded ${participant.isReady ? 'bg-green-600 text-white' : 'bg-yellow-600 text-white'}`}>
                        {participant.isReady ? 'Confirmed' : 'Pending'}
                      </span>
                    </td>
                    {shouldSeeAll && (
                      <td className="px-4 py-3 text-sm text-gray-400">
                        {participant.wantsToPlayWith?.username || '-'}
                      </td>
                    )}
                    <td className="px-4 py-3">
                      {!participant.isReady && isGameAdmin && (
                        <button
                          onClick={() => {
                            if (confirm(`Remove ${participant.username || participant.email}?`))
                              removePlayerMutation.mutate(participant.id);
                          }}
                          className="text-red-400 hover:text-red-300 text-xs font-semibold"
                        >Remove</button>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>

        <button onClick={() => navigate('/')}
          className="px-6 py-3 bg-mepbm-gold text-white font-bold rounded hover:bg-yellow-400 transition">
          Back to Games
        </button>
      </div>
    </div>
  );
}

// ═══════════════════════════════════════════
// ACTIVE GAME VIEW
// ═══════════════════════════════════════════
function ActiveGameView({ gameState, isTestAdmin, selectedNationId, setSelectedNationId, selectedHex, setSelectedHex, activeTab, setActiveTab, navigate }: any) {
  const nation = gameState?.nation;
  const nations = gameState?.nations || [];
  const characters = gameState?.characters || [];
  const armies = gameState?.armies || [];
  const populationCentres = gameState?.populationCentres || [];
  const hexTiles = gameState?.hexTiles || [];
  const currentTurn = gameState?.currentTurn;

  // Only admins see the nation sidebar
  const canSwitchNations = isTestAdmin || nations.length > 1;

  const gameId = gameState?.game?.id as string;
  const turns = gameState?.turns || [];
  const canProcess = gameState?.isGameAdmin === true || isTestAdmin;
  const needsNation = !!gameState?.player && !gameState.player.nationId;
  const [showNationPicker, setShowNationPicker] = useState(false);
  const processTurnMutation = useProcessTurn(gameId);

  const tabs: { key: TabType; label: string }[] = [
    { key: 'nation', label: 'Nation' },
    { key: 'map', label: 'Map' },
    { key: 'cities', label: `Cities (${populationCentres.length})` },
    { key: 'armies', label: `Armies (${armies.length})` },
    { key: 'characters', label: `Characters (${characters.length})` },
    { key: 'orders', label: 'Orders' },
    { key: 'messages', label: 'Messages' },
    { key: 'relations', label: 'Relations' },
    { key: 'reports', label: `Reports (${turns.length})` },
    { key: 'standings', label: 'Standings' },
  ];

  // Group nations by allegiance for sidebar
  const freeNations = nations.filter((n: any) => n.allegiance === 'free_peoples');
  const darkNations = nations.filter((n: any) => n.allegiance === 'dark_servants');
  const neutralNations = nations.filter((n: any) => n.allegiance === 'neutral');

  return (
    <div className="min-h-screen bg-gray-900 flex">
      {/* ── Nation sidebar (only for admins) ── */}
      {canSwitchNations && (
        <div className="w-56 bg-gray-800 border-r border-gray-700 flex-shrink-0 overflow-y-auto">
          <div className="p-3 border-b border-gray-700">
            <h3 className="text-xs font-bold text-mepbm-gold uppercase tracking-wider">Nations</h3>
          </div>
          <div className="p-2 space-y-1">
            {freeNations.length > 0 && (
              <div className="mb-2">
                <div className="px-2 py-1 text-[10px] font-bold text-green-400 uppercase tracking-wider">Free Peoples</div>
                {freeNations.map((n: any) => (
                  <button
                    key={n.id}
                    onClick={() => setSelectedNationId(n.id)}
                    className={`w-full text-left px-3 py-2 rounded text-sm flex items-center gap-2 transition ${
                      n.id === (selectedNationId || nation?.id)
                        ? 'bg-gray-700 border border-mepbm-gold text-white'
                        : 'text-gray-300 hover:bg-gray-750 hover:text-white'
                    }`}
                  >
                    <div className="w-3 h-3 rounded-full flex-shrink-0" style={{ backgroundColor: n.color }} />
                    <span className="truncate">{n.name}</span>
                  </button>
                ))}
              </div>
            )}
            {darkNations.length > 0 && (
              <div className="mb-2">
                <div className="px-2 py-1 text-[10px] font-bold text-red-400 uppercase tracking-wider">Dark Servants</div>
                {darkNations.map((n: any) => (
                  <button
                    key={n.id}
                    onClick={() => setSelectedNationId(n.id)}
                    className={`w-full text-left px-3 py-2 rounded text-sm flex items-center gap-2 transition ${
                      n.id === (selectedNationId || nation?.id)
                        ? 'bg-gray-700 border border-mepbm-gold text-white'
                        : 'text-gray-300 hover:bg-gray-750 hover:text-white'
                    }`}
                  >
                    <div className="w-3 h-3 rounded-full flex-shrink-0" style={{ backgroundColor: n.color }} />
                    <span className="truncate">{n.name}</span>
                  </button>
                ))}
              </div>
            )}
            {neutralNations.length > 0 && (
              <div className="mb-2">
                <div className="px-2 py-1 text-[10px] font-bold text-gray-400 uppercase tracking-wider">Neutral</div>
                {neutralNations.map((n: any) => (
                  <button
                    key={n.id}
                    onClick={() => setSelectedNationId(n.id)}
                    className={`w-full text-left px-3 py-2 rounded text-sm flex items-center gap-2 transition ${
                      n.id === (selectedNationId || nation?.id)
                        ? 'bg-gray-700 border border-mepbm-gold text-white'
                        : 'text-gray-300 hover:bg-gray-750 hover:text-white'
                    }`}
                  >
                    <div className="w-3 h-3 rounded-full flex-shrink-0" style={{ backgroundColor: n.color }} />
                    <span className="truncate">{n.name}</span>
                  </button>
                ))}
              </div>
            )}
          </div>
        </div>
      )}

      {/* ── Main content ── */}
      <div className="flex-1 flex flex-col min-w-0">
        {/* Top bar */}
        <div className="bg-gray-800 border-b border-gray-700 px-6 py-3 flex items-center justify-between">
          <div className="flex items-center gap-4">
            <button onClick={() => navigate('/')} className="text-mepbm-gold hover:text-yellow-400 text-sm font-semibold">
              ← Games
            </button>
            <h1 className="text-lg font-bold text-white">{gameState?.game?.name}</h1>
            {currentTurn && (
              <span className="text-sm text-gray-400">
                Turn {currentTurn.number} · {currentTurn.season} · Deadline: {new Date(currentTurn.deadline).toLocaleDateString()}
              </span>
            )}
          </div>
          <div className="flex items-center gap-3">
            {canProcess && (
              <div className="flex items-center gap-2">
                <button
                  onClick={() => processTurnMutation.mutate()}
                  disabled={processTurnMutation.isLoading}
                  className="px-4 py-2 bg-red-600 text-white text-sm font-bold rounded hover:bg-red-500 transition disabled:opacity-50"
                >
                  {processTurnMutation.isLoading ? 'Processing...' : 'Process Turn'}
                </button>
                {processTurnMutation.isError && (
                  <span className="text-red-400 text-xs">
                    {(processTurnMutation.error as any)?.response?.data?.error || 'Failed'}
                  </span>
                )}
                {processTurnMutation.isSuccess && (
                  <span className="text-green-400 text-xs">Turn processed</span>
                )}
              </div>
            )}
            {nation && (
              <div className="flex items-center gap-2">
                <div className="w-4 h-4 rounded" style={{ backgroundColor: nation.color }} />
                <span className="font-bold text-white">{nation.name}</span>
                <span className="text-sm text-gray-400">({nation.allegiance})</span>
              </div>
            )}
          </div>
        </div>

        {needsNation && (
          <div className="bg-gray-800 border-b border-yellow-600 px-6 py-3 flex items-center gap-4">
            <span className="text-sm text-yellow-300">You have no nation yet. Claim one of the free nations:</span>
            <button
              onClick={() => setShowNationPicker(true)}
              className="px-4 py-2 bg-blue-600 text-white text-sm font-bold rounded hover:bg-blue-500 transition"
            >
              Choose Your Nation
            </button>
          </div>
        )}
        {showNationPicker && (
          <NationPicker
            gameId={gameId}
            mode="update"
            onClose={() => setShowNationPicker(false)}
            onJoined={() => setShowNationPicker(false)}
          />
        )}

        {/* Nation resources bar */}
        {nation && (
          <div className="bg-gray-800 border-b border-gray-700 px-6 py-2 flex gap-6 text-sm">
            <span className="text-yellow-400">Gold: {nation.gold}</span>
            <span className="text-green-400">Food: {nation.food}</span>
            <span className="text-amber-600">Timber: {nation.timber}</span>
            <span className="text-orange-400">Leather: {nation.leather}</span>
            <span className="text-gray-300">Bronze: {nation.bronze}</span>
            <span className="text-blue-300">Steel: {nation.steel}</span>
            {nation.mithril > 0 && <span className="text-purple-400">Mithril: {nation.mithril}</span>}
            <span className="text-emerald-400">Mounts: {nation.mounts}</span>
            <span className="text-gray-500">Tax: {nation.taxRate}%</span>
          </div>
        )}

        {/* Tab bar */}
        <div className="bg-gray-800 border-b border-gray-700 px-6 flex gap-1 overflow-x-auto">
          {tabs.map(({ key, label }) => (
            <button
              key={key}
              onClick={() => setActiveTab(key)}
              className={`px-4 py-2 text-sm font-semibold rounded-t transition whitespace-nowrap ${
                activeTab === key
                  ? 'bg-gray-900 text-mepbm-gold border-t border-x border-gray-700'
                  : 'text-gray-400 hover:text-white'
              }`}
            >
              {label}
            </button>
          ))}
        </div>

        {/* Tab content */}
        <div className="p-6 flex-1 overflow-y-auto">
          {activeTab === 'nation' && (
            <NationTab
              nation={nation}
              populationCentres={populationCentres}
              armies={armies}
              characters={characters}
              currentTurn={currentTurn}
            />
          )}
          {activeTab === 'map' && (
            <div className="space-y-4">
              <HexMap
                hexes={hexTiles}
                armies={armies}
                characters={characters}
                populationCentres={populationCentres}
                selectedHex={selectedHex}
                onHexClick={(q: number, r: number) => setSelectedHex({ q, r })}
              />

              {selectedHex && (
                <div className="bg-gray-800 rounded-lg p-4 border border-gray-700">
                  <h3 className="text-sm font-bold text-mepbm-gold mb-2">
                    Hex: {selectedHex.q}, {selectedHex.r}
                  </h3>
                  <div className="text-sm text-gray-400">
                    {(() => {
                      const hex = hexTiles.find((h: any) => h.q === selectedHex.q && h.r === selectedHex.r);
                      if (!hex) return <span>No data</span>;
                      const feats = [
                        hex.hasMajorRiver ? 'Major river' : null,
                        hex.hasMinorRiver ? 'Minor river' : null,
                        hex.hasRoad ? 'Road' : null,
                        hex.hasFord ? 'Ford' : null,
                        hex.hasBridge ? 'Bridge' : null,
                      ].filter(Boolean);
                      return <span>Terrain: {hex.terrain}{feats.length > 0 ? ` (${feats.join(' · ')})` : ''}</span>;
                    })()}
                    {(() => {
                      const pc = populationCentres.find((p: any) => p.locationHex === `${selectedHex.q},${selectedHex.r}`);
                      return pc ? <span className="ml-4 text-mepbm-gold">★ {pc.name}{pc.isCapital ? ' (Capital)' : ''}</span> : null;
                    })()}
                    {(() => {
                      const army = armies.find((a: any) => a.locationHex === `${selectedHex.q},${selectedHex.r}`);
                      return army ? <span className="ml-4 text-red-400">⚔ {army.name}</span> : null;
                    })()}
                  </div>
                </div>
              )}
            </div>
          )}

          {activeTab === 'cities' && (
            <CitiesTab populationCentres={populationCentres} hexTiles={hexTiles} taxRate={nation?.taxRate} />
          )}

          {activeTab === 'armies' && (
            <ArmiesTab armies={armies} characters={characters} populationCentres={populationCentres} hexTiles={hexTiles} nationName={nation?.name} />
          )}

          {activeTab === 'characters' && (
            <CharactersTab characters={characters} armies={armies} populationCentres={populationCentres} nationName={nation?.name} nations={nations} />
          )}

          {activeTab === 'orders' && (
            <OrdersPanel gameId={gameState?.game?.id} characters={characters} />
          )}

          {activeTab === 'messages' && (
            <MessagesPanel gameId={gameState?.game?.id} />
          )}

          {activeTab === 'relations' && (
            <RelationsTab
              gameId={gameState?.game?.id}
              nationId={selectedNationId || nation?.id}
              nationName={nation?.name}
              allNations={gameState?.allNations || []}
              relations={gameState?.relations || []}
            />
          )}

          {activeTab === 'reports' && (
            <ReportsTab
              gameId={gameState?.game?.id}
              turns={gameState?.turns || []}
            />
          )}

          {activeTab === 'standings' && (
            <StandingsTab
              allNations={gameState?.allNations || []}
            />
          )}
        </div>
      </div>
    </div>
  );
}

// ═══════════════════════════════════════════
// RELATIONS TAB
// ═══════════════════════════════════════════
const RELATION_LEVELS = [
  { value: 2, label: 'Ally' },
  { value: 1, label: 'Tolerant' },
  { value: 0, label: 'Neutral' },
  { value: -1, label: 'Hostile' },
  { value: -2, label: 'Enemy' },
];

const ALLEGIANCE_LABELS: Record<string, string> = {
  free_peoples: 'Free Peoples',
  dark_servants: 'Dark Servants',
  neutral: 'Neutral',
};

function relationBadge(level: number) {
  const color =
    level >= 2 ? 'bg-green-600 text-white'
    : level === 1 ? 'bg-blue-600 text-white'
    : level === 0 ? 'bg-gray-600 text-white'
    : level === -1 ? 'bg-orange-600 text-white'
    : 'bg-red-600 text-white';
  const label = RELATION_LEVELS.find((l) => l.value === level)?.label ?? `${level}`;
  return <span className={`px-2 py-1 rounded text-xs ${color}`}>{label} ({level})</span>;
}

function RelationsTab({ gameId, nationId, nationName, allNations, relations }: {
  gameId: string;
  nationId?: string;
  nationName?: string;
  allNations: any[];
  relations: any[];
}) {
  const queryClient = useQueryClient();
  const [savingId, setSavingId] = useState<string | null>(null);

  const mutation = useMutation(
    async ({ targetId, level }: { targetId: string; level: number }) => {
      setSavingId(targetId);
      await gamesApi.setRelation(gameId, nationId!, targetId, level);
    },
    {
      onSuccess: () => {
        setSavingId(null);
        queryClient.invalidateQueries(['game', gameId]);
      },
      onError: () => setSavingId(null),
    }
  );

  if (!nationId) {
    return <div className="text-gray-400">Select a nation to view its relations.</div>;
  }

  const relByTarget = new Map<string, number>();
  for (const r of relations) relByTarget.set(r.targetNationId, r.level);
  const others = (allNations || []).filter((n: any) => n.id !== nationId);

  return (
    <div className="space-y-4">
      <div className="bg-gray-800 rounded-lg p-4 border border-gray-700 text-sm text-gray-300">
        Relations of <span className="font-bold text-white">{nationName}</span>.
        Level &gt; 0 (tolerant or ally) lets your armies pass; 0 or less blocks them.
      </div>
      <div className="bg-gray-800 rounded-lg border border-gray-700 overflow-hidden">
        <table className="w-full">
          <thead>
            <tr className="border-b border-gray-700 bg-gray-750">
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-400 uppercase">Nation</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-400 uppercase">Side</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-400 uppercase">Relation</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-400 uppercase">Change to</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-700">
            {others.map((n: any) => {
              const level = relByTarget.has(n.id) ? relByTarget.get(n.id)! : 0;
              return (
                <tr key={n.id} className="hover:bg-gray-750">
                  <td className="px-4 py-3">
                    <span className="text-sm font-medium text-white flex items-center gap-2">
                      <span className="w-3 h-3 rounded-full inline-block" style={{ backgroundColor: n.color }} />
                      {n.name}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-sm text-gray-300">{ALLEGIANCE_LABELS[n.allegiance] || n.allegiance}</td>
                  <td className="px-4 py-3 text-sm">{relationBadge(level)}</td>
                  <td className="px-4 py-3 text-sm">
                    <select
                      className="bg-gray-700 text-white text-sm rounded px-2 py-1 border border-gray-600"
                      value={level}
                      disabled={savingId === n.id}
                      onChange={(e) => mutation.mutate({ targetId: n.id, level: parseInt(e.target.value, 10) })}
                    >
                      {RELATION_LEVELS.map((l) => (
                        <option key={l.value} value={l.value}>{l.label} ({l.value})</option>
                      ))}
                    </select>
                    {savingId === n.id && <span className="ml-2 text-xs text-gray-400">Saving…</span>}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}

// ═══════════════════════════════════════════
// REPORTS TAB (turn results)
// ═══════════════════════════════════════════
function ReportsTab({ gameId, turns }: { gameId: string; turns: any[] }) {
  const [selectedTurnId, setSelectedTurnId] = useState<string | null>(turns[0]?.id ?? null);
  const activeTurnId = turns.some((t: any) => t.id === selectedTurnId) ? selectedTurnId : turns[0]?.id ?? null;

  const { data, isLoading, isError } = useQuery(
    ['turn-report', gameId, activeTurnId],
    async () => {
      const { data } = await gamesApi.getTurnReport(gameId, activeTurnId!);
      return data as { turn: any; sections: Array<{ title: string; entries: any[] }> };
    },
    { enabled: !!gameId && !!activeTurnId, retry: false }
  );

  if (turns.length === 0) {
    return <div className="text-gray-400">No turns yet. Reports appear after the first turn is processed.</div>;
  }

  return (
    <div className="space-y-4">
      <div className="flex gap-2 flex-wrap">
        {turns.map((t: any) => (
          <button
            key={t.id}
            onClick={() => setSelectedTurnId(t.id)}
            className={`px-4 py-2 rounded text-sm font-semibold transition ${
              t.id === activeTurnId
                ? 'bg-mepbm-gold text-gray-900'
                : 'bg-gray-800 text-gray-300 hover:bg-gray-700 border border-gray-700'
            }`}
          >
            Turn {t.number} · {t.season} ({t.status})
          </button>
        ))}
      </div>

      {isLoading && <div className="text-gray-400">Loading report...</div>}
      {isError && <div className="text-red-400">Failed to load the turn report.</div>}
      {data && (
        <div className="space-y-4">
          {data.sections.length === 0 && (
            <div className="text-gray-400">No results recorded for this turn yet.</div>
          )}
          {data.sections.map((s, i) => (
            <div key={i} className="bg-gray-800 rounded-lg p-4 border border-gray-700">
              <h3 className="text-md font-bold text-mepbm-gold mb-2">{s.title}</h3>
              <div className="space-y-1">
                {(s.entries || []).map((e: any, j: number) => (
                  <div key={j} className="text-sm text-gray-300">
                    {Object.entries(e || {})
                      .filter(([, v]) => v === null || ['string', 'number', 'boolean'].includes(typeof v))
                      .map(([k, v]) => (
                        <span key={k} className="mr-3">
                          <span className="text-gray-500">{k}: </span>
                          <span className="text-gray-100">{String(v)}</span>
                        </span>
                      ))}
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

// ═══════════════════════════════════════════
// STANDINGS TAB (victory points per allegiance)
// ═══════════════════════════════════════════
function StandingsTab({ allNations }: { allNations: any[] }) {
  if (allNations.length === 0) {
    return <div className="text-gray-400">No nations in this game.</div>;
  }

  const groups: Array<{ key: string; title: string; color: string }> = [
    { key: 'free_peoples', title: 'Free Peoples', color: 'text-green-400' },
    { key: 'dark_servants', title: 'Dark Servants', color: 'text-red-400' },
    { key: 'neutral', title: 'Neutral', color: 'text-gray-400' },
  ];

  return (
    <div className="space-y-4">
      <div className="bg-gray-800 rounded-lg p-4 border border-gray-700 text-sm text-gray-300">
        Turn victory points per nation (recalculated each turn from areas of play, not cumulative).
        Eliminated nations are ranked but cannot win.
      </div>
      {groups.map((g) => {
        const rows = allNations
          .filter((n: any) => n.allegiance === g.key)
          .sort((a: any, b: any) => (b.victoryPoints ?? 0) - (a.victoryPoints ?? 0));
        if (rows.length === 0) return null;
        return (
          <div key={g.key} className="bg-gray-800 rounded-lg border border-gray-700 overflow-hidden">
            <div className={`px-4 py-2 text-sm font-bold uppercase tracking-wider ${g.color}`}>{g.title}</div>
            <table className="w-full">
              <tbody className="divide-y divide-gray-700">
                {rows.map((n: any, i: number) => (
                  <tr key={n.id} className="hover:bg-gray-750">
                    <td className="px-4 py-2 text-sm text-gray-400 w-12">
                      {i === 0 ? '🥇' : i === 1 ? '🥈' : i === 2 ? '🥉' : `${i + 1}º`}
                    </td>
                    <td className="px-4 py-2">
                      <span className="text-sm font-medium text-white flex items-center gap-2">
                        <span className="w-3 h-3 rounded-full inline-block" style={{ backgroundColor: n.color }} />
                        {n.name}
                        {n.isEliminated && (
                          <span className="text-xs px-2 py-0.5 rounded bg-red-900 text-red-300">eliminated</span>
                        )}
                      </span>
                    </td>
                    <td className="px-4 py-2 text-sm text-right text-mepbm-gold font-bold">{n.victoryPoints ?? 0} VP</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        );
      })}
    </div>
  );
}

// ═══════════════════════════════════════════
// NATION TAB (turn-0 style overview)
// ═══════════════════════════════════════════
function NationTab({ nation, populationCentres, armies, characters, currentTurn }: {
  nation: any;
  populationCentres: any[];
  armies: any[];
  characters: any[];
  currentTurn: any;
}) {
  if (!nation) {
    return <div className="text-gray-400">No nation selected.</div>;
  }
  const capital = populationCentres.find((p: any) => p.isCapital);
  const abilities: any[] = nation.abilities || [];
  const stats = [
    { label: 'Cities', value: populationCentres.length },
    { label: 'Armies', value: armies.length },
    { label: 'Characters', value: characters.length },
    { label: 'Tax rate', value: `${nation.taxRate ?? ''}%` },
    { label: 'Victory points', value: nation.victoryPoints ?? 0 },
    { label: 'Warship strength', value: nation.warshipStrength ?? 0 },
  ];

  return (
    <div className="space-y-4">
      <div className="bg-gray-800 rounded-lg p-6 border border-gray-700">
        <div className="flex items-center gap-3">
          <div className="w-6 h-6 rounded-full" style={{ backgroundColor: nation.color }} />
          <div>
            <h2 className="text-2xl font-bold text-white">{nation.name}</h2>
            <p className="text-sm text-gray-400">
              {ALLEGIANCE_LABELS[nation.allegiance] || nation.allegiance}
              {currentTurn ? ` · Turn ${currentTurn.number}${currentTurn.season ? ` · ${currentTurn.season}` : ''}` : ''}
            </p>
          </div>
        </div>
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3 mt-4">
          {stats.map((s) => (
            <div key={s.label} className="bg-gray-750 rounded-lg p-3 border border-gray-700">
              <div className="text-xs text-gray-400 uppercase">{s.label}</div>
              <div className="text-lg font-bold text-mepbm-gold">{s.value}</div>
            </div>
          ))}
        </div>
        {capital && (
          <p className="mt-3 text-sm text-gray-300">
            ★ Capital: {capital.name} <span className="text-gray-500">@ {capital.locationHex}</span>
          </p>
        )}
      </div>
      <div className="bg-gray-800 rounded-lg p-6 border border-gray-700">
        <h3 className="text-sm font-bold text-mepbm-gold uppercase tracking-wider mb-3">Special Nation Abilities</h3>
        {abilities.length === 0 ? (
          <p className="text-sm text-gray-400">—</p>
        ) : (
          <ul className="list-disc list-inside space-y-1">
            {abilities.map((a: any) => (
              <li key={a.id} className="text-sm text-gray-200">
                <span className="font-mono text-xs text-gray-500 mr-2">{a.id}</span>{a.name}
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}

// ── Shared turn-0 helpers ──
function terrainAt(hexTiles: any[], locationHex: string): string {
  const [q, r] = locationHex.split(',').map((v) => parseInt(v.trim(), 10));
  const hex = hexTiles.find((h: any) => h.q === q && h.r === r);
  return hex ? hex.terrain : '?';
}

function pcSentence(populationCentres: any[], locationHex: string, nationName?: string): string | null {
  const pc = populationCentres.find((p: any) => p.locationHex === locationHex);
  if (!pc) return null;
  const fort = pc.fortification ? ` / ${pc.fortification}` : '';
  return `The ${pc.size}${fort} of ${pc.name} flying the flag of ${nationName || 'us'} is here.`;
}

// ═══════════════════════════════════════════
// CITIES TAB (turn-0 style cards)
// ═══════════════════════════════════════════
const CITY_RESOURCES = [
  { key: 'gold', label: 'Gold', color: 'text-yellow-400' },
  { key: 'food', label: 'Food', color: 'text-green-400' },
  { key: 'timber', label: 'Timber', color: 'text-amber-600' },
  { key: 'leather', label: 'Leather', color: 'text-orange-400' },
  { key: 'bronze', label: 'Bronze', color: 'text-gray-300' },
  { key: 'steel', label: 'Steel', color: 'text-blue-300' },
  { key: 'mithril', label: 'Mithril', color: 'text-purple-400' },
  { key: 'mounts', label: 'Mounts', color: 'text-emerald-400' },
];

function pcGoldIncome(pc: any, taxRate: number): number {
  const base = ({ city: 100, 'major town': 75, town: 50, village: 25 } as Record<string, number>)[pc.size?.toLowerCase()] ?? 0;
  return Math.max(0, pc.production) + base * taxRate;
}

function pcResources(pc: any, taxRate: number): Record<string, { production: number; stores: number }> {
  return {
    gold: { production: pcGoldIncome(pc, taxRate), stores: 0 },
    food: { production: 0, stores: 0 },
    timber: { production: 0, stores: pc.stores },
    leather: { production: 0, stores: 0 },
    bronze: { production: 0, stores: 0 },
    steel: { production: 0, stores: 0 },
    mithril: { production: 0, stores: 0 },
    mounts: { production: 0, stores: 0 },
  };
}

function CitiesTab({ populationCentres, hexTiles, taxRate }: { populationCentres: any[]; hexTiles: any[]; taxRate?: number }) {
  if (populationCentres.length === 0) {
    return <div className="text-gray-400">No population centres visible.</div>;
  }

  const docks = (pc: any) => pc.hasPort ? 'Port' : pc.hasHarbour ? 'Harbour' : 'None';

  return (
    <div className="space-y-4">
      {populationCentres.map((pc: any) => {
        const rate = taxRate ?? 30;
        const resources = pcResources(pc, rate);
        return (
          <div key={pc.id} className="bg-gray-800 rounded-lg p-5 border border-gray-700">
            <div className="flex items-center gap-2 flex-wrap">
              <h3 className="text-lg font-bold text-white">
                {pc.isCapital ? '★ ' : '▢ '}{pc.name}{pc.isCapital ? ' (Capital)' : ''}
              </h3>
            </div>
            <p className="text-sm text-gray-400 mt-1">
              Location: @ {pc.locationHex} in {terrainAt(hexTiles, pc.locationHex)}
            </p>
            <dl className="grid grid-cols-2 md:grid-cols-4 gap-x-4 gap-y-1 mt-3 text-sm">
              <div><dt className="inline text-gray-500">Size: </dt><dd className="inline text-gray-200 capitalize">{pc.size}</dd></div>
              <div><dt className="inline text-gray-500">Fortifications: </dt><dd className="inline text-gray-200">{pc.fortification || 'None'}</dd></div>
              <div><dt className="inline text-gray-500">Loyalty: </dt><dd className="inline text-gray-200">{pc.loyalty}</dd></div>
              <div><dt className="inline text-gray-500">Docks: </dt><dd className="inline text-gray-200">{docks(pc)}</dd></div>
              <div><dt className="inline text-gray-500">Hidden?: </dt><dd className="inline text-gray-200">{pc.isHidden ? 'Yes' : 'No'}</dd></div>
              <div><dt className="inline text-gray-500">Sieged?: </dt><dd className="inline text-gray-200">{pc.isSieged ? 'Yes' : 'No'}</dd></div>
              <div><dt className="inline text-gray-500">Tax: </dt><dd className="inline text-gray-200">{rate}%</dd></div>
              <div><dt className="inline text-gray-500">Mined gold: </dt><dd className="inline text-gray-200">{Math.max(0, pc.production)}</dd></div>
            </dl>
            <div className="mt-4 border-t border-gray-700 pt-3">
              <h4 className="text-sm font-bold text-gray-300 mb-2">Resources (turn)</h4>
              <div className="grid grid-cols-2 sm:grid-cols-4 gap-2 text-xs">
                {CITY_RESOURCES.map((r) => (
                  <div key={r.key} className="bg-gray-900 rounded p-2 border border-gray-800">
                    <div className={`font-semibold ${r.color}`}>{r.label}</div>
                    <div className="text-gray-300 mt-1">Prod: <span className="text-gray-100">{resources[r.key].production}</span></div>
                    <div className="text-gray-300">Stores: <span className="text-gray-100">{resources[r.key].stores}</span></div>
                  </div>
                ))}
              </div>
            </div>
          </div>
        );
      })}
    </div>
  );
}

// ═══════════════════════════════════════════
// ARMIES TAB (turn-0 style blocks)
// ═══════════════════════════════════════════
const TROOP_ROWS = [
  { key: 'heavyCavalry', label: 'Heavy Cavalry', w: 'hcWeaponRank', a: 'hcArmourRank', tr: 'hcTraining' },
  { key: 'lightCavalry', label: 'Light Cavalry', w: 'lcWeaponRank', a: 'lcArmourRank', tr: 'lcTraining' },
  { key: 'heavyInfantry', label: 'Heavy Infantry', w: 'hiWeaponRank', a: 'hiArmourRank', tr: 'hiTraining' },
  { key: 'lightInfantry', label: 'Light Infantry', w: 'liWeaponRank', a: 'liArmourRank', tr: 'liTraining' },
  { key: 'archers', label: 'Archers', w: 'archerWeaponRank', a: 'archerArmourRank', tr: 'archerTraining' },
  { key: 'menAtArms', label: 'Men-at-Arms', w: 'maaWeaponRank', a: 'maaArmourRank', tr: 'maaTraining' },
];

function materialName(rank: number, kind: 'weapon' | 'armour'): string {
  if (rank >= 100) return 'mithril';
  if (rank >= 60) return 'steel';
  if (rank >= 30) return 'bronze';
  if (kind === 'armour') return rank >= 10 ? 'leather' : 'none';
  return rank >= 10 ? 'wood' : 'none';
}

// Wiki (armies topic) + CombatResolver: Str/Con base, best/worst tactic, upkeep.
const TROOP_INFO: Record<string, { str: number; con: number; best: string; worst: string; terrain: string; gold: number; food: number }> = {
  heavyCavalry: { str: 16, con: 16, best: 'ch', worst: 'am', terrain: 'plains', gold: 6, food: 2 },
  lightCavalry: { str: 8, con: 8, best: 'su', worst: 'am', terrain: 'plains / desert / coast', gold: 3, food: 2 },
  heavyInfantry: { str: 10, con: 10, best: 'fl', worst: 'su', terrain: 'hills / mountains', gold: 4, food: 1 },
  lightInfantry: { str: 5, con: 5, best: 'hr', worst: 'ch', terrain: 'forest', gold: 2, food: 1 },
  archers: { str: 6, con: 2, best: 'am', worst: 'fl', terrain: 'hills / forest', gold: 2, food: 1 },
  menAtArms: { str: 2, con: 2, best: 'hr', worst: 'ch', terrain: 'plains / hills / coast', gold: 1, food: 1 },
};

function armyFoodCost(army: any): number {
  return TROOP_ROWS.reduce((s, t) => s + (army[t.key] || 0) * (TROOP_INFO[t.key]?.food ?? 1), 0);
}

function armyGoldCost(army: any): number {
  return TROOP_ROWS.reduce((s, t) => s + (army[t.key] || 0) * (TROOP_INFO[t.key]?.gold ?? 0), 0);
}

function armyTroopTotal(army: any): number {
  return TROOP_ROWS.reduce((s, t) => s + (army[t.key] || 0), 0);
}

function ArmyCharChip({ c, isCommander }: { c: any; isCommander: boolean }) {
  return (
    <Tip
      trigger={
        <span className={`inline-block px-2 py-1 rounded text-xs border ${isCommander ? 'bg-gray-700 border-mepbm-gold text-mepbm-gold' : 'bg-gray-700 border-gray-600 text-gray-200'}`}>
          {c.name}{isCommander ? ' ★' : ''}
        </span>
      }
    >
      <span className="block text-white font-bold text-sm">{c.name}{isCommander ? ' (commander)' : ''}</span>
      <span className="block text-xs text-gray-400 mt-0.5">{c.type ?? ''}</span>
      <span className="block text-sm text-gray-200 mt-1">
        Command {c.commandSkill ?? 0} · Agent {c.agentSkill ?? 0} · Emissary {c.emissarySkill ?? 0} · Mage {c.mageSkill ?? 0}
      </span>
      <span className="block text-sm text-gray-200">Health {c.health ?? '?'}{c.maxHealth ? ` / ${c.maxHealth}` : ''}</span>
    </Tip>
  );
}

function ArmiesTab({ armies, characters, populationCentres, hexTiles, nationName }: {
  armies: any[]; characters: any[]; populationCentres: any[]; hexTiles: any[]; nationName?: string;
}) {
  if (armies.length === 0) {
    return <div className="text-gray-400">No armies visible.</div>;
  }

  const charById = new Map<string, any>();
  for (const c of characters) charById.set(c.id, c);

  return (
    <div className="space-y-5">
      {armies.map((army: any) => {
        const commander = army.commanderId ? charById.get(army.commanderId) : null;
        const pcLine = pcSentence(populationCentres, army.locationHex, nationName);
        const members = characters.filter((c: any) => c.armyId === army.id);
        const total = armyTroopTotal(army);
        const eats = armyFoodCost(army);
        const upkeep = armyGoldCost(army);
        const food = army.food ?? 0;
        const turns = eats > 0 ? Math.floor(food / eats) : null;
        const rows = TROOP_ROWS.filter((t) => (army[t.key] || 0) > 0);
        const sparesW = army.spareWeapons ?? 0;
        const sparesWMat = army.spareWeaponsMaterial ?? 'none';
        const sparesA = army.spareArmour ?? 0;
        const sparesAMat = army.spareArmourMaterial ?? 'none';
        return (
          <div key={army.id} className="bg-gray-800 rounded-lg p-5 border border-gray-700 space-y-4">
            <div className="flex items-center gap-2 flex-wrap">
              <h3 className="text-xl font-bold text-white">{army.name}</h3>
              <span className="text-xs px-2 py-1 rounded bg-gray-900 border border-gray-600 text-gray-300">
                @ {army.locationHex} · {terrainAt(hexTiles, army.locationHex)}
              </span>
              <span className="ml-auto text-xs text-gray-400">{total} troops</span>
            </div>

            <div>
              <div className="text-[11px] uppercase tracking-wide text-gray-500 mb-1">Command & Upkeep</div>
              <div className="grid grid-cols-2 md:grid-cols-4 gap-2">
                <StatBox label="Morale" value={army.morale ?? 0} />
                <StatBox label="Training (avg)" value={army.training ?? 0} />
                <StatBox label="Food/turn" value={`${eats} (${total} troops)`} />
                <StatBox label="Gold/turn" value={upkeep} />
              </div>
            </div>

            <div>
              <div className="text-[11px] uppercase tracking-wide text-gray-500 mb-1">Baggage Train</div>
              <div className="grid grid-cols-2 md:grid-cols-4 gap-2">
                <StatBox label="Food" value={`${food}${turns != null ? ` (${turns} turns)` : ''}`} />
                <StatBox label="War Machines" value={army.warMachines ?? 0} />
                <StatBox label="Spare Weapons" value={sparesW > 0 ? `${sparesW} ${sparesWMat}` : '—'} />
                <StatBox label="Spare Armour" value={sparesA > 0 ? `${sparesA} ${sparesAMat}` : '—'} />
              </div>
            </div>

            {rows.length > 0 && (
              <div>
                <div className="text-[11px] uppercase tracking-wide text-gray-500 mb-1">Troops by type, weapon & armour</div>
                <table className="w-full text-sm">
                  <thead>
                    <tr className="text-left text-xs text-gray-500 uppercase">
                      <th className="py-1 pr-3">Type</th>
                      <th className="py-1 pr-3 text-right">#</th>
                      <th className="py-1 pr-3">Weapon</th>
                      <th className="py-1 pr-3">Armour</th>
                      <th className="py-1 pr-3">Training</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-700">
                    {rows.map((t) => {
                      const count = army[t.key] || 0;
                      const w = army[t.w] ?? 0;
                      const a = army[t.a] ?? 0;
                      const tr = army[t.tr] ?? army.training ?? 0;
                      const info = TROOP_INFO[t.key];
                      const shareFood = count * (info?.food ?? 1);
                      const shareGold = count * (info?.gold ?? 0);
                      return (
                        <tr key={t.key} className="text-gray-200">
                          <td className="py-1 pr-3">
                            <Tip
                              trigger={<span className="cursor-help">{t.label}</span>}
                            >
                              <span className="block text-white font-bold text-sm">{t.label} × {count}</span>
                              <span className="block text-sm text-gray-200 mt-1">Strength {info?.str} · Constitution {info?.con}</span>
                              <span className="block text-sm text-gray-200">Upkeep: {shareGold} gold + {shareFood} food/turn ({info?.gold}g + {info?.food}f each)</span>
                              <span className="block text-sm text-gray-200">Best tactic {info?.best} · worst {info?.worst}</span>
                              <span className="block text-sm text-gray-200">Terrain: {info?.terrain}</span>
                              <span className="block text-sm text-gray-200 mt-1">Weapons: {materialName(w, 'weapon')} ({w})</span>
                              <span className="block text-sm text-gray-200">Armour: {materialName(a, 'armour')} ({a})</span>
                              <span className="block text-sm text-gray-200">Training: {tr}</span>
                            </Tip>
                          </td>
                          <td className="py-1 pr-3 text-right font-mono">{count}</td>
                          <td className="py-1 pr-3">{materialName(w, 'weapon')} <span className="text-gray-500 font-mono">({w})</span></td>
                          <td className="py-1 pr-3">{materialName(a, 'armour')} <span className="text-gray-500 font-mono">({a})</span></td>
                          <td className="py-1 pr-3 font-mono">{tr}</td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}

            <div>
              <div className="text-[11px] uppercase tracking-wide text-gray-500 mb-1">
                Characters{members.length > 0 ? ` (${members.length})` : ''}
              </div>
              {members.length > 0 ? (
                <div className="flex flex-wrap gap-2">
                  {members.map((c: any) => (
                    <ArmyCharChip key={c.id} c={c} isCommander={c.id === army.commanderId} />
                  ))}
                </div>
              ) : (
                <p className="text-sm text-gray-500">—</p>
              )}
              {!commander && <p className="text-xs text-gray-500 mt-1">No commander assigned.</p>}
            </div>

            {pcLine && <p className="text-sm text-gray-400 border-t border-gray-700 pt-3">{pcLine}</p>}
          </div>
        );
      })}
    </div>
  );
}

// ═══════════════════════════════════════════
// CHARACTERS TAB (turn-0 style cards)
// ═══════════════════════════════════════════
function Tip({ trigger, children }: { trigger: React.ReactNode; children: React.ReactNode }) {
  return (
    <span className="relative inline-block group/tip">
      <span className="cursor-help">{trigger}</span>
      <span className="hidden group-hover/tip:block absolute left-0 top-full mt-1 z-30 w-72 max-w-[80vw] rounded-none bg-gray-900 border-2 border-mepbm-gold p-3 text-left shadow-xl">
        {children}
      </span>
    </span>
  );
}

function ArtifactChip({ a, holder, nationName }: { a: any; holder: string; nationName?: string }) {
  const type = a.type ?? a.Type;
  const bonus = a.bonus ?? a.Bonus ?? 0;
  const alignment = a.alignment ?? a.Alignment;
  const loc = a.locationHex ?? a.LocationHex;
  const wikiId = a.wikiId ?? a.WikiId;
  const primary = a.primaryBenefit ?? a.PrimaryBenefit;
  const secondary = a.secondaryPower ?? a.SecondaryPower;
  return (
    <Tip
      trigger={
        <span className="inline-block px-2 py-1 rounded bg-gray-700 border border-amber-600/60 text-xs text-amber-200">
          {wikiId ? `#${wikiId} ` : ''}{a.name ?? a.Name ?? '?'}
        </span>
      }
    >
      <span className="block text-amber-200 font-bold text-sm">{wikiId ? `#${wikiId} ` : ''}{a.name ?? a.Name ?? 'Artifact'}</span>
      {type && <span className="block text-xs text-gray-400 mt-0.5">{type}</span>}
      {primary && <span className="block text-sm text-gray-200 mt-1">{primary}</span>}
      {secondary && secondary !== '-' && <span className="block text-sm text-gray-200">{secondary}</span>}
      <span className="block text-sm text-gray-200 mt-1">Bonus +{bonus}</span>
      {alignment && <span className="block text-sm text-gray-200">Alignment: {alignment}</span>}
      {loc && <span className="block text-sm text-gray-200">Location: {loc}</span>}
      {nationName && <span className="block text-sm text-gray-200">Nation: {nationName}</span>}
      <span className="block text-sm text-gray-200">Held by: {holder}</span>
    </Tip>
  );
}

function SpellChip({ s }: { s: any }) {
  const id = s.spellId ?? s.SpellId;
  const info = SPELL_DEFINITIONS.find((d: any) => d.id === id);
  const college = s.wikiCollege ?? info?.category ?? s.college ?? '';
  const minRank = s.minRank ?? info?.minCastingRank;
  const difficulty = s.difficulty;
  const castOrder = s.castOrder;
  const prereqs = s.prerequisites;
  const reqInfo = s.requiredInfo;
  const effect = s.effect ?? info?.description;
  return (
    <Tip
      trigger={
        <span className="inline-block px-2 py-1 rounded bg-gray-700 border border-violet-500/60 text-xs text-violet-200">
          #{id} {s.name ?? s.Name ?? info?.name ?? '?'} <span className="text-gray-400">({s.rank ?? s.Rank ?? 0})</span>
        </span>
      }
    >
      <span className="block text-violet-200 font-bold text-sm">#{id} {s.name ?? s.Name ?? info?.name ?? 'Spell'}</span>
      <span className="block text-xs text-gray-400 mt-0.5">
        {college}{difficulty ? ` · ${difficulty}` : ''}{minRank != null ? ` · min rank ${minRank}` : ''} · rank {s.rank ?? s.Rank ?? 0}
      </span>
      {effect && <span className="block text-sm text-gray-200 mt-1">{effect}</span>}
      {prereqs && <span className="block text-sm text-gray-200 mt-1">Prerequisites: {prereqs}</span>}
      {reqInfo && <span className="block text-sm text-gray-200">Required info: {reqInfo}</span>}
      {castOrder && <span className="block text-xs text-gray-400 mt-1">{castOrder}</span>}
    </Tip>
  );
}

function StatBox({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="bg-gray-900 rounded px-3 py-2 border border-gray-700">
      <div className="text-[11px] uppercase tracking-wide text-gray-500">{label}</div>
      <div className="text-lg font-bold text-white leading-tight">{value}</div>
    </div>
  );
}

function CharactersTab({ characters, armies, populationCentres, nationName, nations }: {
  characters: any[]; armies: any[]; populationCentres: any[]; nationName?: string; nations?: any[];
}) {
  if (characters.length === 0) {
    return <div className="text-gray-400">No characters visible.</div>;
  }

  const armyById = new Map<string, any>();
  for (const a of armies) armyById.set(a.id, a);
  const nationById = new Map<string, string>();
  for (const n of nations ?? []) nationById.set(n.id, n.name);

  const typeColor: Record<string, string> = {
    commander: 'bg-blue-600',
    agent: 'bg-purple-600',
    emissary: 'bg-teal-600',
    mage: 'bg-orange-600',
  };

  return (
    <div className="space-y-5">
      {characters.map((char: any) => {
        const army = char.armyId ? armyById.get(char.armyId) : null;
        const pcLine = pcSentence(populationCentres, char.locationHex, nationName);
        const artifacts: any[] = char.artifacts || [];
        const spells: any[] = char.spells || [];
        return (
          <div key={char.id} className="bg-gray-800 rounded-lg p-5 border border-gray-700 space-y-4">
            <div className="flex items-center gap-2 flex-wrap">
              <h3 className="text-xl font-bold text-white">{char.name}</h3>
              <span className={`text-xs px-2 py-1 rounded text-white ${typeColor[char.type] || 'bg-gray-600'}`}>
                {char.type}
              </span>
              {char.isChampion && <span className="text-xs px-2 py-1 rounded bg-yellow-600 text-white">Champion</span>}
              {char.isDead && <span className="text-xs px-2 py-1 rounded bg-red-600 text-white">Dead</span>}
              {char.isKidnapped && <span className="text-xs px-2 py-1 rounded bg-orange-600 text-white">Kidnapped</span>}
              <span className="ml-auto text-xs px-2 py-1 rounded bg-gray-900 border border-gray-600 text-gray-300">
                @ {char.locationHex}
              </span>
            </div>

            <div>
              <div className="text-[11px] uppercase tracking-wide text-gray-500 mb-1">Skills</div>
              <div className="grid grid-cols-2 md:grid-cols-4 gap-2">
                <StatBox label="Command" value={char.commandSkill} />
                <StatBox label="Agent" value={char.agentSkill} />
                <StatBox label="Emissary" value={char.emissarySkill} />
                <StatBox label="Mage" value={char.mageSkill} />
              </div>
            </div>

            <div>
              <div className="text-[11px] uppercase tracking-wide text-gray-500 mb-1">Status</div>
              <div className="grid grid-cols-3 gap-2 max-w-md">
                <StatBox label="Health" value={`${char.health ?? '?'}${char.maxHealth ? ` / ${char.maxHealth}` : ''}`} />
                <StatBox label="Stealth" value={char.stealth ?? 0} />
                <StatBox label="Challenge" value={char.challengeRank ?? 0} />
              </div>
            </div>

            <div>
              <div className="text-[11px] uppercase tracking-wide text-gray-500 mb-1">
                Artifacts{artifacts.length > 0 ? ` (${artifacts.length})` : ''}
              </div>
              {artifacts.length > 0 ? (
                <div className="flex flex-wrap gap-2">
                  {artifacts.map((a: any) => (
                    <ArtifactChip
                      key={a.id ?? a.Id ?? a.name}
                      a={a}
                      holder={char.name}
                      nationName={nationById.get(a.nationId ?? a.NationId)}
                    />
                  ))}
                </div>
              ) : (
                <p className="text-sm text-gray-500">—</p>
              )}
            </div>

            <div>
              <div className="text-[11px] uppercase tracking-wide text-gray-500 mb-1">
                Spells{spells.length > 0 ? ` (${spells.length})` : ''}
              </div>
              {spells.length > 0 ? (
                <div className="flex flex-wrap gap-2">
                  {spells.map((s: any) => <SpellChip key={s.spellId ?? s.SpellId} s={s} />)}
                </div>
              ) : (
                <p className="text-sm text-gray-500">—</p>
              )}
            </div>

            <p className="text-sm text-gray-400 border-t border-gray-700 pt-3">
              {army ? `${char.name} commands an army at ${char.locationHex}.` : `${char.name} is currently at ${char.locationHex}.`}
              {pcLine ? ` ${pcLine}` : ''}
            </p>
          </div>
        );
      })}
    </div>
  );
}
