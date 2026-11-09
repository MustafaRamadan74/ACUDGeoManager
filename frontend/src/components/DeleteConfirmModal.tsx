'use client';

import React, { useState } from 'react';
import { GeoJsonFeature } from '@/lib/types';
import { api, ApiError } from '@/lib/api';
import { useToast } from '@/components/Toast';
import { useAuth } from '@/lib/authContext';
import { AlertTriangle, Trash2, Loader2, X } from 'lucide-react';

interface DeleteConfirmModalProps {
  feature: GeoJsonFeature | null;
  onClose: () => void;
  onDeleted: (deletedId: string) => void;
}

export const DeleteConfirmModal: React.FC<DeleteConfirmModalProps> = ({
  feature,
  onClose,
  onDeleted,
}) => {
  const { lang } = useAuth();
  const { showSuccess, showError, showConflict } = useToast();
  const [isDeleting, setIsDeleting] = useState(false);
  const isAr = lang === 'ar';

  if (!feature) return null;

  const { id, properties, geometry } = feature;
  const version = properties.version ?? 1;
  const geometryKind = properties.geometryKind || (
    geometry.type === 'Point' ? 'POINT' :
    geometry.type === 'LineString' ? 'LINE' : 'POLYGON'
  );

  const handleDelete = async () => {
    setIsDeleting(true);
    try {
      let res: any = null;
      if (geometryKind === 'POINT') {
        res = await api.deletePoint(id, version);
      } else if (geometryKind === 'LINE') {
        res = await api.deleteLine(id, version);
      } else {
        res = await api.deletePolygon(id, version);
      }

      if (res?.isPendingApproval) {
        showSuccess(
          isAr ? 'تم إرسال طلب الحذف للاعتماد' : 'Deletion Submitted for Approval',
          res.message || (isAr ? 'هذه الطبقة تتطلب اعتماد المدير. تم إرسال طلب الحذف للمراجعة.' : 'Layer requires approval. Deletion queued.')
        );
        onClose();
        return;
      }

      showSuccess(
        isAr ? 'تم حذف المعلم بنجاح' : 'Feature deleted successfully',
        isAr ? `تمت إزالة المعلم "${properties.name}" نهائياً من شبكة المرافق.` : `Feature "${properties.name}" removed.`
      );
      onDeleted(id);
      onClose();
    } catch (err: any) {
      console.error('Delete feature failed:', err);
      if (err instanceof ApiError && err.status === 409) {
        showConflict(
          isAr
            ? `تعارض في الإصدار (409): لقد تم تعديل هذا المعلم بواسطة شخص آخر أثناء محاولة حذفه. يرجى إعادة تحميل الخريطة.`
            : `Version conflict (409): Feature was modified concurrently by another user.`
        );
      } else {
        showError(
          isAr ? 'فشل حذف المعلم' : 'Failed to delete feature',
          err?.message || (isAr ? 'حدث خطأ غير متوقع أثناء الحذف.' : 'An error occurred.')
        );
      }
    } finally {
      setIsDeleting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-sm animate-in fade-in duration-200">
      <div className="bg-slate-900 border border-slate-700/80 rounded-2xl max-w-md w-full shadow-2xl overflow-hidden p-6 space-y-5">
        {/* Header Icon + Title */}
        <div className="flex items-start gap-4">
          <div className="w-12 h-12 rounded-xl bg-rose-500/10 border border-rose-500/30 flex items-center justify-center text-rose-400 flex-shrink-0">
            <AlertTriangle className="w-6 h-6 animate-pulse" />
          </div>

          <div className="flex-1">
            <h3 className="text-base font-bold text-white leading-tight">
              {isAr ? 'تأكيد حذف المعلم الجغرافي' : 'Confirm Feature Deletion'}
            </h3>
            <p className="text-xs text-slate-400 mt-1">
              {isAr
                ? 'هذا الإجراء سيقوم بحذف المعلم نهائياً وتسجيل العملية في سجل التدقيق.'
                : 'This action will permanently delete this feature and log the audit change.'}
            </p>
          </div>

          <button
            onClick={onClose}
            disabled={isDeleting}
            className="p-1 text-slate-400 hover:text-white rounded-lg hover:bg-slate-800 transition"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Feature summary */}
        <div className="bg-slate-950/60 rounded-xl p-3.5 border border-slate-800 space-y-2 text-xs">
          <div className="flex justify-between items-center">
            <span className="text-slate-400">{isAr ? 'الاسم:' : 'Name:'}</span>
            <span className="font-bold text-white">{properties.name}</span>
          </div>
          <div className="flex justify-between items-center">
            <span className="text-slate-400">{isAr ? 'النوع:' : 'Type:'}</span>
            <span className="font-mono text-sky-400">{properties.featureTypeCode} ({geometryKind})</span>
          </div>
          <div className="flex justify-between items-center">
            <span className="text-slate-400">{isAr ? 'الإصدار الحالي:' : 'Current Version:'}</span>
            <span className="font-mono text-amber-400">v{version}</span>
          </div>
          <div className="flex justify-between items-center">
            <span className="text-slate-400">ID:</span>
            <span className="font-mono text-slate-500 text-[11px] truncate max-w-[200px]">{id}</span>
          </div>
        </div>

        {/* Warning text */}
        <div className="text-xs text-rose-300/90 bg-rose-950/30 p-3 rounded-lg border border-rose-900/40 leading-relaxed">
          {isAr
            ? 'تنبيه: سيتم تسجيل أمر الحذف مع معرّف المستخدم ووقت العملية، ولن يظهر المعلم على الخريطة.'
            : 'Warning: The deletion will be audited with your user ID and timestamp, and will be removed from the map.'}
        </div>

        {/* Actions */}
        <div className="flex items-center justify-end gap-3 pt-2">
          <button
            type="button"
            onClick={onClose}
            disabled={isDeleting}
            className="px-4 py-2 rounded-xl text-xs font-semibold text-slate-300 hover:text-white bg-slate-800 hover:bg-slate-700 transition"
          >
            {isAr ? 'إلغاء' : 'Cancel'}
          </button>

          <button
            type="button"
            onClick={handleDelete}
            disabled={isDeleting}
            className="px-4 py-2 rounded-xl text-xs font-semibold text-white bg-rose-600 hover:bg-rose-500 disabled:opacity-50 transition flex items-center gap-2 shadow-lg shadow-rose-950/50"
          >
            {isDeleting ? (
              <>
                <Loader2 className="w-4 h-4 animate-spin" />
                <span>{isAr ? 'جارٍ الحذف...' : 'Deleting...'}</span>
              </>
            ) : (
              <>
                <Trash2 className="w-4 h-4" />
                <span>{isAr ? 'تأكيد الحذف' : 'Confirm Delete'}</span>
              </>
            )}
          </button>
        </div>
      </div>
    </div>
  );
};

