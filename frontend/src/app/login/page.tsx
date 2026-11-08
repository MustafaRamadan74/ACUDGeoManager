'use client';

import React, { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useAuth } from '@/lib/authContext';
import { Shield, KeyRound, User, AlertCircle, Loader2, Globe } from 'lucide-react';

export default function LoginPage() {
  const router = useRouter();
  const { login, lang, setLang } = useAuth();

  const [username, setUsername] = useState('admin');
  const [password, setPassword] = useState('Admin@Passw0rd123!');
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const isAr = lang === 'ar';

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setIsLoading(true);

    try {
      await login(username, password);
      router.push('/');
    } catch (err: any) {
      setError(err?.message || (isAr ? 'فشل تسجيل الدخول. تحقق من اسم المستخدم وكلمة المرور.' : 'Login failed. Please check credentials.'));
    } finally {
      setIsLoading(false);
    }
  };

  const setPresetUser = (user: string, pass: string) => {
    setUsername(user);
    setPassword(pass);
    setError(null);
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-900 bg-[radial-gradient(ellipse_at_top,_var(--tw-gradient-stops))] from-sky-900 via-slate-900 to-black px-4">
      {/* Language Switcher in corner */}
      <div className="absolute top-6 left-6 rtl:left-auto rtl:right-6">
        <button
          onClick={() => setLang(isAr ? 'en' : 'ar')}
          className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-slate-800/80 hover:bg-slate-700 text-slate-300 text-sm border border-slate-700 transition"
        >
          <Globe className="w-4 h-4 text-sky-400" />
          <span>{isAr ? 'English' : 'العربية'}</span>
        </button>
      </div>

      <div className="w-full max-w-md bg-slate-800/90 backdrop-blur-md rounded-2xl shadow-2xl border border-slate-700/80 p-8">
        {/* Header Branding */}
        <div className="text-center mb-8">
          <div className="inline-flex items-center justify-center w-16 h-16 rounded-2xl bg-gradient-to-tr from-sky-600 to-amber-500 mb-4 shadow-lg shadow-sky-500/20">
            <Shield className="w-8 h-8 text-white" />
          </div>
          <h1 className="text-2xl font-bold tracking-tight text-white">
            {isAr ? 'العاصمة الإدارية للتنمية العمرانية' : 'New Administrative Capital'}
          </h1>
          <p className="text-sm font-medium text-amber-400 mt-1">
            ACUDGeoManager &bull; {isAr ? 'منصة إدارة شبكات المرافق الجغرافية' : 'Multi-Utility GIS Platform'}
          </p>
        </div>

        {/* Error message */}
        {error && (
          <div className="mb-6 p-4 rounded-xl bg-red-950/60 border border-red-800/80 text-red-200 text-sm flex items-start gap-3">
            <AlertCircle className="w-5 h-5 text-red-400 flex-shrink-0 mt-0.5" />
            <div className="flex-1 leading-relaxed">{error}</div>
          </div>
        )}

        {/* Form */}
        <form onSubmit={handleSubmit} className="space-y-5">
          <div>
            <label className="block text-sm font-medium text-slate-300 mb-1.5">
              {isAr ? 'اسم المستخدم' : 'Username'}
            </label>
            <div className="relative">
              <div className="absolute inset-y-0 left-0 rtl:left-auto rtl:right-0 pl-3.5 rtl:pl-0 rtl:pr-3.5 flex items-center pointer-events-none text-slate-400">
                <User className="w-5 h-5" />
              </div>
              <input
                type="text"
                required
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                className="w-full pl-10 rtl:pl-4 rtl:pr-10 pr-4 py-2.5 bg-slate-900/80 border border-slate-700 rounded-xl text-white placeholder-slate-500 focus:outline-none focus:ring-2 focus:ring-sky-500 focus:border-transparent transition"
                placeholder={isAr ? 'أدخل اسم المستخدم' : 'Enter your username'}
              />
            </div>
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-300 mb-1.5">
              {isAr ? 'كلمة المرور' : 'Password'}
            </label>
            <div className="relative">
              <div className="absolute inset-y-0 left-0 rtl:left-auto rtl:right-0 pl-3.5 rtl:pl-0 rtl:pr-3.5 flex items-center pointer-events-none text-slate-400">
                <KeyRound className="w-5 h-5" />
              </div>
              <input
                type="password"
                required
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                className="w-full pl-10 rtl:pl-4 rtl:pr-10 pr-4 py-2.5 bg-slate-900/80 border border-slate-700 rounded-xl text-white placeholder-slate-500 focus:outline-none focus:ring-2 focus:ring-sky-500 focus:border-transparent transition"
                placeholder="••••••••••••"
              />
            </div>
          </div>

          <button
            type="submit"
            disabled={isLoading}
            className="w-full py-3 px-4 rounded-xl font-medium text-white bg-gradient-to-r from-sky-600 hover:from-sky-500 to-sky-700 active:scale-[0.99] shadow-lg shadow-sky-600/30 transition disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2 mt-6"
          >
            {isLoading ? (
              <>
                <Loader2 className="w-5 h-5 animate-spin" />
                <span>{isAr ? 'جارٍ تسجيل الدخول...' : 'Signing in...'}</span>
              </>
            ) : (
              <span>{isAr ? 'تسجيل الدخول' : 'Sign In'}</span>
            )}
          </button>
        </form>

        {/* Demo Users Presets */}
        <div className="mt-8 pt-6 border-t border-slate-700/60">
          <p className="text-xs font-semibold text-slate-400 uppercase tracking-wider mb-3 text-center">
            {isAr ? 'حسابات الاختبار التجريبية (جاهزة للنقر)' : 'Demo Role Accounts'}
          </p>
          <div className="grid grid-cols-3 gap-2">
            <button
              type="button"
              onClick={() => setPresetUser('admin', 'Admin@Passw0rd123!')}
              className="px-2.5 py-1.5 rounded-lg bg-sky-950/60 hover:bg-sky-900/80 text-sky-300 text-xs font-medium border border-sky-800/60 transition text-center"
            >
              👑 Admin
            </button>
            <button
              type="button"
              onClick={() => setPresetUser('editor', 'Editor@Passw0rd123!')}
              className="px-2.5 py-1.5 rounded-lg bg-emerald-950/60 hover:bg-emerald-900/80 text-emerald-300 text-xs font-medium border border-emerald-800/60 transition text-center"
            >
              ✏️ Editor
            </button>
            <button
              type="button"
              onClick={() => setPresetUser('viewer', 'Viewer@Passw0rd123!')}
              className="px-2.5 py-1.5 rounded-lg bg-purple-950/60 hover:bg-purple-900/80 text-purple-300 text-xs font-medium border border-purple-800/60 transition text-center"
            >
              👁️ Viewer
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

