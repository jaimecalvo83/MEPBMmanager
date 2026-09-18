import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuthStore } from '../../stores/authStore';
import { authApi } from '../../api/client';
import { useLang } from '../../i18n/lang';

export default function Register() {
  const { t, lang } = useLang();
  const [username, setUsername] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const { setAuth } = useAuthStore();
  const navigate = useNavigate();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      const { data } = await authApi.register(username, email, password, lang);
      setAuth(data.token, data.user);
      navigate('/');
    } catch (err: unknown) {
      const axiosError = err as { response?: { data?: { error?: string } } };
      setError(axiosError.response?.data?.error || t('auth.regFailed'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center">
      <form onSubmit={handleSubmit} className="bg-gray-800 p-8 rounded-lg shadow-xl w-96 space-y-4">
        <h1 className="text-2xl font-bold text-center text-mepbm-gold">{t('auth.createAccount')}</h1>
        <input
          type="text"
          placeholder={t('auth.username')}
          value={username}
          onChange={(e) => setUsername(e.target.value)}
          className="w-full p-3 bg-gray-700 rounded border border-gray-600 focus:border-mepbm-gold focus:outline-none"
          required
        />
        <input
          type="email"
          placeholder={t('auth.email')}
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          className="w-full p-3 bg-gray-700 rounded border border-gray-600 focus:border-mepbm-gold focus:outline-none"
          required
        />
        <input
          type="password"
          placeholder={t('auth.passwordMin')}
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          minLength={6}
          className="w-full p-3 bg-gray-700 rounded border border-gray-600 focus:border-mepbm-gold focus:outline-none"
          required
        />
        {error && <p className="text-red-400 text-sm">{error}</p>}
        <button
          type="submit"
          disabled={loading}
          className="w-full p-3 bg-mepbm-gold text-gray-900 font-bold rounded hover:bg-yellow-400 transition disabled:opacity-50"
        >
          {loading ? t('auth.creating') : t('auth.register')}
        </button>
        <p className="text-center text-gray-400 text-sm">
          {t('auth.haveAccount')} <a href="/login" className="text-mepbm-gold hover:underline">{t('auth.login')}</a>
        </p>
      </form>
    </div>
  );
}
