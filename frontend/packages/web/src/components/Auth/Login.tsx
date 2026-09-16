import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuthStore } from '../../stores/authStore';
import { authApi } from '../../api/client';
import { useMutation } from 'react-query';
import { useLang } from '../../i18n/lang';

export default function Login() {
  const { t } = useLang();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const { setAuth } = useAuthStore();
  const navigate = useNavigate();

  const loginMutation = useMutation(
    () => authApi.login(email, password),
    {
      onSuccess: ({ data }) => {
        setAuth(data.token, data.user);
        navigate('/');
      },
      onError: (err: any) => {
        setError(err.response?.data?.error || t('auth.loginFailed'));
      },
    }
  );

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    loginMutation.mutate();
  };

  return (
    <div className="min-h-screen flex items-center justify-center">
      <form onSubmit={handleSubmit} className="bg-gray-800 p-8 rounded-lg shadow-xl w-96 space-y-4">
        <h1 className="text-2xl font-bold text-center text-mepbm-gold">MEPBMmanager</h1>
        <p className="text-center text-gray-400 text-sm">{t('auth.subtitle')}</p>
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
          placeholder={t('auth.password')}
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          className="w-full p-3 bg-gray-700 rounded border border-gray-600 focus:border-mepbm-gold focus:outline-none"
          required
        />
        {error && <p className="text-red-400 text-sm">{error}</p>}
        <button
          type="submit"
          disabled={loginMutation.isLoading}
          className="w-full p-3 bg-mepbm-gold text-gray-900 font-bold rounded hover:bg-yellow-400 transition disabled:opacity-50"
        >
          {loginMutation.isLoading ? t('auth.loggingIn') : t('auth.login')}
        </button>
        <p className="text-center text-gray-400 text-sm">
          {t('auth.noAccount')} <a href="/register" className="text-mepbm-gold hover:underline">{t('auth.register')}</a>
        </p>
      </form>
    </div>
  );
}
