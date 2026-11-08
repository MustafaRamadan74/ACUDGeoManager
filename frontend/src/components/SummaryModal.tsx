'use client';

import React, { useEffect, useState } from 'react';
import { NetworkDto, RichWorkspaceSummaryDto } from '@/lib/types';
import { api } from '@/lib/api';
import { useAuth } from '@/lib/authContext';
import {
  BarChart3,
  X,
  Loader2,
  CheckCircle2,
  AlertTriangle,
  Clock,
  Layers,
  RefreshCw,
  Ruler,
  Maximize2,
} from 'lucide-react';

interface SummaryModalProps {
  network: NetworkDto | null;
  onClose: () => void;
}

export const SummaryModal: React.FC<SummaryModalProps> = ({ network, onClose }) => {
  const { lang } = useAuth();
  const isAr = lang === 'ar';

  const [loading, setLoading] = useState(true);
  const [summary, setSummary] = useState<RichWorkspaceSummaryDto | null>(null);
  const [error, setError] = useState<string | null>(null);

  const fetchSummary = () => {
    if (!network) return;
    setLoading(true);
    setError(null);
    api
      .getNetworkSummary(network.id)
      .then((data) => setSummary(data))
      .catch((err) => {
        console.error('Failed to load network summary:', err);
        setError(err.message || (isAr ? 'فشل تحميل ملخص الشبكة' : 'Failed to load network summary'));
      })
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    fetchSummary();
  }, [network]);

  if (!network) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-sm animate-in fade-in duration-200">
      <div className="bg-slate-900 border border-slate-700/80 rounded-2xl max-w-3xl w-full shadow-2xl flex flex-col max-h-[88vh] overflow-hidden">
        {/* Header */}
        <div className="p-4 border-b border-slate-800 flex items-center justify-between bg-slate-950/80">
          <div className="flex items-center gap-2.5">
            <div className="p-2 rounded-lg bg-sky-500/10 border border-sky-500/30 text-sky-400">
              <BarChart3 className="w-5 h-5" />
            </div>
            <div>
              <h3 className="text-sm font-bold text-white flex items-center gap-2">
                <span>{isAr ? 'ملخص شبكة المرافق الجغرافية والإحصاءات' : 'Network Summary & Spatial Statistics'}</span>
                <span className="text-xs text-sky-400 font-mono font-normal">
                  ({network.name})
                </span>
              </h3>
              <p className="text-[11px] text-slate-400">
                {isAr
                  ? `مجال الشبكة: ${network.domain} • التسامح الهندسي: ${network.snapToleranceM}م`
                  : `Domain: ${network.domain} • Snap tolerance: ${network.snapToleranceM}m`}
              </p>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <button
              onClick={fetchSummary}
              disabled={loading}
              className="p-1.5 text-slate-400 hover:text-white rounded-lg hover:bg-slate-800 transition"
              title={isAr ? 'تحديث البيانات' : 'Refresh'}
            >
              <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
            </button>
            <button
              onClick={onClose}
              className="p-1.5 text-slate-400 hover:text-white rounded-lg hover:bg-slate-800 transition"
            >
              <X className="w-5 h-5" />
            </button>
          </div>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto p-5 space-y-5">
          {loading ? (
            <div className="py-20 flex flex-col items-center justify-center text-slate-400 gap-3">
              <Loader2 className="w-8 h-8 animate-spin text-sky-400" />
              <span className="text-xs">{isAr ? 'جارٍ احتساب إحصاءات المعالم والأطوال والمساحات...' : 'Calculating spatial statistics...'}</span>
            </div>
          ) : error ? (
            <div className="p-4 rounded-xl bg-rose-950/40 border border-rose-800/60 text-rose-200 text-xs">
              {error}
            </div>
          ) : summary ? (
            <>
              {/* Asset Count Stat Cards Grid */}
              <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
                {/* Total Assets */}
                <div className="bg-slate-950/70 p-3 rounded-xl border border-slate-800 space-y-1">
                  <span className="text-[11px] text-slate-400 font-medium flex items-center gap-1.5">
                    <Layers className="w-3.5 h-3.5 text-sky-400" />
                    <span>{isAr ? 'إجمالي المعالم' : 'Total Assets'}</span>
                  </span>
                  <div className="text-2xl font-black text-white font-mono">
                    {summary.totalFeatures.toLocaleString()}
                  </div>
                </div>

                {/* Active Assets */}
                <div className="bg-emerald-950/20 p-3 rounded-xl border border-emerald-900/40 space-y-1">
                  <span className="text-[11px] text-emerald-300 font-medium flex items-center gap-1.5">
                    <CheckCircle2 className="w-3.5 h-3.5 text-emerald-400" />
                    <span>{isAr ? 'نشط (ACTIVE)' : 'Active'}</span>
                  </span>
                  <div className="text-2xl font-black text-emerald-400 font-mono">
                    {(summary.countsByStatus['ACTIVE'] || 0).toLocaleString()}
                  </div>
                </div>

                {/* Planned Assets */}
                <div className="bg-sky-950/20 p-3 rounded-xl border border-sky-900/40 space-y-1">
                  <span className="text-[11px] text-sky-300 font-medium flex items-center gap-1.5">
                    <Clock className="w-3.5 h-3.5 text-sky-400" />
                    <span>{isAr ? 'مخطط (PLANNED)' : 'Planned'}</span>
                  </span>
                  <div className="text-2xl font-black text-sky-400 font-mono">
                    {(summary.countsByStatus['PLANNED'] || 0).toLocaleString()}
                  </div>
                </div>

                {/* Damaged Assets */}
                <div className="bg-rose-950/20 p-3 rounded-xl border border-rose-900/40 space-y-1">
                  <span className="text-[11px] text-rose-300 font-medium flex items-center gap-1.5">
                    <AlertTriangle className="w-3.5 h-3.5 text-rose-400" />
                    <span>{isAr ? 'تالف / عطلان' : 'Damaged'}</span>
                  </span>
                  <div className="text-2xl font-black text-rose-400 font-mono">
                    {(summary.countsByStatus['DAMAGED'] || 0).toLocaleString()}
                  </div>
                </div>
              </div>

              {/* Spatial Geometry Metrics: Total Length & Area */}
              <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                {/* Total Pipe Length */}
                <div className="bg-gradient-to-br from-slate-950 to-sky-950/30 p-3.5 rounded-xl border border-sky-800/40 space-y-1.5 shadow-md">
                  <div className="flex items-center justify-between text-xs text-sky-400 font-medium">
                    <span className="flex items-center gap-1.5">
                      <Ruler className="w-4 h-4 text-sky-400" />
                      <span>{isAr ? 'إجمالي أطوال خطوط الأنابيب والشبكة' : 'Total Line Network Length'}</span>
                    </span>
                    <span className="text-[10px] font-mono px-2 py-0.5 rounded bg-sky-900/40 border border-sky-700/50 text-sky-300">
                      PostGIS ST_Length
                    </span>
                  </div>
                  <div className="flex items-baseline gap-2">
                    <span className="text-2xl font-black text-white font-mono">
                      {(summary.totalLengthM || 0).toLocaleString(undefined, { minimumFractionDigits: 1, maximumFractionDigits: 2 })}
                    </span>
                    <span className="text-xs font-semibold text-slate-400">{isAr ? 'متر' : 'm'}</span>
                    <span className="text-xs font-mono text-sky-400 font-medium">
                      ({((summary.totalLengthM || 0) / 1000).toFixed(2)} {isAr ? 'كم' : 'km'})
                    </span>
                  </div>
                </div>

                {/* Total Area */}
                <div className="bg-gradient-to-br from-slate-950 to-emerald-950/30 p-3.5 rounded-xl border border-emerald-800/40 space-y-1.5 shadow-md">
                  <div className="flex items-center justify-between text-xs text-emerald-400 font-medium">
                    <span className="flex items-center gap-1.5">
                      <Maximize2 className="w-4 h-4 text-emerald-400" />
                      <span>{isAr ? 'إجمالي مساحات المضلعات والمحطات' : 'Total Polygon Coverage Area'}</span>
                    </span>
                    <span className="text-[10px] font-mono px-2 py-0.5 rounded bg-emerald-900/40 border border-emerald-700/50 text-emerald-300">
                      PostGIS ST_Area
                    </span>
                  </div>
                  <div className="flex items-baseline gap-2">
                    <span className="text-2xl font-black text-white font-mono">
                      {(summary.totalAreaSqm || 0).toLocaleString(undefined, { minimumFractionDigits: 1, maximumFractionDigits: 2 })}
                    </span>
                    <span className="text-xs font-semibold text-slate-400">{isAr ? 'م²' : 'm²'}</span>
                    <span className="text-xs font-mono text-emerald-400 font-medium">
                      ({((summary.totalAreaSqm || 0) / 10000).toFixed(2)} {isAr ? 'هكتار' : 'ha'})
                    </span>
                  </div>
                </div>
              </div>

              {/* Breakdown by Feature Type with Length & Area */}
              <div className="space-y-3 pt-2">
                <h4 className="text-xs font-bold text-slate-300 uppercase tracking-wider flex items-center justify-between">
                  <span>{isAr ? 'توزيع الأصول حسب نوع المعلم ومقاييسها الجغرافية' : 'Assets by Feature Type & Spatial Metrics'}</span>
                  <span className="text-[11px] text-slate-400 font-normal">
                    {Object.keys(summary.countsByType).length} {isAr ? 'أنواع أصول' : 'asset types'}
                  </span>
                </h4>

                <div className="bg-slate-950/60 rounded-xl p-3.5 border border-slate-800 space-y-2.5">
                  {Object.entries(summary.countsByType).map(([typeCode, count]) => {
                    const percent = summary.totalFeatures > 0 ? (count / summary.totalFeatures) * 100 : 0;
                    const lengthM = summary.lengthsByTypeM?.[typeCode];
                    const areaSqm = summary.areasByTypeSqm?.[typeCode];

                    return (
                      <div key={typeCode} className="space-y-1">
                        <div className="flex items-center justify-between text-xs">
                          <div className="flex items-center gap-2">
                            <span className="font-mono text-sky-300 font-semibold">{typeCode}</span>
                            {lengthM !== undefined && lengthM > 0 && (
                              <span className="text-[10px] font-mono text-sky-400 bg-sky-950/60 px-1.5 py-0.5 rounded border border-sky-800/40">
                                {lengthM.toLocaleString()} m
                              </span>
                            )}
                            {areaSqm !== undefined && areaSqm > 0 && (
                              <span className="text-[10px] font-mono text-emerald-400 bg-emerald-950/60 px-1.5 py-0.5 rounded border border-emerald-800/40">
                                {areaSqm.toLocaleString()} m²
                              </span>
                            )}
                          </div>
                          <div className="flex items-center gap-2">
                            <span className="font-mono text-white font-bold">{count}</span>
                            <span className="text-[11px] text-slate-500 font-mono">({percent.toFixed(1)}%)</span>
                          </div>
                        </div>
                        <div className="h-1.5 w-full bg-slate-800 rounded-full overflow-hidden">
                          <div
                            className="h-full bg-sky-500 rounded-full transition-all duration-500"
                            style={{ width: `${percent}%` }}
                          />
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>

              {/* Status Breakdown Matrix Table */}
              <div className="space-y-3 pt-2">
                <h4 className="text-xs font-bold text-slate-300 uppercase tracking-wider">
                  {isAr ? 'تفاصيل النوع والحالة التشغيلية' : 'Type & Status Matrix'}
                </h4>

                <div className="rounded-xl border border-slate-800 overflow-hidden bg-slate-950/40">
                  <table className="w-full text-xs text-left rtl:text-right">
                    <thead className="bg-slate-950 text-slate-400 border-b border-slate-800 font-semibold">
                      <tr>
                        <th className="p-2.5">{isAr ? 'نوع المعلم' : 'Feature Type'}</th>
                        <th className="p-2.5">{isAr ? 'الحالة التشغيلية' : 'Status'}</th>
                        <th className="p-2.5 text-center">{isAr ? 'العدد' : 'Count'}</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-800/80 text-slate-300">
                      {summary.details.map((detail, idx) => (
                        <tr key={idx} className="hover:bg-slate-800/40 transition">
                          <td className="p-2.5 font-mono text-sky-300 font-medium">
                            {detail.featureTypeCode}
                          </td>
                          <td className="p-2.5">
                            <span
                              className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                                detail.status === 'ACTIVE'
                                  ? 'bg-emerald-500/20 text-emerald-300'
                                  : detail.status === 'PLANNED'
                                  ? 'bg-sky-500/20 text-sky-300'
                                  : detail.status === 'DAMAGED'
                                  ? 'bg-rose-500/20 text-rose-300'
                                  : 'bg-amber-500/20 text-amber-300'
                              }`}
                            >
                              {detail.status}
                            </span>
                          </td>
                          <td className="p-2.5 text-center font-mono font-bold text-white">
                            {detail.count}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            </>
          ) : null}
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
