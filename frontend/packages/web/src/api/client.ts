import axios from 'axios';

const api = axios.create({
  baseURL: '/api',
});

api.interceptors.request.use((config) => {
  const stored = localStorage.getItem('mepbm-auth');
  if (stored) {
    const { state } = JSON.parse(stored);
    if (state?.token) {
      config.headers.Authorization = `Bearer ${state.token}`;
    }
  }
  return config;
});

api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      localStorage.removeItem('mepbm-auth');
      window.location.href = '/login';
    }
    return Promise.reject(error);
  }
);

export const orderLang = () => {
  try {
    return localStorage.getItem('mepbm-lang') === 'es' ? 'es' : 'en';
  } catch {
    return 'en';
  }
};

export const authApi = {
  login: (email: string, password: string) =>
    api.post('/auth/login', { email, password }),
  register: (username: string, email: string, password: string, preferredLanguage?: string) =>
    api.post('/auth/register', { username, email, password, preferredLanguage }),
  me: () => api.get('/auth/me'),
  updateLanguage: (language: string) =>
    api.patch('/auth/language', { language }),
};

export const usersApi = {
  list: () => api.get('/users'),
};

export const gamesApi = {
  list: () => api.get('/games'),
  get: (id: string) => api.get(`/games/${id}`),
  create: (data: { name: string; gameTypeCode?: string; playerEmails?: string[]; adminEmails?: string[] }) =>
    api.post('/games', data),
  delete: (gameId: string) => api.delete(`/games/${gameId}`),
  join: (gameId: string, nationId?: string) =>
    api.post(`/games/${gameId}/join`, { nationId }),
  getNations: (gameId: string) =>
    api.get(`/games/${gameId}/nations`),
  start: (gameId: string) =>
    api.post(`/games/${gameId}/start`),
  processTurn: (gameId: string) =>
    api.post(`/games/${gameId}/process-turn`),
  getTurnReport: (gameId: string, turnId: string) =>
    api.get(`/games/${gameId}/turns/${turnId}/report`, { params: { lang: orderLang() } }),
  getState: (gameId: string, nationId?: string) =>
    api.get(`/games/${gameId}/state`, { params: nationId ? { nationId } : undefined }),
  updateNation: (gameId: string, nationId: string) =>
    api.put(`/games/${gameId}/nation`, { nationId }),
  setRelation: (gameId: string, nationId: string, targetNationId: string, level: number) =>
    api.put(`/games/${gameId}/relations`, { nationId, targetNationId, level }),
  accept: (gameId: string, wantsToPlayWithUserId?: string) =>
    api.post(`/games/${gameId}/accept`, { wantsToPlayWithUserId }),
  getPlayers: (gameId: string) =>
    api.get(`/games/${gameId}/players`),
  getAdmins: (gameId: string) =>
    api.get(`/games/${gameId}/admins`),
  acceptAdmin: (gameId: string) =>
    api.post(`/games/${gameId}/accept-admin`),
  addPlayer: (gameId: string, email: string, isAdmin = false) =>
    api.post(`/games/${gameId}/players`, { email, isAdmin }),
  removePlayer: (gameId: string, playerId: string) =>
    api.delete(`/games/${gameId}/players/${playerId}`),
};

export const ordersApi = {
  list: (gameId: string) =>
    api.get(`/games/${gameId}/orders`, { params: { lang: orderLang() } }),
  submit: (gameId: string, data: { characterId: string; code: number; parameters?: Record<string, unknown>; armyId?: string }) =>
    api.post(`/games/${gameId}/orders?lang=${orderLang()}`, data),
  eligible: (gameId: string, characterId: string) =>
    api.get(`/games/${gameId}/orders/eligible`, { params: { characterId, lang: orderLang() } }),
  estimate: (gameId: string, data: { characterId: string; code: number; parameters?: Record<string, unknown>; armyId?: string; navyId?: string; afterOrder?: { code: number; parameters?: Record<string, unknown> } }) =>
    api.post(`/games/${gameId}/orders/estimate?lang=${orderLang()}`, data),
  cancel: (gameId: string, orderId: string) =>
    api.delete(`/games/${gameId}/orders/${orderId}?lang=${orderLang()}`),
  validate: (gameId: string) =>
    api.post(`/games/${gameId}/orders/validate?lang=${orderLang()}`),
};

export const messagesApi = {
  list: (gameId: string, page = 1) =>
    api.get(`/games/${gameId}/messages?page=${page}`),
  send: (gameId: string, subject: string, content: string) =>
    api.post(`/games/${gameId}/messages`, { subject, content }),
  markRead: (gameId: string, messageId: string) =>
    api.put(`/games/${gameId}/messages/${messageId}/read`),
};

export default api;
