import { useQuery, useMutation, useQueryClient } from 'react-query';
import { messagesApi } from '../api/client';
import type { MessageItem } from '../types';

export function useMessages(gameId: string) {
  return useQuery<MessageItem[]>(['messages', gameId], async () => {
    const { data } = await messagesApi.list(gameId);
    return data.messages;
  });
}

export function useSendMessage(gameId: string) {
  const queryClient = useQueryClient();
  return useMutation(
    ({ subject, content }: { subject: string; content: string }) =>
      messagesApi.send(gameId, subject, content),
    { onSuccess: () => queryClient.invalidateQueries(['messages', gameId]) }
  );
}

export function useMarkRead(gameId: string) {
  const queryClient = useQueryClient();
  return useMutation(
    (messageId: string) => messagesApi.markRead(gameId, messageId),
    { onSuccess: () => queryClient.invalidateQueries(['messages', gameId]) }
  );
}
