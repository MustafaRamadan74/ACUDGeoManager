'use client';

import React, { useState, useEffect, useCallback } from 'react';
import {
  ShieldAlert,
  Play,
  RotateCcw,
  RotateCw,
  CheckCircle2,
  Clock,
  AlertTriangle,
  Info,
  X,
  Search,
  Filter,
  Eye,
  Check,
  ChevronRight,
  Loader2,
  Layers,
  Network as NetworkIcon
} from 'lucide-react';
import { api } from '@/lib/api';
import { useAuth } from '@/lib/authContext';
import {
  NetworkDto,
  ValidationRunDto,
  ValidationIssueDto,
  ValidationSeverity,
  IssueStatus,
  GeoJsonFeature,
  TopologyStatusDto
} from '@/lib/types';

const RULE_LABELS: Record<string, { ar: string; en: string }> = {
  RULE_VALVE_OFF_PIPE: { ar: 'محبس غير متصل بخط', en: 'Valve Off Pipe' },
  RULE_DANGLING_PIPE: { ar: 'نهاية خط معلقة', en: 'Dangling Pipe End' },
  RULE_DUPLICATE_POINTS: { ar: 'تكرار نقاط متطابقة', en: 'Duplicate Points' },
  RULE_DIR_UNKNOWN: { ar: 'اتجاه سريان غير محدد', en: 'Unknown Flow Direction' },
  RULE_ISOLATED_NO_SOURCE: { ar: 'مكون معزول بلا مصدر', en: 'Isolated Component (No Source)' },
  RULE_REVERSED_FLOW: { ar: 'سريان معكوس / غير مغذى', en: 'Unfed / Reversed Flow' },
  RULE_DIRECTIONAL_CYCLE: { ar: 'حلقة سريان مغلقة', en: 'Directional Flow Cycle' },
  RULE_MISSING_TERMINAL: { ar: 'نهاية بلا معلم طرفي', en: 'Missing Terminal Feature' },
  RULE_DUPLICATE_ID: { ar: 'تكرار معرف المعلم', en: 'Duplicate External ID' },
  RULE_SELF_INTERSECTION: { ar: 'تقاطع ذاتي للخط', en: 'Self Intersection' },
  RULE_INVALID_GEOMETRY: { ar: 'هندسة جغرافية غير صالحة', en: 'Invalid Geometry' },
};

interface ValidationDashboardModalProps {
  isOpen: boolean;
  onClose: () => void;
  networks: NetworkDto[];
  activeNetworkId: string | null;
  onSelectNetwork: (networkId: string) => void;
  onZoomToIssue: (feature: GeoJsonFeature) => void;
  onIssuesUpdated?: (issues: ValidationIssueDto[]) => void;
}

