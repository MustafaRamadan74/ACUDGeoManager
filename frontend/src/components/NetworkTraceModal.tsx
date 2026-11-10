'use client';

import React, { useState } from 'react';
import {
  Compass,
  ArrowDownRight,
  ArrowUpRight,
  AlertOctagon,
  Play,
  RotateCcw,
  X,
  FileText,
  FileSpreadsheet,
  Eye,
  CheckCircle2,
  AlertTriangle,
  Loader2,
  Navigation,
  Layers,
  MapPin,
  Check
} from 'lucide-react';
import { api } from '@/lib/api';
import { useAuth } from '@/lib/authContext';
import {
  NetworkDto,
  GeoJsonFeature,
  TraceMode,
  NetworkTraceResultDto,
  TraceValveDto,
  TraceEdgeDto
} from '@/lib/types';

interface NetworkTraceModalProps {
  isOpen: boolean;
  onClose: () => void;
  networks: NetworkDto[];
  activeNetworkId: string | null;
  onSelectNetwork: (networkId: string) => void;
  selectedFeature: GeoJsonFeature | null;
  onZoomToCoordinates: (coords: [number, number], zoom?: number) => void;
  onTraceResultApplied: (result: NetworkTraceResultDto | null) => void;
}

export const NetworkTraceModal: React.FC<NetworkTraceModalProps> = ({
  isOpen,
  onClose,
  networks,
  activeNetworkId,
  onSelectNetwork,
  selectedFeature,
  onZoomToCoordinates,
  onTraceResultApplied,
}) => {
  const { lang } = useAuth();
  const isAr = lang === 'ar';

  const [mode, setMode] = useState<TraceMode>('ISOLATION');
  const [maxDepth, setMaxDepth] = useState<number>(150);
  const [isRunning, setIsRunning] = useState(false);
  const [isExportingPdf, setIsExportingPdf] = useState(false);
  const [isExportingCsv, setIsExportingCsv] = useState(false);
  const [traceResult, setTraceResult] = useState<NetworkTraceResultDto | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<'valves' | 'pipes' | 'zones'>('valves');

  if (!isOpen) return null;

  const currentNetwork = networks.find((n) => n.id === activeNetworkId);

  // Determine starting candidate from selected feature
  const canStartFromSelected =
    selectedFeature &&
    (selectedFeature.geometry.type === 'LineString' || selectedFeature.geometry.type === 'Point');

  const startKind: 'LINE' | 'POINT' =
    selectedFeature?.geometry.type === 'LineString' ? 'LINE' : 'POINT';

  const handleRunTrace = async () => {
    if (!activeNetworkId) return;

    if (!selectedFeature) {
      setErrorMessage(
        isAr
          ? 'يرجى تحديد عنصر (أنبوب أو محبس) على الخريطة أولاً كنقطة بداية للتتبع'
          : 'Please select a pipe or valve feature on the map to set as the trace origin.'
      );
      return;
    }

    setIsRunning(true);
    setErrorMessage(null);

    try {
      const res = await api.executeNetworkTrace(activeNetworkId, {
        networkId: activeNetworkId,
        mode,
        startFeatureId: selectedFeature.id,
        startKind,
        maxDepth,
      });

      if (res.error) {
        setErrorMessage(res.error);
      } else {
        setTraceResult(res);
        onTraceResultApplied(res);
        if (mode === 'ISOLATION' && res.valvesToClose.length > 0) {
          setActiveTab('valves');
        } else {
          setActiveTab('pipes');
        }
      }
    } catch (err: any) {
      console.error('Network trace failed', err);
      setErrorMessage(err.message || 'Trace failed to execute.');
    } finally {
      setIsRunning(false);
    }
  };

  const handleClearTrace = () => {
    setTraceResult(null);
    setErrorMessage(null);
    onTraceResultApplied(null);
  };

  const handleExport = async (format: 'PDF' | 'CSV') => {
    if (!traceResult) return;
    if (format === 'PDF') setIsExportingPdf(true);
    else setIsExportingCsv(true);

    try {
      await api.downloadTraceReport({
        traceResult,
        networkName: currentNetwork?.name || 'Water Network',
        format,
      });
    } catch (err) {
      console.error('Failed to export trace report', err);
    } finally {
      setIsExportingPdf(false);
      setIsExportingCsv(false);
    }
  };

  const handleZoomToValve = (valve: TraceValveDto) => {
    if (valve.coordinates && valve.coordinates.length >= 2) {
      onZoomToCoordinates([valve.coordinates[1], valve.coordinates[0]], 18);
    }
  };

  const handleZoomToEdge = (edge: TraceEdgeDto) => {
    if (edge.geojson && edge.geojson.coordinates && edge.geojson.coordinates.length > 0) {
      const first = edge.geojson.coordinates[0];
      onZoomToCoordinates([first[1], first[0]], 18);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/75 backdrop-blur-sm animate-in fade-in duration-200">
      <div className="bg-slate-900 border border-slate-700/80 rounded-2xl w-full max-w-4xl max-h-[92vh] flex flex-col shadow-2xl overflow-hidden">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-slate-800 bg-slate-900/90">
          <div className="flex items-center gap-3">
            <div className="p-2.5 rounded-xl bg-sky-500/10 text-sky-400 border border-sky-500/20">
              <Compass className="w-6 h-6" />
            </div>
            <div>
              <h2 className="text-lg font-bold text-white flex items-center gap-2">
                <span>{isAr ? 'محرك تتبع الشبكات وعزل الأعطال' : 'Network Trace & Outage Isolation Engine'}</span>
                <span className="text-xs px-2 py-0.5 rounded-full bg-slate-800 text-teal-400 border border-slate-700 font-mono">
                  Directed Utility
                </span>
              </h2>
              <p className="text-xs text-slate-400 mt-0.5">
                {isAr
                  ? 'محاكاة السريان، تتبع مصادر المياه، وتحديد محابس الإغلاق لحصر الكسور والأعطال'
                  : 'Flow direction analysis, upstream source tracing, and optimal isolation valve shutoff'}
              </p>
            </div>
          </div>

          <button
            onClick={onClose}
            className="p-2 text-slate-400 hover:text-white rounded-lg hover:bg-slate-800 transition"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Toolbar & Parameters */}
        <div className="p-6 border-b border-slate-800 bg-slate-800/30 space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            {/* Mode: Isolation */}
            <button
              type="button"
              onClick={() => setMode('ISOLATION')}
              className={`p-3.5 rounded-xl border text-left rtl:text-right transition relative ${
                mode === 'ISOLATION'
                  ? 'bg-amber-950/40 border-amber-500/60 shadow-lg shadow-amber-950/30 ring-1 ring-amber-500/50'
                  : 'bg-slate-800/50 border-slate-700/60 hover:bg-slate-800/80 text-slate-400'
              }`}
            >
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2 font-bold text-xs text-white">
                  <AlertOctagon className={`w-4 h-4 ${mode === 'ISOLATION' ? 'text-amber-400' : 'text-slate-400'}`} />
                  <span>{isAr ? 'عزل الأعطال (Isolation)' : 'Isolation Shutoff'}</span>
                </div>
                {mode === 'ISOLATION' && <Check className="w-4 h-4 text-amber-400" />}
              </div>
              <p className="text-[11px] text-slate-400 mt-1.5 leading-relaxed">
                {isAr
                  ? 'تحديد المحابس الواجب إغلاقها لعزل الكسر وحصر الشبكة المتأثرة'
                  : 'Locate boundary valves to shut off and isolate pipe break incident.'}
              </p>
            </button>

            {/* Mode: Downstream */}
            <button
              type="button"
              onClick={() => setMode('DOWNSTREAM')}
              className={`p-3.5 rounded-xl border text-left rtl:text-right transition relative ${
                mode === 'DOWNSTREAM'
                  ? 'bg-sky-950/40 border-sky-500/60 shadow-lg shadow-sky-950/30 ring-1 ring-sky-500/50'
                  : 'bg-slate-800/50 border-slate-700/60 hover:bg-slate-800/80 text-slate-400'
              }`}
            >
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2 font-bold text-xs text-white">
                  <ArrowDownRight className={`w-4 h-4 ${mode === 'DOWNSTREAM' ? 'text-sky-400' : 'text-slate-400'}`} />
                  <span>{isAr ? 'مع السريان (Downstream)' : 'Downstream Flow'}</span>
                </div>
                {mode === 'DOWNSTREAM' && <Check className="w-4 h-4 text-sky-400" />}
              </div>
              <p className="text-[11px] text-slate-400 mt-1.5 leading-relaxed">
                {isAr
                  ? 'تتبع مسار المياه للأمام وصولاً للمستهلكين ومحابس الإغلاق'
                  : 'Trace water flow forward following hydraulic digitized direction.'}
              </p>
            </button>

            {/* Mode: Upstream */}
            <button
              type="button"
              onClick={() => setMode('UPSTREAM')}
              className={`p-3.5 rounded-xl border text-left rtl:text-right transition relative ${
                mode === 'UPSTREAM'
                  ? 'bg-indigo-950/40 border-indigo-500/60 shadow-lg shadow-indigo-950/30 ring-1 ring-indigo-500/50'
                  : 'bg-slate-800/50 border-slate-700/60 hover:bg-slate-800/80 text-slate-400'
              }`}
            >
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2 font-bold text-xs text-white">
                  <ArrowUpRight className={`w-4 h-4 ${mode === 'UPSTREAM' ? 'text-indigo-400' : 'text-slate-400'}`} />
                  <span>{isAr ? 'عكس السريان (Upstream)' : 'Upstream Sources'}</span>
                </div>
                {mode === 'UPSTREAM' && <Check className="w-4 h-4 text-indigo-400" />}
              </div>
              <p className="text-[11px] text-slate-400 mt-1.5 leading-relaxed">
                {isAr
                  ? 'تتبع المسار عكسياً للوصول إلى محطات الضخ والخزانات المغذية'
                  : 'Trace backwards against flow to locate source pump stations or tanks.'}
              </p>
            </button>
          </div>

          {/* Start Origin Status & Action */}
          <div className="flex flex-wrap items-center justify-between gap-4 p-3 bg-slate-900/60 border border-slate-700/50 rounded-xl">
            <div className="flex items-center gap-3">
              <MapPin className="w-4 h-4 text-slate-400" />
              <div>
                <div className="text-xs font-semibold text-white">
                  {selectedFeature ? (
                    <span className="flex items-center gap-2">
                      <span className="text-sky-400">{selectedFeature.properties.name || selectedFeature.id}</span>
                      <span className="text-[10px] px-1.5 py-0.5 rounded bg-slate-800 text-slate-400 uppercase font-mono">
                        {selectedFeature.properties.featureTypeCode}
                      </span>
                    </span>
                  ) : (
                    <span className="text-amber-400">
                      {isAr ? 'لم يتم تحديد عنصر على الخريطة' : 'No feature selected on map'}
                    </span>
                  )}
                </div>
                <div className="text-[11px] text-slate-400">
                  {selectedFeature
                    ? (isAr ? 'العنصر المختار سيُستخدم كنقطة انطلاق للتحليل' : 'Origin node/edge for network traversal')
                    : (isAr ? 'اضغط على أي خط أنبوب أو محبس في الخريطة لبدء التتبع' : 'Click any pipe or valve on the map to begin trace')}
                </div>
              </div>
            </div>

            <div className="flex items-center gap-2">
              {/* Max Depth */}
              <div className="flex items-center gap-1.5 text-xs text-slate-400">
                <span>{isAr ? 'العمق الأقصى:' : 'Max Depth:'}</span>
                <input
                  type="number"
                  min={10}
                  max={500}
                  value={maxDepth}
                  onChange={(e) => setMaxDepth(parseInt(e.target.value, 10) || 150)}
                  className="w-16 bg-slate-800 border border-slate-700 rounded-lg px-2 py-1 text-xs text-white font-mono focus:outline-none"
                />
              </div>

              {/* Run Trace Button */}
              <button
                type="button"
                onClick={handleRunTrace}
                disabled={isRunning || !canStartFromSelected}
                className="flex items-center gap-2 px-4 py-2 rounded-xl bg-sky-600 hover:bg-sky-500 disabled:opacity-50 text-white text-xs font-bold shadow-md shadow-sky-950/50 transition active:scale-95"
              >
                {isRunning ? (
                  <>
                    <Loader2 className="w-4 h-4 animate-spin" />
                    <span>{isAr ? 'جارٍ تحليل السريان...' : 'Tracing Network...'}</span>
                  </>
                ) : (
                  <>
                    <Play className="w-4 h-4 fill-white" />
                    <span>{isAr ? 'بدء التتبع' : 'Run Trace'}</span>
                  </>
                )}
              </button>

              {traceResult && (
                <button
                  type="button"
                  onClick={handleClearTrace}
                  className="flex items-center gap-1.5 px-3 py-2 rounded-xl bg-slate-800 hover:bg-slate-700 text-slate-300 text-xs font-medium border border-slate-700 transition"
                >
                  <RotateCcw className="w-3.5 h-3.5" />
                  <span>{isAr ? 'مسح' : 'Clear'}</span>
                </button>
              )}
            </div>
          </div>

          {errorMessage && (
            <div className="p-3 rounded-xl bg-rose-500/10 border border-rose-500/30 text-rose-300 text-xs flex items-center gap-2">
              <AlertTriangle className="w-4 h-4 shrink-0" />
              <span>{errorMessage}</span>
            </div>
          )}
        </div>

        {/* Results Area */}
        <div className="flex-1 overflow-y-auto p-6 space-y-4">
          {traceResult ? (
            <>
              {/* KPI Cards */}
              <div className="grid grid-cols-2 md:grid-cols-5 gap-3">
                <div className="p-3.5 rounded-xl bg-slate-800/60 border border-slate-700/60">
                  <div className="text-[11px] text-slate-400 font-medium">
                    {isAr ? 'الطول المتأثر الإجمالي' : 'Total Affected Length'}
                  </div>
                  <div className="text-lg font-bold text-white mt-0.5 font-mono">
                    {traceResult.summary.totalLengthM >= 1000
                      ? `${(traceResult.summary.totalLengthM / 1000).toFixed(2)} km`
                      : `${traceResult.summary.totalLengthM.toFixed(1)} m`}
                  </div>
                </div>

                <div className="p-3.5 rounded-xl bg-slate-800/60 border border-slate-700/60">
                  <div className="text-[11px] text-slate-400 font-medium">
                    {isAr ? 'الأنابيب المتأثرة' : 'Traversed Pipes'}
                  </div>
                  <div className="text-lg font-bold text-white mt-0.5 font-mono">
                    {traceResult.summary.edgeCount}
                  </div>
                </div>

                <div className="p-3.5 rounded-xl bg-red-950/20 border border-red-800/40">
                  <div className="text-[11px] text-red-300 font-medium">
                    {isAr ? 'محابس للإغلاق (Valves)' : 'Valves to Close'}
                  </div>
                  <div className="text-lg font-bold text-red-400 mt-0.5 font-mono">
                    {traceResult.summary.valvesToCloseCount}
                  </div>
                </div>

                <div className="p-3.5 rounded-xl bg-amber-950/20 border border-amber-800/40">
                  <div className="text-[11px] text-amber-300 font-medium">
                    {isAr ? 'مناطق الخدمة' : 'Service Zones'}
                  </div>
                  <div className="text-lg font-bold text-amber-400 mt-0.5 font-mono">
                    {traceResult.summary.affectedZonesCount}
                  </div>
                </div>

                <div className="p-3.5 rounded-xl bg-slate-800/60 border border-slate-700/60">
                  <div className="text-[11px] text-slate-400 font-medium">
                    {isAr ? 'زمن المعالجة' : 'Execution Time'}
                  </div>
                  <div className="text-lg font-bold text-emerald-400 mt-0.5 font-mono">
                    {traceResult.summary.executionMs.toFixed(1)} ms
                  </div>
                </div>
              </div>

              {/* Navigation Tabs */}
              <div className="flex items-center justify-between border-b border-slate-800 pb-2">
                <div className="flex items-center gap-2">
                  <button
                    type="button"
                    onClick={() => setActiveTab('valves')}
                    className={`px-3 py-1.5 rounded-lg text-xs font-semibold transition ${
                      activeTab === 'valves'
                        ? 'bg-red-600 text-white shadow'
                        : 'text-slate-400 hover:text-white'
                    }`}
                  >
                    {isAr ? 'محابس الإغلاق المطلوبة' : 'Valves to Close'} ({traceResult.valvesToClose.length})
                  </button>

                  <button
                    type="button"
                    onClick={() => setActiveTab('pipes')}
                    className={`px-3 py-1.5 rounded-lg text-xs font-semibold transition ${
                      activeTab === 'pipes'
                        ? 'bg-sky-600 text-white shadow'
                        : 'text-slate-400 hover:text-white'
                    }`}
                  >
                    {isAr ? 'الأنابيب المتأثرة' : 'Affected Pipes'} ({traceResult.edges.length})
                  </button>

                  <button
                    type="button"
                    onClick={() => setActiveTab('zones')}
                    className={`px-3 py-1.5 rounded-lg text-xs font-semibold transition ${
                      activeTab === 'zones'
                        ? 'bg-amber-600 text-white shadow'
                        : 'text-slate-400 hover:text-white'
                    }`}
                  >
                    {isAr ? 'مناطق الخدمة' : 'Service Zones'} ({traceResult.affectedPolygons.length})
                  </button>
                </div>

                {/* Export Buttons */}
                <div className="flex items-center gap-2">
                  <button
                    type="button"
                    onClick={() => handleExport('PDF')}
                    disabled={isExportingPdf}
                    className="flex items-center gap-1.5 px-3 py-1 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-medium border border-slate-700 transition"
                  >
                    {isExportingPdf ? (
                      <Loader2 className="w-3.5 h-3.5 animate-spin" />
                    ) : (
                      <FileText className="w-3.5 h-3.5 text-rose-400" />
                    )}
                    <span>{isAr ? 'تقرير PDF' : 'PDF Report'}</span>
                  </button>

                  <button
                    type="button"
                    onClick={() => handleExport('CSV')}
                    disabled={isExportingCsv}
                    className="flex items-center gap-1.5 px-3 py-1 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-medium border border-slate-700 transition"
                  >
                    {isExportingCsv ? (
                      <Loader2 className="w-3.5 h-3.5 animate-spin" />
                    ) : (
                      <FileSpreadsheet className="w-3.5 h-3.5 text-emerald-400" />
                    )}
                    <span>{isAr ? 'تصدير CSV' : 'Export CSV'}</span>
                  </button>
                </div>
              </div>

              {/* Tab 1: Valves to Close */}
              {activeTab === 'valves' && (
                <div className="space-y-3">
                  {traceResult.valvesToClose.length === 0 ? (
                    <div className="py-12 text-center text-slate-400 flex flex-col items-center gap-2">
                      <CheckCircle2 className="w-8 h-8 text-emerald-400" />
                      <span className="text-sm font-medium text-slate-300">
                        {isAr ? 'لا توجد محابس إضافية مطلوبة للإغلاق' : 'No boundary valves required to close'}
                      </span>
                      <span className="text-xs text-slate-500">
                        {isAr
                          ? 'القطاع معزول بالفعل أو لا يحتوي على محابس تحكم مباشرة'
                          : 'Segment is either naturally bounded by terminals or existing closed valves.'}
                      </span>
                    </div>
                  ) : (
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                      {traceResult.valvesToClose.map((valve, i) => (
                        <div
                          key={valve.id}
                          className="p-3.5 rounded-xl bg-red-950/20 border border-red-800/40 flex items-center justify-between gap-3 hover:bg-red-950/30 transition"
                        >
                          <div className="space-y-1">
                            <div className="flex items-center gap-2">
                              <span className="w-5 h-5 rounded-full bg-red-500 text-white font-bold text-[10px] flex items-center justify-center">
                                {i + 1}
                              </span>
                              <span className="text-xs font-bold text-white">{valve.name}</span>
                            </div>
                            <div className="text-[11px] text-slate-400 flex items-center gap-3">
                              <span>
                                {isAr ? 'النوع:' : 'Type:'}{' '}
                                <strong className="text-slate-300">{valve.valve_type || 'GATE'}</strong>
                              </span>
                              <span className="font-mono text-[10px] text-slate-400">
                                {valve.coordinates.map((c) => c.toFixed(5)).join(', ')}
                              </span>
                            </div>
                          </div>

                          <button
                            type="button"
                            onClick={() => handleZoomToValve(valve)}
                            className="p-2 rounded-lg bg-red-900/40 hover:bg-red-800/60 text-red-300 border border-red-700/50 text-xs flex items-center gap-1 transition"
                            title={isAr ? 'تكبير وتحديد موقع المحبس' : 'Zoom to valve'}
                          >
                            <Eye className="w-3.5 h-3.5" />
                            <span>{isAr ? 'تكبير' : 'Zoom'}</span>
                          </button>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              )}

              {/* Tab 2: Affected Pipes */}
              {activeTab === 'pipes' && (
                <div className="border border-slate-800 rounded-xl overflow-hidden bg-slate-900/50 max-h-72 overflow-y-auto">
                  <table className="w-full text-left rtl:text-right border-collapse text-xs">
                    <thead>
                      <tr className="border-b border-slate-800 bg-slate-800/50 text-slate-400 font-semibold sticky top-0">
                        <th className="py-2 px-3">#</th>
                        <th className="py-2 px-3">{isAr ? 'اسم الخط / الأنبوب' : 'Pipe Name'}</th>
                        <th className="py-2 px-3">{isAr ? 'القطر' : 'Diameter'}</th>
                        <th className="py-2 px-3">{isAr ? 'المادة' : 'Material'}</th>
                        <th className="py-2 px-3">{isAr ? 'الاتجاه' : 'Direction'}</th>
                        <th className="py-2 px-3">{isAr ? 'الطول' : 'Length'}</th>
                        <th className="py-2 px-3 text-center">{isAr ? 'معاينة' : 'Inspect'}</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-800">
                      {traceResult.edges.map((edge, i) => (
                        <tr key={edge.id} className="hover:bg-slate-800/40 transition">
                          <td className="py-2.5 px-3 font-mono text-slate-400">{i + 1}</td>
                          <td className="py-2.5 px-3 font-medium text-white">{edge.name || `Pipe ${edge.id}`}</td>
                          <td className="py-2.5 px-3 font-mono text-slate-300">
                            {edge.diameter_mm ? `${edge.diameter_mm} mm` : '-'}
                          </td>
                          <td className="py-2.5 px-3 text-slate-400">{edge.material || 'HDPE'}</td>
                          <td className="py-2.5 px-3 font-mono text-[10px] text-slate-400">
                            {edge.direction || 'WITH_DIGITIZED'}
                          </td>
                          <td className="py-2.5 px-3 font-mono text-slate-200 font-bold">
                            {edge.length_m.toFixed(1)} m
                          </td>
                          <td className="py-2.5 px-3 text-center">
                            <button
                              type="button"
                              onClick={() => handleZoomToEdge(edge)}
                              className="p-1 rounded bg-slate-800 hover:bg-slate-700 text-sky-400 transition"
                              title="Zoom"
                            >
                              <Eye className="w-3.5 h-3.5" />
                            </button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}

              {/* Tab 3: Affected Zones */}
              {activeTab === 'zones' && (
                <div className="space-y-3">
                  {traceResult.affectedPolygons.length === 0 ? (
                    <div className="py-12 text-center text-slate-400">
                      <span className="text-sm font-medium text-slate-300">
                        {isAr ? 'لا توجد مناطق خدمة متداخلة مع مسار التتبع' : 'No service zones intersect this trace'}
                      </span>
                    </div>
                  ) : (
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                      {traceResult.affectedPolygons.map((zone) => (
                        <div
                          key={zone.id}
                          className="p-3.5 rounded-xl bg-amber-950/20 border border-amber-800/40 space-y-1"
                        >
                          <div className="font-bold text-white text-xs">{zone.name}</div>
                          <div className="text-[11px] text-slate-400 font-mono">
                            Type: <span className="text-amber-300">{zone.feature_type_code}</span>
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              )}
            </>
          ) : (
            /* Empty State */
            <div className="py-16 text-center text-slate-400 flex flex-col items-center gap-3">
              <Compass className="w-12 h-12 text-slate-600" />
              <div className="max-w-md space-y-1">
                <p className="text-sm font-semibold text-slate-300">
                  {isAr ? 'حدد معلماً على الخريطة لبدء محاكاة التتبع' : 'Select an origin feature on the map to begin'}
                </p>
                <p className="text-xs text-slate-500 leading-relaxed">
                  {isAr
                    ? 'اختر وضع التتبع المطلوب (عزل الأعطال، مع السريان، أو عكس السريان)، ثم اضغط على زر "بدء التتبع" لإظهار المسار والمحابس ومناطق الخدمة المتأثرة'
                    : 'Choose your desired trace mode (Emergency Isolation, Downstream, or Upstream), then click "Run Trace" to calculate network connectivity and valve operations.'}
                </p>
              </div>
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="px-6 py-3 border-t border-slate-800 bg-slate-900/90 flex items-center justify-between text-xs text-slate-400">
          <div>
            {isAr
              ? 'محاكاة هيدروليكية متوافقة مع اشتراطات شركة العاصمة الإدارية للتنمية العمرانية (ACUD)'
              : 'ACUD Utility Network Trace & Spatial Analysis Compliant'}
          </div>
          <button
            onClick={onClose}
            className="px-4 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 transition font-medium"
          >
            {isAr ? 'إغلاق' : 'Close'}
          </button>
        </div>
      </div>
    </div>
  );
};

