'use client';

import React, { useEffect, useState } from 'react';
import { AuditLogEntry, GeoJsonFeature } from '@/lib/types';
import { api } from '@/lib/api';
import { useAuth } from '@/lib/authContext';
import { History, X, Loader2, User, Clock, ChevronDown, ChevronRight, FileText } from 'lucide-react';

interface FeatureHistoryModalProps {
  feature: GeoJsonFeature | null;
  onClose: () => void;
}

export const FeatureHistoryModal: React.FC<FeatureHistoryModalProps> = ({
  feature,
  onClose,
}) => {
  const { lang } = useAuth();
  const isAr = lang === 'ar';
  const [loading, setLoading] = useState(true);
  const [entries, setEntries] = useState<AuditLogEntry[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [expandedId, setExpandedId] = useState<number | null>(null);

  useEffect(() => {
    if (!feature) return;

    setLoading(true);
    setError(null);
    api
      .getFeatureHistory(feature.id)
      .then((res) => {
        setEntries(res.items || []);
        if (res.items && res.items.length > 0) {
          setExpandedId(res.items[0].id); // expand most recent entry
        }
      })
      .catch((err) => {
        console.error('Failed to load feature history:', err);
        setError(err.message || (isAr ? 'فشل تحميل سجل التعديلات.' : 'Failed to load history.'));
      })
      .finally(() => {
        setLoading(false);
      });
  }, [feature, isAr]);

  if (!feature) return null;

  const getActionBadge = (action: string) => {
    switch (action.toUpperCase()) {
      case 'INSERT':
        return (
          <span className="px-2 py-0.5 rounded text-[11px] font-bold bg-emerald-500/20 text-emerald-300 border border-emerald-500/40">
            {isAr ? 'إنشاء (INSERT)' : 'INSERT'}
          </span>
        );
      case 'UPDATE':
        return (
          <span className="px-2 py-0.5 rounded text-[11px] font-bold bg-sky-500/20 text-sky-300 border border-sky-500/40">
            {isAr ? 'تعديل (UPDATE)' : 'UPDATE'}
          </span>
        );
      case 'DELETE':
        return (
          <span className="px-2 py-0.5 rounded text-[11px] font-bold bg-rose-500/20 text-rose-300 border border-rose-500/40">
            {isAr ? 'حذف (DELETE)' : 'DELETE'}
          </span>
        );
      default:
        return (
          <span className="px-2 py-0.5 rounded text-[11px] font-bold bg-slate-500/20 text-slate-300 border border-slate-500/40">
            {action}
          </span>
        );
    }
  };

  const formatDate = (isoString: string) => {
    try {
      const d = new Date(isoString);
      return d.toLocaleString(isAr ? 'ar-EG' : 'en-US', {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
      });
    } catch {
      return isoString;
    }
  };

  const renderJsonPretty = (rawJson: string | null | undefined) => {
    if (!rawJson) return <span className="text-slate-500 italic">None</span>;
    try {
      const parsed = JSON.parse(rawJson);
      return (
        <pre className="text-[11px] font-mono text-slate-300 bg-slate-950/80 p-2.5 rounded-lg border border-slate-800 overflow-x-auto whitespace-pre-wrap leading-relaxed max-h-48 overflow-y-auto">
          {JSON.stringify(parsed, null, 2)}
        </pre>
      );
    } catch {
      return (
        <pre className="text-[11px] font-mono text-slate-300 bg-slate-950/80 p-2.5 rounded-lg border border-slate-800 overflow-x-auto whitespace-pre-wrap leading-relaxed">
          {rawJson}
        </pre>
      );
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-sm animate-in fade-in duration-200">
      <div className="bg-slate-900 border border-slate-700/80 rounded-2xl max-w-2xl w-full shadow-2xl flex flex-col max-h-[85vh] overflow-hidden">
        {/* Header */}
        <div className="p-4 border-b border-slate-800 flex items-center justify-between bg-slate-950/80">
          <div className="flex items-center gap-2.5">
            <div className="p-2 rounded-lg bg-sky-500/10 border border-sky-500/30 text-sky-400">
              <History className="w-5 h-5" />
            </div>
            <div>
              <h3 className="text-sm font-bold text-white flex items-center gap-2">
                <span>{isAr ? 'سجل التدقيق والتعديلات' : 'Feature Audit History'}</span>
                <span className="text-xs text-sky-400 font-mono font-normal">
                  ({feature.properties.name})
                </span>
              </h3>
              <p className="text-[11px] text-slate-400">
                {isAr
                  ? 'تتبع تاريخي كامل لجميع العمليات والمستخدمين والأوقات والقيم السابقة'
                  : 'Complete historical tracking of all edits, acting users, and timestamps.'}
              </p>
            </div>
          </div>

          <button
            onClick={onClose}
            className="p-1.5 text-slate-400 hover:text-white rounded-lg hover:bg-slate-800 transition"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto p-4 space-y-3">
          {loading ? (
            <div className="py-16 flex flex-col items-center justify-center text-slate-400 gap-3">
              <Loader2 className="w-8 h-8 animate-spin text-sky-400" />
              <span className="text-xs">{isAr ? 'جارٍ جلب سجل التعديلات...' : 'Loading history logs...'}</span>
            </div>
          ) : error ? (
            <div className="p-4 rounded-xl bg-rose-950/40 border border-rose-800/60 text-rose-200 text-xs">
              {error}
            </div>
          ) : entries.length === 0 ? (
            <div className="py-16 text-center text-slate-500 text-xs italic">
              {isAr ? 'لا توجد سجلات تدقيق مسجلة لهذا المعلم.' : 'No audit history records found for this feature.'}
            </div>
          ) : (
            entries.map((entry) => {
              const isExpanded = expandedId === entry.id;
              return (
                <div
                  key={entry.id}
                  className="rounded-xl border border-slate-800 bg-slate-950/50 overflow-hidden transition"
                >
                  {/* Summary Row */}
                  <div
                    onClick={() => setExpandedId(isExpanded ? null : entry.id)}
                    className="p-3 flex items-center justify-between cursor-pointer hover:bg-slate-800/40 transition gap-2"
                  >
                    <div className="flex items-center gap-3 flex-wrap">
                      {getActionBadge(entry.action)}

                      <div className="flex items-center gap-1.5 text-xs text-white font-medium">
                        <User className="w-3.5 h-3.5 text-slate-400" />
                        <span>{entry.username || 'System'}</span>
                        <span className="text-[10px] text-slate-500 uppercase px-1 rounded bg-slate-800">
                          {entry.role}
                        </span>
                      </div>

                      <div className="flex items-center gap-1 text-[11px] text-slate-400">
                        <Clock className="w-3 h-3 text-slate-500" />
                        <span>{formatDate(entry.changedAt)}</span>
                      </div>
                    </div>

                    <div className="flex items-center gap-2">
                      <span className="text-xs text-slate-400 font-mono text-[11px] truncate max-w-[150px]">
                        {entry.reason || entry.tableName}
                      </span>
                      {isExpanded ? (
                        <ChevronDown className="w-4 h-4 text-slate-400" />
                      ) : (
                        <ChevronRight className="w-4 h-4 text-slate-400" />
                      )}
                    </div>
                  </div>

                  {/* Expanded Detail (Old Data vs New Data) */}
                  {isExpanded && (
                    <div className="p-3 border-t border-slate-800/80 bg-slate-900/60 space-y-3 text-xs">
                      {entry.reason && (
                        <div className="text-slate-300 flex items-center gap-2">
                          <FileText className="w-3.5 h-3.5 text-amber-400" />
                          <span className="font-semibold text-slate-400">{isAr ? 'السبب:' : 'Reason:'}</span>
                          <span>{entry.reason}</span>
                        </div>
                      )}

                      <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                        {/* Old Data */}
                        <div className="space-y-1">
                          <span className="text-[11px] font-semibold text-rose-400 flex items-center gap-1">
                            <span>{isAr ? 'البيانات السابقة (Old Data):' : 'Old Data:'}</span>
                          </span>
                          {renderJsonPretty(entry.oldData)}
                        </div>

                        {/* New Data */}
                        <div className="space-y-1">
                          <span className="text-[11px] font-semibold text-emerald-400 flex items-center gap-1">
                            <span>{isAr ? 'البيانات الجديدة (New Data):' : 'New Data:'}</span>
                          </span>
                          {renderJsonPretty(entry.newData)}
                        </div>
                      </div>
                    </div>
                  )}
                </div>
              );
            })
          )}
        </div>

        {/* Footer */}
        <div className="p-3 border-t border-slate-800 bg-slate-950/60 flex justify-end">
          <button
            onClick={onClose}
            className="px-4 py-1.5 rounded-xl text-xs font-semibold text-slate-300 hover:text-white bg-slate-800 hover:bg-slate-700 transition"
          >
            {isAr ? 'إغلاق' : 'Close'}
          </button>
        </div>
      </div>
    </div>
  );
};

