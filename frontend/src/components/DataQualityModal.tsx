'use client';

import React, { useEffect, useState, useMemo } from 'react';
import { NetworkDto, DataQualityDashboardDto } from '@/lib/types';
import { api } from '@/lib/api';
import { useAuth } from '@/lib/authContext';
import {
  ShieldCheck,
  X,
  Loader2,
  CheckCircle2,
  AlertTriangle,
  Paperclip,
  Link2,
  RefreshCw,
  Search,
  Filter,
  Layers,
  Database,
  BarChart2,
} from 'lucide-react';

interface DataQualityModalProps {
  network: NetworkDto | null;
  onClose: () => void;
}

export const DataQualityModal: React.FC<DataQualityModalProps> = ({ network, onClose }) => {
  const { lang } = useAuth();
  const isAr = lang === 'ar';

  const [loading, setLoading] = useState(true);
  const [dashboard, setDashboard] = useState<DataQualityDashboardDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [geomFilter, setGeomFilter] = useState<'ALL' | 'POINT' | 'LINE' | 'POLYGON'>('ALL');

  const fetchDashboard = () => {
    setLoading(true);
    setError(null);
    api
      .getDataQualityDashboard(network?.id)
      .then((data) => setDashboard(data))
      .catch((err) => {
        console.error('Failed to load data quality dashboard:', err);
        setError(err.message || (isAr ? 'فشل تحميل لوحة جودة البيانات' : 'Failed to load data quality dashboard'));
      })
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    fetchDashboard();
  }, [network?.id]);

  const filteredLayers = useMemo(() => {
    if (!dashboard?.layers) return [];
    return dashboard.layers.filter((layer) => {
      const matchesSearch =
        layer.displayName.toLowerCase().includes(searchQuery.toLowerCase()) ||
        layer.featureTypeCode.toLowerCase().includes(searchQuery.toLowerCase());

      const matchesGeom =
        geomFilter === 'ALL' || layer.geometryKind.toUpperCase() === geomFilter;

      return matchesSearch && matchesGeom;
    });
  }, [dashboard, searchQuery, geomFilter]);

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/75 backdrop-blur-sm animate-in fade-in duration-200">
      <div className="bg-slate-900 border border-slate-700/80 rounded-2xl max-w-4xl w-full shadow-2xl flex flex-col max-h-[90vh] overflow-hidden">
        {/* Header */}
        <div className="p-4 border-b border-slate-800 flex items-center justify-between bg-slate-950/80">
          <div className="flex items-center gap-3">
            <div className="p-2.5 rounded-xl bg-emerald-500/10 border border-emerald-500/30 text-emerald-400">
              <ShieldCheck className="w-6 h-6" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h2 className="text-base font-bold text-white">
                  {isAr ? 'لوحة جودة البيانات وتكامل الطبقات' : 'Data Quality & Layer Completeness'}
                </h2>
                {network && (
                  <span className="text-xs px-2 py-0.5 rounded-full bg-slate-800 text-sky-400 border border-slate-700 font-mono">
                    {network.name}
                  </span>
                )}
              </div>
              <p className="text-xs text-slate-400">
                {isAr
                  ? 'تدقيق الهندسة، نسبة اكتمال السمات، والمرفقات والعلاقات المكانية'
                  : 'Geometry validity, attribute completeness, attachments, and spatial links'}
              </p>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <button
              onClick={fetchDashboard}
              disabled={loading}
              className="p-2 rounded-lg text-slate-400 hover:text-white hover:bg-slate-800 transition border border-slate-800"
              title={isAr ? 'تحديث' : 'Refresh'}
            >
              <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
            </button>
            <button
              onClick={onClose}
              className="p-2 rounded-lg text-slate-400 hover:text-white hover:bg-slate-800 transition border border-slate-800"
            >
              <X className="w-5 h-5" />
            </button>
          </div>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto p-5 space-y-6">
          {loading && !dashboard ? (
            <div className="flex flex-col items-center justify-center py-20 text-slate-400 space-y-3">
              <Loader2 className="w-8 h-8 animate-spin text-emerald-500" />
              <span className="text-sm">{isAr ? 'جاري فحص مؤشرات جودة البيانات...' : 'Analyzing layer quality metrics...'}</span>
            </div>
          ) : error ? (
            <div className="p-4 rounded-xl bg-rose-500/10 border border-rose-500/30 text-rose-300 text-sm flex items-center gap-3">
              <AlertTriangle className="w-5 h-5 flex-shrink-0" />
              <span>{error}</span>
            </div>
          ) : dashboard ? (
            <>
              {/* Overall KPI Cards */}
              <div className="grid grid-cols-2 md:grid-cols-6 gap-3">
                {/* Total Features */}
                <div className="bg-slate-950/60 p-3.5 rounded-xl border border-slate-800/80 space-y-1">
                  <div className="flex items-center justify-between text-slate-400 text-xs">
                    <span>{isAr ? 'إجمالي المعالم' : 'Total Features'}</span>
                    <Database className="w-3.5 h-3.5 text-sky-400" />
                  </div>
                  <div className="text-xl font-bold text-white font-mono">
                    {dashboard.totalFeatures.toLocaleString()}
                  </div>
                </div>

                {/* Valid Geometries */}
                <div className="bg-slate-950/60 p-3.5 rounded-xl border border-slate-800/80 space-y-1">
                  <div className="flex items-center justify-between text-slate-400 text-xs">
                    <span>{isAr ? 'هندسة صالحة' : 'Valid Geom'}</span>
                    <CheckCircle2 className="w-3.5 h-3.5 text-emerald-400" />
                  </div>
                  <div className="text-xl font-bold text-emerald-400 font-mono">
                    {dashboard.totalValidGeometries.toLocaleString()}
                  </div>
                </div>

                {/* Open Issues */}
                <div className="bg-slate-950/60 p-3.5 rounded-xl border border-slate-800/80 space-y-1">
                  <div className="flex items-center justify-between text-slate-400 text-xs">
                    <span>{isAr ? 'ملاحظات تدقيق' : 'Open Issues'}</span>
                    <AlertTriangle className="w-3.5 h-3.5 text-amber-400" />
                  </div>
                  <div className={`text-xl font-bold font-mono ${dashboard.totalOpenIssues > 0 ? 'text-amber-400' : 'text-slate-200'}`}>
                    {dashboard.totalOpenIssues.toLocaleString()}
                  </div>
                </div>

                {/* Completeness Pct */}
                <div className="bg-slate-950/60 p-3.5 rounded-xl border border-slate-800/80 space-y-1">
                  <div className="flex items-center justify-between text-slate-400 text-xs">
                    <span>{isAr ? 'اكتمال السمات' : 'Completeness'}</span>
                    <BarChart2 className="w-3.5 h-3.5 text-indigo-400" />
                  </div>
                  <div className="text-xl font-bold text-indigo-400 font-mono">
                    {dashboard.overallCompletenessPct}%
                  </div>
                </div>

                {/* Attachments */}
                <div className="bg-slate-950/60 p-3.5 rounded-xl border border-slate-800/80 space-y-1">
                  <div className="flex items-center justify-between text-slate-400 text-xs">
                    <span>{isAr ? 'المرفقات' : 'Attachments'}</span>
                    <Paperclip className="w-3.5 h-3.5 text-purple-400" />
                  </div>
                  <div className="text-xl font-bold text-purple-400 font-mono">
                    {dashboard.totalAttachments.toLocaleString()}
                  </div>
                </div>

                {/* Relationships */}
                <div className="bg-slate-950/60 p-3.5 rounded-xl border border-slate-800/80 space-y-1">
                  <div className="flex items-center justify-between text-slate-400 text-xs">
                    <span>{isAr ? 'العلاقات' : 'Relations'}</span>
                    <Link2 className="w-3.5 h-3.5 text-teal-400" />
                  </div>
                  <div className="text-xl font-bold text-teal-400 font-mono">
                    {dashboard.totalRelationships.toLocaleString()}
                  </div>
                </div>
              </div>

              {/* Filters Bar */}
              <div className="flex flex-col md:flex-row gap-3 items-stretch md:items-center justify-between bg-slate-950/40 p-3 rounded-xl border border-slate-800">
                <div className="relative flex-1">
                  <Search className="w-4 h-4 absolute left-3 rtl:left-auto rtl:right-3 top-1/2 -translate-y-1/2 text-slate-500" />
                  <input
                    type="text"
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                    placeholder={isAr ? 'بحث باسم الطبقة أو الكود...' : 'Search by layer name or code...'}
                    className="w-full bg-slate-900 border border-slate-700/70 rounded-lg pl-9 pr-3 rtl:pl-3 rtl:pr-9 py-1.5 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-sky-500 transition"
                  />
                </div>

                <div className="flex items-center gap-1.5 bg-slate-900 p-1 rounded-lg border border-slate-800 self-start md:self-auto">
                  {(
                    [
                      { key: 'ALL', label: isAr ? 'الكل' : 'All' },
                      { key: 'POINT', label: isAr ? 'نقاط' : 'Points' },
                      { key: 'LINE', label: isAr ? 'خطوط' : 'Lines' },
                      { key: 'POLYGON', label: isAr ? 'مضلعات' : 'Polygons' },
                    ] as const
                  ).map((tab) => (
                    <button
                      key={tab.key}
                      onClick={() => setGeomFilter(tab.key)}
                      className={`px-2.5 py-1 text-xs rounded font-medium transition ${
                        geomFilter === tab.key
                          ? 'bg-sky-600 text-white shadow-sm'
                          : 'text-slate-400 hover:text-white'
                      }`}
                    >
                      {tab.label}
                    </button>
                  ))}
                </div>
              </div>

              {/* Layers Table */}
              <div className="rounded-xl border border-slate-800 overflow-hidden bg-slate-950/40">
                <div className="overflow-x-auto">
                  <table className="w-full text-left rtl:text-right text-xs">
                    <thead className="bg-slate-950 text-slate-400 font-semibold uppercase tracking-wider text-[11px] border-b border-slate-800">
                      <tr>
                        <th className="py-3 px-4">{isAr ? 'الطبقة / النوع' : 'Layer / Type'}</th>
                        <th className="py-3 px-3 text-center">{isAr ? 'العدد' : 'Count'}</th>
                        <th className="py-3 px-3 text-center">{isAr ? 'صلاحية الهندسة' : 'Geometry Validity'}</th>
                        <th className="py-3 px-3 text-center">{isAr ? 'اكتمال السمات' : 'Completeness'}</th>
                        <th className="py-3 px-3 text-center">{isAr ? 'ملاحظات مفتوحة' : 'Open Issues'}</th>
                        <th className="py-3 px-3 text-center">{isAr ? 'المرفقات' : 'Files'}</th>
                        <th className="py-3 px-3 text-center">{isAr ? 'العلاقات' : 'Links'}</th>
                        <th className="py-3 px-4 text-center">{isAr ? 'الحالة' : 'Status'}</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-800/80">
                      {filteredLayers.length === 0 ? (
                        <tr>
                          <td colSpan={8} className="py-8 text-center text-slate-500">
                            {isAr ? 'لا توجد طبقات مطابقة للبحث' : 'No layers matching search'}
                          </td>
                        </tr>
                      ) : (
                        filteredLayers.map((layer) => {
                          const isHealthy = layer.validGeomCount === layer.totalCount && layer.openIssuesCount === 0;
                          const hasIssues = layer.openIssuesCount > 0 || layer.validGeomCount < layer.totalCount;

                          return (
                            <tr key={layer.featureTypeCode} className="hover:bg-slate-900/60 transition">
                              <td className="py-3 px-4">
                                <div className="space-y-0.5">
                                  <div className="font-semibold text-white text-xs flex items-center gap-2">
                                    <span>{layer.displayName}</span>
                                    <span className="text-[10px] font-mono px-1.5 py-0.2 rounded bg-slate-800 text-slate-400 border border-slate-700">
                                      {layer.geometryKind}
                                    </span>
                                  </div>
                                  <div className="text-[10px] font-mono text-slate-500">
                                    {layer.featureTypeCode}
                                  </div>
                                </div>
                              </td>

                              <td className="py-3 px-3 text-center font-mono font-bold text-slate-200">
                                {layer.totalCount.toLocaleString()}
                              </td>

                              <td className="py-3 px-3 text-center">
                                <div className="inline-flex items-center gap-1.5 font-mono">
                                  <span className={layer.validGeomCount === layer.totalCount ? 'text-emerald-400' : 'text-amber-400'}>
                                    {layer.validGeomCount} / {layer.totalCount}
                                  </span>
                                </div>
                              </td>

                              <td className="py-3 px-3 text-center">
                                <div className="w-24 mx-auto space-y-1">
                                  <div className="flex justify-between text-[10px] font-mono text-slate-400">
                                    <span>{layer.completenessPct}%</span>
                                  </div>
                                  <div className="w-full bg-slate-800 rounded-full h-1.5 overflow-hidden">
                                    <div
                                      className={`h-full rounded-full ${
                                        layer.completenessPct >= 90
                                          ? 'bg-emerald-500'
                                          : layer.completenessPct >= 70
                                          ? 'bg-indigo-500'
                                          : 'bg-amber-500'
                                      }`}
                                      style={{ width: `${Math.min(layer.completenessPct, 100)}%` }}
                                    />
                                  </div>
                                </div>
                              </td>

                              <td className="py-3 px-3 text-center font-mono">
                                {layer.openIssuesCount > 0 ? (
                                  <span className="inline-flex items-center gap-1 text-amber-400 bg-amber-500/10 px-2 py-0.5 rounded-full border border-amber-500/30 text-xs font-semibold">
                                    <AlertTriangle className="w-3 h-3" />
                                    {layer.openIssuesCount.toLocaleString()}
                                  </span>
                                ) : (
                                  <span className="text-slate-500">0</span>
                                )}
                              </td>

                              <td className="py-3 px-3 text-center font-mono text-purple-400">
                                {layer.attachmentsCount > 0 ? layer.attachmentsCount : <span className="text-slate-600">-</span>}
                              </td>

                              <td className="py-3 px-3 text-center font-mono text-teal-400">
                                {layer.relationshipsCount > 0 ? layer.relationshipsCount : <span className="text-slate-600">-</span>}
                              </td>

                              <td className="py-3 px-4 text-center">
                                {isHealthy ? (
                                  <span className="inline-flex items-center gap-1 text-emerald-400 bg-emerald-500/10 px-2 py-0.5 rounded-full border border-emerald-500/30 text-[11px] font-medium">
                                    <CheckCircle2 className="w-3 h-3" />
                                    {isAr ? 'سليم' : 'Healthy'}
                                  </span>
                                ) : hasIssues ? (
                                  <span className="inline-flex items-center gap-1 text-amber-300 bg-amber-500/10 px-2 py-0.5 rounded-full border border-amber-500/30 text-[11px] font-medium">
                                    <AlertTriangle className="w-3 h-3" />
                                    {isAr ? 'يحتاج تدقيق' : 'Needs Review'}
                                  </span>
                                ) : (
                                  <span className="text-slate-500 text-[11px]">-</span>
                                )}
                              </td>
                            </tr>
                          );
                        })
                      )}
                    </tbody>
                  </table>
                </div>
              </div>
            </>
          ) : null}
        </div>
      </div>
    </div>
  );
};

