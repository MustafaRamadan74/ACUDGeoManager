'use client';

import React from 'react';
import { useAuth } from '@/lib/authContext';
import { GeoJsonFeature } from '@/lib/types';
import { X, MapPin, Activity, Info, Lock } from 'lucide-react';

interface FeatureDetailsProps {
  feature: GeoJsonFeature | null;
  onClose: () => void;
}

export const FeatureDetails: React.FC<FeatureDetailsProps> = ({
  feature,
  onClose,
}) => {
  const { lang, canEdit } = useAuth();
  const isAr = lang === 'ar';

  if (!feature) {
    return null;
  }

  const { properties, id, geometry } = feature;
  const attributes = properties.attributes || {};

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'ACTIVE':
        return <span className="px-2 py-0.5 rounded-full text-xs font-semibold bg-emerald-500/20 text-emerald-300 border border-emerald-500/40">نشط / ACTIVE</span>;
      case 'PLANNED':
        return <span className="px-2 py-0.5 rounded-full text-xs font-semibold bg-sky-500/20 text-sky-300 border border-sky-500/40">مخطط / PLANNED</span>;
      case 'DAMAGED':
        return <span className="px-2 py-0.5 rounded-full text-xs font-semibold bg-red-500/20 text-red-300 border border-red-500/40">تالف / DAMAGED</span>;
      case 'ABANDONED':
        return <span className="px-2 py-0.5 rounded-full text-xs font-semibold bg-amber-500/20 text-amber-300 border border-amber-500/40">مهجور / ABANDONED</span>;
      default:
        return <span className="px-2 py-0.5 rounded-full text-xs font-semibold bg-slate-500/20 text-slate-300 border border-slate-500/40">{status}</span>;
    }
  };

  return (
    <div className="w-80 bg-slate-900 border-l rtl:border-l-0 rtl:border-r border-slate-800 flex flex-col h-full shadow-2xl z-20 overflow-hidden">
      {/* Header */}
      <div className="p-4 border-b border-slate-800 flex items-start justify-between bg-slate-950/60">
        <div className="space-y-1 pr-2 rtl:pr-0 rtl:pl-2">
          <div className="flex items-center gap-2">
            <span className="text-[11px] font-bold text-sky-400 uppercase tracking-wider bg-sky-950/80 px-2 py-0.5 rounded border border-sky-800/60">
              {properties.featureTypeCode}
            </span>
            <span className="text-[11px] text-slate-400 font-mono">
              {geometry.type}
            </span>
          </div>
          <h2 className="text-sm font-bold text-white leading-tight">
            {properties.name}
          </h2>
        </div>
        <button
          onClick={onClose}
          className="p-1 rounded-lg text-slate-400 hover:text-white hover:bg-slate-800 transition"
        >
          <X className="w-5 h-5" />
        </button>
      </div>

      {/* Body */}
      <div className="flex-1 overflow-y-auto p-4 space-y-4">
        {/* Status */}
        <div className="bg-slate-800/60 p-3 rounded-xl border border-slate-700/60 space-y-2">
          <div className="flex items-center justify-between text-xs">
            <span className="text-slate-400 flex items-center gap-1.5">
              <Activity className="w-3.5 h-3.5 text-sky-400" />
              <span>{isAr ? 'الحالة التشغيلية' : 'Operating Status'}</span>
            </span>
            {getStatusBadge(properties.status)}
          </div>
          <div className="text-[11px] text-slate-500 font-mono truncate">
            ID: {id}
          </div>
        </div>

        {/* Read-Only Banner for Viewers */}
        {!canEdit && (
          <div className="p-3 rounded-xl bg-purple-950/40 border border-purple-800/60 text-purple-200 text-xs flex items-center gap-2.5">
            <Lock className="w-4 h-4 text-purple-400 flex-shrink-0" />
            <span>{isAr ? 'حساب مشاهد: وضع القراءة فقط دون أدوات تحرير.' : 'Viewer role: Read-only access without edit controls.'}</span>
          </div>
        )}

        {/* Attributes Table */}
        <div className="space-y-2">
          <h3 className="text-xs font-semibold text-slate-300 uppercase tracking-wider flex items-center gap-1.5">
            <Info className="w-3.5 h-3.5 text-amber-400" />
            <span>{isAr ? 'بيانات وخصائص المعلم' : 'Feature Attributes'}</span>
          </h3>

          {Object.keys(attributes).length === 0 ? (
            <div className="p-3 rounded-lg bg-slate-800/40 border border-slate-800 text-slate-500 text-xs italic text-center">
              {isAr ? 'لا توجد خصائص إضافية مسجلة' : 'No additional attributes'}
            </div>
          ) : (
            <div className="rounded-xl border border-slate-800 overflow-hidden divide-y divide-slate-800 bg-slate-950/40">
              {Object.entries(attributes).map(([key, val]) => (
                <div key={key} className="px-3 py-2 flex items-center justify-between text-xs">
                  <span className="font-mono text-slate-400 text-[11px]">{key}</span>
                  <span className="font-semibold text-slate-200 text-right rtl:text-right ltr:text-left">
                    {typeof val === 'boolean' ? (val ? 'نعم / Yes' : 'لا / No') : String(val)}
                  </span>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

