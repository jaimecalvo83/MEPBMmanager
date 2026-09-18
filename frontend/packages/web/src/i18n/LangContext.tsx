import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { en, type DictKey } from './dict-en';
import { es } from './dict-es';
import { useAuthStore } from '../stores/authStore';
import { authApi } from '../api/client';

export type Lang = 'en' | 'es';

const dicts: Record<Lang, Record<DictKey, string>> = { en, es };

export type TFunc = (key: DictKey, vars?: Record<string, string | number>) => string;

const LangCtx = createContext<{ lang: Lang; setLang: (l: Lang) => void; t: TFunc }>({
  lang: 'en',
  setLang: () => {},
  t: (key) => en[key] ?? key,
});

export function LangProvider({ children }: { children: ReactNode }) {
  const { user } = useAuthStore();
  const [lang, setLangState] = useState<Lang>(() => {
    try {
      return (localStorage.getItem('mepbm-lang') as Lang) || 'en';
    } catch {
      return 'en';
    }
  });

  // Sync with auth store preferredLanguage on mount/login
  useEffect(() => {
    if (user?.preferredLanguage === 'en' || user?.preferredLanguage === 'es') {
      setLangState(user.preferredLanguage);
      try { localStorage.setItem('mepbm-lang', user.preferredLanguage); } catch {}
    }
  }, [user?.id]);

  const setLang = (l: Lang) => {
    setLangState(l);
    try { localStorage.setItem('mepbm-lang', l); } catch {}
    // Persist to DB if logged in
    if (user) {
      authApi.updateLanguage(l).catch(() => {});
      useAuthStore.getState().setAuth(
        useAuthStore.getState().token!,
        { ...user, preferredLanguage: l }
      );
    }
  };

  const t: TFunc = (key, vars) => {
    let s: string = dicts[lang][key] ?? en[key] ?? key;
    if (vars) {
      for (const k of Object.keys(vars)) {
        s = s.split(`{${k}}`).join(String(vars[k]));
      }
    }
    return s;
  };

  return <LangCtx.Provider value={{ lang, setLang, t }}>{children}</LangCtx.Provider>;
}

export function useLang() {
  return useContext(LangCtx);
}

export function LanguageSwitcher({ small }: { small?: boolean }) {
  const { lang, setLang } = useLang();
  const cls = (active: boolean) =>
    `${small ? 'px-2 py-0.5 text-xs' : 'px-2 py-1 text-sm'} rounded font-bold transition ${
      active ? 'bg-mepbm-gold text-gray-900' : 'bg-gray-700 text-gray-400 hover:text-white'
    }`;
  return (
    <span className="inline-flex gap-1">
      <button type="button" onClick={() => setLang('en')} className={cls(lang === 'en')}>EN</button>
      <button type="button" onClick={() => setLang('es')} className={cls(lang === 'es')}>ES</button>
    </span>
  );
}
