import { useQuery } from 'react-query';
import { gamesApi } from '../api/client';
import type { NationListItem } from '../types';

export function useNations(gameId: string) {
  return useQuery<NationListItem[]>(['nations', gameId], async () => {
    const { data } = await gamesApi.getNations(gameId);
    return data.nations;
  });
}
