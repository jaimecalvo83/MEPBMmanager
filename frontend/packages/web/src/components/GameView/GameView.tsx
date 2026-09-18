import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useAuthStore } from '../../stores/authStore';
import { useGameState } from '../../hooks/useGameState';
import { gamesApi } from '../../api/client';
import { useQuery, useMutation, useQueryClient } from 'react-query';
import SetupView from './SetupView';
import ActiveGameView from './ActiveGameView';
import type { PlayerInfo, AdminInfo, TabType } from './types';
import { useLang } from '../../i18n/lang';


export default function GameView() {
  const { t } = useLang();
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
        <div className="text-gray-400 text-lg">{t('setup.loading')}</div>
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
