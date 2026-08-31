import { useQuery, useMutation, useQueryClient } from 'react-query';
import { ordersApi } from '../api/client';
import type { OrderListItem } from '../types';

export function useOrders(gameId: string) {
  return useQuery<OrderListItem[]>(['orders', gameId], async () => {
    const { data } = await ordersApi.list(gameId);
    return data.orders;
  });
}

export function useSubmitOrder(gameId: string) {
  const queryClient = useQueryClient();
  return useMutation(
    (payload: { characterId: string; code: number; parameters?: Record<string, unknown>; armyId?: string }) =>
      ordersApi.submit(gameId, payload),
    { onSuccess: () => queryClient.invalidateQueries(['orders', gameId]) }
  );
}

export function useCancelOrder(gameId: string) {
  const queryClient = useQueryClient();
  return useMutation(
    (orderId: string) => ordersApi.cancel(gameId, orderId),
    { onSuccess: () => queryClient.invalidateQueries(['orders', gameId]) }
  );
}

export function useValidateOrders(gameId: string) {
  return useMutation(() => ordersApi.validate(gameId));
}
