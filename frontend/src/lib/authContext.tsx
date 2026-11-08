'use client';

import React, { createContext, useContext, useEffect, useState } from 'react';
import { api, setAccessToken } from './api';
import { UserDto } from './types';

interface AuthContextType {
  user: UserDto | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  isAdmin: boolean;
  isEditor: boolean;
  canEdit: boolean;
  lang: 'ar' | 'en';
  setLang: (lang: 'ar' | 'en') => void;
  login: (username: string, password: string) => Promise<UserDto>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<UserDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [lang, setLangState] = useState<'ar' | 'en'>('ar');

  useEffect(() => {
    const savedLang = localStorage.getItem('acud_lang') as 'ar' | 'en';
    if (savedLang) {
      setLangState(savedLang);
      document.documentElement.dir = savedLang === 'ar' ? 'rtl' : 'ltr';
      document.documentElement.lang = savedLang;
    } else {
      document.documentElement.dir = 'rtl';
      document.documentElement.lang = 'ar';
    }

    // Attempt silent restore using HttpOnly refresh cookie
    api.refresh()
      .then((authData) => {
        setUser(authData.user);
      })
      .catch(() => {
        setAccessToken(null);
        setUser(null);
      })
      .finally(() => {
        setIsLoading(false);
      });
  }, []);

  const setLang = (newLang: 'ar' | 'en') => {
    setLangState(newLang);
    localStorage.setItem('acud_lang', newLang);
    document.documentElement.dir = newLang === 'ar' ? 'rtl' : 'ltr';
    document.documentElement.lang = newLang;
  };

  const login = async (username: string, password: string): Promise<UserDto> => {
    const data = await api.login(username, password);
    setUser(data.user);
    return data.user;
  };

  const logout = async () => {
    try {
      await api.logout();
    } finally {
      setUser(null);
      if (typeof window !== 'undefined') {
        window.location.href = '/login';
      }
    }
  };

  const role = user?.role;
  const isAdmin = role === 'ADMIN';
  const isEditor = role === 'EDITOR';
  const canEdit = isAdmin || isEditor;

  return (
    <AuthContext.Provider
      value={{
        user,
        isAuthenticated: !!user,
        isLoading,
        isAdmin,
        isEditor,
        canEdit,
        lang,
        setLang,
        login,
        logout,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = (): AuthContextType => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};

