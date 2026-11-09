'use client';

import React, { useState, useEffect, useCallback } from 'react';
import {
  ShieldCheck,
  CheckCircle2,
  XCircle,
  AlertCircle,
  Eye,
  Filter,
  Search,
  X,
  Layers,
  Clock,
  User,
  AlertTriangle,
  RotateCw,
  Sliders,
  Check,
} from 'lucide-react';
import { api, ApiError } from '@/lib/api';
import { useAuth } from '@/lib/authContext';
import {
  NetworkDto,
  FeatureTypeDto,
  PendingChangeDto,
} from '@/lib/types';

interface ApprovalsModalProps {
  isOpen: boolean;
  onClose: () => void;
  networks: NetworkDto[];
  activeNetworkId: string | null;
  featureTypes: FeatureTypeDto[];
  onFeatureTypesUpdated?: (updated: FeatureTypeDto[]) => void;
  onRefreshData?: () => void;
  onPreviewGeometry?: (geometry: any | null) => void;
}

export const ApprovalsModal: React.FC<ApprovalsModalProps> = ({
  isOpen,
  onClose,
  networks,
  activeNetworkId,
  featureTypes,
  onFeatureTypesUpdated,
  onRefreshData,
  onPreviewGeometry,
}) => {
  const { lang, user } = useAuth();
  const isAr = lang === 'ar';
  const isAdmin = user?.role === 'ADMIN';

  const [activeTab, setActiveTab] = useState<'queue' | 'layers'>('queue');
  const [pendingQueue, setPendingQueue] = useState<PendingChangeDto[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [selectedNetworkId, setSelectedNetworkId] = useState<string>(activeNetworkId || 'ALL');
  const [filterOperation, setFilterOperation] = useState<string>('ALL');
  const [searchQuery, setSearchQuery] = useState('');
  const [actionError, setActionError] = useState<string | null>(null);
  const [actionSuccess, setActionSuccess] = useState<string | null>(null);

  // Rejection Dialog State
  const [rejectingItem, setRejectingItem] = useState<PendingChangeDto | null>(null);
  const [rejectionReason, setRejectionReason] = useState('');
  const [isSubmittingAction, setIsSubmittingAction] = useState(false);

  // Approve Confirmation State
  const [approvingItem, setApprovingItem] = useState<PendingChangeDto | null>(null);

  // Expanded Attributes
  const [expandedId, setExpandedId] = useState<string | null>(null);

  // Local feature types copy for toggling
  const [localFeatureTypes, setLocalFeatureTypes] = useState<FeatureTypeDto[]>(featureTypes);

  useEffect(() => {
    setLocalFeatureTypes(featureTypes);
  }, [featureTypes]);

  const fetchPendingQueue = useCallback(async () => {
    setIsLoading(true);
    setActionError(null);
    try {
      const netId = selectedNetworkId !== 'ALL' ? selectedNetworkId : undefined;
      const data = await api.getPendingApprovals(netId);
      setPendingQueue(data);
    } catch (err: any) {
      console.error('Failed to load pending queue:', err);
      setActionError(err.message || (isAr ? 'فشل تحميل قائمة طلبات الاعتماد' : 'Failed to fetch pending approvals.'));
    } finally {
      setIsLoading(false);
    }
  }, [selectedNetworkId, isAr]);

  useEffect(() => {
    if (isOpen) {
      fetchPendingQueue();
      setActionSuccess(null);
      setActionError(null);
    } else {
      // Clear preview when closing modal
      onPreviewGeometry?.(null);
    }
  }, [isOpen, fetchPendingQueue, onPreviewGeometry]);

  const handlePreview = (item: PendingChangeDto) => {
    if (!item.geometryGeoJson) {
      setActionError(isAr ? 'لا توجد هندسة مكانية متاحة للمعاينة في هذا الطلب' : 'No spatial geometry available for preview.');
      return;
    }

    try {
      const parsed = JSON.parse(item.geometryGeoJson);
      onPreviewGeometry?.(parsed);
      setActionSuccess(isAr ? `جاري معاينة "${item.name}" على الخريطة بالألوان المميزة` : `Previewing "${item.name}" on map.`);
    } catch {
      setActionError(isAr ? 'فشل تحليل الهندسة المكانية للمعاينة' : 'Failed to parse geometry for preview.');
    }
  };

  const handleApprove = async (item: PendingChangeDto) => {
    setIsSubmittingAction(true);
    setActionError(null);
    setActionSuccess(null);
    try {
      const res = await api.approvePendingChange(item.id);
      setActionSuccess(res.message || (isAr ? 'تم اعتماد التعديل وتطبيقه بنجاح' : 'Change approved and applied successfully.'));
      setApprovingItem(null);
      await fetchPendingQueue();
      onRefreshData?.();
    } catch (err: any) {
      console.error('Approve error:', err);
      if (err instanceof ApiError && err.status === 409) {
        setActionError(
          isAr
            ? 'تعارض في التعديل: تم تحديث هذا العنصر في قاعدة البيانات بواسطة مستخدم آخر أثناء انتظار الاعتماد. يجب على المقترح إعادة المراجعة والتقديم.'
            : 'Concurrency Conflict: The target feature was modified concurrently by another user. Approval rejected.'
        );
      } else {
        setActionError(err.message || (isAr ? 'فشل اعتماد التعديل' : 'Failed to approve change.'));
      }
    } finally {
      setIsSubmittingAction(false);
    }
  };

  const handleReject = async () => {
    if (!rejectingItem) return;
    if (!rejectionReason.trim()) {
      setActionError(isAr ? 'يرجى إدخال سبب الرفض' : 'Please provide a rejection reason.');
      return;
    }

    setIsSubmittingAction(true);
    setActionError(null);
    setActionSuccess(null);
    try {
      const res = await api.rejectPendingChange(rejectingItem.id, rejectionReason.trim());
      setActionSuccess(res.message || (isAr ? 'تم رفض المقترح وإشعار المقترح بالسبب' : 'Change proposal rejected.'));
      setRejectingItem(null);
      setRejectionReason('');
      await fetchPendingQueue();
      onRefreshData?.();
    } catch (err: any) {
      console.error('Reject error:', err);
      setActionError(err.message || (isAr ? 'فشل رفض التعديل' : 'Failed to reject change.'));
    } finally {
      setIsSubmittingAction(false);
    }
  };

  const handleToggleLayerApproval = async (layerCode: string, currentVal: boolean) => {
    setActionError(null);
    setActionSuccess(null);
    const newVal = !currentVal;
    try {
      await api.updateLayerApprovalRequirement(layerCode, newVal);
      const updated = localFeatureTypes.map((ft) =>
        ft.code === layerCode ? { ...ft, requiresApproval: newVal } : ft
      );
      setLocalFeatureTypes(updated);
      onFeatureTypesUpdated?.(updated);
      setActionSuccess(
        isAr
          ? `تم تحديث سياسة الاعتماد للطبقة ${layerCode}: ${newVal ? 'مطلوب اعتماد' : 'تعديل مباشر'}`
          : `Updated layer ${layerCode} approval requirement to: ${newVal ? 'Requires Approval' : 'Direct Edit'}`
      );
    } catch (err: any) {
      console.error('Failed to toggle layer approval:', err);
      setActionError(err.message || (isAr ? 'فشل تحديث سياسة الطبقة' : 'Failed to update layer approval policy.'));
    }
  };

  if (!isOpen) return null;

  // Filtered pending changes
  const filteredQueue = pendingQueue.filter((item) => {
    if (filterOperation !== 'ALL' && item.operation !== filterOperation) return false;
    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase();
      const matchName = item.name.toLowerCase().includes(q);
      const matchType = item.featureTypeCode.toLowerCase().includes(q);
      const matchProposer = item.proposerUsername.toLowerCase().includes(q);
      if (!matchName && !matchType && !matchProposer) return false;
    }
    return true;
  });

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4 animate-in fade-in duration-200">
      <div
        className="flex flex-col bg-slate-900 border border-slate-700/80 rounded-2xl shadow-2xl w-full max-w-5xl h-[85vh] overflow-hidden text-slate-100"
        dir={isAr ? 'rtl' : 'ltr'}
      >
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-slate-800 bg-slate-950/60">
          <div className="flex items-center gap-3">
            <div className="p-2.5 rounded-xl bg-amber-500/10 border border-amber-500/20 text-amber-400">
              <ShieldCheck className="w-6 h-6" />
            </div>
            <div>
              <h2 className="text-lg font-bold flex items-center gap-2">
                {isAr ? 'نظام سير العمل واعتماد التعديلات' : 'Approval Workflow & Change Management'}
                {pendingQueue.length > 0 && (
                  <span className="px-2 py-0.5 text-xs font-semibold rounded-full bg-amber-500/20 text-amber-300 border border-amber-500/30">
                    {pendingQueue.length} {isAr ? 'معلق' : 'pending'}
                  </span>
                )}
              </h2>
              <p className="text-xs text-slate-400">
                {isAr
                  ? 'مراجعة واعتماد أو رفض التعديلات المقترحة من قِبل المحررين على الطبقات الحساسة'
                  : 'Review, preview, approve, or reject proposed edits on protected network layers'}
              </p>
            </div>
          </div>

          <div className="flex items-center gap-3">
            <button
              onClick={fetchPendingQueue}
              disabled={isLoading}
              className="p-2 text-slate-400 hover:text-slate-200 hover:bg-slate-800 rounded-lg transition-colors"
              title={isAr ? 'تحديث البيانات' : 'Refresh queue'}
            >
              <RotateCw className={`w-4 h-4 ${isLoading ? 'animate-spin text-amber-400' : ''}`} />
            </button>
            <button
              onClick={onClose}
              className="p-2 text-slate-400 hover:text-slate-200 hover:bg-slate-800 rounded-lg transition-colors"
            >
              <X className="w-5 h-5" />
            </button>
          </div>
        </div>

        {/* Tabs Bar */}
        <div className="flex items-center justify-between px-6 border-b border-slate-800 bg-slate-900/40">
          <div className="flex gap-4">
            <button
              onClick={() => setActiveTab('queue')}
              className={`flex items-center gap-2 py-3 px-1 border-b-2 font-medium text-sm transition-colors ${
                activeTab === 'queue'
                  ? 'border-amber-500 text-amber-400'
                  : 'border-transparent text-slate-400 hover:text-slate-200'
              }`}
            >
              <Clock className="w-4 h-4" />
              {isAr ? 'طابور الاعتماد' : 'Pending Queue'}
              <span className="text-xs bg-slate-800 px-2 py-0.5 rounded-full text-slate-300">
                {pendingQueue.length}
              </span>
            </button>

            {isAdmin && (
              <button
                onClick={() => setActiveTab('layers')}
                className={`flex items-center gap-2 py-3 px-1 border-b-2 font-medium text-sm transition-colors ${
                  activeTab === 'layers'
                    ? 'border-amber-500 text-amber-400'
                    : 'border-transparent text-slate-400 hover:text-slate-200'
                }`}
              >
                <Sliders className="w-4 h-4" />
                {isAr ? 'إعدادات اعتماد الطبقات' : 'Layer Approval Policies'}
              </button>
            )}
          </div>

          {activeTab === 'queue' && (
            <div className="flex items-center gap-2 py-2">
              {/* Network Filter */}
              <select
                value={selectedNetworkId}
                onChange={(e) => setSelectedNetworkId(e.target.value)}
                className="bg-slate-950 border border-slate-800 text-slate-200 text-xs rounded-lg px-2.5 py-1.5 focus:outline-none focus:border-amber-500"
              >
                <option value="ALL">{isAr ? 'كل الشبكات' : 'All Networks'}</option>
                {networks.map((net) => (
                  <option key={net.id} value={net.id}>
                    {net.name} ({net.code})
                  </option>
                ))}
              </select>

              {/* Operation Filter */}
              <select
                value={filterOperation}
                onChange={(e) => setFilterOperation(e.target.value)}
                className="bg-slate-950 border border-slate-800 text-slate-200 text-xs rounded-lg px-2.5 py-1.5 focus:outline-none focus:border-amber-500"
              >
                <option value="ALL">{isAr ? 'كل العمليات' : 'All Operations'}</option>
                <option value="INSERT">{isAr ? 'إضافة (INSERT)' : 'Create (INSERT)'}</option>
                <option value="UPDATE">{isAr ? 'تعديل (UPDATE)' : 'Update (UPDATE)'}</option>
                <option value="DELETE">{isAr ? 'حذف (DELETE)' : 'Delete (DELETE)'}</option>
              </select>

              {/* Search Box */}
              <div className="relative">
                <Search className="w-3.5 h-3.5 text-slate-500 absolute top-2.5 start-2.5" />
                <input
                  type="text"
                  placeholder={isAr ? 'بحث بالاسم أو المقترح...' : 'Search name or proposer...'}
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  className="bg-slate-950 border border-slate-800 text-slate-200 text-xs rounded-lg ps-8 pe-3 py-1.5 focus:outline-none focus:border-amber-500 w-44"
                />
              </div>
            </div>
          )}
        </div>

        {/* Feedback Alert Banners */}
        {actionError && (
          <div className="mx-6 mt-3 p-3 rounded-xl bg-rose-500/10 border border-rose-500/20 text-rose-300 text-xs flex items-center justify-between">
            <div className="flex items-center gap-2">
              <AlertTriangle className="w-4 h-4 shrink-0 text-rose-400" />
              <span>{actionError}</span>
            </div>
            <button onClick={() => setActionError(null)} className="text-rose-400 hover:text-rose-200">
              <X className="w-4 h-4" />
            </button>
          </div>
        )}

        {actionSuccess && (
          <div className="mx-6 mt-3 p-3 rounded-xl bg-emerald-500/10 border border-emerald-500/20 text-emerald-300 text-xs flex items-center justify-between">
            <div className="flex items-center gap-2">
              <Check className="w-4 h-4 shrink-0 text-emerald-400" />
              <span>{actionSuccess}</span>
            </div>
            <button onClick={() => setActionSuccess(null)} className="text-emerald-400 hover:text-emerald-200">
              <X className="w-4 h-4" />
            </button>
          </div>
        )}

        {/* Body Content */}
        <div className="flex-1 overflow-y-auto p-6">
          {activeTab === 'queue' ? (
            filteredQueue.length === 0 ? (
              <div className="h-full flex flex-col items-center justify-center text-center py-16 text-slate-500">
                <ShieldCheck className="w-16 h-16 text-slate-700 stroke-[1.5] mb-3" />
                <p className="text-base font-medium text-slate-300">
                  {isAr ? 'لا توجد طلبات معلقة للاعتماد حالياً' : 'No pending approval requests'}
                </p>
                <p className="text-xs text-slate-500 max-w-md mt-1">
                  {isAr
                    ? 'عند قيام المحررين بإضافة أو تعديل معالم على طبقات مشروطة بالاعتماد، ستظهر المقترحات هنا فوراً.'
                    : 'When editors submit modifications on layers requiring approval, they will appear here.'}
                </p>
              </div>
            ) : (
              <div className="space-y-3">
                {filteredQueue.map((item) => {
                  const isExpanded = expandedId === item.id;
                  const operationBadge =
                    item.operation === 'INSERT'
                      ? { label: isAr ? 'إضافة' : 'INSERT', bg: 'bg-emerald-500/15 text-emerald-400 border-emerald-500/30' }
                      : item.operation === 'UPDATE'
                      ? { label: isAr ? 'تعديل' : 'UPDATE', bg: 'bg-amber-500/15 text-amber-400 border-amber-500/30' }
                      : { label: isAr ? 'حذف' : 'DELETE', bg: 'bg-rose-500/15 text-rose-400 border-rose-500/30' };

                  return (
                    <div
                      key={item.id}
                      className="bg-slate-950/70 border border-slate-800 rounded-xl p-4 transition-all hover:border-slate-700 hover:shadow-lg"
                    >
                      <div className="flex flex-col md:flex-row md:items-center justify-between gap-3">
                        {/* Left Info */}
                        <div className="flex items-start gap-3">
                          <span
                            className={`px-2.5 py-1 text-xs font-bold rounded-lg border uppercase tracking-wider shrink-0 ${operationBadge.bg}`}
                          >
                            {operationBadge.label}
                          </span>

                          <div>
                            <div className="flex items-center gap-2">
                              <span className="font-semibold text-slate-100 text-sm">{item.name}</span>
                              <span className="text-xs px-2 py-0.5 rounded bg-slate-800 text-slate-300 font-mono">
                                {item.featureTypeCode}
                              </span>
                              <span className="text-[11px] text-slate-500">
                                ({item.geometryKind})
                              </span>
                            </div>

                            <div className="flex items-center gap-4 text-xs text-slate-400 mt-1">
                              <span className="flex items-center gap-1">
                                <User className="w-3.5 h-3.5 text-slate-500" />
                                {item.proposerUsername}
                              </span>
                              <span className="flex items-center gap-1">
                                <Clock className="w-3.5 h-3.5 text-slate-500" />
                                {new Date(item.proposedAt).toLocaleString(isAr ? 'ar-EG' : 'en-US')}
                              </span>
                              {item.targetVersion !== null && (
                                <span className="text-slate-500">
                                  v{item.targetVersion}
                                </span>
                              )}
                            </div>
                          </div>
                        </div>

                        {/* Action Buttons */}
                        <div className="flex items-center gap-2 self-end md:self-auto shrink-0">
                          {/* Preview Geometry Button */}
                          {item.geometryGeoJson && (
                            <button
                              onClick={() => handlePreview(item)}
                              className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg bg-slate-800 hover:bg-slate-700 text-cyan-300 border border-slate-700 transition-colors"
                              title={isAr ? 'معاينة على الخريطة' : 'Preview on map'}
                            >
                              <Eye className="w-3.5 h-3.5" />
                              {isAr ? 'معاينة' : 'Preview'}
                            </button>
                          )}

                          {/* Inspect Attributes Toggle */}
                          <button
                            onClick={() => setExpandedId(isExpanded ? null : item.id)}
                            className="px-2.5 py-1.5 text-xs text-slate-400 hover:text-slate-200 hover:bg-slate-800 rounded-lg transition-colors border border-transparent hover:border-slate-700"
                          >
                            {isExpanded ? (isAr ? 'إخفاء الخصائص' : 'Hide Attributes') : (isAr ? 'عرض الخصائص' : 'Attributes')}
                          </button>

                          {/* Reject Button */}
                          <button
                            onClick={() => {
                              setRejectingItem(item);
                              setRejectionReason('');
                            }}
                            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg bg-rose-500/10 hover:bg-rose-500/20 text-rose-300 border border-rose-500/30 transition-colors"
                          >
                            <XCircle className="w-3.5 h-3.5" />
                            {isAr ? 'رفض' : 'Reject'}
                          </button>

                          {/* Approve Button */}
                          <button
                            onClick={() => setApprovingItem(item)}
                            className="flex items-center gap-1.5 px-3.5 py-1.5 text-xs font-semibold rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white shadow-sm shadow-emerald-900/30 transition-colors"
                          >
                            <CheckCircle2 className="w-3.5 h-3.5" />
                            {isAr ? 'اعتماد' : 'Approve'}
                          </button>
                        </div>
                      </div>

                      {/* Expanded Details / Attributes */}
                      {isExpanded && (
                        <div className="mt-3 pt-3 border-t border-slate-800 text-xs">
                          <div className="grid grid-cols-2 md:grid-cols-4 gap-2 mb-2 text-slate-300">
                            {item.status && (
                              <div>
                                <span className="text-slate-500">{isAr ? 'الحالة التشغيلية: ' : 'Status: '}</span>
                                <span className="font-semibold">{item.status}</span>
                              </div>
                            )}
                            {item.diameterMm !== null && (
                              <div>
                                <span className="text-slate-500">{isAr ? 'القطر: ' : 'Diameter: '}</span>
                                <span className="font-semibold">{item.diameterMm} mm</span>
                              </div>
                            )}
                            {item.material && (
                              <div>
                                <span className="text-slate-500">{isAr ? 'المادة: ' : 'Material: '}</span>
                                <span className="font-semibold">{item.material}</span>
                              </div>
                            )}
                            {item.flowDirection && (
                              <div>
                                <span className="text-slate-500">{isAr ? 'اتجاه السريان: ' : 'Flow: '}</span>
                                <span className="font-semibold">{item.flowDirection}</span>
                              </div>
                            )}
                          </div>

                          <div className="bg-slate-900 p-2.5 rounded-lg border border-slate-800 text-slate-400 font-mono text-[11px] overflow-x-auto max-h-40">
                            <pre>{JSON.stringify(JSON.parse(item.attributesJson || '{}'), null, 2)}</pre>
                          </div>
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>
            )
          ) : (
            /* Layer Approval Settings (Admin Only) */
            <div className="space-y-4">
              <div className="p-3.5 rounded-xl bg-slate-950 border border-slate-800 text-xs text-slate-400 flex items-start gap-2.5">
                <Sliders className="w-4 h-4 text-amber-400 shrink-0 mt-0.5" />
                <p>
                  {isAr
                    ? 'يمكن لمدير النظام تحديد الطبقات التي تتطلب موافقة المدير (APPROVER) قبل اعتماد التعديلات. التعديلات على الطبقات المفعلة لن تُحفظ في الجداول الأساسية مباشرة، بل تُوجّه إلى طابور الاعتماد.'
                    : 'Configure which layers enforce approval workflows. Edits to flagged layers will be intercepted and queued for managerial review before taking effect in production tables.'}
                </p>
              </div>

              <div className="border border-slate-800 rounded-xl overflow-hidden bg-slate-950/60">
                <table className="w-full text-xs text-start">
                  <thead className="bg-slate-900/80 text-slate-400 border-b border-slate-800">
                    <tr>
                      <th className="py-3 px-4 text-start font-semibold">{isAr ? 'كود الطبقة' : 'Code'}</th>
                      <th className="py-3 px-4 text-start font-semibold">{isAr ? 'اسم الطبقة' : 'Display Name'}</th>
                      <th className="py-3 px-4 text-start font-semibold">{isAr ? 'نوع الهندسة' : 'Geometry'}</th>
                      <th className="py-3 px-4 text-start font-semibold">{isAr ? 'النطاق' : 'Domain'}</th>
                      <th className="py-3 px-4 text-center font-semibold">{isAr ? 'يتطلب موافقة؟' : 'Requires Approval'}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-800/60">
                    {localFeatureTypes.map((ft) => {
                      const requires = !!ft.requiresApproval;
                      return (
                        <tr key={ft.code} className="hover:bg-slate-900/40 transition-colors">
                          <td className="py-3 px-4 font-mono font-medium text-slate-200">{ft.code}</td>
                          <td className="py-3 px-4 text-slate-300 font-medium">{ft.displayName}</td>
                          <td className="py-3 px-4 text-slate-400">
                            <span className="px-2 py-0.5 rounded bg-slate-800 text-slate-300 font-mono text-[11px]">
                              {ft.geometryKind}
                            </span>
                          </td>
                          <td className="py-3 px-4 text-slate-400">{ft.networkDomain}</td>
                          <td className="py-3 px-4 text-center">
                            <button
                              onClick={() => handleToggleLayerApproval(ft.code, requires)}
                              className={`relative inline-flex h-5 w-10 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none ${
                                requires ? 'bg-amber-600' : 'bg-slate-800'
                              }`}
                            >
                              <span
                                className={`pointer-events-none inline-block h-4 w-4 transform rounded-full bg-white shadow-lg ring-0 transition duration-200 ease-in-out ${
                                  requires
                                    ? isAr
                                      ? '-translate-x-5'
                                      : 'translate-x-5'
                                    : 'translate-x-0'
                                }`}
                              />
                            </button>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="px-6 py-3 border-t border-slate-800 bg-slate-950/60 flex items-center justify-between text-xs text-slate-500">
          <span>
            {isAr
              ? `إجمالي الطلبات المعروضة: ${filteredQueue.length}`
              : `Showing ${filteredQueue.length} pending proposals`}
          </span>
          <button
            onClick={onClose}
            className="px-4 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 font-medium transition-colors"
          >
            {isAr ? 'إغلاق' : 'Close'}
          </button>
        </div>
      </div>

      {/* Confirmation Modal for Approve */}
      {approvingItem && (
        <div className="fixed inset-0 z-60 flex items-center justify-center bg-black/70 backdrop-blur-sm p-4">
          <div
            className="bg-slate-900 border border-slate-700 rounded-xl p-5 max-w-md w-full shadow-2xl text-slate-100"
            dir={isAr ? 'rtl' : 'ltr'}
          >
            <div className="flex items-center gap-3 text-emerald-400 mb-3">
              <CheckCircle2 className="w-6 h-6" />
              <h3 className="font-bold text-base">
                {isAr ? 'تأكيد اعتماد التعديل' : 'Confirm Change Approval'}
              </h3>
            </div>
            <p className="text-xs text-slate-300 leading-relaxed">
              {isAr
                ? `هل أنت متأكد من اعتماد العملية (${approvingItem.operation}) على المعلم "${approvingItem.name}" في الطبقة "${approvingItem.featureTypeCode}"؟ سيتم تطبيق التعديل مباشرة على قاعدة البيانات مع توثيق اسمك واسم المقترح في سجل التدقيق.`
                : `Are you sure you want to approve (${approvingItem.operation}) for "${approvingItem.name}" in layer "${approvingItem.featureTypeCode}"? This change will be permanently committed to production.`}
            </p>

            <div className="flex justify-end gap-2 mt-5">
              <button
                onClick={() => setApprovingItem(null)}
                disabled={isSubmittingAction}
                className="px-3.5 py-1.5 text-xs text-slate-400 hover:text-slate-200 hover:bg-slate-800 rounded-lg transition-colors"
              >
                {isAr ? 'إلغاء' : 'Cancel'}
              </button>
              <button
                onClick={() => handleApprove(approvingItem)}
                disabled={isSubmittingAction}
                className="px-4 py-1.5 text-xs font-semibold rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white transition-colors"
              >
                {isSubmittingAction ? (isAr ? 'جاري الاعتماد...' : 'Approving...') : (isAr ? 'تأكيد الاعتماد' : 'Confirm Approval')}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Rejection Modal with Mandatory Reason */}
      {rejectingItem && (
        <div className="fixed inset-0 z-60 flex items-center justify-center bg-black/70 backdrop-blur-sm p-4">
          <div
            className="bg-slate-900 border border-slate-700 rounded-xl p-5 max-w-md w-full shadow-2xl text-slate-100"
            dir={isAr ? 'rtl' : 'ltr'}
          >
            <div className="flex items-center gap-3 text-rose-400 mb-3">
              <XCircle className="w-6 h-6" />
              <h3 className="font-bold text-base">
                {isAr ? 'رفض مقترح التعديل' : 'Reject Change Proposal'}
              </h3>
            </div>
            <p className="text-xs text-slate-300 mb-3">
              {isAr
                ? `يرجى تحديد سبب رفض العملية (${rejectingItem.operation}) على المعلم "${rejectingItem.name}". سيتم توثيق السبب لإشعار المقترح.`
                : `Please provide an explanation for rejecting this ${rejectingItem.operation} change on "${rejectingItem.name}".`}
            </p>

            <textarea
              rows={3}
              placeholder={isAr ? 'اكتب سبب الرفض هنا بالتفصيل (إلزامي)...' : 'Enter rejection reason (mandatory)...'}
              value={rejectionReason}
              onChange={(e) => setRejectionReason(e.target.value)}
              className="w-full bg-slate-950 border border-slate-800 rounded-lg p-2.5 text-xs text-slate-200 focus:outline-none focus:border-rose-500 placeholder-slate-600 resize-none"
            />

            <div className="flex justify-end gap-2 mt-4">
              <button
                onClick={() => {
                  setRejectingItem(null);
                  setRejectionReason('');
                }}
                disabled={isSubmittingAction}
                className="px-3.5 py-1.5 text-xs text-slate-400 hover:text-slate-200 hover:bg-slate-800 rounded-lg transition-colors"
              >
                {isAr ? 'إلغاء' : 'Cancel'}
              </button>
              <button
                onClick={handleReject}
                disabled={isSubmittingAction || !rejectionReason.trim()}
                className="px-4 py-1.5 text-xs font-semibold rounded-lg bg-rose-600 hover:bg-rose-500 disabled:bg-rose-900/40 text-white transition-colors"
              >
                {isSubmittingAction ? (isAr ? 'جاري الرفض...' : 'Rejecting...') : (isAr ? 'تأكيد الرفض' : 'Confirm Rejection')}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
