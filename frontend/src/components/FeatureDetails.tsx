'use client';

import React, { useState, useEffect } from 'react';
import { useAuth } from '@/lib/authContext';
import {
  GeoJsonFeature,
  FeatureAttachmentDto,
  FeatureRelationshipDto,
  FeatureLockDto,
} from '@/lib/types';
import { api } from '@/lib/api';
import {
  X,
  MapPin,
  Activity,
  Info,
  Lock,
  Unlock,
  Edit3,
  Trash2,
  History,
  Paperclip,
  Link2,
  Upload,
  Download,
  Loader2,
  AlertTriangle,
  Plus,
  FileText,
  File,
  CheckCircle2,
} from 'lucide-react';

interface FeatureDetailsProps {
  feature: GeoJsonFeature | null;
  onClose: () => void;
  onEdit?: (feature: GeoJsonFeature) => void;
  onDelete?: (feature: GeoJsonFeature) => void;
  onShowHistory?: (feature: GeoJsonFeature) => void;
}

export const FeatureDetails: React.FC<FeatureDetailsProps> = ({
  feature,
  onClose,
  onEdit,
  onDelete,
  onShowHistory,
}) => {
  const { lang, canEdit, user } = useAuth();
  const isAr = lang === 'ar';

  const [activeTab, setActiveTab] = useState<'DETAILS' | 'ATTACHMENTS' | 'RELATIONSHIPS'>('DETAILS');

  // Enterprise State
  const [attachments, setAttachments] = useState<FeatureAttachmentDto[]>([]);
  const [loadingAttachments, setLoadingAttachments] = useState(false);
  const [uploadingAttachment, setUploadingAttachment] = useState(false);
  const [attachmentDesc, setAttachmentDesc] = useState('');

  const [relationships, setRelationships] = useState<FeatureRelationshipDto[]>([]);
  const [loadingRelationships, setLoadingRelationships] = useState(false);
  const [showAddRel, setShowAddRel] = useState(false);
  const [relTargetId, setRelTargetId] = useState('');
  const [relTargetTable, setRelTargetTable] = useState<'points' | 'lines' | 'polygons'>('points');
  const [relType, setRelType] = useState<string>('CONTAINS');
  const [relDesc, setRelDesc] = useState('');
  const [creatingRel, setCreatingRel] = useState(false);

  const [lockStatus, setLockStatus] = useState<FeatureLockDto | null>(null);
  const [lockingLoading, setLockingLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  if (!feature) {
    return null;
  }

  const { properties, id, geometry } = feature;
  const attributes = properties.attributes || {};

  const getFeatureTable = (f: GeoJsonFeature): string => {
    const kind = (f.properties.geometryKind || f.geometry.type || '').toLowerCase();
    if (kind.includes('point')) return 'points';
    if (kind.includes('line')) return 'lines';
    if (kind.includes('poly')) return 'polygons';
    return 'points';
  };

  const currentTable = getFeatureTable(feature);

  // Load Enterprise Data
  useEffect(() => {
    if (!id) return;
    setErrorMessage(null);

    // 1. Load Lock Status
    api
      .getFeatureLock(id)
      .then((l) => setLockStatus(l.isLocked ? l : null))
      .catch(() => setLockStatus(null));

    // 2. Load Attachments
    setLoadingAttachments(true);
    api
      .getFeatureAttachments(id)
      .then((data) => setAttachments(data))
      .catch((err) => console.error('Failed to load attachments:', err))
      .finally(() => setLoadingAttachments(false));

    // 3. Load Relationships
    setLoadingRelationships(true);
    api
      .getFeatureRelationships(id)
      .then((data) => setRelationships(data))
      .catch((err) => console.error('Failed to load relationships:', err))
      .finally(() => setLoadingRelationships(false));
  }, [id]);

  // Lock Actions
  const handleToggleLock = async () => {
    if (!canEdit) return;
    setLockingLoading(true);
    setErrorMessage(null);
    try {
      if (lockStatus?.isLockedByCurrentUser) {
        // Release Lock
        await api.releaseFeatureLock(id);
        setLockStatus(null);
      } else {
        // Acquire Lock
        const newLock = await api.acquireFeatureLock(id, currentTable, 10);
        setLockStatus(newLock);
      }
    } catch (err: any) {
      setErrorMessage(err.message || 'Lock operation failed');
    } finally {
      setLockingLoading(false);
    }
  };

  // Attachment Actions
  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setUploadingAttachment(true);
    setErrorMessage(null);
    try {
      const uploaded = await api.uploadFeatureAttachment(id, currentTable, file, attachmentDesc);
      setAttachments((prev) => [uploaded, ...prev]);
      setAttachmentDesc('');
      e.target.value = '';
    } catch (err: any) {
      setErrorMessage(err.message || 'File upload failed');
    } finally {
      setUploadingAttachment(false);
    }
  };

  const handleDownloadAttachment = async (att: FeatureAttachmentDto) => {
    try {
      await api.downloadFeatureAttachment(att.id, att.fileName);
    } catch (err: any) {
      setErrorMessage(err.message || 'Download failed');
    }
  };

  const handleDeleteAttachment = async (attId: string) => {
    if (!window.confirm(isAr ? 'هل أنت متأكد من حذف هذا المرفق؟' : 'Delete this attachment?')) return;
    try {
      await api.deleteFeatureAttachment(attId);
      setAttachments((prev) => prev.filter((a) => a.id !== attId));
    } catch (err: any) {
      setErrorMessage(err.message || 'Delete attachment failed');
    }
  };

  // Relationship Actions
  const handleCreateRelationship = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!relTargetId.trim()) return;

    setCreatingRel(true);
    setErrorMessage(null);
    try {
      const created = await api.createFeatureRelationship({
        sourceFeatureId: id,
        sourceTable: currentTable,
        targetFeatureId: relTargetId.trim(),
        targetTable: relTargetTable,
        relationshipType: relType,
        description: relDesc.trim() || undefined,
      });

      setRelationships((prev) => [created, ...prev]);
      setRelTargetId('');
      setRelDesc('');
      setShowAddRel(false);
    } catch (err: any) {
      setErrorMessage(err.message || 'Create relationship failed');
    } finally {
      setCreatingRel(false);
    }
  };

  const handleDeleteRelationship = async (relId: string) => {
    if (!window.confirm(isAr ? 'هل أنت متأكد من إزالة هذا الرابط؟' : 'Remove this relationship?')) return;
    try {
      await api.deleteFeatureRelationship(relId);
      setRelationships((prev) => prev.filter((r) => r.id !== relId));
    } catch (err: any) {
      setErrorMessage(err.message || 'Delete relationship failed');
    }
  };

  const formatBytes = (bytes: number) => {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  };

  const isLockedByOther = lockStatus?.isLocked && !lockStatus.isLockedByCurrentUser;

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
    <div className="w-84 bg-slate-900 border-l rtl:border-l-0 rtl:border-r border-slate-800 flex flex-col h-full shadow-2xl z-20 overflow-hidden">
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
            {properties.version !== undefined && (
              <span className="text-[10px] text-amber-400 font-mono bg-amber-950/60 px-1.5 py-0.5 rounded border border-amber-800/50">
                v{properties.version}
              </span>
            )}
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

      {/* Lock Banner / Multi-user edit status */}
      {isLockedByOther ? (
        <div className="px-3 py-2 bg-rose-950/60 border-b border-rose-800/60 text-rose-200 text-xs flex items-center justify-between gap-2">
          <div className="flex items-center gap-1.5">
            <Lock className="w-3.5 h-3.5 text-rose-400 flex-shrink-0" />
            <span>
              {isAr ? `مقفل بواسطة ${lockStatus.lockedByUsername}` : `Locked by ${lockStatus.lockedByUsername}`}
            </span>
          </div>
          <span className="text-[10px] font-mono text-rose-300">
            {new Date(lockStatus.expiresAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
          </span>
        </div>
      ) : lockStatus?.isLockedByCurrentUser ? (
        <div className="px-3 py-2 bg-emerald-950/60 border-b border-emerald-800/60 text-emerald-200 text-xs flex items-center justify-between gap-2">
          <div className="flex items-center gap-1.5">
            <Lock className="w-3.5 h-3.5 text-emerald-400 flex-shrink-0" />
            <span>{isAr ? 'قفل التعديل نشط لك' : 'Edit Lock active (You)'}</span>
          </div>
          <button
            onClick={handleToggleLock}
            disabled={lockingLoading}
            className="text-[11px] text-emerald-300 hover:text-white underline"
          >
            {isAr ? 'إلغاء القفل' : 'Unlock'}
          </button>
        </div>
      ) : null}

      {/* Action Toolbar */}
      <div className="p-3 bg-slate-950/80 border-b border-slate-800 flex items-center justify-between gap-2">
        <button
          onClick={() => onShowHistory?.(feature)}
          className="flex-1 py-1.5 px-2 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-medium flex items-center justify-center gap-1.5 transition border border-slate-700/60"
        >
          <History className="w-3.5 h-3.5 text-sky-400" />
          <span>{isAr ? 'السجل' : 'History'}</span>
        </button>

        {canEdit && (
          <>
            {/* Lock / Unlock Toggle Button */}
            <button
              onClick={handleToggleLock}
              disabled={lockingLoading || isLockedByOther}
              className={`py-1.5 px-2 rounded-lg text-xs font-medium flex items-center justify-center gap-1 transition border ${
                lockStatus?.isLockedByCurrentUser
                  ? 'bg-emerald-600/20 text-emerald-300 border-emerald-500/40 hover:bg-emerald-600/30'
                  : 'bg-slate-800 text-slate-300 border-slate-700/60 hover:bg-slate-700'
              }`}
              title={isAr ? 'قفل المعلم لمنع تعديل الآخرين في نفس الوقت' : 'Lock feature to prevent multi-user edit conflicts'}
            >
              {lockingLoading ? (
                <Loader2 className="w-3.5 h-3.5 animate-spin" />
              ) : lockStatus?.isLockedByCurrentUser ? (
                <Lock className="w-3.5 h-3.5 text-emerald-400" />
              ) : (
                <Unlock className="w-3.5 h-3.5 text-slate-400" />
              )}
              <span>{lockStatus?.isLockedByCurrentUser ? (isAr ? 'مقفل' : 'Locked') : (isAr ? 'قفل' : 'Lock')}</span>
            </button>

            <button
              onClick={() => onEdit?.(feature)}
              disabled={isLockedByOther}
              className={`flex-1 py-1.5 px-2 rounded-lg text-xs font-medium flex items-center justify-center gap-1.5 transition border ${
                isLockedByOther
                  ? 'bg-slate-800/40 text-slate-500 border-slate-800 cursor-not-allowed'
                  : 'bg-sky-600/20 hover:bg-sky-600/30 text-sky-300 border-sky-500/40'
              }`}
            >
              <Edit3 className="w-3.5 h-3.5" />
              <span>{isAr ? 'تعديل' : 'Edit'}</span>
            </button>

            <button
              onClick={() => onDelete?.(feature)}
              disabled={isLockedByOther}
              className={`py-1.5 px-2.5 rounded-lg text-xs font-medium flex items-center justify-center transition border ${
                isLockedByOther
                  ? 'bg-slate-800/40 text-slate-500 border-slate-800 cursor-not-allowed'
                  : 'bg-rose-500/20 hover:bg-rose-500/30 text-rose-300 border-rose-500/40'
              }`}
              title={isAr ? 'حذف المعلم' : 'Delete feature'}
            >
              <Trash2 className="w-3.5 h-3.5" />
            </button>
          </>
        )}
      </div>

      {/* Tabs */}
      <div className="flex items-center border-b border-slate-800 bg-slate-950/60 text-xs px-2">
        <button
          onClick={() => setActiveTab('DETAILS')}
          className={`flex-1 py-2 font-medium border-b-2 transition flex items-center justify-center gap-1 ${
            activeTab === 'DETAILS'
              ? 'border-sky-500 text-sky-400 font-bold'
              : 'border-transparent text-slate-400 hover:text-slate-200'
          }`}
        >
          <Info className="w-3.5 h-3.5" />
          <span>{isAr ? 'الخصائص' : 'Details'}</span>
        </button>

        <button
          onClick={() => setActiveTab('ATTACHMENTS')}
          className={`flex-1 py-2 font-medium border-b-2 transition flex items-center justify-center gap-1 ${
            activeTab === 'ATTACHMENTS'
              ? 'border-purple-500 text-purple-400 font-bold'
              : 'border-transparent text-slate-400 hover:text-slate-200'
          }`}
        >
          <Paperclip className="w-3.5 h-3.5" />
          <span>{isAr ? `المرفقات (${attachments.length})` : `Files (${attachments.length})`}</span>
        </button>

        <button
          onClick={() => setActiveTab('RELATIONSHIPS')}
          className={`flex-1 py-2 font-medium border-b-2 transition flex items-center justify-center gap-1 ${
            activeTab === 'RELATIONSHIPS'
              ? 'border-teal-500 text-teal-400 font-bold'
              : 'border-transparent text-slate-400 hover:text-slate-200'
          }`}
        >
          <Link2 className="w-3.5 h-3.5" />
          <span>{isAr ? `العلاقات (${relationships.length})` : `Links (${relationships.length})`}</span>
        </button>
      </div>

      {/* Error message alert */}
      {errorMessage && (
        <div className="p-3 bg-rose-500/10 border-b border-rose-500/30 text-rose-300 text-xs flex items-center gap-2">
          <AlertTriangle className="w-4 h-4 flex-shrink-0" />
          <span>{errorMessage}</span>
        </div>
      )}

      {/* Body Content */}
      <div className="flex-1 overflow-y-auto p-4 space-y-4">
        {/* ================= DETAILS TAB ================= */}
        {activeTab === 'DETAILS' && (
          <>
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

            {/* Engineering Properties */}
            {(properties.diameterMm || properties.flowDirection || properties.material || properties.isOpen !== undefined) && (
              <div className="space-y-1.5">
                <h3 className="text-xs font-semibold text-slate-300 uppercase tracking-wider flex items-center gap-1.5">
                  <MapPin className="w-3.5 h-3.5 text-sky-400" />
                  <span>{isAr ? 'الخصائص الهندسية' : 'Engineering Properties'}</span>
                </h3>

                <div className="rounded-xl border border-slate-800 overflow-hidden divide-y divide-slate-800 bg-slate-950/40">
                  {properties.isOpen !== undefined && (
                    <div className="px-3 py-2 flex items-center justify-between text-xs">
                      <span className="text-slate-400 text-[11px]">{isAr ? 'مفتوح للتدفق' : 'Flow Open'}</span>
                      <span className={`font-semibold ${properties.isOpen ? 'text-emerald-400' : 'text-rose-400'}`}>
                        {properties.isOpen ? (isAr ? 'نعم (مفتوح)' : 'Yes') : (isAr ? 'لا (مغلق)' : 'No')}
                      </span>
                    </div>
                  )}

                  {properties.flowDirection && (
                    <div className="px-3 py-2 flex items-center justify-between text-xs">
                      <span className="text-slate-400 text-[11px]">{isAr ? 'اتجاه السريان' : 'Flow Direction'}</span>
                      <span className="font-mono text-slate-200 text-[11px]">{properties.flowDirection}</span>
                    </div>
                  )}

                  {properties.diameterMm && (
                    <div className="px-3 py-2 flex items-center justify-between text-xs">
                      <span className="text-slate-400 text-[11px]">{isAr ? 'القطر' : 'Diameter'}</span>
                      <span className="font-semibold text-slate-200">{properties.diameterMm} mm</span>
                    </div>
                  )}

                  {properties.material && (
                    <div className="px-3 py-2 flex items-center justify-between text-xs">
                      <span className="text-slate-400 text-[11px]">{isAr ? 'مادة الصنع' : 'Material'}</span>
                      <span className="font-semibold text-slate-200">{properties.material}</span>
                    </div>
                  )}
                </div>
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
          </>
        )}

        {/* ================= ATTACHMENTS TAB ================= */}
        {activeTab === 'ATTACHMENTS' && (
          <div className="space-y-4">
            {/* Upload Box (Only for editors) */}
            {canEdit && (
              <div className="p-3 rounded-xl bg-slate-950/60 border border-dashed border-purple-500/40 space-y-2.5">
                <div className="flex items-center justify-between text-xs font-semibold text-purple-300">
                  <span className="flex items-center gap-1.5">
                    <Upload className="w-3.5 h-3.5" />
                    <span>{isAr ? 'إرفاق ملف هندسي أو صورة' : 'Attach Engineering Document'}</span>
                  </span>
                  {uploadingAttachment && <Loader2 className="w-3.5 h-3.5 animate-spin text-purple-400" />}
                </div>

                <input
                  type="text"
                  value={attachmentDesc}
                  onChange={(e) => setAttachmentDesc(e.target.value)}
                  placeholder={isAr ? 'وصف المرفق (اختياري)...' : 'Attachment description (optional)...'}
                  className="w-full bg-slate-900 border border-slate-800 rounded-lg px-2.5 py-1 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-purple-500"
                />

                <label className="flex items-center justify-center gap-2 w-full py-1.5 px-3 rounded-lg bg-purple-600/20 hover:bg-purple-600/30 text-purple-300 text-xs font-medium cursor-pointer border border-purple-500/30 transition">
                  <Paperclip className="w-3.5 h-3.5" />
                  <span>{uploadingAttachment ? (isAr ? 'جاري الرفع...' : 'Uploading...') : (isAr ? 'اختر ملفاً (PDF, CAD, صور)' : 'Select file (PDF, CAD, Photo)')}</span>
                  <input
                    type="file"
                    className="hidden"
                    disabled={uploadingAttachment}
                    accept=".pdf,.png,.jpg,.jpeg,.dwg,.dxf,.csv,.docx,.xlsx"
                    onChange={handleFileUpload}
                  />
                </label>
                <div className="text-[10px] text-slate-500 text-center">
                  {isAr ? 'الحد الأقصى: 25 ميجابايت (.pdf, .dwg, .dxf, .png, .jpg)' : 'Max 25 MB (.pdf, .dwg, .dxf, .png, .jpg)'}
                </div>
              </div>
            )}

            {/* Attachments List */}
            {loadingAttachments ? (
              <div className="flex items-center justify-center py-6 text-slate-400 text-xs gap-2">
                <Loader2 className="w-4 h-4 animate-spin text-purple-400" />
                <span>{isAr ? 'تحميل المرفقات...' : 'Loading attachments...'}</span>
              </div>
            ) : attachments.length === 0 ? (
              <div className="p-4 rounded-xl bg-slate-950/40 border border-slate-800 text-center text-slate-500 text-xs">
                {isAr ? 'لا توجد مرفقات مسجلة لهذا المعلم' : 'No attachments linked to this feature'}
              </div>
            ) : (
              <div className="space-y-2">
                {attachments.map((att) => (
                  <div
                    key={att.id}
                    className="p-2.5 rounded-xl bg-slate-950/60 border border-slate-800 hover:border-slate-700 transition flex items-center justify-between gap-2"
                  >
                    <div className="min-w-0 flex-1 space-y-0.5">
                      <div className="text-xs font-semibold text-white truncate flex items-center gap-1.5">
                        <FileText className="w-3.5 h-3.5 text-purple-400 flex-shrink-0" />
                        <span className="truncate">{att.fileName}</span>
                      </div>
                      {att.description && (
                        <div className="text-[10px] text-slate-400 truncate">{att.description}</div>
                      )}
                      <div className="text-[10px] font-mono text-slate-500 flex items-center gap-2">
                        <span>{formatBytes(att.fileSizeBytes)}</span>
                        <span>•</span>
                        <span>{att.uploadedBy}</span>
                        <span>•</span>
                        <span>{new Date(att.uploadedAt).toLocaleDateString()}</span>
                      </div>
                    </div>

                    <div className="flex items-center gap-1">
                      <button
                        onClick={() => handleDownloadAttachment(att)}
                        className="p-1.5 rounded-lg text-slate-400 hover:text-white hover:bg-slate-800 transition"
                        title={isAr ? 'تنزيل المرفق' : 'Download file'}
                      >
                        <Download className="w-3.5 h-3.5 text-sky-400" />
                      </button>
                      {canEdit && (
                        <button
                          onClick={() => handleDeleteAttachment(att.id)}
                          className="p-1.5 rounded-lg text-slate-400 hover:text-rose-400 hover:bg-slate-800 transition"
                          title={isAr ? 'حذف المرفق' : 'Delete file'}
                        >
                          <Trash2 className="w-3.5 h-3.5" />
                        </button>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {/* ================= RELATIONSHIPS TAB ================= */}
        {activeTab === 'RELATIONSHIPS' && (
          <div className="space-y-4">
            {/* Add Relationship Button / Form */}
            {canEdit && (
              <div>
                {!showAddRel ? (
                  <button
                    onClick={() => setShowAddRel(true)}
                    className="w-full py-1.5 px-3 rounded-lg bg-teal-600/20 hover:bg-teal-600/30 text-teal-300 text-xs font-medium flex items-center justify-center gap-1.5 border border-teal-500/30 transition"
                  >
                    <Plus className="w-3.5 h-3.5" />
                    <span>{isAr ? 'إضافة علاقة مع معلم آخر' : 'Link with Another Feature'}</span>
                  </button>
                ) : (
                  <form onSubmit={handleCreateRelationship} className="p-3 rounded-xl bg-slate-950/70 border border-teal-500/40 space-y-2.5">
                    <div className="flex items-center justify-between text-xs font-semibold text-teal-300">
                      <span>{isAr ? 'ربط معلم جديد' : 'New Feature Link'}</span>
                      <button
                        type="button"
                        onClick={() => setShowAddRel(false)}
                        className="text-slate-400 hover:text-white"
                      >
                        <X className="w-3.5 h-3.5" />
                      </button>
                    </div>

                    <div>
                      <label className="text-[10px] text-slate-400 block mb-0.5">
                        {isAr ? 'معرّف المعلم الهدف (UUID)' : 'Target Feature ID (UUID)'}
                      </label>
                      <input
                        type="text"
                        required
                        value={relTargetId}
                        onChange={(e) => setRelTargetId(e.target.value)}
                        placeholder="00000000-0000-0000-0000-000000000000"
                        className="w-full bg-slate-900 border border-slate-800 rounded-lg px-2.5 py-1 text-xs text-white font-mono placeholder-slate-600 focus:outline-none focus:border-teal-500"
                      />
                    </div>

                    <div className="grid grid-cols-2 gap-2">
                      <div>
                        <label className="text-[10px] text-slate-400 block mb-0.5">
                          {isAr ? 'نوع الهندسة' : 'Target Table'}
                        </label>
                        <select
                          value={relTargetTable}
                          onChange={(e) => setRelTargetTable(e.target.value as any)}
                          className="w-full bg-slate-900 border border-slate-800 rounded-lg px-2 py-1 text-xs text-white focus:outline-none focus:border-teal-500"
                        >
                          <option value="points">{isAr ? 'نقاط (points)' : 'Points'}</option>
                          <option value="lines">{isAr ? 'خطوط (lines)' : 'Lines'}</option>
                          <option value="polygons">{isAr ? 'مضلعات (polygons)' : 'Polygons'}</option>
                        </select>
                      </div>

                      <div>
                        <label className="text-[10px] text-slate-400 block mb-0.5">
                          {isAr ? 'نوع العلاقة' : 'Relation Type'}
                        </label>
                        <select
                          value={relType}
                          onChange={(e) => setRelType(e.target.value)}
                          className="w-full bg-slate-900 border border-slate-800 rounded-lg px-2 py-1 text-xs text-white focus:outline-none focus:border-teal-500"
                        >
                          <option value="CONTAINS">CONTAINS (يحتوي)</option>
                          <option value="SERVES">SERVES (يخدم)</option>
                          <option value="ATTACHED_TO">ATTACHED_TO (مرتبط بـ)</option>
                          <option value="FEEDS">FEEDS (يغذي)</option>
                          <option value="ASSOCIATED_WITH">ASSOCIATED_WITH (ملحق)</option>
                        </select>
                      </div>
                    </div>

                    <div>
                      <input
                        type="text"
                        value={relDesc}
                        onChange={(e) => setRelDesc(e.target.value)}
                        placeholder={isAr ? 'ملاحظة أو وصف للعلاقة...' : 'Relation description...'}
                        className="w-full bg-slate-900 border border-slate-800 rounded-lg px-2.5 py-1 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-teal-500"
                      />
                    </div>

                    <button
                      type="submit"
                      disabled={creatingRel}
                      className="w-full py-1.5 rounded-lg bg-teal-600 hover:bg-teal-500 text-white text-xs font-semibold flex items-center justify-center gap-1.5 transition"
                    >
                      {creatingRel ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Link2 className="w-3.5 h-3.5" />}
                      <span>{isAr ? 'حفظ الرابط' : 'Save Link'}</span>
                    </button>
                  </form>
                )}
              </div>
            )}

            {/* Relationships List */}
            {loadingRelationships ? (
              <div className="flex items-center justify-center py-6 text-slate-400 text-xs gap-2">
                <Loader2 className="w-4 h-4 animate-spin text-teal-400" />
                <span>{isAr ? 'تحميل العلاقات...' : 'Loading relationships...'}</span>
              </div>
            ) : relationships.length === 0 ? (
              <div className="p-4 rounded-xl bg-slate-950/40 border border-slate-800 text-center text-slate-500 text-xs">
                {isAr ? 'لا توجد علاقات مكانية مسجلة لهذا المعلم' : 'No relationships linked to this feature'}
              </div>
            ) : (
              <div className="space-y-2">
                {relationships.map((rel) => {
                  const isSource = rel.sourceFeatureId === id;
                  const targetName = isSource ? (rel.targetFeatureName || rel.targetFeatureId) : (rel.sourceFeatureName || rel.sourceFeatureId);
                  const targetCode = isSource ? rel.targetFeatureTypeCode : rel.sourceFeatureTypeCode;

                  return (
                    <div
                      key={rel.id}
                      className="p-2.5 rounded-xl bg-slate-950/60 border border-slate-800 hover:border-slate-700 transition flex items-center justify-between gap-2"
                    >
                      <div className="min-w-0 flex-1 space-y-1">
                        <div className="flex items-center gap-2">
                          <span className="px-1.5 py-0.5 rounded text-[10px] font-bold bg-teal-500/20 text-teal-300 border border-teal-500/40 font-mono">
                            {rel.relationshipType}
                          </span>
                          {targetCode && (
                            <span className="text-[10px] text-slate-400 font-mono">
                              [{targetCode}]
                            </span>
                          )}
                        </div>
                        <div className="text-xs font-semibold text-white truncate font-mono">
                          {targetName}
                        </div>
                        {rel.description && (
                          <div className="text-[10px] text-slate-400 truncate">{rel.description}</div>
                        )}
                        <div className="text-[9px] text-slate-500 font-mono">
                          {isAr ? 'بواسطة:' : 'By:'} {rel.createdBy} • {new Date(rel.createdAt).toLocaleDateString()}
                        </div>
                      </div>

                      {canEdit && (
                        <button
                          onClick={() => handleDeleteRelationship(rel.id)}
                          className="p-1.5 rounded-lg text-slate-400 hover:text-rose-400 hover:bg-slate-800 transition"
                          title={isAr ? 'حذف الرابط' : 'Remove relationship'}
                        >
                          <Trash2 className="w-3.5 h-3.5" />
                        </button>
                      )}
                    </div>
                  );
                })}
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
};
