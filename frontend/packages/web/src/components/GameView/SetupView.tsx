import { useLang, statusLabel } from '../../i18n/lang';
import type { PlayerInfo, AdminInfo } from './types';


// ═══════════════════════════════════════════
// SETUP VIEW
// ═══════════════════════════════════════════
export default function SetupView({
  gameState, playersData, adminsData, currentUserId, isGameAdmin, shouldSeeAll,
  wantsToPlayWithId, setWantsToPlayWithId, newPlayerEmail, setNewPlayerEmail,
  newPlayerIsAdmin, setNewPlayerIsAdmin,
  acceptMutation, addPlayerMutation, removePlayerMutation, startGameMutation, acceptAdminMutation, navigate
}: any) {
  const { t } = useLang();
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
    if (isAdmin && isP) return t('setup.roleBoth');
    if (isAdmin) return t('setup.roleAdmin');
    return t('setup.rolePlayer');
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
        <h2 className="text-xl font-bold text-mepbm-gold mb-2">{t('setup.gameTitle')}{gameState?.game?.name}</h2>
        <p className="text-gray-400 mb-4">{t('setup.statusWaiting', { s: statusLabel(gameState.game.status, t) })}</p>

        {isGameAdmin && (
          <div className="bg-gray-800 rounded-lg p-4 mb-6 border border-blue-600">
            <h3 className="text-lg font-semibold text-white mb-2">{t('setup.title')}</h3>
            <p className="text-gray-400 mb-3 text-sm">
              {pendingAdmins.length > 0 && t('setup.adminsPending', { n: pendingAdmins.length })}
              {pendingPlayers.length > 0 && t('setup.playersPending', { n: pendingPlayers.length })}
              {pendingAdmins.length === 0 && pendingPlayers.length === 0 && allParticipants.length >= 1 && t('setup.allConfirmed')}
              {allParticipants.length < 1 && t('setup.needOne')}
              {canStart ? t('setup.ready') : t('setup.notReady')}
            </p>
            {gameState?.game?.gameTypeCode === '2950' && (
              <p className="text-gray-400 mb-3 text-sm">
                {t('setup.brackets')}
              </p>
            )}
            <button
              onClick={() => startGameMutation.mutate()}
              disabled={!canStart || startGameMutation.isLoading}
              className="px-6 py-3 bg-blue-600 text-white font-bold rounded hover:bg-blue-500 transition disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {startGameMutation.isLoading ? t('setup.starting') : t('setup.start')}
            </button>
            {startGameMutation.isError && (
              <p className="text-red-400 text-sm mt-2">
                {(startGameMutation.error as any)?.response?.data?.error || t('setup.startFailed')}
              </p>
            )}
          </div>
        )}

        {isGameAdmin && (
          <div className="bg-gray-800 rounded-lg p-4 mb-6 border border-gray-600">
            <h3 className="text-lg font-semibold text-white mb-3">{t('setup.addPlayer')}</h3>
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
                  {t('dash.admin')}
                </label>
                <button
                  onClick={handleAddPlayer}
                  disabled={!newPlayerEmail.trim() || addPlayerMutation.isLoading}
                  className="px-6 py-3 bg-green-600 text-white font-bold rounded hover:bg-green-500 transition disabled:opacity-50"
                >
                  {addPlayerMutation.isLoading ? t('dash.adding') : t('common.add')}
                </button>
              </div>
              {addPlayerMutation.isError && (
                <p className="text-red-400 text-sm mt-2">
                  {(addPlayerMutation.error as any)?.response?.data?.error || t('setup.addFailed')}
                </p>
              )}
          </div>
        )}

        {isPlayer && !hasAccepted && (
          <div className="bg-gray-800 rounded-lg p-4 mb-6 border border-yellow-600">
            <h3 className="text-lg font-semibold text-yellow-400 mb-3">{t('setup.invited')}</h3>
            <p className="text-gray-400 mb-3">{t('setup.chooseWith')}</p>
            <select
              value={wantsToPlayWithId}
              onChange={(e) => setWantsToPlayWithId(e.target.value)}
              className="w-full p-3 bg-gray-700 rounded border border-gray-600 focus:border-mepbm-gold focus:outline-none mb-4"
            >
              <option value="">{t('setup.noPref')}</option>
              {otherPlayers.map((p: PlayerInfo) => (
                <option key={p.userId} value={p.userId}>{p.username} ({p.email})</option>
              ))}
            </select>
            <button
              onClick={() => acceptMutation.mutate(wantsToPlayWithId || undefined)}
              disabled={acceptMutation.isLoading}
              className="px-6 py-3 bg-green-600 text-white font-bold rounded hover:bg-green-500 transition disabled:opacity-50"
            >
              {acceptMutation.isLoading ? t('setup.confirming') : t('setup.confirmAtt')}
            </button>
          </div>
        )}

        {isPlayer && hasAccepted && (
          <div className="bg-gray-800 rounded-lg p-4 mb-6 border border-green-600">
            <p className="text-green-400 font-semibold">{t('setup.confirmedAtt')}</p>
            {currentPlayer?.wantsToPlayWith && (
              <p className="text-gray-400 mt-1">{t('setup.playingWith', { x: currentPlayer.wantsToPlayWith.username })}</p>
            )}
          </div>
        )}

        {needsAdminAccept && (
          <div className="bg-gray-800 rounded-lg p-4 mb-6 border border-purple-600">
            <h3 className="text-lg font-semibold text-purple-300 mb-2">{t('setup.invitedAdmin')}</h3>
            <p className="text-gray-400 mb-3 text-sm">{t('setup.adminMsg')}</p>
            <button
              onClick={() => acceptAdminMutation.mutate()}
              disabled={acceptAdminMutation.isLoading}
              className="px-6 py-3 bg-purple-600 text-white font-bold rounded hover:bg-purple-500 transition disabled:opacity-50"
            >
              {acceptAdminMutation.isLoading ? t('setup.accepting') : t('setup.acceptAdmin')}
            </button>
            {acceptAdminMutation.isError && (
              <p className="text-red-400 text-sm mt-2">
                {(acceptAdminMutation.error as any)?.response?.data?.error || t('setup.acceptFailed')}
              </p>
            )}
          </div>
        )}

        <div className="bg-gray-800 rounded-lg p-4 mb-6">
          <h3 className="text-lg font-semibold text-white mb-2">
            {t('setup.participants', { c: confirmedParticipants.length, t: visibleParticipants.length })}
          </h3>
          <table className="w-full">
            <thead>
              <tr className="border-b border-gray-700">
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-400 uppercase">{t('setup.thName')}</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-400 uppercase">{t('setup.thRole')}</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-400 uppercase">{t('setup.thStatus')}</th>
                {shouldSeeAll && (
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-400 uppercase">{t('setup.thPlayingWith')}</th>
                )}
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-400 uppercase">{t('setup.thActions')}</th>
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
                        {participant.isReady ? t('setup.confirmed') : t('setup.pending')}
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
                            if (confirm(t('setup.confirmRemove', { x: participant.username || participant.email })))
                              removePlayerMutation.mutate(participant.id);
                           }}
                          className="text-red-400 hover:text-red-300 text-xs font-semibold"
                        >{t('common.remove')}</button>
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
          {t('setup.back')}
        </button>
      </div>
    </div>
  );
}
