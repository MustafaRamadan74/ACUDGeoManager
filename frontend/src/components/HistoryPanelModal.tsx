'use client';

import React, { useState, useEffect } from 'react';
import { api } from '@/lib/api';
import { useAuth } from '@/lib/authContext';
import {
  UndoRedoStackDto,
  ChangeSetSummaryDto,
  ConflictingFeatureDto,
} from '@/lib/types';
import {
  History,
  Undo2,
  Redo2,
  X,
  AlertTriangle,
  Clock,
  Layers,
  RefreshCw,
  CheckCircle2,
  RotateCcw,
} from 'lucide-react';

interface HistoryPanelModalProps {
  isOpen: boolean;
  onClose: () => void;
  onStackChanged?: () => void;
}

export const HistoryPanelModal: React.FC<HistoryPanelModalProps> = ({
  isOpen,
  onClose,
  onStackChanged,
}) => {
  const { lang } = useAuth();
  const isAr = lang === 'ar';

  const [stack, setStack] = useState<UndoRedoStackDto | null>(null);
  const [activeTab, setActiveTab] = useState<'undo' | 'redo'>('undo');
  const [isLoading, setIsLoading] = useState(false);
  const [isActing, setIsActing] = useState(false);
  const [conflicts, setConflicts] = useState<ConflictingFeatureDto[] | null>(null);
  const [statusMessage, setStatusMessage] = useState<string | null>(null);

  const fetchStack = async () => {
    setIsLoading(true);
    setConflicts(null);
    try {
      const data = await api.getHistoryStack();
      setStack(data);
    } catch (err: any) {
      console.error('Failed to load history stack', err);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    if (isOpen) {
      fetchStack();
    }
  }, [isOpen]);

  const handleUndo = async () => {
    setIsActing(true);
    setConflicts(null);
    setStatusMessage(null);
    try {
      const res = await api.undoLatest();
      setStatusMessage(
        isAr
          ? `تم التراجع بنجاح عن "${res.description}" (${res.revertedActionsCount} عملية)`
          : `Successfully undid "${res.description}" (${res.revertedActionsCount} actions)`
      );
      await fetchStack();
      onStackChanged?.();
    } catch (err: any) {
      if (err?.status === 409 && err?.data?.conflicts) {
        setConflicts(err.data.conflicts);
      } else {
        setStatusMessage(err?.message || (isAr ? 'فشل التراجع' : 'Undo failed'));
      }
    } finally {
      setIsActing(false);
    }
  };

  const handleRedo = async () => {
    setIsActing(true);
    setConflicts(null);
    setStatusMessage(null);
    try {
      const res = await api.redoLatest();
      setStatusMessage(
        isAr
          ? `تمت الإعادة بنجاح لـ "${res.description}" (${res.reappliedActionsCount} عملية)`
          : `Successfully redid "${res.description}" (${res.reappliedActionsCount} actions)`
      );
      await fetchStack();
      onStackChanged?.();
    } catch (err: any) {
      if (err?.status === 409 && err?.data?.conflicts) {
        setConflicts(err.data.conflicts);
      } else {
        setStatusMessage(err?.message || (isAr ? 'فشلت الإعادة' : 'Redo failed'));
      }
    } finally {
      setIsActing(false);
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4">
      <div className="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-2xl max-h-[85vh] flex flex-col shadow-2xl overflow-hidden text-slate-100">
        {/* Header */}
        <div className="px-6 py-4 border-b border-slate-800 flex items-center justify-between bg-slate-950/60">
          <div className="flex items-center gap-3">
            <div className="w-9 h-9 rounded-xl bg-sky-500/10 border border-sky-500/20 flex items-center justify-center text-sky-400">
              <History className="w-5 h-5" />
            </div>
            <div>
              <h2 className="text-base font-bold text-white flex items-center gap-2">
                {isAr ? 'مكدس التراجع وسجل التعديلات' : 'Undo / Redo History Stack'}
              </h2>
              <p className="text-xs text-slate-400">
                {isAr
                  ? 'إدارة التعديلات والتراجع والإعادة الخاصة بالمستخدم'
                  : 'Manage your applied and undone change sets'}
              </p>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <button
              onClick={fetchStack}
              disabled={isLoading}
              className="p-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 transition"
              title={isAr ? 'تحديث المكدس' : 'Refresh stack'}
            >
              <RefreshCw className={`w-4 h-4 ${isLoading ? 'animate-spin' : ''}`} />
            </button>
            <button
              onClick={onClose}
              className="p-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-400 hover:text-white transition"
            >
              <X className="w-4 h-4" />
            </button>
          </div>
        </div>

        {/* Global Action Toolbar */}
        <div className="px-6 py-3 bg-slate-900/80 border-b border-slate-800 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <button
              onClick={handleUndo}
              disabled={!stack?.canUndo || isActing}
              className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-semibold transition border ${
                stack?.canUndo && !isActing
                  ? 'bg-amber-600 hover:bg-amber-500 text-white border-amber-500 shadow-sm'
                  : 'bg-slate-800/60 text-slate-500 border-slate-700/50 cursor-not-allowed'
              }`}
              title="Ctrl+Z"
            >
              <Undo2 className="w-3.5 h-3.5" />
              <span>{isAr ? 'تراجع عن آخر تعديل' : 'Undo Latest'}</span>
              <kbd className="px-1.5 py-0.5 text-[10px] bg-slate-950/40 rounded border border-white/20">
                Ctrl+Z
              </kbd>
            </button>

            <button
              onClick={handleRedo}
              disabled={!stack?.canRedo || isActing}
              className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-semibold transition border ${
                stack?.canRedo && !isActing
                  ? 'bg-sky-600 hover:bg-sky-500 text-white border-sky-500 shadow-sm'
                  : 'bg-slate-800/60 text-slate-500 border-slate-700/50 cursor-not-allowed'
              }`}
              title="Ctrl+Y"
            >
              <Redo2 className="w-3.5 h-3.5" />
              <span>{isAr ? 'إعادة آخر تعديل' : 'Redo Latest'}</span>
              <kbd className="px-1.5 py-0.5 text-[10px] bg-slate-950/40 rounded border border-white/20">
                Ctrl+Y
              </kbd>
            </button>
          </div>

          {/* Tab selector */}
          <div className="flex items-center bg-slate-950/80 rounded-xl p-1 border border-slate-800 text-xs">
            <button
              onClick={() => setActiveTab('undo')}
              className={`px-3 py-1 rounded-lg font-medium transition flex items-center gap-1.5 ${
                activeTab === 'undo'
                  ? 'bg-amber-500/20 text-amber-300 font-semibold shadow-sm'
                  : 'text-slate-400 hover:text-slate-200'
              }`}
            >
              <RotateCcw className="w-3 h-3" />
              <span>{isAr ? 'مكدس التراجع' : 'Undo Stack'}</span>
              <span className="px-1.5 py-0.2 rounded-full text-[10px] bg-slate-800 text-slate-300">
                {stack?.undoStack.length ?? 0}
              </span>
            </button>

            <button
              onClick={() => setActiveTab('redo')}
              className={`px-3 py-1 rounded-lg font-medium transition flex items-center gap-1.5 ${
                activeTab === 'redo'
                  ? 'bg-sky-500/20 text-sky-300 font-semibold shadow-sm'
                  : 'text-slate-400 hover:text-slate-200'
              }`}
            >
              <Redo2 className="w-3 h-3" />
              <span>{isAr ? 'مكدس الإعادة' : 'Redo Stack'}</span>
              <span className="px-1.5 py-0.2 rounded-full text-[10px] bg-slate-800 text-slate-300">
                {stack?.redoStack.length ?? 0}
              </span>
            </button>
          </div>
        </div>

        {/* Status Message / Notification */}
        {statusMessage && (
          <div className="px-6 py-2.5 bg-emerald-950/40 border-b border-emerald-900/60 text-emerald-300 text-xs flex items-center gap-2">
            <CheckCircle2 className="w-4 h-4 text-emerald-400 flex-shrink-0" />
            <span>{statusMessage}</span>
          </div>
        )}

        {/* Conflict Warning Alert */}
        {conflicts && conflicts.length > 0 && (
          <div className="mx-6 my-3 p-4 bg-rose-950/40 border border-rose-800/80 rounded-xl text-xs text-rose-200 flex flex-col gap-2">
            <div className="flex items-center gap-2 font-semibold text-rose-400">
              <AlertTriangle className="w-4 h-4 flex-shrink-0" />
              <span>
                {isAr
                  ? 'تعذر التراجع/الإعادة: تم رصد تعارض في المعالم التالية'
                  : 'Conflict Detected: Cannot Undo/Redo the requested change set'}
              </span>
            </div>
            <div className="space-y-1.5 pl-6 rtl:pl-0 rtl:pr-6">
              {conflicts.map((c, i) => (
                <div
                  key={i}
                  className="bg-slate-900/80 p-2 rounded-lg border border-rose-900/40 flex items-center justify-between text-[11px]"
                >
                  <div className="flex items-center gap-2">
                    <span className="font-mono text-slate-400">[{c.tableName}]</span>
                    <span className="font-semibold text-white">{c.featureName}</span>
                    <span className="text-slate-400">({c.reason})</span>
                  </div>
                  <div className="text-slate-400 font-mono text-[10px]">
                    {isAr ? 'النسخة المتوقعة:' : 'Exp:'} {c.expectedVersion} |{' '}
                    {isAr ? 'الحالية:' : 'Cur:'} {c.currentVersion ?? 'deleted'}
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Content list */}
        <div className="p-6 overflow-y-auto flex-1 space-y-3">
          {isLoading ? (
            <div className="py-12 flex flex-col items-center justify-center text-slate-400 gap-2">
              <RefreshCw className="w-6 h-6 animate-spin text-sky-400" />
              <span className="text-xs">{isAr ? 'جاري تحميل المكدس...' : 'Loading history stack...'}</span>
            </div>
          ) : activeTab === 'undo' ? (
            stack?.undoStack.length === 0 ? (
              <div className="py-12 text-center text-slate-400 text-xs">
                {isAr ? 'لا توجد تعديلات يمكن التراجع عنها حالياً' : 'No applied change sets to undo.'}
              </div>
            ) : (
              stack?.undoStack.map((item, idx) => (
                <ChangeSetCard
                  key={item.id}
                  item={item}
                  isTop={idx === 0}
                  isAr={isAr}
                  actionLabel={isAr ? 'أحدث تعديل' : 'Latest Edit'}
                />
              ))
            )
          ) : stack?.redoStack.length === 0 ? (
            <div className="py-12 text-center text-slate-400 text-xs">
              {isAr ? 'لا توجد تعديلات يمكن إعادتها حالياً' : 'No undone change sets to redo.'}
            </div>
          ) : (
            stack?.redoStack.map((item, idx) => (
              <ChangeSetCard
                key={item.id}
                item={item}
                isTop={idx === 0}
                isAr={isAr}
                actionLabel={isAr ? 'أحدث تراجع' : 'Latest Undone'}
              />
            ))
          )}
        </div>

        {/* Footer */}
        <div className="px-6 py-3 border-t border-slate-800 bg-slate-950/60 flex items-center justify-between text-xs text-slate-400">
          <div>
            {isAr
              ? 'ملاحظة: تنفيذ أي تعديل جديد يمسح مكدس الإعادة تلقائياً.'
              : 'Note: Any new edit automatically clears your redo stack.'}
          </div>
          <button
            onClick={onClose}
            className="px-4 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-white font-medium transition"
          >
            {isAr ? 'إغلاق' : 'Close'}
          </button>
        </div>
      </div>
    </div>
  );
};

interface ChangeSetCardProps {
  item: ChangeSetSummaryDto;
  isTop: boolean;
  isAr: boolean;
  actionLabel: string;
}

const ChangeSetCard: React.FC<ChangeSetCardProps> = ({ item, isTop, isAr, actionLabel }) => {
  return (
    <div
      className={`p-3.5 rounded-xl border transition flex flex-col gap-2 ${
        isTop
          ? 'bg-slate-800/80 border-sky-500/50 shadow-md ring-1 ring-sky-500/20'
          : 'bg-slate-800/40 border-slate-800 hover:border-slate-700'
      }`}
    >
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          {isTop && (
            <span className="px-2 py-0.5 rounded-full text-[10px] font-bold bg-sky-500/20 text-sky-300 border border-sky-500/30">
              {actionLabel}
            </span>
          )}
          <span className="font-semibold text-xs text-white">
            {item.description || (isAr ? 'تعديل شبكة' : 'Network Edit')}
          </span>
        </div>

        <div className="flex items-center gap-1.5 text-[11px] text-slate-400">
          <Clock className="w-3.5 h-3.5" />
          <span>{new Date(item.undoneAt || item.createdAt).toLocaleTimeString()}</span>
        </div>
      </div>

      {/* Affected Features list */}
      {item.affectedFeatures && item.affectedFeatures.length > 0 && (
        <div className="flex flex-wrap gap-1.5 items-center pt-1">
          <span className="text-[10px] text-slate-400 flex items-center gap-1">
            <Layers className="w-3 h-3 text-slate-500" />
            {item.actionsCount} {isAr ? 'إجراءات:' : 'actions:'}
          </span>
          {item.affectedFeatures.map((feat, i) => (
            <span
              key={i}
              className="px-2 py-0.5 rounded text-[10px] font-mono bg-slate-900 border border-slate-700 text-slate-300"
            >
              {feat}
            </span>
          ))}
        </div>
      )}
    </div>
  );
};
