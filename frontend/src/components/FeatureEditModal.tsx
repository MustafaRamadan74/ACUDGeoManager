'use client';

import React, { useState, useEffect } from 'react';
import { z } from 'zod';
import { FeatureTypeDto, GeoJsonFeature, NetworkDto } from '@/lib/types';
import { api, ApiError } from '@/lib/api';
import { useToast } from '@/components/Toast';
import { useAuth } from '@/lib/authContext';
import { X, Save, Loader2, AlertCircle, AlertTriangle, Layers, Compass, Sliders } from 'lucide-react';

interface FeatureEditModalProps {
  mode: 'create' | 'edit';
  feature?: GeoJsonFeature | null; // provided in edit mode
  initialGeometry?: {
    kind: 'POINT' | 'LINE' | 'POLYGON';
    coordinates: any;
  } | null; // provided in create mode from Geoman draw
  networks: NetworkDto[];
  featureTypes: FeatureTypeDto[];
  onClose: () => void;
  onSaved: (savedFeature: any) => void;
}

export const FeatureEditModal: React.FC<FeatureEditModalProps> = ({
  mode,
  feature,
  initialGeometry,
  networks,
  featureTypes,
  onClose,
  onSaved,
}) => {
  const { lang } = useAuth();
  const { showSuccess, showError, showConflict } = useToast();
  const isAr = lang === 'ar';

  const isCreate = mode === 'create';

  // Determine geometry kind
  const geometryKind: 'POINT' | 'LINE' | 'POLYGON' = isCreate
    ? initialGeometry?.kind || 'POINT'
    : (feature?.properties.geometryKind as any) ||
      (feature?.geometry.type === 'Point' ? 'POINT' : feature?.geometry.type === 'LineString' ? 'LINE' : 'POLYGON');

  // Filter available feature types matching geometry kind
  const compatibleTypes = featureTypes.filter((ft) => ft.geometryKind === geometryKind);

  // Initial State Setup
  const [selectedTypeCode, setSelectedTypeCode] = useState<string>(
    feature?.properties.featureTypeCode || compatibleTypes[0]?.code || ''
  );
  const [selectedNetworkId, setSelectedNetworkId] = useState<string>(
    feature?.properties.networkId || networks[0]?.id || ''
  );
  const [name, setName] = useState<string>(feature?.properties.name || '');
  const [status, setStatus] = useState<string>(feature?.properties.status || 'ACTIVE');

  // Point specific
  const [isOpen, setIsOpen] = useState<boolean>(feature?.properties.isOpen ?? true);

  // Line specific
  const [flowDirection, setFlowDirection] = useState<string>(
    feature?.properties.flowDirection || 'WITH_DIGITIZED'
  );
  const [diameterMm, setDiameterMm] = useState<number | string>(
    feature?.properties.diameterMm ?? ''
  );
  const [material, setMaterial] = useState<string>(
    feature?.properties.material || ''
  );

  // Dynamic attributes state
  const [attributes, setAttributes] = useState<Record<string, any>>(
    feature?.properties.attributes || {}
  );

  // Errors state: field -> errorMessage
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [generalError, setGeneralError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Selected feature type object & its attribute schema
  const currentFeatureType = featureTypes.find((ft) => ft.code === selectedTypeCode);
  const schemaProps = (currentFeatureType?.attributeSchema?.properties as Record<string, any>) || {};

  // Build Zod validation schema mirroring server rules
  const buildValidationSchema = () => {
    let schemaObj: Record<string, z.ZodTypeAny> = {
      name: z
        .string()
        .min(1, isAr ? 'اسم المعلم مطلوب' : 'Name is required')
        .max(120, isAr ? 'الاسم لا يتجاوز 120 حرفاً' : 'Name max 120 chars')
        .regex(
          new RegExp('^[\\p{L}\\p{N} _\\-./()]{1,120}$', 'u'),
          isAr ? 'الاسم يحتوي على رموز غير مسموح بها' : 'Name contains invalid characters'
        ),
      status: z.enum(['ACTIVE', 'INACTIVE', 'ABANDONED', 'PLANNED', 'DAMAGED']),
    };

    if (isCreate) {
      schemaObj.networkId = z.string().min(1, isAr ? 'الشبكة مطلوبة' : 'Network is required');
      schemaObj.featureTypeCode = z.string().min(1, isAr ? 'نوع المعلم مطلوب' : 'Feature type is required');
    }

    if (geometryKind === 'LINE') {
      schemaObj.flowDirection = z.enum([
        'WITH_DIGITIZED',
        'AGAINST_DIGITIZED',
        'BIDIRECTIONAL',
        'UNKNOWN',
      ]);

      if (selectedTypeCode === 'PIPE') {
        schemaObj.diameterMm = z.preprocess(
          (val) => (val === '' || val === null || val === undefined ? undefined : Number(val)),
          z.number({ message: isAr ? 'القطر مطلوب لخطوط الأنابيب (PIPE)' : 'Diameter is required for PIPE' })
            .positive(isAr ? 'يجب أن يكون القطر قيمة موجبة' : 'Diameter must be positive')
        );
      }
    }

    // Dynamic schema validation for attributes
    const attrShape: Record<string, z.ZodTypeAny> = {};
    for (const [key, prop] of Object.entries(schemaProps)) {
      if (prop.type === 'integer' || prop.type === 'number') {
        let numRule = z.number();
        if (typeof prop.minimum === 'number') {
          numRule = numRule.min(
            prop.minimum,
            isAr ? `الحد الأدنى هو ${prop.minimum}` : `Min is ${prop.minimum}`
          );
        }
        if (typeof prop.maximum === 'number') {
          numRule = numRule.max(
            prop.maximum,
            isAr ? `الحد الأقصى هو ${prop.maximum}` : `Max is ${prop.maximum}`
          );
        }
        attrShape[key] = z.preprocess(
          (val) => (val === '' || val === null || val === undefined ? undefined : Number(val)),
          numRule.optional()
        );
      } else if (prop.type === 'string') {
        if (Array.isArray(prop.enum) && prop.enum.length > 0) {
          attrShape[key] = z.enum(prop.enum as [string, ...string[]]).optional();
        } else {
          attrShape[key] = z.string().optional();
        }
      } else if (prop.type === 'boolean') {
        attrShape[key] = z.boolean().optional();
      }
    }
    schemaObj.attributes = z.object(attrShape).optional();

    return z.object(schemaObj);
  };

  const handleAttributeChange = (propKey: string, val: any) => {
    setAttributes((prev) => ({
      ...prev,
      [propKey]: val,
    }));
    // Clear error on change
    if (fieldErrors[propKey]) {
      setFieldErrors((prev) => {
        const next = { ...prev };
        delete next[propKey];
        return next;
      });
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setFieldErrors({});
    setGeneralError(null);

    // 1. Client-side Zod validation
    const validationSchema = buildValidationSchema();
    const dataToValidate: Record<string, any> = {
      name: name.trim(),
      status,
      attributes,
    };

    if (isCreate) {
      dataToValidate.networkId = selectedNetworkId;
      dataToValidate.featureTypeCode = selectedTypeCode;
    }

    if (geometryKind === 'LINE') {
      dataToValidate.flowDirection = flowDirection;
      dataToValidate.diameterMm = diameterMm === '' ? undefined : diameterMm;
      dataToValidate.material = material.trim() || undefined;
    }

    const parseResult = validationSchema.safeParse(dataToValidate);
    if (!parseResult.success) {
      const errMap: Record<string, string> = {};
      parseResult.error.issues.forEach((err) => {
        const path = err.path.join('.');
        errMap[path] = err.message;
      });
      setFieldErrors(errMap);
      return;
    }

    setIsSubmitting(true);

    try {
      if (isCreate) {
        // CREATE FLOW
        if (geometryKind === 'POINT') {
          const coords = initialGeometry?.coordinates; // [lng, lat]
          const res = await api.createPoint({
            networkId: selectedNetworkId,
            featureTypeCode: selectedTypeCode,
            name: name.trim(),
            status,
            attributes,
            x: coords[0],
            y: coords[1],
            isOpen: isOpen,
          });
          showSuccess(
            isAr ? 'تم إنشاء النقطة بنجاح' : 'Point feature created',
            isAr ? `تمت إضافة ${name} إلى قاعدة البيانات.` : `Added ${name} to database.`
          );
          onSaved(res);
        } else if (geometryKind === 'LINE') {
          const coords = initialGeometry?.coordinates; // [[lng, lat], ...]
          const res = await api.createLine({
            networkId: selectedNetworkId,
            featureTypeCode: selectedTypeCode,
            name: name.trim(),
            status,
            attributes,
            coordinates: coords,
            flowDirection,
            diameterMm: diameterMm === '' ? undefined : Number(diameterMm),
            material: material.trim() || undefined,
          });
          showSuccess(
            isAr ? 'تم إنشاء الخط بنجاح' : 'Line feature created',
            isAr ? `تمت إضافة ${name} إلى قاعدة البيانات.` : `Added ${name} to database.`
          );
          onSaved(res);
        } else {
          // POLYGON
          const ringCoords = initialGeometry?.coordinates; // [[[lng, lat], ...]]
          const res = await api.createPolygon({
            networkId: selectedNetworkId,
            featureTypeCode: selectedTypeCode,
            name: name.trim(),
            status,
            attributes,
            ringCoordinates: ringCoords,
          });
          showSuccess(
            isAr ? 'تم إنشاء المضلع بنجاح' : 'Polygon feature created',
            isAr ? `تمت إضافة ${name} إلى قاعدة البيانات.` : `Added ${name} to database.`
          );
          onSaved(res);
        }
      } else {
        // UPDATE FLOW
        if (!feature) return;
        const currentVersion = feature.properties.version ?? 1;

        if (geometryKind === 'POINT') {
          const res = await api.updatePoint(feature.id, {
            name: name.trim(),
            status,
            attributes,
            isOpen,
            version: currentVersion,
          });
          showSuccess(
            isAr ? 'تم تحديث النقطة بنجاح' : 'Point feature updated',
            isAr ? `تم حفظ التعديلات على "${name}".` : `Updated ${name}.`
          );
          onSaved(res);
        } else if (geometryKind === 'LINE') {
          const res = await api.updateLine(feature.id, {
            name: name.trim(),
            status,
            attributes,
            flowDirection,
            diameterMm: diameterMm === '' ? undefined : Number(diameterMm),
            material: material.trim() || undefined,
            version: currentVersion,
          });
          showSuccess(
            isAr ? 'تم تحديث الخط بنجاح' : 'Line feature updated',
            isAr ? `تم حفظ التعديلات على "${name}".` : `Updated ${name}.`
          );
          onSaved(res);
        } else {
          // POLYGON
          const res = await api.updatePolygon(feature.id, {
            name: name.trim(),
            status,
            attributes,
            version: currentVersion,
          });
          showSuccess(
            isAr ? 'تم تحديث المضلع بنجاح' : 'Polygon feature updated',
            isAr ? `تم حفظ التعديلات على "${name}".` : `Updated ${name}.`
          );
          onSaved(res);
        }
      }

      onClose();
    } catch (err: any) {
      console.error('Save feature failed:', err);

      if (err instanceof ApiError) {
        // Concurrency Conflict (409)
        if (err.status === 409) {
          showConflict(
            isAr
              ? 'تعارض في الإصدار (409): قام مستخدم آخر بتعديل هذا المعلم. يرجى إغلاق النافذة وتحديث الخريطة لرؤية أحدث إصدار.'
              : 'Concurrency Conflict (409): This feature was modified by another user. Please refresh to load latest version.'
          );
          setGeneralError(
            isAr
              ? 'تعارض في التعديل: المعلم لم يعد بالإصدار نفسه في قاعدة البيانات. تم إلغاء الحفظ.'
              : 'Conflict detected: The feature version has changed on the server.'
          );
          return;
        }

        // ProblemDetails mapping
        if (err.problem?.errors) {
          const errMap: Record<string, string> = {};
          for (const [k, msgArr] of Object.entries(err.problem.errors)) {
            const lowKey = k.toLowerCase();
            const message = Array.isArray(msgArr) ? msgArr.join(', ') : String(msgArr);
            errMap[k] = message;
            errMap[lowKey] = message;
          }
          setFieldErrors(errMap);
        }

        const msg = err.problem?.detail || err.message;
        setGeneralError(msg);
        showError(isAr ? 'خطأ في التحقق من البيانات' : 'Validation Error', msg);
      } else {
        const msg = err?.message || (isAr ? 'حدث خطأ أثناء حفظ المعلم' : 'Unexpected error saving feature');
        setGeneralError(msg);
        showError(isAr ? 'فشل الحفظ' : 'Failed to save', msg);
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-sm animate-in fade-in duration-200">
      <div className="bg-slate-900 border border-slate-700/80 rounded-2xl max-w-xl w-full shadow-2xl flex flex-col max-h-[90vh] overflow-hidden">
        {/* Modal Header */}
        <div className="p-4 border-b border-slate-800 flex items-center justify-between bg-slate-950/80">
          <div className="flex items-center gap-2.5">
            <div className="p-2 rounded-lg bg-sky-500/10 border border-sky-500/30 text-sky-400">
              <Sliders className="w-5 h-5" />
            </div>
            <div>
              <h3 className="text-sm font-bold text-white flex items-center gap-2">
                <span>
                  {isCreate
                    ? isAr
                      ? `إضافة معلم جديد (${geometryKind})`
                      : `Add New Feature (${geometryKind})`
                    : isAr
                    ? `تعديل المعلم (${geometryKind})`
                    : `Edit Feature (${geometryKind})`}
                </span>
                {!isCreate && feature && (
                  <span className="text-xs text-amber-400 font-mono font-normal">
                    v{feature.properties.version ?? 1}
                  </span>
                )}
              </h3>
              <p className="text-[11px] text-slate-400">
                {isAr
                  ? 'نموذج إدخال ديناميكي مشتق من مخطط الخصائص وقواعد التحقق'
                  : 'Dynamic form driven by attribute schema and server rules'}
              </p>
            </div>
          </div>

          <button
            onClick={onClose}
            disabled={isSubmitting}
            className="p-1.5 text-slate-400 hover:text-white rounded-lg hover:bg-slate-800 transition"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Modal Body */}
        <form onSubmit={handleSubmit} className="flex-1 overflow-y-auto p-5 space-y-4">
          {/* General Error Banner */}
          {generalError && (
            <div className="p-3.5 rounded-xl bg-rose-950/40 border border-rose-800/60 text-rose-200 text-xs flex items-start gap-2.5">
              <AlertCircle className="w-4 h-4 text-rose-400 flex-shrink-0 mt-0.5" />
              <div className="space-y-1">
                <span className="font-bold">{isAr ? 'فشل التحقق:' : 'Validation error:'}</span>
                <p className="leading-relaxed">{generalError}</p>
              </div>
            </div>
          )}

          {/* Section 1: Type & Network (Enabled in Create Mode) */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
            {/* Feature Type */}
            <div className="space-y-1.5">
              <label className="text-xs font-semibold text-slate-300 flex items-center gap-1.5">
                <Layers className="w-3.5 h-3.5 text-sky-400" />
                <span>{isAr ? 'نوع المعلم / الطبقة' : 'Feature Type'}</span>
              </label>
              {isCreate ? (
                <select
                  value={selectedTypeCode}
                  onChange={(e) => setSelectedTypeCode(e.target.value)}
                  className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white focus:outline-none focus:border-sky-500"
                >
                  {compatibleTypes.map((ft) => (
                    <option key={ft.code} value={ft.code}>
                      {ft.displayName} ({ft.code})
                    </option>
                  ))}
                </select>
              ) : (
                <div className="w-full bg-slate-950/60 border border-slate-800 rounded-xl px-3 py-2 text-xs text-slate-400 font-mono">
                  {currentFeatureType?.displayName || selectedTypeCode} ({selectedTypeCode})
                </div>
              )}
            </div>

            {/* Network */}
            <div className="space-y-1.5">
              <label className="text-xs font-semibold text-slate-300 flex items-center gap-1.5">
                <Compass className="w-3.5 h-3.5 text-emerald-400" />
                <span>{isAr ? 'شبكة المرافق' : 'Utility Network'}</span>
              </label>
              {isCreate ? (
                <select
                  value={selectedNetworkId}
                  onChange={(e) => setSelectedNetworkId(e.target.value)}
                  className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white focus:outline-none focus:border-sky-500"
                >
                  {networks.map((net) => (
                    <option key={net.id} value={net.id}>
                      {net.name} ({net.domain})
                    </option>
                  ))}
                </select>
              ) : (
                <div className="w-full bg-slate-950/60 border border-slate-800 rounded-xl px-3 py-2 text-xs text-slate-400">
                  {networks.find((n) => n.id === selectedNetworkId)?.name || 'Utility Network'}
                </div>
              )}
            </div>
          </div>

          {/* Section 2: Core Fields (Name & Status) */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
            {/* Feature Name */}
            <div className="space-y-1.5">
              <label className="text-xs font-semibold text-slate-300">
                {isAr ? 'اسم المعلم / المعرّف' : 'Feature Name'}{' '}
                <span className="text-rose-400">*</span>
              </label>
              <input
                type="text"
                value={name}
                onChange={(e) => {
                  setName(e.target.value);
                  if (fieldErrors['name'] || fieldErrors['Name']) {
                    setFieldErrors((prev) => {
                      const next = { ...prev };
                      delete next['name'];
                      delete next['Name'];
                      return next;
                    });
                  }
                }}
                placeholder={isAr ? 'مثال: محبس عزل R3-V-12' : 'e.g. Valve R3-V-12'}
                className={`w-full bg-slate-950 border ${
                  fieldErrors['name'] || fieldErrors['Name']
                    ? 'border-rose-500'
                    : 'border-slate-700'
                } rounded-xl px-3 py-2 text-xs text-white focus:outline-none focus:border-sky-500`}
              />
              {(fieldErrors['name'] || fieldErrors['Name']) && (
                <span className="text-[11px] text-rose-400 block">
                  {fieldErrors['name'] || fieldErrors['Name']}
                </span>
              )}
            </div>

            {/* Operating Status */}
            <div className="space-y-1.5">
              <label className="text-xs font-semibold text-slate-300">
                {isAr ? 'الحالة التشغيلية' : 'Operating Status'}
              </label>
              <select
                value={status}
                onChange={(e) => setStatus(e.target.value)}
                className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white focus:outline-none focus:border-sky-500"
              >
                <option value="ACTIVE">{isAr ? 'نشط / ACTIVE' : 'ACTIVE'}</option>
                <option value="PLANNED">{isAr ? 'مخطط / PLANNED' : 'PLANNED'}</option>
                <option value="DAMAGED">{isAr ? 'تالف / DAMAGED' : 'DAMAGED'}</option>
                <option value="ABANDONED">{isAr ? 'مهجور / ABANDONED' : 'ABANDONED'}</option>
                <option value="INACTIVE">{isAr ? 'معطل / INACTIVE' : 'INACTIVE'}</option>
              </select>
            </div>
          </div>

          {/* Section 3: Geometry Kind Specifics */}
          {geometryKind === 'POINT' && (
            <div className="p-3 bg-slate-950/60 rounded-xl border border-slate-800 flex items-center justify-between">
              <div>
                <span className="text-xs font-semibold text-slate-200">
                  {isAr ? 'مفتوح للتدفق (Is Open)' : 'Flow Open State'}
                </span>
                <p className="text-[11px] text-slate-400">
                  {isAr ? 'يحدد ما إذا كان المحبس يسمح بمرور المياه' : 'Controls valve flow status'}
                </p>
              </div>
              <input
                type="checkbox"
                checked={isOpen}
                onChange={(e) => setIsOpen(e.target.checked)}
                className="w-4 h-4 rounded text-sky-600 bg-slate-900 border-slate-700 focus:ring-0 focus:ring-offset-0 cursor-pointer"
              />
            </div>
          )}

          {geometryKind === 'LINE' && (
            <div className="grid grid-cols-1 md:grid-cols-3 gap-3 p-3 bg-slate-950/60 rounded-xl border border-slate-800">
              {/* Flow Direction */}
              <div className="space-y-1">
                <label className="text-[11px] font-semibold text-slate-300">
                  {isAr ? 'اتجاه السريان' : 'Flow Direction'}
                </label>
                <select
                  value={flowDirection}
                  onChange={(e) => setFlowDirection(e.target.value)}
                  className="w-full bg-slate-900 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-white focus:outline-none focus:border-sky-500"
                >
                  <option value="WITH_DIGITIZED">مع اتجاه الرسم (WITH_DIGITIZED)</option>
                  <option value="AGAINST_DIGITIZED">عكس الرسم (AGAINST_DIGITIZED)</option>
                  <option value="BIDIRECTIONAL">ثنائي الاتجاه (BIDIRECTIONAL)</option>
                  <option value="UNKNOWN">غير محدد (UNKNOWN)</option>
                </select>
              </div>

              {/* Diameter mm */}
              <div className="space-y-1">
                <label className="text-[11px] font-semibold text-slate-300">
                  {isAr ? 'القطر (مم)' : 'Diameter (mm)'}{' '}
                  {selectedTypeCode === 'PIPE' && <span className="text-rose-400">*</span>}
                </label>
                <input
                  type="number"
                  min="1"
                  value={diameterMm}
                  onChange={(e) => setDiameterMm(e.target.value)}
                  placeholder="250"
                  className={`w-full bg-slate-900 border ${
                    fieldErrors['diameterMm'] || fieldErrors['DiameterMm']
                      ? 'border-rose-500'
                      : 'border-slate-700'
                  } rounded-lg px-2.5 py-1.5 text-xs text-white focus:outline-none focus:border-sky-500`}
                />
                {(fieldErrors['diameterMm'] || fieldErrors['DiameterMm']) && (
                  <span className="text-[10px] text-rose-400 block">
                    {fieldErrors['diameterMm'] || fieldErrors['DiameterMm']}
                  </span>
                )}
              </div>

              {/* Material */}
              <div className="space-y-1">
                <label className="text-[11px] font-semibold text-slate-300">
                  {isAr ? 'مادة الصنع' : 'Material'}
                </label>
                <input
                  type="text"
                  value={material}
                  onChange={(e) => setMaterial(e.target.value)}
                  placeholder="HDPE / DI / PVC"
                  className="w-full bg-slate-900 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-white focus:outline-none focus:border-sky-500"
                />
              </div>
            </div>
          )}

          {/* Section 4: Dynamic Attributes Schema Form */}
          {Object.keys(schemaProps).length > 0 && (
            <div className="space-y-3 pt-2 border-t border-slate-800">
              <h4 className="text-xs font-semibold text-slate-400 uppercase tracking-wider">
                {isAr ? 'خصائص النوع التفصيلية (Attribute Schema)' : 'Specific Attributes'}
              </h4>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-3 bg-slate-950/40 p-3.5 rounded-xl border border-slate-800">
                {Object.entries(schemaProps).map(([propKey, propDef]) => {
                  const val = attributes[propKey] ?? '';
                  const propError =
                    fieldErrors[propKey] ||
                    fieldErrors[`attributes.${propKey}`] ||
                    fieldErrors[`Attributes.${propKey}`];

                  // Enum Dropdown
                  if (Array.isArray(propDef.enum)) {
                    return (
                      <div key={propKey} className="space-y-1">
                        <label className="text-[11px] font-medium text-slate-300 font-mono">
                          {propKey}
                        </label>
                        <select
                          value={val}
                          onChange={(e) => handleAttributeChange(propKey, e.target.value)}
                          className={`w-full bg-slate-900 border ${
                            propError ? 'border-rose-500' : 'border-slate-700'
                          } rounded-lg px-2.5 py-1.5 text-xs text-white focus:outline-none focus:border-sky-500`}
                        >
                          <option value="">-- {isAr ? 'اختر' : 'Select'} --</option>
                          {propDef.enum.map((opt: string) => (
                            <option key={opt} value={opt}>
                              {opt}
                            </option>
                          ))}
                        </select>
                        {propError && (
                          <span className="text-[10px] text-rose-400 block">{propError}</span>
                        )}
                      </div>
                    );
                  }

                  // Number / Integer
                  if (propDef.type === 'number' || propDef.type === 'integer') {
                    return (
                      <div key={propKey} className="space-y-1">
                        <label className="text-[11px] font-medium text-slate-300 font-mono">
                          {propKey}{' '}
                          {propDef.minimum !== undefined && (
                            <span className="text-slate-500 text-[10px]">(min: {propDef.minimum})</span>
                          )}
                        </label>
                        <input
                          type="number"
                          step={propDef.type === 'integer' ? '1' : 'any'}
                          min={propDef.minimum}
                          max={propDef.maximum}
                          value={val}
                          onChange={(e) =>
                            handleAttributeChange(
                              propKey,
                              e.target.value === '' ? undefined : Number(e.target.value)
                            )
                          }
                          className={`w-full bg-slate-900 border ${
                            propError ? 'border-rose-500' : 'border-slate-700'
                          } rounded-lg px-2.5 py-1.5 text-xs text-white focus:outline-none focus:border-sky-500`}
                        />
                        {propError && (
                          <span className="text-[10px] text-rose-400 block">{propError}</span>
                        )}
                      </div>
                    );
                  }

                  // Boolean
                  if (propDef.type === 'boolean') {
                    return (
                      <div
                        key={propKey}
                        className="flex items-center justify-between p-2 rounded-lg bg-slate-900 border border-slate-800"
                      >
                        <label className="text-[11px] font-medium text-slate-300 font-mono">
                          {propKey}
                        </label>
                        <input
                          type="checkbox"
                          checked={Boolean(val)}
                          onChange={(e) => handleAttributeChange(propKey, e.target.checked)}
                          className="w-4 h-4 rounded text-sky-600 bg-slate-800 border-slate-700 focus:ring-0 focus:ring-offset-0 cursor-pointer"
                        />
                      </div>
                    );
                  }

                  // Standard String
                  return (
                    <div key={propKey} className="space-y-1">
                      <label className="text-[11px] font-medium text-slate-300 font-mono">
                        {propKey}
                      </label>
                      <input
                        type="text"
                        value={val}
                        onChange={(e) => handleAttributeChange(propKey, e.target.value)}
                        className={`w-full bg-slate-900 border ${
                          propError ? 'border-rose-500' : 'border-slate-700'
                        } rounded-lg px-2.5 py-1.5 text-xs text-white focus:outline-none focus:border-sky-500`}
                      />
                      {propError && (
                        <span className="text-[10px] text-rose-400 block">{propError}</span>
                      )}
                    </div>
                  );
                })}
              </div>
            </div>
          )}

          {/* Form Actions */}
          <div className="pt-3 border-t border-slate-800 flex items-center justify-end gap-3">
            <button
              type="button"
              onClick={onClose}
              disabled={isSubmitting}
              className="px-4 py-2 rounded-xl text-xs font-semibold text-slate-300 hover:text-white bg-slate-800 hover:bg-slate-700 transition"
            >
              {isAr ? 'إلغاء' : 'Cancel'}
            </button>

            <button
              type="submit"
              disabled={isSubmitting}
              className="px-5 py-2 rounded-xl text-xs font-semibold text-white bg-sky-600 hover:bg-sky-500 disabled:opacity-50 transition flex items-center gap-2 shadow-lg shadow-sky-950/50"
            >
              {isSubmitting ? (
                <>
                  <Loader2 className="w-4 h-4 animate-spin" />
                  <span>{isAr ? 'جارٍ الحفظ...' : 'Saving...'}</span>
                </>
              ) : (
                <>
                  <Save className="w-4 h-4" />
                  <span>{isCreate ? (isAr ? 'إنشاء المعلم' : 'Create Feature') : (isAr ? 'حفظ التعديلات' : 'Save Changes')}</span>
                </>
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
