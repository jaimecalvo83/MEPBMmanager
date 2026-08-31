import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuthStore } from '../../stores/authStore';
import { authApi } from '../../api/client';
import { useMutation } from 'react-query';

export default function Login() {
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
        setError(err.response?.data?.error || 'Login failed');
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
        <p className="text-center text-gray-400 text-sm">Middle-earth Play By Mail</p>
        <input
          type="email"
          placeholder="Email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          className="w-full p-3 bg-gray-700 rounded border border-gray-600 focus:border-mepbm-gold focus:outline-none"
          required
        />
        <input
          type="password"
          placeholder="Password"
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
          {loginMutation.isLoading ? 'Logging in...' : 'Login'}
        </button>
        <p className="text-center text-gray-400 text-sm">
          Don't have an account? <a href="/register" className="text-mepbm-gold hover:underline">Register</a>
        </p>
      </form>
    </div>
  );
}
