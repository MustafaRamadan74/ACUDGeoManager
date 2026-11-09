'use client';

import React, { useState, useEffect } from 'react';
import {
  Activity,
  Ruler,
  Maximize,
  Compass,
  PieChart,
  FileText,
  Download,
  Loader2,
  X,
  AlertCircle,
  CheckCircle2,
  Layers,
  ArrowRight,
  ShieldAlert,
  BarChart3,
  Flame,
  RotateCcw,
} from 'lucide-react';
import * as turf from '@turf/turf';
import { api } from '@/lib/api';
import {
  NetworkDto,
  FeatureTypeDto,
  GeoJsonFeature,
  BufferAnalysisResultDto,
  ServiceAreaResultDto,
  SpatialJoinResultDto,
  NetworkStatisticsDto,
  ReportFormat,
} from '@/lib/types';
import { useToast } from '@/components/Toast';

interface AnalysisModalProps {
  isOpen: boolean;
  onClose: () => void;
  networks: NetworkDto[];
  activeNetworkId: string | null;
  featureTypes: FeatureTypeDto[];
  selectedFeature: GeoJsonFeature | null;
  currentMapCenter: [number, number];
  onSetAnalysisGeometry: (geometry: any | null) => void;
  onStartMeasureDrawing?: (kind: 'LINE' | 'POLYGON') => void;
  drawnMeasureGeometry?: any | null;
  onClearMeasureDrawing?: () => void;
  isAr?: boolean;
}

type AnalysisTab = 'MEASURE' | 'BUFFER' | 'SERVICE_AREA' | 'SPATIAL_JOIN' | 'STATISTICS' | 'REPORTS';

