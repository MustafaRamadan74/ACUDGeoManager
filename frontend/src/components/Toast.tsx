'use client';

import React, { createContext, useContext, useState, useCallback, useEffect } from 'react';
import { CheckCircle2, AlertTriangle, AlertCircle, Info, X } from 'lucide-react';

export type ToastType = 'success' | 'error' | 'conflict' | 'info';

export interface ToastItem {
  id: string;
  type: ToastType;
  title: string;
  message?: string;
  duration?: number;
}

interface ToastContextType {
  toasts: ToastItem[];
  showToast: (toast: Omit<ToastItem, 'id'>) => string;
  dismissToast: (id: string) => void;
  showSuccess: (title: string, message?: string) => void;
  showError: (title: string, message?: string) => void;
  showConflict: (message?: string) => void;
  showInfo: (title: string, message?: string) => void;
}

const ToastContext = createContext<ToastContextType | undefined>(undefined);

export const ToastProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [toasts, setToasts] = useState<ToastItem[]>([]);

  const dismissToast = useCallback((id: string) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  const showToast = useCallback(
    ({ type, title, message, duration = 5000 }: Omit<ToastItem, 'id'>) => {
      const id = Math.random().toString(36).substring(2, 9);
      const newToast: ToastItem = { id, type, title, message, duration };

      setToasts((prev) => [...prev, newToast]);

      if (duration > 0) {
        setTimeout(() => {
          dismissToast(id);
        }, duration);
      }

      return id;
    },
    [dismissToast]
  );

  const showSuccess = useCallback(
    (title: string, message?: string) => {
      showToast({ type: 'success', title, message, duration: 5000 });
    },
    [showToast]
  );

  const showError = useCallback(
    (title: string, message?: string) => {
      showToast({ type: 'error', title, message, duration: 5000 });
    },
    [showToast]
  );

  const showConflict = useCallback(
    (
      message = 'تعارض في إصدار المعلم (409): قام مستخدم آخر بتعديل أو حذف هذا المعلم أثناء عملك عليه. تم إلغاء العملية لحماية البيانات.'
    ) => {
      showToast({
        type: 'conflict',
        title: 'تعارض في التعديل / Concurrency Conflict (409)',
        message,
        duration: 5000,
      });
    },
    [showToast]
  );

  const showInfo = useCallback(
    (title: string, message?: string) => {
      showToast({ type: 'info', title, message, duration: 5000 });
    },
    [showToast]
  );

  return (
    <ToastContext.Provider
      value={{
        toasts,
        showToast,
        dismissToast,
        showSuccess,
        showError,
        showConflict,
        showInfo,
      }}
    >
      {children}
      {/* Toast Render Container */}
      <div className="fixed bottom-5 right-5 rtl:right-auto rtl:left-5 z-50 flex flex-col gap-2 max-w-md w-full pointer-events-none p-4">
        {toasts.map((toast) => {
          const isSuccess = toast.type === 'success';
          const isError = toast.type === 'error';
          const isConflict = toast.type === 'conflict';
          const isInfo = toast.type === 'info';

          return (
            <div
              key={toast.id}
              className={`pointer-events-auto rounded-xl p-4 shadow-2xl border backdrop-blur-md transition-all duration-300 transform translate-y-0 flex items-start gap-3 text-sm animate-in fade-in slide-in-from-bottom-2 ${
                isSuccess
                  ? 'bg-slate-900/95 border-emerald-500/50 text-emerald-100 shadow-emerald-950/40'
                  : isConflict
                  ? 'bg-amber-950/95 border-amber-500/60 text-amber-100 shadow-amber-950/50 ring-1 ring-amber-500/30'
                  : isError
                  ? 'bg-rose-950/95 border-rose-500/60 text-rose-100 shadow-rose-950/50'
                  : 'bg-slate-900/95 border-sky-500/50 text-sky-100 shadow-sky-950/40'
              }`}
            >
              <div className="flex-shrink-0 mt-0.5">
                {isSuccess && <CheckCircle2 className="w-5 h-5 text-emerald-400" />}
                {isConflict && <AlertTriangle className="w-5 h-5 text-amber-400 animate-pulse" />}
                {isError && <AlertCircle className="w-5 h-5 text-rose-400" />}
                {isInfo && <Info className="w-5 h-5 text-sky-400" />}
              </div>

              <div className="flex-1 space-y-1">
                <div className="font-bold text-white text-xs tracking-wide">
                  {toast.title}
                </div>
                {toast.message && (
                  <div className="text-xs text-slate-300 leading-relaxed font-sans">
                    {toast.message}
                  </div>
                )}
                {/* 5s duration indicator bar */}
                <div className="h-0.5 w-full bg-white/10 rounded overflow-hidden mt-1.5">
                  <div
                    className={`h-full animate-[shrink_5s_linear_forwards] ${
                      isSuccess
                        ? 'bg-emerald-400'
                        : isConflict
                        ? 'bg-amber-400'
                        : isError
                        ? 'bg-rose-400'
                        : 'bg-sky-400'
                    }`}
                    style={{ animationDuration: `${toast.duration || 5000}ms` }}
                  />
                </div>
              </div>

              <button
                onClick={() => dismissToast(toast.id)}
                className="flex-shrink-0 text-slate-400 hover:text-white transition p-1"
                aria-label="Dismiss toast"
              >
                <X className="w-4 h-4" />
              </button>
            </div>
          );
        })}
      </div>
    </ToastContext.Provider>
  );
};

export const useToast = () => {
  const context = useContext(ToastContext);
  if (!context) {
    throw new Error('useToast must be used within a ToastProvider');
  }
  return context;
};
