import { useQuery, useMutation, useQueryClient } from 'react-query';
import { gamesApi } from '../api/client';
import type { GameState } from '../types';

export function useGameState(gameId: string, nationId?: string) {
  return useQuery<GameState>(['game', gameId, nationId ?? 'default'], async () => {
    const { data } = await gamesApi.getState(gameId, nationId);
    return data;
  }, { retry: false });
}

export function useProcessTurn(gameId: string) {
  const queryClient = useQueryClient();
  return useMutation(
    () => gamesApi.processTurn(gameId),
    { onSuccess: () => queryClient.invalidateQueries(['game', gameId]) }
  );
}
