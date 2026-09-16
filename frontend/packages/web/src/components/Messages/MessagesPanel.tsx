import { useState } from 'react';
import { useMessages, useSendMessage, useMarkRead } from '../../hooks/useMessages';
import { useLang } from '../../i18n/lang';

interface MessagesPanelProps {
  gameId: string;
}

export default function MessagesPanel({ gameId }: MessagesPanelProps) {
  const { t } = useLang();
  const { data: messages, isLoading } = useMessages(gameId);
  const sendMessage = useSendMessage(gameId);
  const markRead = useMarkRead(gameId);
  const [showCompose, setShowCompose] = useState(false);
  const [subject, setSubject] = useState('');
  const [content, setContent] = useState('');

  const handleSend = async () => {
    if (!subject.trim() || !content.trim()) return;
    await sendMessage.mutateAsync({ subject, content });
    setSubject('');
    setContent('');
    setShowCompose(false);
  };

  const handleMarkRead = async (messageId: string) => {
    await markRead.mutateAsync(messageId);
  };

  if (isLoading) {
    return <div className="text-gray-400 p-4">{t('msg.loading')}</div>;
  }

  return (
    <div className="bg-gray-800 rounded-lg border border-gray-700 p-4">
      <div className="flex justify-between items-center mb-4">
        <h2 className="text-lg font-bold text-mepbm-gold">{t('msg.title')}</h2>
        <button
          onClick={() => setShowCompose(!showCompose)}
          className="px-3 py-1 bg-mepbm-gold text-gray-900 text-sm font-bold rounded hover:bg-yellow-400 transition"
        >
          {t('msg.compose')}
        </button>
      </div>

      {showCompose && (
        <div className="mb-4 bg-gray-700 p-4 rounded-lg space-y-3">
          <input
            type="text"
            placeholder={t('msg.subject')}
            value={subject}
            onChange={(e) => setSubject(e.target.value)}
            className="w-full p-2 bg-gray-600 rounded border border-gray-500"
          />
          <textarea
            placeholder={t('msg.contentPh')}
            value={content}
            onChange={(e) => setContent(e.target.value)}
            rows={4}
            className="w-full p-2 bg-gray-600 rounded border border-gray-500 resize-none"
          />
          <div className="flex gap-2">
            <button
              onClick={handleSend}
              disabled={sendMessage.isLoading || !subject.trim() || !content.trim()}
              className="px-4 py-2 bg-green-600 text-white text-sm rounded hover:bg-green-500 transition disabled:opacity-50"
            >
              {sendMessage.isLoading ? t('msg.sending') : t('msg.send')}
            </button>
            <button
              onClick={() => setShowCompose(false)}
              className="px-4 py-2 bg-gray-600 text-gray-300 text-sm rounded hover:bg-gray-500 transition"
            >
              {t('common.cancel')}
            </button>
          </div>
        </div>
      )}

      <div className="space-y-2 max-h-96 overflow-y-auto">
        {(!messages || messages.length === 0) ? (
          <p className="text-gray-500 text-sm text-center py-4">{t('msg.none')}</p>
        ) : (
          messages.map((message) => (
            <div
              key={message.id}
              className={`p-3 rounded-lg border cursor-pointer transition ${
                message.isRead
                  ? 'bg-gray-750 border-gray-700'
                  : 'bg-gray-700 border-mepbm-gold'
              }`}
              onClick={() => !message.isRead && handleMarkRead(message.id)}
            >
              <div className="flex justify-between items-start">
                <div>
                  <span
                    className="text-sm font-medium"
                    style={{ color: message.sender.color }}
                  >
                    {message.sender.name}
                  </span>
                  <h4 className="text-white font-medium">{message.subject}</h4>
                </div>
                <span className="text-xs text-gray-500">
                  {new Date(message.createdAt).toLocaleDateString()}
                </span>
              </div>
              <p className="text-gray-400 text-sm mt-1 line-clamp-2">{message.content}</p>
              {!message.isRead && (
                <div className="w-2 h-2 bg-mepbm-gold rounded-full mt-2" />
              )}
            </div>
          ))
        )}
      </div>
    </div>
  );
}
