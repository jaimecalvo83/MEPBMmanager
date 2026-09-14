import { useQuery, useMutation, useQueryClient } from 'react-query';
import { gamesApi } from '../api/client';
import type { GameSummary } from '../types';

export function useGames() {
  return useQuery<GameSummary[]>('games', async () => {
    const { data } = await gamesApi.list();
    return data.games;
  });
}

export function useCreateGame() {
  const queryClient = useQueryClient();
  return useMutation(
    (payload: { name: string; gameTypeCode?: string; playerEmails?: string[]; adminEmails?: string[] }) => gamesApi.create(payload),
    { onSuccess: () => queryClient.invalidateQueries('games') }
  );
}

export function useDeleteGame() {
  const queryClient = useQueryClient();
  return useMutation(
    (gameId: string) => gamesApi.delete(gameId),
    { onSuccess: () => queryClient.invalidateQueries('games') }
  );
}
