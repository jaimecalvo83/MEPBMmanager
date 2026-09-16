import { useQuery, useMutation, useQueryClient } from 'react-query';
import { ordersApi } from '../api/client';
import { useLang } from '../i18n/lang';
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

export interface EligibleOrder {
  code: number;
  ok: boolean;
  reason: string;
}

export function useEligibleOrders(gameId: string, characterId: string | null) {
  const { lang } = useLang();
  return useQuery<EligibleOrder[]>(
    ['eligible-orders', gameId, characterId, lang],
    async () => {
      const { data } = await ordersApi.eligible(gameId, characterId!);
      return data.eligible;
    },
    { enabled: !!gameId && !!characterId, retry: false }
  );
}

export interface OrderFieldOption {
  value: string;
  label: string;
}

export interface OrderFieldSpec {
  key: string;
  label: string;
  kind: string;
  required: boolean;
  min?: number | null;
  max?: number | null;
  def?: string | null;
  options?: OrderFieldOption[] | null;
}

export interface OrderEstimate {
  ok: boolean;
  errors: string[];
  warnings: string[];
  costs: Record<string, number>;
  maxAmount: number | null;
  expectedGold: number | null;
  requires: OrderFieldSpec[];
  effectiveLocation: string | null;
  movedByFirstOrder: boolean;
  suggestNames: string[] | null;
}

export function useOrderEstimate(
  gameId: string,
  characterId: string | null,
  code: number,
  paramsKey: string,
  afterKey: string,
  buildPayload: () => { parameters: Record<string, unknown>; armyId?: string; afterOrder?: { code: number; parameters?: Record<string, unknown> } } | null,
  ready = true
) {
  const { lang } = useLang();
  return useQuery<OrderEstimate>(
    ['order-estimate', gameId, characterId, code, paramsKey, afterKey, lang],
    async () => {
      const payload = buildPayload();
      if (!payload) throw new Error('No payload');
      const { data } = await ordersApi.estimate(gameId, {
        characterId: characterId!,
        code,
        ...payload,
      });
      return data;
    },
    { enabled: !!gameId && !!characterId && code > 0 && !!paramsKey && ready, retry: false, staleTime: 30000 }
  );
}