export const ValidationDashboardModal: React.FC<ValidationDashboardModalProps> = ({
  isOpen,
  onClose,
  networks,
  activeNetworkId,
  onSelectNetwork,
  onZoomToIssue,
  onIssuesUpdated,
}) => {
  const { lang, user } = useAuth();
  const isAr = lang === 'ar';
  const canEdit = user?.role === 'ADMIN' || user?.role === 'EDITOR';

  const [activeTab, setActiveTab] = useState<'issues' | 'history'>('issues');
  const [isRunning, setIsRunning] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [runs, setRuns] = useState<ValidationRunDto[]>([]);
  const [selectedRunId, setSelectedRunId] = useState<string | null>(null);

  // Issues state
  const [issues, setIssues] = useState<ValidationIssueDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [severityFilter, setSeverityFilter] = useState<string>('ALL');
  const [statusFilter, setStatusFilter] = useState<string>('ALL');
  const [searchQuery, setSearchQuery] = useState('');
  const [updatingIssueId, setUpdatingIssueId] = useState<string | null>(null);

  // Topology state
  const [topologyStatus, setTopologyStatus] = useState<TopologyStatusDto | null>(null);
  const [isRebuildingTopology, setIsRebuildingTopology] = useState(false);

  // Fetch topology status
  const fetchTopologyStatus = useCallback(async () => {
    if (!activeNetworkId) {
      setTopologyStatus(null);
      return;
    }
    try {
      const status = await api.getTopologyStatus(activeNetworkId);
      setTopologyStatus(status);
    } catch (err) {
      console.error('Failed to load topology status', err);
    }
  }, [activeNetworkId]);

  // Fetch runs history
  const fetchRuns = useCallback(async () => {
    try {
      const data = await api.getValidationRuns(activeNetworkId || undefined, 20);
      setRuns(data);
      if (data.length > 0 && !selectedRunId) {
        setSelectedRunId(data[0].id);
      }
    } catch (err) {
      console.error('Failed to load validation runs', err);
    }
  }, [activeNetworkId, selectedRunId]);

  // Fetch issues for the current run / network
  const fetchIssues = useCallback(async () => {
    setIsLoading(true);
    try {
      const res = await api.queryValidationIssues({
        runId: selectedRunId || undefined,
        networkId: selectedRunId ? undefined : (activeNetworkId || undefined),
        status: statusFilter !== 'ALL' ? (statusFilter as IssueStatus) : undefined,
        severity: severityFilter !== 'ALL' ? (severityFilter as ValidationSeverity) : undefined,
        page: 1,
        pageSize: 100,
      });

      setIssues(res.items);
      setTotalCount(res.totalCount);
      if (onIssuesUpdated) {
        onIssuesUpdated(res.items);
      }
    } catch (err) {
      console.error('Failed to fetch validation issues', err);
    } finally {
      setIsLoading(false);
    }
  }, [selectedRunId, activeNetworkId, statusFilter, severityFilter, onIssuesUpdated]);

  useEffect(() => {
    if (isOpen) {
      fetchRuns();
    }
  }, [isOpen, fetchRuns]);

  useEffect(() => {
    if (isOpen) {
      fetchIssues();
    }
  }, [isOpen, selectedRunId, statusFilter, severityFilter, fetchIssues]);

  useEffect(() => {
    if (isOpen && activeNetworkId) {
      fetchTopologyStatus();
    }
  }, [isOpen, activeNetworkId, fetchTopologyStatus]);

  // Rebuild network topology
  const handleRebuildTopology = async () => {
    if (!activeNetworkId) return;
    setIsRebuildingTopology(true);
    try {
      await api.rebuildTopology(activeNetworkId);
      await fetchTopologyStatus();
    } catch (err) {
      console.error('Failed to rebuild topology', err);
    } finally {
      setIsRebuildingTopology(false);
    }
  };

  // Trigger a new validation run
  const handleRunValidation = async () => {
    if (!activeNetworkId) return;
    setIsRunning(true);
    try {
      const newRun = await api.runValidation(activeNetworkId);
      await fetchRuns();
      setSelectedRunId(newRun.id);
      setActiveTab('issues');
    } catch (err) {
      console.error('Failed to run validation', err);
    } finally {
      setIsRunning(false);
    }
  };

  // Update issue status (Acknowledge, Resolve, Reopen)
  const handleUpdateStatus = async (issueId: string, newStatus: IssueStatus) => {
    setUpdatingIssueId(issueId);
    try {
      const updated = await api.updateIssueStatus(issueId, newStatus);
      setIssues((prev) => prev.map((item) => (item.id === issueId ? updated : item)));
    } catch (err) {
      console.error('Failed to update issue status', err);
    } finally {
      setUpdatingIssueId(null);
    }
  };

  // Zoom to issue on map
  const handleZoom = (issue: ValidationIssueDto) => {
    if (!issue.geometryGeoJson) return;
    try {
      const geom = JSON.parse(issue.geometryGeoJson);
      const kind: 'POINT' | 'LINE' | 'POLYGON' =
        geom.type === 'Point' ? 'POINT' : geom.type === 'LineString' ? 'LINE' : 'POLYGON';

      const feature: GeoJsonFeature = {
        id: issue.featureId,
        type: 'Feature',
        geometry: geom,
        properties: {
          id: issue.featureId,
          name: issue.assetName,
          featureTypeCode: issue.featureTable,
          geometryKind: kind,
          status: 'ACTIVE',
          version: 1,
          networkId: activeNetworkId || '',
          attributes: {},
        },
      };
      onZoomToIssue(feature);
      onClose();
    } catch (e) {
      console.error('Failed to parse issue geometry for zoom', e);
    }
  };

  if (!isOpen) return null;

  // Filtered issues by search query
  const filteredIssues = issues.filter((issue) => {
    if (!searchQuery) return true;
    const q = searchQuery.toLowerCase();
    return (
      issue.assetName.toLowerCase().includes(q) ||
      issue.message.toLowerCase().includes(q) ||
      issue.ruleCode.toLowerCase().includes(q)
    );
  });

  const errorsCount = issues.filter((i) => i.severity === 'ERROR').length;
  const warningsCount = issues.filter((i) => i.severity === 'WARNING').length;
  const resolvedCount = issues.filter((i) => i.status === 'RESOLVED').length;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/70 backdrop-blur-sm animate-in fade-in duration-200">
      <div className="bg-slate-900 border border-slate-700/80 rounded-2xl w-full max-w-5xl max-h-[90vh] flex flex-col shadow-2xl overflow-hidden">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-slate-800 bg-slate-900/90">
          <div className="flex items-center gap-3">
            <div className="p-2.5 rounded-xl bg-rose-500/10 text-rose-400 border border-rose-500/20">
              <ShieldAlert className="w-6 h-6" />
            </div>
            <div>
              <h2 className="text-lg font-bold text-white flex items-center gap-2">
                <span>{isAr ? 'محرك التدقيق ومطابقة قواعد البيانات الجغرافية' : 'Validation & Quality Engine'}</span>
                <span className="text-xs px-2 py-0.5 rounded-full bg-slate-800 text-sky-400 border border-slate-700 font-mono">
                  PostGIS Topology
                </span>
              </h2>
              <p className="text-xs text-slate-400 mt-0.5">
                {isAr
                  ? 'فحص الأخطاء الهندسية، التداخلات غير المسموحة، ازدواجية العناصر، واكتمال الشبكة'
                  : 'Automated geometric validity, duplicate detection, overlap rules & network completeness'}
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

        {/* Toolbar: Network selector, Trigger button, Tabs */}
        <div className="px-6 py-3 bg-slate-800/40 border-b border-slate-800 flex flex-wrap items-center justify-between gap-3">
          <div className="flex items-center gap-3">
            {/* Network Selector */}
            <div className="flex items-center gap-2">
              <Layers className="w-4 h-4 text-slate-400" />
              <select
                value={activeNetworkId || ''}
                onChange={(e) => onSelectNetwork(e.target.value)}
                className="bg-slate-900 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-white focus:outline-none focus:ring-1 focus:ring-sky-500"
              >
                {networks.map((net) => (
                  <option key={net.id} value={net.id}>
                    {net.name}
                  </option>
                ))}
              </select>
            </div>

            {/* Run Validation Button */}
            {canEdit && (
              <button
                onClick={handleRunValidation}
                disabled={isRunning || !activeNetworkId}
                className="flex items-center gap-2 px-3.5 py-1.5 rounded-lg bg-rose-600 hover:bg-rose-500 disabled:opacity-50 text-white text-xs font-semibold shadow-md shadow-rose-950/40 transition active:scale-95"
              >
                {isRunning ? (
                  <>
                    <Loader2 className="w-4 h-4 animate-spin" />
                    <span>{isAr ? 'جارٍ الفحص...' : 'Evaluating Rules...'}</span>
                  </>
                ) : (
                  <>
                    <Play className="w-4 h-4 fill-white" />
                    <span>{isAr ? 'بدء فحص الشبكة' : 'Run Validation'}</span>
                  </>
                )}
              </button>
            )}

            {/* Rebuild Topology Button */}
            {canEdit && (
              <button
                type="button"
                onClick={handleRebuildTopology}
                disabled={isRebuildingTopology || !activeNetworkId}
                className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium border transition ${
                  topologyStatus?.isDirty
                    ? 'bg-amber-950/60 hover:bg-amber-900/60 text-amber-300 border-amber-700/60 shadow-sm shadow-amber-950/40'
                    : 'bg-slate-800 hover:bg-slate-700 text-slate-200 border-slate-700'
                } disabled:opacity-50`}
                title={
                  topologyStatus
                    ? `${isAr ? 'العقد' : 'Nodes'}: ${topologyStatus.nodeCount}, ${isAr ? 'الحواف' : 'Edges'}: ${topologyStatus.edgeCount}${topologyStatus.isDirty ? (isAr ? ' (تتطلب إعادة بناء)' : ' (Needs rebuild)') : ''}`
                    : ''
                }
              >
                <RotateCw className={`w-3.5 h-3.5 ${isRebuildingTopology ? 'animate-spin text-sky-400' : ''}`} />
                <span>
                  {isRebuildingTopology
                    ? (isAr ? 'جارٍ بناء الطوبولوجيا...' : 'Rebuilding...')
                    : (isAr ? 'إعادة بناء الطوبولوجيا' : 'Rebuild Topology')}
                </span>
                {topologyStatus && (
                  <span
                    className={`text-[10px] px-1.5 py-0.2 rounded-full font-semibold ${
                      topologyStatus.isDirty
                        ? 'bg-amber-500/20 text-amber-300 border border-amber-500/30'
                        : 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/30'
                    }`}
                  >
                    {topologyStatus.isDirty
                      ? (isAr ? 'بحاجة لتحديث' : 'Dirty')
                      : (isAr ? 'محدّثة' : 'Clean')}
                  </span>
                )}
              </button>
            )}
          </div>

          {/* Navigation Tabs */}
          <div className="flex items-center bg-slate-900/80 p-1 rounded-xl border border-slate-700/60">
            <button
              onClick={() => setActiveTab('issues')}
              className={`px-3 py-1 rounded-lg text-xs font-medium transition ${
                activeTab === 'issues'
                  ? 'bg-sky-600 text-white shadow'
                  : 'text-slate-400 hover:text-white'
              }`}
            >
              {isAr ? 'المخالفات المرصودة' : 'Issues'} ({totalCount})
            </button>
            <button
              onClick={() => setActiveTab('history')}
              className={`px-3 py-1 rounded-lg text-xs font-medium transition ${
                activeTab === 'history'
                  ? 'bg-sky-600 text-white shadow'
                  : 'text-slate-400 hover:text-white'
              }`}
            >
              {isAr ? 'سجل الفحوصات' : 'History'} ({runs.length})
            </button>
          </div>
        </div>

        {/* Content Body */}
        <div className="flex-1 overflow-y-auto p-6 space-y-4">
          {activeTab === 'issues' ? (
            <>
              {/* Topology Summary Bar */}
              {topologyStatus && (
                <div className="flex flex-wrap items-center justify-between px-4 py-2.5 bg-slate-800/50 border border-slate-700/60 rounded-xl text-xs gap-3">
                  <div className="flex flex-wrap items-center gap-4 text-slate-300">
                    <span className="font-semibold text-slate-200 flex items-center gap-2">
                      <NetworkIcon className="w-4 h-4 text-sky-400" />
                      <span>{isAr ? 'حالة الطوبولوجيا:' : 'Topology:'}</span>
                      <span
                        className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-semibold ${
                          topologyStatus.isDirty
                            ? 'bg-amber-500/20 text-amber-300 border border-amber-500/30'
                            : 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/30'
                        }`}
                      >
                        <span className={`w-1.5 h-1.5 rounded-full ${topologyStatus.isDirty ? 'bg-amber-400 animate-pulse' : 'bg-emerald-400'}`} />
                        {topologyStatus.isDirty
                          ? (isAr ? 'بحاجة لإعادة بناء (Dirty)' : 'Dirty (Out of sync)')
                          : (isAr ? 'جاهزة ومطابقة (Clean)' : 'Clean')}
                      </span>
                    </span>

                    <span className="text-slate-400">
                      {isAr ? 'العقد (Nodes):' : 'Nodes:'}{' '}
                      <strong className="text-white font-mono text-xs">{topologyStatus.nodeCount}</strong>
                    </span>

                    <span className="text-slate-400">
                      {isAr ? 'حواف السريان (Edges):' : 'Edges:'}{' '}
                      <strong className="text-white font-mono text-xs">{topologyStatus.edgeCount}</strong>
                    </span>

                    {topologyStatus.lastRebuiltAt && (
                      <span className="text-slate-400">
                        {isAr ? 'آخر بناء:' : 'Last Rebuild:'}{' '}
                        <span className="font-mono text-slate-300">
                          {new Date(topologyStatus.lastRebuiltAt).toLocaleTimeString()}
                        </span>
                      </span>
                    )}
                  </div>

                  {topologyStatus.isDirty && (
                    <span className="text-[11px] text-amber-300/90 font-medium">
                      {isAr
                        ? 'تنبيه: تم إدخال تعديلات هندسية على الخطوط أو النقاط، يُرجى إعادة بناء الطوبولوجيا'
                        : 'Notice: Network geometry has changed. Rebuild topology to update network connectivity.'}
                    </span>
                  )}
                </div>
              )}

              {/* Stat Cards */}
              <div className="grid grid-cols-4 gap-3">
                <div className="p-3.5 rounded-xl bg-slate-800/60 border border-slate-700/60 flex items-center justify-between">
                  <div>
                    <div className="text-[11px] text-slate-400 font-medium">
                      {isAr ? 'إجمالي المخالفات' : 'Total Issues'}
                    </div>
                    <div className="text-xl font-bold text-white mt-0.5">{issues.length}</div>
                  </div>
                  <div className="p-2 rounded-lg bg-slate-700/50 text-slate-300">
                    <ShieldAlert className="w-5 h-5" />
                  </div>
                </div>

                <div className="p-3.5 rounded-xl bg-rose-950/20 border border-rose-800/40 flex items-center justify-between">
                  <div>
                    <div className="text-[11px] text-rose-300 font-medium">
                      {isAr ? 'أخطاء حرجة (Errors)' : 'Errors'}
                    </div>
                    <div className="text-xl font-bold text-rose-400 mt-0.5">{errorsCount}</div>
                  </div>
                  <div className="p-2 rounded-lg bg-rose-500/20 text-rose-400">
                    <AlertTriangle className="w-5 h-5" />
                  </div>
                </div>

                <div className="p-3.5 rounded-xl bg-amber-950/20 border border-amber-800/40 flex items-center justify-between">
                  <div>
                    <div className="text-[11px] text-amber-300 font-medium">
                      {isAr ? 'تحذيرات (Warnings)' : 'Warnings'}
                    </div>
                    <div className="text-xl font-bold text-amber-400 mt-0.5">{warningsCount}</div>
                  </div>
                  <div className="p-2 rounded-lg bg-amber-500/20 text-amber-400">
                    <Clock className="w-5 h-5" />
                  </div>
                </div>

                <div className="p-3.5 rounded-xl bg-emerald-950/20 border border-emerald-800/40 flex items-center justify-between">
                  <div>
                    <div className="text-[11px] text-emerald-300 font-medium">
                      {isAr ? 'تم الحل (Resolved)' : 'Resolved'}
                    </div>
                    <div className="text-xl font-bold text-emerald-400 mt-0.5">{resolvedCount}</div>
                  </div>
                  <div className="p-2 rounded-lg bg-emerald-500/20 text-emerald-400">
                    <CheckCircle2 className="w-5 h-5" />
                  </div>
                </div>
              </div>

              {/* Filters Bar */}
              <div className="flex flex-wrap items-center justify-between gap-3 bg-slate-800/30 p-3 rounded-xl border border-slate-800">
                <div className="flex items-center gap-2 flex-1 max-w-sm">
                  <div className="relative w-full">
                    <Search className="w-4 h-4 absolute left-3 rtl:left-auto rtl:right-3 top-1/2 -translate-y-1/2 text-slate-400" />
                    <input
                      type="text"
                      placeholder={isAr ? 'بحث بالاسم، القاعدة، أو المعلم...' : 'Search by asset or message...'}
                      value={searchQuery}
                      onChange={(e) => setSearchQuery(e.target.value)}
                      className="w-full bg-slate-900 border border-slate-700 rounded-lg pl-9 pr-4 rtl:pl-4 rtl:pr-9 py-1.5 text-xs text-white placeholder-slate-500 focus:outline-none focus:ring-1 focus:ring-sky-500"
                    />
                  </div>
                </div>

                <div className="flex items-center gap-2">
                  <div className="flex items-center gap-1.5 text-xs text-slate-400">
                    <Filter className="w-3.5 h-3.5" />
                    <span>{isAr ? 'الحالة:' : 'Status:'}</span>
                  </div>
                  <select
                    value={statusFilter}
                    onChange={(e) => setStatusFilter(e.target.value)}
                    className="bg-slate-900 border border-slate-700 rounded-lg px-2.5 py-1 text-xs text-white focus:outline-none"
                  >
                    <option value="ALL">{isAr ? 'الكل' : 'All Statuses'}</option>
                    <option value="OPEN">{isAr ? 'مفتوح (OPEN)' : 'OPEN'}</option>
                    <option value="ACKNOWLEDGED">{isAr ? 'مؤكد (ACKNOWLEDGED)' : 'ACKNOWLEDGED'}</option>
                    <option value="RESOLVED">{isAr ? 'محلول (RESOLVED)' : 'RESOLVED'}</option>
                  </select>

                  <div className="flex items-center gap-1.5 text-xs text-slate-400 ml-2 rtl:ml-0 rtl:mr-2">
                    <span>{isAr ? 'الخطورة:' : 'Severity:'}</span>
                  </div>
                  <select
                    value={severityFilter}
                    onChange={(e) => setSeverityFilter(e.target.value)}
                    className="bg-slate-900 border border-slate-700 rounded-lg px-2.5 py-1 text-xs text-white focus:outline-none"
                  >
                    <option value="ALL">{isAr ? 'الكل' : 'All Severities'}</option>
                    <option value="ERROR">{isAr ? 'خطأ (ERROR)' : 'ERROR'}</option>
                    <option value="WARNING">{isAr ? 'تحذير (WARNING)' : 'WARNING'}</option>
                    <option value="INFO">{isAr ? 'معلومة (INFO)' : 'INFO'}</option>
                  </select>
                </div>
              </div>

              {/* Issues Table */}
              <div className="border border-slate-800 rounded-xl overflow-hidden bg-slate-900/50">
                {isLoading ? (
                  <div className="py-16 text-center text-slate-400 flex flex-col items-center gap-3">
                    <Loader2 className="w-6 h-6 animate-spin text-sky-400" />
                    <span className="text-xs">{isAr ? 'جارٍ تحميل البيانات...' : 'Loading issues...'}</span>
                  </div>
                ) : filteredIssues.length === 0 ? (
                  <div className="py-16 text-center text-slate-400 flex flex-col items-center gap-2">
                    <CheckCircle2 className="w-8 h-8 text-emerald-400" />
                    <span className="text-sm font-medium text-slate-300">
                      {isAr ? 'لا توجد مخالفات في نتائج الفحص الحالية' : 'No issues found for current filter criteria'}
                    </span>
                    <span className="text-xs text-slate-500">
                      {isAr ? 'الشبكة متوافقة بالكامل مع القواعد الجغرافية المعتمدة' : 'Dataset conforms to all active validation rules'}
                    </span>
                  </div>
                ) : (
                  <table className="w-full text-left rtl:text-right border-collapse text-xs">
                    <thead>
                      <tr className="border-b border-slate-800 bg-slate-800/50 text-slate-400 font-semibold">
                        <th className="py-2.5 px-3">{isAr ? 'الخطورة' : 'Severity'}</th>
                        <th className="py-2.5 px-3">{isAr ? 'القاعدة' : 'Rule'}</th>
                        <th className="py-2.5 px-3">{isAr ? 'اسم الأصل' : 'Asset'}</th>
                        <th className="py-2.5 px-3">{isAr ? 'تفاصيل المخالفة' : 'Description'}</th>
                        <th className="py-2.5 px-3">{isAr ? 'الحالة والاعتماد' : 'Status & Audit'}</th>
                        <th className="py-2.5 px-3 text-center">{isAr ? 'إجراءات' : 'Actions'}</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-800">
                      {filteredIssues.map((issue) => (
                        <tr key={issue.id} className="hover:bg-slate-800/40 transition">
                          {/* Severity Badge */}
                          <td className="py-3 px-3 whitespace-nowrap">
                            {issue.severity === 'ERROR' && (
                              <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-semibold bg-rose-500/10 text-rose-400 border border-rose-500/30">
                                <AlertTriangle className="w-3 h-3" />
                                ERROR
                              </span>
                            )}
                            {issue.severity === 'WARNING' && (
                              <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-semibold bg-amber-500/10 text-amber-300 border border-amber-500/30">
                                <Clock className="w-3 h-3" />
                                WARNING
                              </span>
                            )}
                            {issue.severity === 'INFO' && (
                              <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-semibold bg-sky-500/10 text-sky-400 border border-sky-500/30">
                                <Info className="w-3 h-3" />
                                INFO
                              </span>
                            )}
                          </td>

                          {/* Rule Code */}
                          <td className="py-3 px-3 whitespace-nowrap">
                            <div className="font-semibold text-white">
                              {RULE_LABELS[issue.ruleCode]?.[isAr ? 'ar' : 'en'] || issue.ruleCode}
                            </div>
                            <div className="text-[10px] text-slate-400 font-mono">
                              {issue.ruleCode}
                            </div>
                          </td>

                          {/* Asset Name */}
                          <td className="py-3 px-3 font-medium text-white whitespace-nowrap">
                            <div className="flex items-center gap-1.5">
                              <span className="px-1.5 py-0.5 rounded text-[10px] bg-slate-800 text-slate-400 uppercase font-mono">
                                {issue.featureTable}
                              </span>
                              <span>{issue.assetName}</span>
                            </div>
                          </td>

                          {/* Message */}
                          <td className="py-3 px-3 text-slate-300 max-w-xs leading-relaxed">
                            {issue.message}
                          </td>

                          {/* Status & Audit */}
                          <td className="py-3 px-3 whitespace-nowrap">
                            <div className="space-y-1">
                              <div>
                                {issue.status === 'OPEN' && (
                                  <span className="px-2 py-0.5 rounded text-[10px] font-medium bg-red-950/60 text-red-300 border border-red-800/60">
                                    OPEN
                                  </span>
                                )}
                                {issue.status === 'ACKNOWLEDGED' && (
                                  <span className="px-2 py-0.5 rounded text-[10px] font-medium bg-amber-950/60 text-amber-300 border border-amber-800/60">
                                    ACKNOWLEDGED
                                  </span>
                                )}
                                {issue.status === 'RESOLVED' && (
                                  <span className="px-2 py-0.5 rounded text-[10px] font-medium bg-emerald-950/60 text-emerald-300 border border-emerald-800/60">
                                    RESOLVED
                                  </span>
                                )}
                              </div>
                              {issue.acknowledgedBy && (
                                <div className="text-[10px] text-slate-400">
                                  Ack by <span className="text-slate-200">{issue.acknowledgedBy}</span>
                                </div>
                              )}
                              {issue.resolvedBy && (
                                <div className="text-[10px] text-slate-400">
                                  Fixed by <span className="text-slate-200">{issue.resolvedBy}</span>
                                </div>
                              )}
                            </div>
                          </td>

                          {/* Actions */}
                          <td className="py-3 px-3 whitespace-nowrap text-center">
                            <div className="flex items-center justify-center gap-1.5">
                              {/* Zoom to issue geometry */}
                              {issue.geometryGeoJson && (
                                <button
                                  type="button"
                                  onClick={() => handleZoom(issue)}
                                  className="p-1.5 rounded-lg bg-sky-950/60 hover:bg-sky-900/60 text-sky-400 border border-sky-800/60 transition"
                                  title={isAr ? 'تكبير وتحديد موقع المعلم على الخريطة' : 'Zoom to feature'}
                                >
                                  <Eye className="w-3.5 h-3.5" />
                                </button>
                              )}

                              {/* Status Action Buttons */}
                              {canEdit && (
                                <>
                                  {issue.status === 'OPEN' && (
                                    <button
                                      type="button"
                                      disabled={updatingIssueId === issue.id}
                                      onClick={() => handleUpdateStatus(issue.id, 'ACKNOWLEDGED')}
                                      className="px-2 py-1 rounded bg-amber-950/50 hover:bg-amber-900/60 text-amber-300 border border-amber-800/50 text-[11px] font-medium transition"
                                      title="Mark Acknowledged"
                                    >
                                      {isAr ? 'تأكيد' : 'Ack'}
                                    </button>
                                  )}

                                  {issue.status !== 'RESOLVED' && (
                                    <button
                                      type="button"
                                      disabled={updatingIssueId === issue.id}
                                      onClick={() => handleUpdateStatus(issue.id, 'RESOLVED')}
                                      className="px-2 py-1 rounded bg-emerald-950/50 hover:bg-emerald-900/60 text-emerald-300 border border-emerald-800/50 text-[11px] font-medium transition"
                                      title="Mark Resolved"
                                    >
                                      <Check className="w-3 h-3 inline mr-0.5 rtl:mr-0 rtl:ml-0.5" />
                                      {isAr ? 'تم الحل' : 'Resolve'}
                                    </button>
                                  )}

                                  {issue.status === 'RESOLVED' && (
                                    <button
                                      type="button"
                                      disabled={updatingIssueId === issue.id}
                                      onClick={() => handleUpdateStatus(issue.id, 'OPEN')}
                                      className="px-2 py-1 rounded bg-slate-800 hover:bg-slate-700 text-slate-300 border border-slate-700 text-[11px] font-medium transition"
                                      title="Reopen Issue"
                                    >
                                      <RotateCcw className="w-3 h-3 inline mr-0.5 rtl:mr-0 rtl:ml-0.5" />
                                      {isAr ? 'إعادة فتح' : 'Reopen'}
                                    </button>
                                  )}
                                </>
                              )}
                            </div>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </div>
            </>
          ) : (
            /* History Tab */
            <div className="border border-slate-800 rounded-xl overflow-hidden bg-slate-900/50">
              <table className="w-full text-left rtl:text-right border-collapse text-xs">
                <thead>
                  <tr className="border-b border-slate-800 bg-slate-800/50 text-slate-400 font-semibold">
                    <th className="py-2.5 px-4">{isAr ? 'تاريخ الفحص' : 'Date / Time'}</th>
                    <th className="py-2.5 px-4">{isAr ? 'المستخدم' : 'Started By'}</th>
                    <th className="py-2.5 px-4">{isAr ? 'المدة' : 'Duration'}</th>
                    <th className="py-2.5 px-4">{isAr ? 'المخالفات المرصودة' : 'Issues Summary'}</th>
                    <th className="py-2.5 px-4 text-center">{isAr ? 'إجراء' : 'Action'}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-800">
                  {runs.map((r) => (
                    <tr
                      key={r.id}
                      className={`hover:bg-slate-800/40 transition ${
                        selectedRunId === r.id ? 'bg-sky-950/30' : ''
                      }`}
                    >
                      <td className="py-3 px-4 font-mono text-slate-300">
                        {new Date(r.startedAt).toLocaleString()}
                      </td>
                      <td className="py-3 px-4 font-medium text-white">{r.startedBy}</td>
                      <td className="py-3 px-4 text-slate-400 font-mono">
                        {r.summary?.durationMs ? `${r.summary.durationMs}ms` : '-'}
                      </td>
                      <td className="py-3 px-4">
                        <div className="flex items-center gap-2">
                          <span className="font-bold text-white">{r.summary?.totalIssues ?? 0}</span>
                          <span className="text-[10px] px-1.5 py-0.2 rounded bg-rose-500/20 text-rose-300">
                            {r.summary?.errorsCount ?? 0} errors
                          </span>
                          <span className="text-[10px] px-1.5 py-0.2 rounded bg-amber-500/20 text-amber-300">
                            {r.summary?.warningsCount ?? 0} warnings
                          </span>
                        </div>
                      </td>
                      <td className="py-3 px-4 text-center">
                        <button
                          type="button"
                          onClick={() => {
                            setSelectedRunId(r.id);
                            setActiveTab('issues');
                          }}
                          className="px-2.5 py-1 rounded-lg bg-sky-600 hover:bg-sky-500 text-white text-[11px] font-semibold transition"
                        >
                          {isAr ? 'عرض المخالفات' : 'Inspect Issues'}
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="px-6 py-3 border-t border-slate-800 bg-slate-900/90 flex items-center justify-between text-xs text-slate-400">
          <div>
            {isAr
              ? 'الامتثال للمعايير: ISO 19157 (Data Quality) وقواعد الهيئة العامة للتخطيط العمراني'
              : 'Standards Compliant: ISO 19157 Spatial Data Quality & New Administrative Capital utility rules'}
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