export const AnalysisModal: React.FC<AnalysisModalProps> = ({
  isOpen,
  onClose,
  networks,
  activeNetworkId,
  featureTypes,
  selectedFeature,
  currentMapCenter,
  onSetAnalysisGeometry,
  onStartMeasureDrawing,
  drawnMeasureGeometry,
  onClearMeasureDrawing,
  isAr = true,
}) => {
  const { showSuccess, showError, showInfo } = useToast();
  const [activeTab, setActiveTab] = useState<AnalysisTab>('MEASURE');
  const [selectedNetworkId, setSelectedNetworkId] = useState<string>(
    activeNetworkId || (networks[0]?.id ?? '')
  );

  // Loading states
  const [isLoading, setIsLoading] = useState(false);
  const [isExporting, setIsExporting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // --- 1. Measure State (Turf.js) ---
  const [measureKind, setMeasureKind] = useState<'LINE' | 'POLYGON'>('LINE');
  const [measureResult, setMeasureResult] = useState<{
    distanceM?: number;
    distanceKm?: number;
    areaSqM?: number;
    areaHectares?: number;
    areaSqKm?: number;
    perimeterM?: number;
  } | null>(null);

  // --- 2. Buffer State ---
  const [bufferDistance, setBufferDistance] = useState<number>(50);
  const [bufferResult, setBufferResult] = useState<BufferAnalysisResultDto | null>(null);

  // --- 3. Service Area State ---
  const [serviceRadii, setServiceRadii] = useState<string>('50, 100, 250');
  const [serviceResult, setServiceResult] = useState<ServiceAreaResultDto | null>(null);

  // --- 4. Spatial Join State ---
  const [joinPolygonType, setJoinPolygonType] = useState<string>('URBAN_DISTRICT');
  const [joinTargetType, setJoinTargetType] = useState<string>('WATER_VALVE');
  const [joinResult, setJoinResult] = useState<SpatialJoinResultDto | null>(null);

  // --- 5. Statistics State ---
  const [statsData, setStatsData] = useState<NetworkStatisticsDto | null>(null);

  // Sync active network
  useEffect(() => {
    if (activeNetworkId) {
      setSelectedNetworkId(activeNetworkId);
    }
  }, [activeNetworkId]);

  // Compute Turf measurement when drawn geometry arrives
  useEffect(() => {
    if (!drawnMeasureGeometry) {
      setMeasureResult(null);
      return;
    }

    try {
      if (drawnMeasureGeometry.type === 'LineString') {
        const line = turf.lineString(drawnMeasureGeometry.coordinates);
        const lengthKm = turf.length(line, { units: 'kilometers' });
        const lengthM = lengthKm * 1000;
        setMeasureResult({
          distanceM: Math.round(lengthM * 10) / 10,
          distanceKm: Math.round(lengthKm * 1000) / 1000,
        });
      } else if (drawnMeasureGeometry.type === 'Polygon') {
        const poly = turf.polygon(drawnMeasureGeometry.coordinates);
        const areaSqM = turf.area(poly);
        const areaHectares = areaSqM / 10000;
        const areaSqKm = areaSqM / 1000000;

        // Perimeter
        const line = turf.polygonToLine(poly);
        const perimeterKm = turf.length(line as any, { units: 'kilometers' });

        setMeasureResult({
          areaSqM: Math.round(areaSqM * 10) / 10,
          areaHectares: Math.round(areaHectares * 100) / 100,
          areaSqKm: Math.round(areaSqKm * 10000) / 10000,
          perimeterM: Math.round(perimeterKm * 1000 * 10) / 10,
        });
      }
    } catch (err: any) {
      console.error('Turf calculation failed:', err);
    }
  }, [drawnMeasureGeometry]);

  // Load stats when switching to STATISTICS tab
  useEffect(() => {
    if (activeTab === 'STATISTICS' && selectedNetworkId) {
      loadStatistics();
    }
  }, [activeTab, selectedNetworkId]);

  const loadStatistics = async () => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.getNetworkStatistics(selectedNetworkId);
      setStatsData(data);
    } catch (err: any) {
      setError(err.message || 'Failed to load statistics');
    } finally {
      setIsLoading(false);
    }
  };

  // --- Run Buffer Analysis ---
  const handleRunBuffer = async () => {
    setIsLoading(true);
    setError(null);
    try {
      let geoJson: string | null = null;
      let featureId: string | null = null;

      if (selectedFeature) {
        featureId = selectedFeature.id;
        geoJson = JSON.stringify(selectedFeature.geometry);
      } else {
        // Fallback to center point
        geoJson = JSON.stringify({
          type: 'Point',
          coordinates: [currentMapCenter[0], currentMapCenter[1]],
        });
      }

      const res = await api.runBufferAnalysis({
        geometryGeoJson: geoJson,
        featureId: featureId,
        distanceMeters: bufferDistance,
        networkId: selectedNetworkId,
      });

      setBufferResult(res);

      if (res.bufferPolygonGeoJson) {
        try {
          const parsed = JSON.parse(res.bufferPolygonGeoJson);
          onSetAnalysisGeometry({
            type: 'Feature',
            geometry: parsed,
            properties: { title: `Buffer ${bufferDistance}m` },
          });
        } catch {
          // ignore parse error
        }
      }

      showSuccess(
        isAr
          ? `تم حساب الحرم بنجاح: تم العثور على ${res.intersectingFeaturesCount} معلماً داخل النطاق`
          : `Buffer computed: ${res.intersectingFeaturesCount} intersecting features found.`
      );
    } catch (err: any) {
      setError(err.message || 'Buffer analysis failed');
      showError(err.message || 'Buffer analysis failed');
    } finally {
      setIsLoading(false);
    }
  };

  // --- Run Service Area Analysis ---
  const handleRunServiceArea = async () => {
    setIsLoading(true);
    setError(null);
    try {
      const parsedRadii = serviceRadii
        .split(',')
        .map((s) => parseFloat(s.trim()))
        .filter((n) => !isNaN(n) && n > 0);

      const res = await api.runServiceAreaAnalysis({
        centerLon: currentMapCenter[0],
        centerLat: currentMapCenter[1],
        radiiMeters: parsedRadii.length > 0 ? parsedRadii : [50, 100, 250],
        networkId: selectedNetworkId,
      });

      setServiceResult(res);

      // Render concentric rings on map
      const ringFeatures = res.rings
        .map((r) => {
          try {
            return {
              type: 'Feature',
              geometry: JSON.parse(r.polygonGeoJson),
              properties: { radius: r.radiusMeters },
            };
          } catch {
            return null;
          }
        })
        .filter(Boolean);

      if (ringFeatures.length > 0) {
        onSetAnalysisGeometry({
          type: 'FeatureCollection',
          features: ringFeatures,
        });
      }

      showSuccess(
        isAr
          ? `تم توليد نطاقات الخدمة لـ ${res.rings.length} حلقات بنجاح`
          : `Generated ${res.rings.length} service area rings.`
      );
    } catch (err: any) {
      setError(err.message || 'Service area analysis failed');
      showError(err.message || 'Service area analysis failed');
    } finally {
      setIsLoading(false);
    }
  };

  // --- Run Spatial Join ---
  const handleRunSpatialJoin = async () => {
    setIsLoading(true);
    setError(null);
    try {
      const res = await api.runSpatialJoin({
        polygonTypeCode: joinPolygonType,
        targetTypeCode: joinTargetType,
        networkId: selectedNetworkId,
      });
      setJoinResult(res);
      showSuccess(
        isAr
          ? `تم الربط المكاني: إجمالي ${res.totalContainedCount} عنصراً في ${res.totalPolygons} نطاقاً`
          : `Spatial Join: ${res.totalContainedCount} assets in ${res.totalPolygons} zones.`
      );
    } catch (err: any) {
      setError(err.message || 'Spatial join failed');
      showError(err.message || 'Spatial join failed');
    } finally {
      setIsLoading(false);
    }
  };

  // --- Export Widget Result to CSV or PDF ---
  const handleExportWidget = async (format: ReportFormat) => {
    setIsExporting(true);
    try {
      if (activeTab === 'BUFFER' && bufferResult) {
        const headers = ['Feature ID', 'Layer Type', 'Name', 'Geometry', 'Status', 'Length (m)', 'Area (m²)'];
        const rows = bufferResult.intersectingFeatures.map((f) => [
          f.id,
          f.featureTypeCode,
          f.name,
          f.geometryKind,
          f.status,
          f.lengthMeters ? f.lengthMeters.toFixed(1) : '-',
          f.areaSqM ? f.areaSqM.toFixed(1) : '-',
        ]);

        await api.downloadAnalysisWidgetReport({
          title: `Buffer Analysis (${bufferResult.distanceMeters}m)`,
          analysisType: 'BUFFER',
          description: `Total Features: ${bufferResult.intersectingFeaturesCount} | Total Length: ${bufferResult.totalPipeLengthMeters}m | Total Area: ${bufferResult.totalAreaSqM}m²`,
          headers,
          rows,
          format,
        });
      } else if (activeTab === 'SERVICE_AREA' && serviceResult) {
        const headers = ['Ring Radius (m)', 'Total Assets', 'Valves Count', 'Pipe Length (m)'];
        const rows = serviceResult.rings.map((r) => [
          r.radiusMeters.toString(),
          r.featuresCount.toString(),
          r.valvesCount.toString(),
          r.pipeLengthMeters.toFixed(1),
        ]);

        await api.downloadAnalysisWidgetReport({
          title: 'Service Area Multi-Ring Analysis',
          analysisType: 'SERVICE_AREA',
          description: `Center: (${serviceResult.centerLon.toFixed(5)}, ${serviceResult.centerLat.toFixed(5)})`,
          headers,
          rows,
          format,
        });
      } else if (activeTab === 'SPATIAL_JOIN' && joinResult) {
        const headers = ['Zone Name', 'Zone ID', 'Contained Assets', 'Total Pipe Length (m)'];
        const rows = joinResult.items.map((i) => [
          i.polygonName,
          i.polygonId,
          i.containedCount.toString(),
          i.totalContainedLengthMeters.toFixed(1),
        ]);

        await api.downloadAnalysisWidgetReport({
          title: `Spatial Join: ${joinResult.targetTypeCode} in ${joinResult.polygonTypeCode}`,
          analysisType: 'SPATIAL_JOIN',
          description: `Total Zones: ${joinResult.totalPolygons} | Total Assets: ${joinResult.totalContainedCount}`,
          headers,
          rows,
          format,
        });
      } else if (activeTab === 'MEASURE' && measureResult) {
        const headers = ['Measurement Metric', 'Value'];
        const rows: string[][] = [];
        if (measureResult.distanceM !== undefined) {
          rows.push(['Distance (Meters)', `${measureResult.distanceM} m`]);
          rows.push(['Distance (Kilometers)', `${measureResult.distanceKm} km`]);
        }
        if (measureResult.areaSqM !== undefined) {
          rows.push(['Area (Square Meters)', `${measureResult.areaSqM} m²`]);
          rows.push(['Area (Hectares)', `${measureResult.areaHectares} ha`]);
          rows.push(['Area (Square Kilometers)', `${measureResult.areaSqKm} km²`]);
          rows.push(['Perimeter (Meters)', `${measureResult.perimeterM} m`]);
        }

        await api.downloadAnalysisWidgetReport({
          title: 'Spatial Measurement Report',
          analysisType: 'MEASURE',
          headers,
          rows,
          format,
        });
      }

      showSuccess(isAr ? 'تم تحميل ملف التقرير بنجاح' : 'Report downloaded successfully');
    } catch (err: any) {
      showError(err.message || 'Export failed');
    } finally {
      setIsExporting(false);
    }
  };

  // --- Official System Reports Generator ---
  const handleDownloadSystemReport = async (
    type: 'SUMMARY' | 'VALIDATION' | 'USER_ACTIVITY',
    format: ReportFormat
  ) => {
    setIsExporting(true);
    try {
      if (type === 'SUMMARY') {
        await api.downloadWorkspaceSummaryReport({
          networkId: selectedNetworkId,
          format,
        });
      } else if (type === 'VALIDATION') {
        await api.downloadValidationReport({
          networkId: selectedNetworkId,
          format,
        });
      } else if (type === 'USER_ACTIVITY') {
        await api.downloadUserActivityReport({
          format,
        });
      }
      showSuccess(isAr ? 'تم استخراج التقرير الرسمي بنجاح' : 'Official report generated successfully');
    } catch (err: any) {
      showError(err.message || 'Failed to generate report');
    } finally {
      setIsExporting(false);
    }
  };

  if (!isOpen) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center p-3 sm:p-4 bg-slate-950/80 backdrop-blur-sm animate-in fade-in duration-200"
      dir={isAr ? 'rtl' : 'ltr'}
    >
      <div className="bg-slate-900 border border-slate-800 w-full max-w-4xl max-h-[92vh] rounded-2xl shadow-2xl flex flex-col overflow-hidden text-slate-100">
        {/* Header */}
        <div className="p-4 border-b border-slate-800 flex items-center justify-between bg-slate-950/60">
          <div className="flex items-center gap-2.5">
            <div className="w-9 h-9 rounded-xl bg-sky-500/10 border border-sky-500/30 flex items-center justify-center text-sky-400">
              <Activity className="w-5 h-5" />
            </div>
            <div>
              <h2 className="text-sm sm:text-base font-bold text-white">
                {isAr ? 'منصة التحليلات المكانية والتقارير' : 'Spatial Analysis & Reports Center'}
              </h2>
              <p className="text-xs text-slate-400">
                {isAr
                  ? 'أدوات القياس، الحرم الجغرافي، نطاقات الخدمة، والتقارير التنفيذية'
                  : 'Turf.js measurements, geodesic buffers, service areas, and PDF/CSV reports'}
              </p>
            </div>
          </div>

          <div className="flex items-center gap-3">
            {/* Network Selector */}
            <select
              value={selectedNetworkId}
              onChange={(e) => setSelectedNetworkId(e.target.value)}
              className="bg-slate-800 border border-slate-700 text-xs text-slate-200 rounded-lg px-2.5 py-1.5 focus:outline-none focus:border-sky-500"
            >
              {networks.map((n) => (
                <option key={n.id} value={n.id}>
                  {n.name}
                </option>
              ))}
            </select>

            <button
              onClick={() => {
                onSetAnalysisGeometry(null);
                onClose();
              }}
              className="p-1.5 rounded-lg text-slate-400 hover:text-white hover:bg-slate-800 transition"
            >
              <X className="w-5 h-5" />
            </button>
          </div>
        </div>

        {/* Tab Navigation */}
        <div className="flex items-center gap-1 p-2 border-b border-slate-800 bg-slate-950/40 overflow-x-auto select-none">
          <button
            onClick={() => setActiveTab('MEASURE')}
            className={`px-3 py-1.5 rounded-lg text-xs font-semibold flex items-center gap-1.5 transition shrink-0 ${
              activeTab === 'MEASURE'
                ? 'bg-sky-600 text-white shadow'
                : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/60'
            }`}
          >
            <Ruler className="w-3.5 h-3.5" />
            <span>{isAr ? 'القياسات الميدانية' : 'Measure Tool'}</span>
          </button>

          <button
            onClick={() => setActiveTab('BUFFER')}
            className={`px-3 py-1.5 rounded-lg text-xs font-semibold flex items-center gap-1.5 transition shrink-0 ${
              activeTab === 'BUFFER'
                ? 'bg-sky-600 text-white shadow'
                : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/60'
            }`}
          >
            <Compass className="w-3.5 h-3.5" />
            <span>{isAr ? 'تحليل الحرم (Buffer)' : 'Geodesic Buffer'}</span>
          </button>

          <button
            onClick={() => setActiveTab('SERVICE_AREA')}
            className={`px-3 py-1.5 rounded-lg text-xs font-semibold flex items-center gap-1.5 transition shrink-0 ${
              activeTab === 'SERVICE_AREA'
                ? 'bg-sky-600 text-white shadow'
                : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/60'
            }`}
          >
            <Maximize className="w-3.5 h-3.5" />
            <span>{isAr ? 'نطاقات الخدمة' : 'Service Areas'}</span>
          </button>

          <button
            onClick={() => setActiveTab('SPATIAL_JOIN')}
            className={`px-3 py-1.5 rounded-lg text-xs font-semibold flex items-center gap-1.5 transition shrink-0 ${
              activeTab === 'SPATIAL_JOIN'
                ? 'bg-sky-600 text-white shadow'
                : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/60'
            }`}
          >
            <Layers className="w-3.5 h-3.5" />
            <span>{isAr ? 'الربط المكاني' : 'Spatial Join'}</span>
          </button>

          <button
            onClick={() => setActiveTab('STATISTICS')}
            className={`px-3 py-1.5 rounded-lg text-xs font-semibold flex items-center gap-1.5 transition shrink-0 ${
              activeTab === 'STATISTICS'
                ? 'bg-sky-600 text-white shadow'
                : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/60'
            }`}
          >
            <BarChart3 className="w-3.5 h-3.5" />
            <span>{isAr ? 'الإحصائيات والرسوم' : 'Statistics'}</span>
          </button>

          <button
            onClick={() => setActiveTab('REPORTS')}
            className={`px-3 py-1.5 rounded-lg text-xs font-semibold flex items-center gap-1.5 transition shrink-0 ${
              activeTab === 'REPORTS'
                ? 'bg-emerald-600 text-white shadow'
                : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/60'
            }`}
          >
            <FileText className="w-3.5 h-3.5" />
            <span>{isAr ? 'التقارير الرسمية' : 'PDF Reports'}</span>
          </button>
        </div>

        {/* Modal Body */}
        <div className="flex-1 overflow-y-auto p-4 sm:p-5 space-y-4">
          {error && (
            <div className="p-3 rounded-xl bg-rose-950/40 border border-rose-500/50 text-rose-300 text-xs flex items-center gap-2">
              <AlertCircle className="w-4 h-4 shrink-0" />
              <span>{error}</span>
            </div>
          )}

          {/* TAB 1: MEASURE TOOL */}
          {activeTab === 'MEASURE' && (
            <div className="space-y-4">
              <div className="p-3.5 rounded-xl bg-slate-800/40 border border-slate-700/60 flex items-center justify-between">
                <div>
                  <h3 className="text-xs font-bold text-white mb-1">
                    {isAr ? 'حساب المسافات والمساحات (Turf.js)' : 'Geodesic Measurement Tool'}
                  </h3>
                  <p className="text-[11px] text-slate-400">
                    {isAr
                      ? 'اختر نوع القياس ثم ارسم المسار أو المضلع على الخريطة لحساب الأبعاد بدقة هندسية'
                      : 'Select measure mode and draw path or polygon on map for geodesic calculation'}
                  </p>
                </div>

                <div className="flex items-center gap-2">
                  <button
                    type="button"
                    onClick={() => {
                      setMeasureKind('LINE');
                      if (onStartMeasureDrawing) onStartMeasureDrawing('LINE');
                    }}
                    className={`px-3 py-1.5 rounded-lg text-xs font-medium border transition flex items-center gap-1.5 ${
                      measureKind === 'LINE'
                        ? 'bg-sky-600 text-white border-sky-400 shadow'
                        : 'bg-slate-800 text-slate-300 border-slate-700 hover:bg-slate-700'
                    }`}
                  >
                    <Ruler className="w-3.5 h-3.5" />
                    <span>{isAr ? 'مسافة (خط)' : 'Distance (Line)'}</span>
                  </button>

                  <button
                    type="button"
                    onClick={() => {
                      setMeasureKind('POLYGON');
                      if (onStartMeasureDrawing) onStartMeasureDrawing('POLYGON');
                    }}
                    className={`px-3 py-1.5 rounded-lg text-xs font-medium border transition flex items-center gap-1.5 ${
                      measureKind === 'POLYGON'
                        ? 'bg-sky-600 text-white border-sky-400 shadow'
                        : 'bg-slate-800 text-slate-300 border-slate-700 hover:bg-slate-700'
                    }`}
                  >
                    <Maximize className="w-3.5 h-3.5" />
                    <span>{isAr ? 'مساحة (مضلع)' : 'Area (Polygon)'}</span>
                  </button>

                  {measureResult && onClearMeasureDrawing && (
                    <button
                      type="button"
                      onClick={() => {
                        onClearMeasureDrawing();
                        setMeasureResult(null);
                      }}
                      className="p-1.5 rounded-lg text-slate-400 hover:text-rose-400 hover:bg-slate-800 transition"
                      title={isAr ? 'مسح الرسم والقياس' : 'Clear Measurement'}
                    >
                      <RotateCcw className="w-4 h-4" />
                    </button>
                  )}
                </div>
              </div>

              {measureResult ? (
                <div className="space-y-3">
                  <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
                    {measureResult.distanceM !== undefined && (
                      <>
                        <div className="p-3 rounded-xl bg-slate-800/60 border border-slate-700/80">
                          <div className="text-[10px] text-slate-400 font-medium">
                            {isAr ? 'المسافة الإجمالية (متر)' : 'Total Distance (m)'}
                          </div>
                          <div className="text-lg font-bold text-sky-400 font-mono mt-1">
                            {measureResult.distanceM.toLocaleString()} m
                          </div>
                        </div>

                        <div className="p-3 rounded-xl bg-slate-800/60 border border-slate-700/80">
                          <div className="text-[10px] text-slate-400 font-medium">
                            {isAr ? 'المسافة (كيلومتر)' : 'Distance (km)'}
                          </div>
                          <div className="text-lg font-bold text-sky-400 font-mono mt-1">
                            {measureResult.distanceKm} km
                          </div>
                        </div>
                      </>
                    )}

                    {measureResult.areaSqM !== undefined && (
                      <>
                        <div className="p-3 rounded-xl bg-slate-800/60 border border-slate-700/80">
                          <div className="text-[10px] text-slate-400 font-medium">
                            {isAr ? 'المساحة (متر مربع)' : 'Area (m²)'}
                          </div>
                          <div className="text-lg font-bold text-amber-400 font-mono mt-1">
                            {measureResult.areaSqM.toLocaleString()} m²
                          </div>
                        </div>

                        <div className="p-3 rounded-xl bg-slate-800/60 border border-slate-700/80">
                          <div className="text-[10px] text-slate-400 font-medium">
                            {isAr ? 'المساحة (هكتار)' : 'Area (Hectares)'}
                          </div>
                          <div className="text-lg font-bold text-amber-400 font-mono mt-1">
                            {measureResult.areaHectares} ha
                          </div>
                        </div>

                        <div className="p-3 rounded-xl bg-slate-800/60 border border-slate-700/80">
                          <div className="text-[10px] text-slate-400 font-medium">
                            {isAr ? 'المساحة (كم²)' : 'Area (km²)'}
                          </div>
                          <div className="text-lg font-bold text-amber-400 font-mono mt-1">
                            {measureResult.areaSqKm} km²
                          </div>
                        </div>

                        <div className="p-3 rounded-xl bg-slate-800/60 border border-slate-700/80">
                          <div className="text-[10px] text-slate-400 font-medium">
                            {isAr ? 'المحيط الخارجي' : 'Perimeter'}
                          </div>
                          <div className="text-lg font-bold text-teal-400 font-mono mt-1">
                            {measureResult.perimeterM?.toLocaleString()} m
                          </div>
                        </div>
                      </>
                    )}
                  </div>

                  <div className="flex justify-end gap-2 pt-2 border-t border-slate-800">
                    <button
                      type="button"
                      disabled={isExporting}
                      onClick={() => handleExportWidget('Csv')}
                      className="px-3 py-1.5 rounded-lg text-xs font-semibold bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 transition flex items-center gap-1.5"
                    >
                      <Download className="w-3.5 h-3.5 text-emerald-400" />
                      <span>{isAr ? 'تصدير CSV' : 'Export CSV'}</span>
                    </button>
                    <button
                      type="button"
                      disabled={isExporting}
                      onClick={() => handleExportWidget('Pdf')}
                      className="px-3 py-1.5 rounded-lg text-xs font-semibold bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 transition flex items-center gap-1.5"
                    >
                      <FileText className="w-3.5 h-3.5 text-rose-400" />
                      <span>{isAr ? 'تقرير PDF' : 'Export PDF'}</span>
                    </button>
                  </div>
                </div>
              ) : (
                <div className="p-8 text-center text-slate-500 border border-dashed border-slate-800 rounded-xl">
                  <Ruler className="w-8 h-8 mx-auto mb-2 text-slate-600 opacity-60" />
                  <p className="text-xs">
                    {isAr
                      ? 'اضغط على زر المسافة أو المساحة ثم ارسم على الخريطة مباشرة لتظهر النتائج هنا'
                      : 'Select Distance or Area above and draw on the map to view instant geodesic metrics'}
                  </p>
                </div>
              )}
            </div>
          )}

          {/* TAB 2: BUFFER ANALYSIS */}
          {activeTab === 'BUFFER' && (
            <div className="space-y-4">
              <div className="p-3.5 rounded-xl bg-slate-800/40 border border-slate-700/60 flex flex-wrap items-center justify-between gap-3">
                <div>
                  <h3 className="text-xs font-bold text-white mb-1">
                    {isAr ? 'تحليل الحرم المكاني المتري (Geodesic Buffer)' : 'Server-Side Geodesic Buffer'}
                  </h3>
                  <p className="text-[11px] text-slate-400">
                    {selectedFeature
                      ? `${isAr ? 'الأصل المحدد:' : 'Target Feature:'} ${selectedFeature.properties.name} (${selectedFeature.properties.featureTypeCode})`
                      : isAr
                      ? 'سيتم إنشاء الحرم حول نقطة مركز الخريطة الحالية أو الأصل المحدد'
                      : 'Buffer will be generated around current center or selected feature'}
                  </p>
                </div>

                <div className="flex items-center gap-2">
                  <div className="flex items-center gap-1.5 bg-slate-900 border border-slate-700 rounded-lg px-2.5 py-1">
                    <span className="text-xs text-slate-400">{isAr ? 'نصف القطر:' : 'Radius:'}</span>
                    <input
                      type="number"
                      min={5}
                      max={10000}
                      step={10}
                      value={bufferDistance}
                      onChange={(e) => setBufferDistance(Number(e.target.value))}
                      className="w-16 bg-transparent text-xs text-sky-400 font-bold focus:outline-none"
                    />
                    <span className="text-xs text-slate-400">{isAr ? 'متر' : 'm'}</span>
                  </div>

                  <button
                    type="button"
                    disabled={isLoading}
                    onClick={handleRunBuffer}
                    className="px-4 py-1.5 rounded-lg text-xs font-bold bg-sky-600 hover:bg-sky-500 text-white shadow transition flex items-center gap-1.5 disabled:opacity-50"
                  >
                    {isLoading ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Compass className="w-3.5 h-3.5" />}
                    <span>{isAr ? 'تنفيذ التحليل' : 'Execute Buffer'}</span>
                  </button>
                </div>
              </div>

              {bufferResult && (
                <div className="space-y-3">
                  <div className="grid grid-cols-3 gap-3">
                    <div className="p-3 rounded-xl bg-slate-800/60 border border-slate-700/80">
                      <div className="text-[10px] text-slate-400 font-medium">
                        {isAr ? 'المعالم المتقاطعة' : 'Intersecting Assets'}
                      </div>
                      <div className="text-xl font-bold text-sky-400 font-mono mt-1">
                        {bufferResult.intersectingFeaturesCount}
                      </div>
                    </div>

                    <div className="p-3 rounded-xl bg-slate-800/60 border border-slate-700/80">
                      <div className="text-[10px] text-slate-400 font-medium">
                        {isAr ? 'طول الأنابيب المتقاطعة' : 'Intersecting Pipe Length'}
                      </div>
                      <div className="text-xl font-bold text-teal-400 font-mono mt-1">
                        {bufferResult.totalPipeLengthMeters} m
                      </div>
                    </div>

                    <div className="p-3 rounded-xl bg-slate-800/60 border border-slate-700/80">
                      <div className="text-[10px] text-slate-400 font-medium">
                        {isAr ? 'مساحة الحرم' : 'Buffer Footprint'}
                      </div>
                      <div className="text-xl font-bold text-amber-400 font-mono mt-1">
                        {bufferResult.totalAreaSqM} m²
                      </div>
                    </div>
                  </div>

                  {/* Intersecting Assets Table */}
                  <div className="border border-slate-800 rounded-xl overflow-hidden">
                    <div className="p-2.5 bg-slate-800/70 border-b border-slate-800 text-xs font-bold flex items-center justify-between">
                      <span>{isAr ? 'المعالم الواقعة داخل نطاق الحرم' : 'Assets Inside Buffer Zone'}</span>
                      <span className="text-[11px] text-slate-400">
                        {bufferResult.intersectingFeatures.length} {isAr ? 'عنصر' : 'items'}
                      </span>
                    </div>

                    <div className="max-h-56 overflow-y-auto">
                      <table className="w-full text-right rtl:text-right ltr:text-left text-xs">
                        <thead className="bg-slate-900/80 text-slate-400 text-[11px] sticky top-0">
                          <tr>
                            <th className="p-2">الاسم (Asset)</th>
                            <th className="p-2">النوع (Type)</th>
                            <th className="p-2">الشكل (Kind)</th>
                            <th className="p-2">الحالة (Status)</th>
                            <th className="p-2">الطول / المساحة</th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-slate-800/60">
                          {bufferResult.intersectingFeatures.map((f) => (
                            <tr key={f.id} className="hover:bg-slate-800/40">
                              <td className="p-2 font-medium text-white">{f.name}</td>
                              <td className="p-2 text-slate-400 font-mono text-[11px]">{f.featureTypeCode}</td>
                              <td className="p-2 text-slate-400">{f.geometryKind}</td>
                              <td className="p-2">
                                <span className="px-1.5 py-0.5 rounded text-[10px] font-semibold bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
                                  {f.status}
                                </span>
                              </td>
                              <td className="p-2 font-mono text-[11px] text-sky-400">
                                {f.lengthMeters ? `${f.lengthMeters.toFixed(1)} m` : f.areaSqM ? `${f.areaSqM.toFixed(1)} m²` : '-'}
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </div>

                  <div className="flex justify-end gap-2 pt-2 border-t border-slate-800">
                    <button
                      type="button"
                      disabled={isExporting}
                      onClick={() => handleExportWidget('Csv')}
                      className="px-3 py-1.5 rounded-lg text-xs font-semibold bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 transition flex items-center gap-1.5"
                    >
                      <Download className="w-3.5 h-3.5 text-emerald-400" />
                      <span>{isAr ? 'تصدير CSV' : 'Export CSV'}</span>
                    </button>
                    <button
                      type="button"
                      disabled={isExporting}
                      onClick={() => handleExportWidget('Pdf')}
                      className="px-3 py-1.5 rounded-lg text-xs font-semibold bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 transition flex items-center gap-1.5"
                    >
                      <FileText className="w-3.5 h-3.5 text-rose-400" />
                      <span>{isAr ? 'تقرير PDF' : 'Export PDF'}</span>
                    </button>
                  </div>
                </div>
              )}
            </div>
          )}

          {/* TAB 3: SERVICE AREA */}
          {activeTab === 'SERVICE_AREA' && (
            <div className="space-y-4">
              <div className="p-3.5 rounded-xl bg-slate-800/40 border border-slate-700/60 flex flex-wrap items-center justify-between gap-3">
                <div>
                  <h3 className="text-xs font-bold text-white mb-1">
                    {isAr ? 'تحليل نطاقات الخدمة متعددة الحلقات (Multi-Ring Service Area)' : 'Multi-Ring Service Area'}
                  </h3>
                  <p className="text-[11px] text-slate-400">
                    {isAr
                      ? 'إنشاء دوائر تركيز مكانية متحدة المركز لقياس الأثر وتغطية المحابس والأنابيب'
                      : 'Generate concentric geodesic buffer rings around center point to assess facility reach'}
                  </p>
                </div>

                <div className="flex items-center gap-2">
                  <div className="flex items-center gap-1.5 bg-slate-900 border border-slate-700 rounded-lg px-2.5 py-1">
                    <span className="text-xs text-slate-400">{isAr ? 'الحلقات (متر):' : 'Radii (m):'}</span>
                    <input
                      type="text"
                      value={serviceRadii}
                      onChange={(e) => setServiceRadii(e.target.value)}
                      placeholder="50, 100, 250"
                      className="w-28 bg-transparent text-xs text-sky-400 font-bold focus:outline-none"
                    />
                  </div>

                  <button
                    type="button"
                    disabled={isLoading}
                    onClick={handleRunServiceArea}
                    className="px-4 py-1.5 rounded-lg text-xs font-bold bg-sky-600 hover:bg-sky-500 text-white shadow transition flex items-center gap-1.5 disabled:opacity-50"
                  >
                    {isLoading ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Maximize className="w-3.5 h-3.5" />}
                    <span>{isAr ? 'توليد النطاقات' : 'Generate Rings'}</span>
                  </button>
                </div>
              </div>

              {serviceResult && (
                <div className="space-y-3">
                  <div className="border border-slate-800 rounded-xl overflow-hidden">
                    <table className="w-full text-right rtl:text-right ltr:text-left text-xs">
                      <thead className="bg-slate-800/80 text-slate-300 font-bold">
                        <tr>
                          <th className="p-3">{isAr ? 'نصف القطر (متر)' : 'Radius (m)'}</th>
                          <th className="p-3">{isAr ? 'إجمالي الأصول' : 'Total Assets'}</th>
                          <th className="p-3">{isAr ? 'عدد المحابس' : 'Valves Count'}</th>
                          <th className="p-3">{isAr ? 'أطوال الخطوط (متر)' : 'Pipe Length (m)'}</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-slate-800/60">
                        {serviceResult.rings.map((ring, idx) => (
                          <tr key={idx} className="hover:bg-slate-800/30">
                            <td className="p-3 font-mono font-bold text-sky-400">Ø {ring.radiusMeters} m</td>
                            <td className="p-3 font-bold text-white">{ring.featuresCount}</td>
                            <td className="p-3 text-amber-400 font-semibold">{ring.valvesCount}</td>
                            <td className="p-3 font-mono text-teal-400">{ring.pipeLengthMeters} m</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>

                  <div className="flex justify-end gap-2 pt-2 border-t border-slate-800">
                    <button
                      type="button"
                      disabled={isExporting}
                      onClick={() => handleExportWidget('Csv')}
                      className="px-3 py-1.5 rounded-lg text-xs font-semibold bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 transition flex items-center gap-1.5"
                    >
                      <Download className="w-3.5 h-3.5 text-emerald-400" />
                      <span>{isAr ? 'تصدير CSV' : 'Export CSV'}</span>
                    </button>
                    <button
                      type="button"
                      disabled={isExporting}
                      onClick={() => handleExportWidget('Pdf')}
                      className="px-3 py-1.5 rounded-lg text-xs font-semibold bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 transition flex items-center gap-1.5"
                    >
                      <FileText className="w-3.5 h-3.5 text-rose-400" />
                      <span>{isAr ? 'تقرير PDF' : 'Export PDF'}</span>
                    </button>
                  </div>
                </div>
              )}
            </div>
          )}

          {/* TAB 4: SPATIAL JOIN */}
          {activeTab === 'SPATIAL_JOIN' && (
            <div className="space-y-4">
              <div className="p-3.5 rounded-xl bg-slate-800/40 border border-slate-700/60 flex flex-wrap items-center justify-between gap-3">
                <div>
                  <h3 className="text-xs font-bold text-white mb-1">
                    {isAr ? 'الربط والعد المكاني (Spatial Join Count)' : 'Spatial Join & Aggregation'}
                  </h3>
                  <p className="text-[11px] text-slate-400">
                    {isAr
                      ? 'إحصاء عدد وأطوال المعالم الفرعية الواقعة داخل كل مضلع مكاني (أحياء، مناطق خدمة)'
                      : 'Aggregate contained asset counts and lengths within boundary polygons'}
                  </p>
                </div>

                <div className="flex items-center gap-2">
                  <div className="flex items-center gap-2">
                    <select
                      value={joinPolygonType}
                      onChange={(e) => setJoinPolygonType(e.target.value)}
                      className="bg-slate-800 border border-slate-700 text-xs text-slate-200 rounded-lg px-2.5 py-1.5"
                    >
                      {featureTypes
                        .filter((ft) => ft.geometryKind === 'POLYGON')
                        .map((ft) => (
                          <option key={ft.code} value={ft.code}>
                            {ft.displayName}
                          </option>
                        ))}
                    </select>

                    <span className="text-slate-500 font-bold">⇄</span>

                    <select
                      value={joinTargetType}
                      onChange={(e) => setJoinTargetType(e.target.value)}
                      className="bg-slate-800 border border-slate-700 text-xs text-slate-200 rounded-lg px-2.5 py-1.5"
                    >
                      {featureTypes.map((ft) => (
                        <option key={ft.code} value={ft.code}>
                          {ft.displayName}
                        </option>
                      ))}
                    </select>
                  </div>

                  <button
                    type="button"
                    disabled={isLoading}
                    onClick={handleRunSpatialJoin}
                    className="px-4 py-1.5 rounded-lg text-xs font-bold bg-sky-600 hover:bg-sky-500 text-white shadow transition flex items-center gap-1.5 disabled:opacity-50"
                  >
                    {isLoading ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Layers className="w-3.5 h-3.5" />}
                    <span>{isAr ? 'حساب الربط' : 'Execute Join'}</span>
                  </button>
                </div>
              </div>

              {joinResult && (
                <div className="space-y-3">
                  <div className="grid grid-cols-2 gap-3">
                    <div className="p-3 rounded-xl bg-slate-800/60 border border-slate-700/80">
                      <div className="text-[10px] text-slate-400 font-medium">
                        {isAr ? 'إجمالي المناطق والمضلعات' : 'Boundary Polygons'}
                      </div>
                      <div className="text-xl font-bold text-sky-400 font-mono mt-1">
                        {joinResult.totalPolygons}
                      </div>
                    </div>

                    <div className="p-3 rounded-xl bg-slate-800/60 border border-slate-700/80">
                      <div className="text-[10px] text-slate-400 font-medium">
                        {isAr ? 'إجمالي الأصول المحتواة' : 'Total Contained Assets'}
                      </div>
                      <div className="text-xl font-bold text-emerald-400 font-mono mt-1">
                        {joinResult.totalContainedCount}
                      </div>
                    </div>
                  </div>

                  <div className="border border-slate-800 rounded-xl overflow-hidden max-h-56 overflow-y-auto">
                    <table className="w-full text-right rtl:text-right ltr:text-left text-xs">
                      <thead className="bg-slate-800/80 text-slate-300 font-bold sticky top-0">
                        <tr>
                          <th className="p-2.5">{isAr ? 'اسم النطاق / المنطقة' : 'Zone Name'}</th>
                          <th className="p-2.5">{isAr ? 'العدد المحتوي' : 'Contained Assets'}</th>
                          <th className="p-2.5">{isAr ? 'إجمالي الأطوال' : 'Total Length'}</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-slate-800/60">
                        {joinResult.items.map((item) => (
                          <tr key={item.polygonId} className="hover:bg-slate-800/40">
                            <td className="p-2.5 font-medium text-white">{item.polygonName}</td>
                            <td className="p-2.5 font-bold font-mono text-emerald-400">{item.containedCount}</td>
                            <td className="p-2.5 font-mono text-slate-400">
                              {item.totalContainedLengthMeters > 0 ? `${item.totalContainedLengthMeters} m` : '-'}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>

                  <div className="flex justify-end gap-2 pt-2 border-t border-slate-800">
                    <button
                      type="button"
                      disabled={isExporting}
                      onClick={() => handleExportWidget('Csv')}
                      className="px-3 py-1.5 rounded-lg text-xs font-semibold bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 transition flex items-center gap-1.5"
                    >
                      <Download className="w-3.5 h-3.5 text-emerald-400" />
                      <span>{isAr ? 'تصدير CSV' : 'Export CSV'}</span>
                    </button>
                    <button
                      type="button"
                      disabled={isExporting}
                      onClick={() => handleExportWidget('Pdf')}
                      className="px-3 py-1.5 rounded-lg text-xs font-semibold bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 transition flex items-center gap-1.5"
                    >
                      <FileText className="w-3.5 h-3.5 text-rose-400" />
                      <span>{isAr ? 'تقرير PDF' : 'Export PDF'}</span>
                    </button>
                  </div>
                </div>
              )}
            </div>
          )}

          {/* TAB 5: STATISTICS & CHARTS */}
          {activeTab === 'STATISTICS' && (
            <div className="space-y-4">
              {isLoading ? (
                <div className="p-12 text-center text-slate-400">
                  <Loader2 className="w-8 h-8 animate-spin mx-auto text-sky-400 mb-2" />
                  <p className="text-xs">{isAr ? 'جارٍ تجميع إحصائيات الشبكة...' : 'Aggregating network statistics...'}</p>
                </div>
              ) : statsData ? (
                <div className="space-y-4">
                  <div className="grid grid-cols-3 gap-3">
                    <div className="p-3.5 rounded-xl bg-slate-800/60 border border-slate-700/80">
                      <div className="text-[10px] text-slate-400 font-medium">
                        {isAr ? 'إجمالي أصول الشبكة' : 'Total Assets'}
                      </div>
                      <div className="text-2xl font-bold text-sky-400 font-mono mt-1">
                        {statsData.totalFeatures}
                      </div>
                    </div>

                    <div className="p-3.5 rounded-xl bg-slate-800/60 border border-slate-700/80">
                      <div className="text-[10px] text-slate-400 font-medium">
                        {isAr ? 'إجمالي أطوال الخطوط' : 'Total Pipe Length'}
                      </div>
                      <div className="text-2xl font-bold text-teal-400 font-mono mt-1">
                        {statsData.totalPipeLengthMeters.toLocaleString()} m
                      </div>
                    </div>

                    <div className="p-3.5 rounded-xl bg-slate-800/60 border border-slate-700/80">
                      <div className="text-[10px] text-slate-400 font-medium">
                        {isAr ? 'إجمالي مساحات المضلعات' : 'Total Area'}
                      </div>
                      <div className="text-2xl font-bold text-amber-400 font-mono mt-1">
                        {statsData.totalAreaSqM.toLocaleString()} m²
                      </div>
                    </div>
                  </div>

                  {/* Distribution by Feature Type */}
                  <div className="p-3.5 rounded-xl bg-slate-800/40 border border-slate-700/60 space-y-2">
                    <h4 className="text-xs font-bold text-slate-200">
                      {isAr ? 'توزيع الأصول حسب الطبقة الجغرافية' : 'Distribution by Layer Type'}
                    </h4>
                    <div className="space-y-1.5">
                      {statsData.byType.map((t) => {
                        const pct = statsData.totalFeatures > 0 ? (t.count / statsData.totalFeatures) * 100 : 0;
                        return (
                          <div key={t.featureTypeCode} className="space-y-0.5">
                            <div className="flex items-center justify-between text-[11px]">
                              <span className="font-medium text-slate-300">{t.displayName}</span>
                              <span className="font-mono text-slate-400">
                                {t.count} ({pct.toFixed(1)}%)
                              </span>
                            </div>
                            <div className="w-full bg-slate-800 h-2 rounded-full overflow-hidden">
                              <div
                                className="bg-sky-500 h-full rounded-full transition-all duration-300"
                                style={{ width: `${pct}%` }}
                              />
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  </div>

                  {/* Pipes by Diameter & Status */}
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                    <div className="p-3.5 rounded-xl bg-slate-800/40 border border-slate-700/60 space-y-2">
                      <h4 className="text-xs font-bold text-slate-200">
                        {isAr ? 'أقطار الأنابيب (Pipes by Diameter)' : 'Pipes by Diameter'}
                      </h4>
                      <div className="space-y-1 max-h-36 overflow-y-auto">
                        {statsData.pipesByDiameter.map((p, idx) => (
                          <div key={idx} className="flex items-center justify-between text-[11px] py-0.5 border-b border-slate-800/60">
                            <span className="font-mono text-white">
                              {p.diameterMm > 0 ? `Ø ${p.diameterMm} mm` : 'غير محدد (Unspecified)'}
                            </span>
                            <span className="font-mono text-teal-400 font-bold">
                              {p.count} seg ({p.totalLengthMeters} m)
                            </span>
                          </div>
                        ))}
                      </div>
                    </div>

                    <div className="p-3.5 rounded-xl bg-slate-800/40 border border-slate-700/60 space-y-2">
                      <h4 className="text-xs font-bold text-slate-200">
                        {isAr ? 'حالة التشغيل (Status Breakdown)' : 'Asset Status Breakdown'}
                      </h4>
                      <div className="space-y-1.5">
                        {statsData.byStatus.map((s, idx) => (
                          <div key={idx} className="flex items-center justify-between text-[11px] py-1 border-b border-slate-800/60">
                            <span className="font-semibold text-slate-300">{s.status}</span>
                            <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-sky-500/10 text-sky-400">
                              {s.count}
                            </span>
                          </div>
                        ))}
                      </div>
                    </div>
                  </div>

                  <div className="flex justify-end gap-2 pt-2 border-t border-slate-800">
                    <button
                      type="button"
                      disabled={isExporting}
                      onClick={() => handleDownloadSystemReport('SUMMARY', 'Csv')}
                      className="px-3 py-1.5 rounded-lg text-xs font-semibold bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 transition flex items-center gap-1.5"
                    >
                      <Download className="w-3.5 h-3.5 text-emerald-400" />
                      <span>{isAr ? 'تصدير ملخص CSV' : 'Export Summary CSV'}</span>
                    </button>
                    <button
                      type="button"
                      disabled={isExporting}
                      onClick={() => handleDownloadSystemReport('SUMMARY', 'Pdf')}
                      className="px-3 py-1.5 rounded-lg text-xs font-semibold bg-sky-600 hover:bg-sky-500 text-white shadow transition flex items-center gap-1.5"
                    >
                      <FileText className="w-3.5 h-3.5" />
                      <span>{isAr ? 'تحميل تقرير الملخص التنفيذي PDF' : 'Download Executive PDF'}</span>
                    </button>
                  </div>
                </div>
              ) : null}
            </div>
          )}

          {/* TAB 6: OFFICIAL REPORTS */}
          {activeTab === 'REPORTS' && (
            <div className="space-y-4">
              <div className="p-3.5 rounded-xl bg-slate-800/40 border border-slate-700/60">
                <h3 className="text-xs font-bold text-white mb-1">
                  {isAr ? 'مركز إصدار التقارير الهندسية والتنفيذية الرسمية' : 'Executive PDF & CSV Reports Center'}
                </h3>
                <p className="text-[11px] text-slate-400">
                  {isAr
                    ? 'تقارير رسمية معتمدة تدعم اللغتين العربية والإنجليزية صادرة من محرك QuestPDF مع ترويسة العاصمة الإدارية'
                    : 'Official bilingual reports generated by server-side QuestPDF engine with ACUD branding'}
                </p>
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                {/* 1. Workspace Summary Report */}
                <div className="p-4 rounded-xl bg-slate-800/50 border border-slate-700/80 flex flex-col justify-between">
                  <div>
                    <div className="w-8 h-8 rounded-lg bg-sky-500/10 border border-sky-500/30 flex items-center justify-center text-sky-400 mb-2">
                      <PieChart className="w-4 h-4" />
                    </div>
                    <h4 className="text-xs font-bold text-white mb-1">
                      {isAr ? 'تقرير ملخص شبكة المرافق' : 'Workspace Summary Report'}
                    </h4>
                    <p className="text-[11px] text-slate-400">
                      {isAr
                        ? 'إجمالي الأصول، أطوال الخطوط، المساحات، والأقطار'
                        : 'Total assets, pipe lengths, footprint areas, and diameter breakdowns'}
                    </p>
                  </div>

                  <div className="flex items-center gap-2 mt-4 pt-3 border-t border-slate-800">
                    <button
                      type="button"
                      disabled={isExporting}
                      onClick={() => handleDownloadSystemReport('SUMMARY', 'Pdf')}
                      className="flex-1 py-1.5 rounded-lg text-xs font-bold bg-sky-600 hover:bg-sky-500 text-white transition flex items-center justify-center gap-1"
                    >
                      <FileText className="w-3.5 h-3.5" />
                      <span>PDF</span>
                    </button>
                    <button
                      type="button"
                      disabled={isExporting}
                      onClick={() => handleDownloadSystemReport('SUMMARY', 'Csv')}
                      className="px-3 py-1.5 rounded-lg text-xs font-semibold bg-slate-800 hover:bg-slate-700 text-slate-300 border border-slate-700 transition"
                    >
                      CSV
                    </button>
                  </div>
                </div>

                {/* 2. Validation Report */}
                <div className="p-4 rounded-xl bg-slate-800/50 border border-slate-700/80 flex flex-col justify-between">
                  <div>
                    <div className="w-8 h-8 rounded-lg bg-amber-500/10 border border-amber-500/30 flex items-center justify-center text-amber-400 mb-2">
                      <ShieldAlert className="w-4 h-4" />
                    </div>
                    <h4 className="text-xs font-bold text-white mb-1">
                      {isAr ? 'تقرير التدقيق الطوبولوجي' : 'Topology Validation Report'}
                    </h4>
                    <p className="text-[11px] text-slate-400">
                      {isAr
                        ? 'قائمة الأخطاء والتحذيرات الطوبولوجية والمكانية المكتشفة'
                        : 'Audit of all detected topology errors, missing sources, and orphan valves'}
                    </p>
                  </div>

                  <div className="flex items-center gap-2 mt-4 pt-3 border-t border-slate-800">
                    <button
                      type="button"
                      disabled={isExporting}
                      onClick={() => handleDownloadSystemReport('VALIDATION', 'Pdf')}
                      className="flex-1 py-1.5 rounded-lg text-xs font-bold bg-amber-600 hover:bg-amber-500 text-white transition flex items-center justify-center gap-1"
                    >
                      <FileText className="w-3.5 h-3.5" />
                      <span>PDF</span>
                    </button>
                    <button
                      type="button"
                      disabled={isExporting}
                      onClick={() => handleDownloadSystemReport('VALIDATION', 'Csv')}
                      className="px-3 py-1.5 rounded-lg text-xs font-semibold bg-slate-800 hover:bg-slate-700 text-slate-300 border border-slate-700 transition"
                    >
                      CSV
                    </button>
                  </div>
                </div>

                {/* 3. User Activity Report */}
                <div className="p-4 rounded-xl bg-slate-800/50 border border-slate-700/80 flex flex-col justify-between">
                  <div>
                    <div className="w-8 h-8 rounded-lg bg-emerald-500/10 border border-emerald-500/30 flex items-center justify-center text-emerald-400 mb-2">
                      <Activity className="w-4 h-4" />
                    </div>
                    <h4 className="text-xs font-bold text-white mb-1">
                      {isAr ? 'تقرير إنتاجية المحررين' : 'User Activity & Audit Report'}
                    </h4>
                    <p className="text-[11px] text-slate-400">
                      {isAr
                        ? 'إحصائيات العمليات وحزم التعديل لكل مستخدم وتاريخ آخر نشاط'
                        : 'Editor performance, total changesets committed, and last activity log'}
                    </p>
                  </div>

                  <div className="flex items-center gap-2 mt-4 pt-3 border-t border-slate-800">
                    <button
                      type="button"
                      disabled={isExporting}
                      onClick={() => handleDownloadSystemReport('USER_ACTIVITY', 'Pdf')}
                      className="flex-1 py-1.5 rounded-lg text-xs font-bold bg-emerald-600 hover:bg-emerald-500 text-white transition flex items-center justify-center gap-1"
                    >
                      <FileText className="w-3.5 h-3.5" />
                      <span>PDF</span>
                    </button>
                    <button
                      type="button"
                      disabled={isExporting}
                      onClick={() => handleDownloadSystemReport('USER_ACTIVITY', 'Csv')}
                      className="px-3 py-1.5 rounded-lg text-xs font-semibold bg-slate-800 hover:bg-slate-700 text-slate-300 border border-slate-700 transition"
                    >
                      CSV
                    </button>
                  </div>
                </div>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
