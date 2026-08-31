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

export const authApi = {
  login: (email: string, password: string) =>
    api.post('/auth/login', { email, password }),
  register: (username: string, email: string, password: string) =>
    api.post('/auth/register', { username, email, password }),
  me: () => api.get('/auth/me'),
};

export const gamesApi = {
  list: () => api.get('/games'),
  get: (id: string) => api.get(`/games/${id}`),
  create: (data: { name: string; gameTypeCode?: string }) =>
    api.post('/games', data),
  join: (gameId: string, nationId?: string) =>
    api.post(`/games/${gameId}/join`, { nationId }),
  getNations: (gameId: string) =>
    api.get(`/games/${gameId}/nations`),
  start: (gameId: string) =>
    api.post(`/games/${gameId}/start`),
  processTurn: (gameId: string) =>
    api.post(`/games/${gameId}/process-turn`),
  getState: (gameId: string) =>
    api.get(`/games/${gameId}/state`),
};

export const ordersApi = {
  list: (gameId: string) =>
    api.get(`/games/${gameId}/orders`),
  submit: (gameId: string, data: { characterId: string; code: number; parameters?: Record<string, unknown>; armyId?: string }) =>
    api.post(`/games/${gameId}/orders`, data),
  cancel: (gameId: string, orderId: string) =>
    api.delete(`/games/${gameId}/orders/${orderId}`),
  validate: (gameId: string) =>
    api.post(`/games/${gameId}/orders/validate`),
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
