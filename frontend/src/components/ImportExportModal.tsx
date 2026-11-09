'use client';

import React, { useState, useRef } from 'react';
import {
  Download,
  Upload,
  FileSpreadsheet,
  FileCode,
  CheckCircle2,
  AlertTriangle,
  XCircle,
  HelpCircle,
  X,
  Layers,
  Globe2,
  Compass,
  ArrowRight,
  RefreshCw,
} from 'lucide-react';
import { api } from '@/lib/api';
import {
  FeatureTypeDto,
  NetworkDto,
  ImportPreviewResultDto,
} from '@/lib/types';

interface ImportExportModalProps {
  isOpen: boolean;
  onClose: () => void;
  networks: NetworkDto[];
  activeNetworkId: string | null;
  featureTypes: FeatureTypeDto[];
  currentBbox?: string;
  isAr?: boolean;
  onImportSuccess?: () => void;
}

export const ImportExportModal: React.FC<ImportExportModalProps> = ({
  isOpen,
  onClose,
  networks,
  activeNetworkId,
  featureTypes,
  currentBbox,
  isAr = true,
  onImportSuccess,
}) => {
  const [activeTab, setActiveTab] = useState<'export' | 'import'>('export');

  // Export State
  const [exportFormat, setExportFormat] = useState<'geojson' | 'csv'>('geojson');
  const [exportSrid, setExportSrid] = useState<number>(4326);
  const [exportType, setExportType] = useState<string>('');
  const [useBbox, setUseBbox] = useState<boolean>(false);
  const [isExporting, setIsExporting] = useState<boolean>(false);
  const [exportError, setExportError] = useState<string | null>(null);

  // Import State
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [importNetworkId, setImportNetworkId] = useState<string>(activeNetworkId || (networks[0]?.id ?? ''));
  const [importDefaultType, setImportDefaultType] = useState<string>('');
  const [importSrid, setImportSrid] = useState<number>(4326);
  const [isPreviewing, setIsPreviewing] = useState<boolean>(false);
  const [previewResult, setPreviewResult] = useState<ImportPreviewResultDto | null>(null);
  const [skipDuplicates, setSkipDuplicates] = useState<boolean>(true);
  const [isCommitting, setIsCommitting] = useState<boolean>(false);
  const [importError, setImportError] = useState<string | null>(null);
  const [commitSuccess, setCommitSuccess] = useState<string | null>(null);

  const fileInputRef = useRef<HTMLInputElement>(null);

  if (!isOpen) return null;

  const handleExport = async () => {
    setIsExporting(true);
    setExportError(null);
    try {
      await api.downloadExport(exportFormat, {
        networkId: activeNetworkId || undefined,
        featureTypeCode: exportType || undefined,
        bbox: useBbox ? currentBbox : undefined,
        srid: exportSrid,
      });
    } catch (err: any) {
      setExportError(err.message || (isAr ? 'فشل التصدير' : 'Export failed'));
    } finally {
      setIsExporting(false);
    }
  };

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files[0]) {
      setSelectedFile(e.target.files[0]);
      setPreviewResult(null);
      setImportError(null);
      setCommitSuccess(null);
    }
  };

  const handlePreview = async () => {
    if (!selectedFile) {
      setImportError(isAr ? 'يرجى اختيار ملف أولاً' : 'Please select a file first');
      return;
    }

    if (!importNetworkId) {
      setImportError(isAr ? 'يرجى اختيار الشبكة المستهدفة' : 'Please select target network');
      return;
    }

    setIsPreviewing(true);
    setImportError(null);
    setCommitSuccess(null);

    try {
      const formData = new FormData();
      formData.append('file', selectedFile);
      formData.append('formNetworkId', importNetworkId);
      if (importDefaultType) {
        formData.append('formFeatureTypeCode', importDefaultType);
      }
      formData.append('formSrid', importSrid.toString());

      const res = await api.previewImport(formData);
      setPreviewResult(res);
    } catch (err: any) {
      setImportError(err.message || (isAr ? 'فشل فحص ملف الاستيراد' : 'Import preview failed'));
    } finally {
      setIsPreviewing(false);
    }
  };

  const handleCommit = async () => {
    if (!previewResult) return;

    setIsCommitting(true);
    setImportError(null);
    try {
      const res = await api.commitImport({
        previewToken: previewResult.previewToken,
        networkId: importNetworkId,
        skipDuplicates,
      });

      setCommitSuccess(
        isAr
          ? `تم استيراد ${res.importedCount} معلم بنجاح في عملية واحدة! (تم تخطي ${res.skippedCount})`
          : `Successfully imported ${res.importedCount} features in a single change set! (${res.skippedCount} skipped)`
      );

      setPreviewResult(null);
      setSelectedFile(null);
      if (fileInputRef.current) fileInputRef.current.value = '';

      if (onImportSuccess) {
        onImportSuccess();
      }
    } catch (err: any) {
      setImportError(err.message || (isAr ? 'فشل إتمام الاستيراد' : 'Import commit failed'));
    } finally {
      setIsCommitting(false);
    }
  };

  const currentNetworkName = networks.find((n) => n.id === activeNetworkId)?.name || (isAr ? 'كل الشبكات' : 'All Networks');

  return (
    <div className="fixed inset-0 z-50 bg-black/70 backdrop-blur-sm flex items-center justify-center p-4">
      <div
        className="bg-slate-900 border border-slate-700/80 rounded-2xl w-full max-w-3xl shadow-2xl overflow-hidden flex flex-col max-h-[90vh] text-slate-200"
        dir={isAr ? 'rtl' : 'ltr'}
      >
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-slate-800 bg-slate-950/40">
          <div className="flex items-center gap-3">
            <div className="p-2 rounded-xl bg-sky-500/10 text-sky-400 border border-sky-500/20">
              <Compass className="w-5 h-5" />
            </div>
            <div>
              <h2 className="text-base font-bold text-white flex items-center gap-2">
                {isAr ? 'استيراد وتصدير البيانات المكانية' : 'Spatial Data Import & Export'}
                <span className="text-xs font-normal text-slate-400 border border-slate-700 rounded px-2 py-0.5">
                  EPSG: 4326 / 22992 / 32636
                </span>
              </h2>
              <p className="text-xs text-slate-400">
                {isAr
                  ? `الشبكة الحالية: ${currentNetworkName} - دعم أنظمة إحداثيات العاصمة الإدارية`
                  : `Active Network: ${currentNetworkName} - ACUD coordinate systems supported`}
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 rounded-lg text-slate-400 hover:text-white hover:bg-slate-800 transition"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Tab Selector */}
        <div className="flex border-b border-slate-800 bg-slate-900/60 px-6 pt-3 gap-2">
          <button
            onClick={() => setActiveTab('export')}
            className={`flex items-center gap-2 px-4 py-2.5 rounded-t-lg font-medium text-xs transition border-b-2 ${
              activeTab === 'export'
                ? 'border-sky-500 text-sky-400 bg-slate-800/60'
                : 'border-transparent text-slate-400 hover:text-slate-200 hover:bg-slate-800/30'
            }`}
          >
            <Download className="w-4 h-4" />
            {isAr ? 'تصدير البيانات (Export)' : 'Export Data'}
          </button>
          <button
            onClick={() => setActiveTab('import')}
            className={`flex items-center gap-2 px-4 py-2.5 rounded-t-lg font-medium text-xs transition border-b-2 ${
              activeTab === 'import'
                ? 'border-emerald-500 text-emerald-400 bg-slate-800/60'
                : 'border-transparent text-slate-400 hover:text-slate-200 hover:bg-slate-800/30'
            }`}
          >
            <Upload className="w-4 h-4" />
            {isAr ? 'استيراد البيانات (Import)' : 'Import Data'}
          </button>
        </div>

        {/* Content Body */}
        <div className="p-6 overflow-y-auto flex-1 space-y-6">
          {activeTab === 'export' ? (
            /* ================= EXPORT TAB ================= */
            <div className="space-y-5">
              {exportError && (
                <div className="p-3 rounded-lg bg-rose-950/50 border border-rose-800 text-rose-300 text-xs flex items-center gap-2">
                  <XCircle className="w-4 h-4 shrink-0 text-rose-400" />
                  <span>{exportError}</span>
                </div>
              )}

              {/* Format Selection */}
              <div>
                <label className="block text-xs font-semibold text-slate-300 mb-2">
                  {isAr ? 'صيغة الملف المستهدف:' : 'Target Export Format:'}
                </label>
                <div className="grid grid-cols-2 gap-3">
                  <button
                    type="button"
                    onClick={() => setExportFormat('geojson')}
                    className={`flex items-center justify-between p-3 rounded-xl border transition ${
                      exportFormat === 'geojson'
                        ? 'border-sky-500 bg-sky-950/30 text-white'
                        : 'border-slate-800 bg-slate-800/40 text-slate-400 hover:border-slate-700'
                    }`}
                  >
                    <div className="flex items-center gap-3">
                      <FileCode className="w-5 h-5 text-sky-400" />
                      <div className="text-right">
                        <div className="text-xs font-bold">GeoJSON</div>
                        <div className="text-[11px] text-slate-400">
                          {isAr ? 'ملف قياسي لنظم المعلومات الجغرافية' : 'Standard OGC GIS Format'}
                        </div>
                      </div>
                    </div>
                    {exportFormat === 'geojson' && <CheckCircle2 className="w-4 h-4 text-sky-400" />}
                  </button>

                  <button
                    type="button"
                    onClick={() => setExportFormat('csv')}
                    className={`flex items-center justify-between p-3 rounded-xl border transition ${
                      exportFormat === 'csv'
                        ? 'border-emerald-500 bg-emerald-950/30 text-white'
                        : 'border-slate-800 bg-slate-800/40 text-slate-400 hover:border-slate-700'
                    }`}
                  >
                    <div className="flex items-center gap-3">
                      <FileSpreadsheet className="w-5 h-5 text-emerald-400" />
                      <div className="text-right">
                        <div className="text-xs font-bold">CSV Spreadsheet</div>
                        <div className="text-[11px] text-slate-400">
                          {isAr ? 'محمي ضد الـ Formula Injection' : 'Formula Injection Protected'}
                        </div>
                      </div>
                    </div>
                    {exportFormat === 'csv' && <CheckCircle2 className="w-4 h-4 text-emerald-400" />}
                  </button>
                </div>
              </div>

              {/* Target SRID */}
              <div>
                <label className="block text-xs font-semibold text-slate-300 mb-2">
                  {isAr ? 'نظام الإحداثيات المستهدف (CRS / SRID):' : 'Target Coordinate Reference System:'}
                </label>
                <div className="space-y-2">
                  {[
                    {
                      srid: 4326,
                      title: 'EPSG:4326 - WGS 84 (GPS Degrees)',
                      desc: isAr ? 'إحداثيات جغرافية قياسية (درجات طول وعرض)' : 'Standard geographic lat/lon degrees',
                    },
                    {
                      srid: 22992,
                      title: 'EPSG:22992 - Egypt 1907 / Red Belt (حزام مصر الأحمر)',
                      desc: isAr ? 'النظام المساحي المتري الرسمي للعاصمة الإدارية (أمتار)' : 'Official ACUD survey metric grid',
                    },
                    {
                      srid: 32636,
                      title: 'EPSG:32636 - WGS 84 / UTM zone 36N',
                      desc: isAr ? 'نظام الإسقاط العالمي ميركاتور الزون 36 شمالاً' : 'Universal Transverse Mercator metric grid',
                    },
                  ].map((s) => (
                    <label
                      key={s.srid}
                      className={`flex items-center gap-3 p-2.5 rounded-lg border cursor-pointer transition ${
                        exportSrid === s.srid
                          ? 'border-sky-500/80 bg-sky-950/20 text-white'
                          : 'border-slate-800 bg-slate-800/30 text-slate-300 hover:border-slate-700'
                      }`}
                    >
                      <input
                        type="radio"
                        name="exportSrid"
                        value={s.srid}
                        checked={exportSrid === s.srid}
                        onChange={() => setExportSrid(s.srid)}
                        className="text-sky-500 focus:ring-sky-500"
                      />
                      <div className="flex-1">
                        <div className="text-xs font-semibold">{s.title}</div>
                        <div className="text-[11px] text-slate-400">{s.desc}</div>
                      </div>
                    </label>
                  ))}
                </div>
              </div>

              {/* Layer Filter & BBox */}
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-semibold text-slate-300 mb-1.5">
                    {isAr ? 'تصفية الطبقة (اختياري):' : 'Filter by Layer (Optional):'}
                  </label>
                  <select
                    value={exportType}
                    onChange={(e) => setExportType(e.target.value)}
                    className="w-full bg-slate-800 border border-slate-700 rounded-lg px-3 py-2 text-xs text-slate-200 focus:outline-none focus:border-sky-500"
                  >
                    <option value="">{isAr ? 'جميع الطبقات' : 'All Layers in Network'}</option>
                    {featureTypes.map((ft) => (
                      <option key={ft.code} value={ft.code}>
                        {ft.displayName} ({ft.code})
                      </option>
                    ))}
                  </select>
                </div>

                <div className="flex items-center pt-6">
                  <label className="flex items-center gap-2 cursor-pointer text-xs text-slate-300">
                    <input
                      type="checkbox"
                      checked={useBbox}
                      onChange={(e) => setUseBbox(e.target.checked)}
                      className="rounded border-slate-700 text-sky-500 focus:ring-sky-500"
                    />
                    <span>{isAr ? 'تصدير نطاق شاشة الخريطة الحالي فقط (BBox)' : 'Export current map viewport only'}</span>
                  </label>
                </div>
              </div>

              {/* Action Button */}
              <div className="pt-3 border-t border-slate-800 flex justify-end">
                <button
                  type="button"
                  onClick={handleExport}
                  disabled={isExporting}
                  className="flex items-center gap-2 px-5 py-2.5 rounded-xl bg-sky-600 hover:bg-sky-500 disabled:opacity-50 text-white text-xs font-bold transition shadow-lg shadow-sky-900/30"
                >
                  {isExporting ? (
                    <>
                      <RefreshCw className="w-4 h-4 animate-spin" />
                      <span>{isAr ? 'جاري تجهيز وتنزيل الملف...' : 'Preparing export...'}</span>
                    </>
                  ) : (
                    <>
                      <Download className="w-4 h-4" />
                      <span>{isAr ? 'تصدير وتحميل الملف' : 'Export & Download'}</span>
                    </>
                  )}
                </button>
              </div>
            </div>
          ) : (
            /* ================= IMPORT TAB ================= */
            <div className="space-y-5">
              {commitSuccess && (
                <div className="p-3 rounded-lg bg-emerald-950/50 border border-emerald-800 text-emerald-300 text-xs flex items-center gap-2">
                  <CheckCircle2 className="w-4 h-4 shrink-0 text-emerald-400" />
                  <span>{commitSuccess}</span>
                </div>
              )}

              {importError && (
                <div className="p-3 rounded-lg bg-rose-950/50 border border-rose-800 text-rose-300 text-xs flex items-center gap-2">
                  <XCircle className="w-4 h-4 shrink-0 text-rose-400" />
                  <span>{importError}</span>
                </div>
              )}

              {/* File Dropzone */}
              <div>
                <label className="block text-xs font-semibold text-slate-300 mb-1.5">
                  {isAr ? 'اختيار ملف الاستيراد (GeoJSON أو CSV):' : 'Select Import File (GeoJSON or CSV):'}
                </label>
                <div
                  onClick={() => fileInputRef.current?.click()}
                  className="border-2 border-dashed border-slate-700 hover:border-emerald-500/70 bg-slate-800/40 hover:bg-slate-800/70 rounded-xl p-5 text-center cursor-pointer transition flex flex-col items-center justify-center gap-2"
                >
                  <Upload className="w-8 h-8 text-emerald-400" />
                  <div className="text-xs font-semibold text-slate-200">
                    {selectedFile
                      ? selectedFile.name
                      : isAr
                      ? 'اضغط هنا أو اسحب الملف لرفعه (حد أقصى 10MB)'
                      : 'Click or drag file here (Max 10MB)'}
                  </div>
                  <div className="text-[11px] text-slate-400">
                    {isAr
                      ? 'يدعم ملفات GeoJSON (.geojson, .json) وملفات الجداول (.csv)'
                      : 'Supports GeoJSON (.geojson, .json) and CSV (.csv)'}
                  </div>
                  <input
                    ref={fileInputRef}
                    type="file"
                    accept=".geojson,.json,.csv"
                    onChange={handleFileChange}
                    className="hidden"
                  />
                </div>
              </div>

              {/* Parameters: Network, Default Type & Source SRID */}
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                <div>
                  <label className="block text-xs font-semibold text-slate-300 mb-1">
                    {isAr ? 'الشبكة المستهدفة:' : 'Target Network:'}
                  </label>
                  <select
                    value={importNetworkId}
                    onChange={(e) => setImportNetworkId(e.target.value)}
                    className="w-full bg-slate-800 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-slate-200 focus:outline-none focus:border-emerald-500"
                  >
                    {networks.map((n) => (
                      <option key={n.id} value={n.id}>
                        {n.name}
                      </option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="block text-xs font-semibold text-slate-300 mb-1">
                    {isAr ? 'نوع المعلم الافتراضي:' : 'Default Layer Type:'}
                  </label>
                  <select
                    value={importDefaultType}
                    onChange={(e) => setImportDefaultType(e.target.value)}
                    className="w-full bg-slate-800 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-slate-200 focus:outline-none focus:border-emerald-500"
                  >
                    <option value="">{isAr ? 'تحديد من الملف تلقائياً' : 'Auto-detect from file'}</option>
                    {featureTypes.map((ft) => (
                      <option key={ft.code} value={ft.code}>
                        {ft.displayName} ({ft.code})
                      </option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="block text-xs font-semibold text-slate-300 mb-1">
                    {isAr ? 'نظام إحداثيات الملف (Source CRS):' : 'Source File CRS (SRID):'}
                  </label>
                  <select
                    value={importSrid}
                    onChange={(e) => setImportSrid(parseInt(e.target.value, 10))}
                    className="w-full bg-slate-800 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-slate-200 focus:outline-none focus:border-emerald-500"
                  >
                    <option value={4326}>EPSG:4326 (WGS 84 Lat/Lon)</option>
                    <option value={22992}>EPSG:22992 (Egypt Red Belt Metres)</option>
                    <option value={32636}>EPSG:32636 (UTM 36N Metres)</option>
                  </select>
                </div>
              </div>

              {/* Preview Button */}
              {!previewResult && (
                <div className="flex justify-end pt-2">
                  <button
                    type="button"
                    onClick={handlePreview}
                    disabled={!selectedFile || isPreviewing}
                    className="flex items-center gap-2 px-5 py-2.5 rounded-xl bg-emerald-600 hover:bg-emerald-500 disabled:opacity-50 text-white text-xs font-bold transition shadow-lg shadow-emerald-900/30"
                  >
                    {isPreviewing ? (
                      <>
                        <RefreshCw className="w-4 h-4 animate-spin" />
                        <span>{isAr ? 'جاري الفحص المسبق والتحقق...' : 'Validating & previewing...'}</span>
                      </>
                    ) : (
                      <>
                        <CheckCircle2 className="w-4 h-4" />
                        <span>{isAr ? 'معاينة وفحص الملف (Step 1)' : 'Preview & Validate (Step 1)'}</span>
                      </>
                    )}
                  </button>
                </div>
              )}

              {/* Preview Results Panel */}
              {previewResult && (
                <div className="space-y-4 pt-3 border-t border-slate-800">
                  {/* Summary Metric Cards */}
                  <div className="grid grid-cols-4 gap-2 text-center">
                    <div className="bg-slate-800/50 border border-slate-700/60 rounded-xl p-2.5">
                      <div className="text-[11px] text-slate-400">{isAr ? 'إجمالي المعالم' : 'Total'}</div>
                      <div className="text-base font-extrabold text-white">{previewResult.totalFeatures}</div>
                    </div>
                    <div className="bg-emerald-950/40 border border-emerald-800/60 rounded-xl p-2.5">
                      <div className="text-[11px] text-emerald-400">{isAr ? 'صالح للاستيراد' : 'Valid'}</div>
                      <div className="text-base font-extrabold text-emerald-300">{previewResult.validCount}</div>
                    </div>
                    <div className="bg-amber-950/40 border border-amber-800/60 rounded-xl p-2.5">
                      <div className="text-[11px] text-amber-400">{isAr ? 'تكرارات' : 'Duplicates'}</div>
                      <div className="text-base font-extrabold text-amber-300">{previewResult.duplicateCount}</div>
                    </div>
                    <div className="bg-rose-950/40 border border-rose-800/60 rounded-xl p-2.5">
                      <div className="text-[11px] text-rose-400">{isAr ? 'أخطاء مرفوضة' : 'Invalid'}</div>
                      <div className="text-base font-extrabold text-rose-300">{previewResult.invalidCount}</div>
                    </div>
                  </div>

                  {/* Duplicate Handling Toggle */}
                  {previewResult.duplicateCount > 0 && (
                    <div className="flex items-center justify-between p-2.5 rounded-lg bg-amber-950/30 border border-amber-800/50 text-xs text-amber-200">
                      <div className="flex items-center gap-2">
                        <AlertTriangle className="w-4 h-4 text-amber-400 shrink-0" />
                        <span>
                          {isAr
                            ? `تم اكتشاف ${previewResult.duplicateCount} معلم مكرر في قاعدة البيانات.`
                            : `Found ${previewResult.duplicateCount} duplicate features in workspace.`}
                        </span>
                      </div>
                      <label className="flex items-center gap-1.5 cursor-pointer text-xs font-semibold text-white">
                        <input
                          type="checkbox"
                          checked={skipDuplicates}
                          onChange={(e) => setSkipDuplicates(e.target.checked)}
                          className="rounded border-slate-700 text-amber-500 focus:ring-amber-500"
                        />
                        <span>{isAr ? 'تخطي التكرارات تلقائياً' : 'Skip duplicates'}</span>
                      </label>
                    </div>
                  )}

                  {/* Errors Table if any */}
                  {previewResult.errors.length > 0 && (
                    <div>
                      <div className="text-xs font-bold text-rose-400 mb-1.5 flex items-center gap-1.5">
                        <XCircle className="w-3.5 h-3.5" />
                        <span>{isAr ? 'قائمة الأخطاء المكتشفة:' : 'Validation Errors:'}</span>
                      </div>
                      <div className="max-h-32 overflow-y-auto border border-rose-900/60 rounded-lg bg-rose-950/20 text-xs">
                        <table className="w-full text-right">
                          <thead className="bg-rose-950/40 text-[11px] text-rose-300 border-b border-rose-900/40">
                            <tr>
                              <th className="p-1.5">{isAr ? 'الصف' : 'Row'}</th>
                              <th className="p-1.5">{isAr ? 'الحقل' : 'Field'}</th>
                              <th className="p-1.5">{isAr ? 'سبب الرفض' : 'Reason'}</th>
                            </tr>
                          </thead>
                          <tbody className="divide-y divide-rose-900/20 text-[11px] text-rose-200">
                            {previewResult.errors.map((err, idx) => (
                              <tr key={idx}>
                                <td className="p-1.5 font-mono">#{err.rowIndex}</td>
                                <td className="p-1.5 font-semibold">{err.field}</td>
                                <td className="p-1.5">{err.error}</td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    </div>
                  )}

                  {/* Sample Preview Rows */}
                  {previewResult.sampleRows.length > 0 && (
                    <div>
                      <div className="text-xs font-bold text-slate-300 mb-1.5">
                        {isAr ? 'عينة من المعالم المفحوصة:' : 'Preview Sample Rows:'}
                      </div>
                      <div className="max-h-40 overflow-y-auto border border-slate-800 rounded-lg bg-slate-950/40 text-[11px]">
                        <table className="w-full text-right">
                          <thead className="bg-slate-800/60 text-slate-400 border-b border-slate-700">
                            <tr>
                              <th className="p-1.5">{isAr ? 'الحالة' : 'Status'}</th>
                              <th className="p-1.5">{isAr ? 'اسم المعلم' : 'Name'}</th>
                              <th className="p-1.5">{isAr ? 'الطبقة' : 'Type'}</th>
                              <th className="p-1.5">{isAr ? 'الهندسة' : 'Geometry'}</th>
                            </tr>
                          </thead>
                          <tbody className="divide-y divide-slate-800/60 text-slate-300">
                            {previewResult.sampleRows.map((row) => (
                              <tr key={row.rowIndex}>
                                <td className="p-1.5">
                                  {!row.isValid ? (
                                    <span className="px-1.5 py-0.5 rounded text-[10px] bg-rose-900/50 text-rose-300 border border-rose-800">
                                      {isAr ? 'خطأ' : 'Error'}
                                    </span>
                                  ) : row.isDuplicate ? (
                                    <span className="px-1.5 py-0.5 rounded text-[10px] bg-amber-900/50 text-amber-300 border border-amber-800">
                                      {isAr ? 'مكرر' : 'Duplicate'}
                                    </span>
                                  ) : (
                                    <span className="px-1.5 py-0.5 rounded text-[10px] bg-emerald-900/50 text-emerald-300 border border-emerald-800">
                                      {isAr ? 'صالح' : 'Valid'}
                                    </span>
                                  )}
                                </td>
                                <td className="p-1.5 font-medium">{row.name}</td>
                                <td className="p-1.5 font-mono text-slate-400">{row.featureTypeCode}</td>
                                <td className="p-1.5 font-mono text-slate-400">{row.geometrySummary}</td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    </div>
                  )}

                  {/* Commit Button */}
                  <div className="flex items-center justify-between pt-3 border-t border-slate-800">
                    <button
                      type="button"
                      onClick={() => setPreviewResult(null)}
                      className="px-3 py-1.5 rounded-lg text-slate-400 hover:text-white hover:bg-slate-800 text-xs transition"
                    >
                      {isAr ? 'إلغاء والمعاينة مجدداً' : 'Re-preview'}
                    </button>

                    <button
                      type="button"
                      onClick={handleCommit}
                      disabled={isCommitting || (skipDuplicates ? previewResult.validCount === 0 : (previewResult.validCount + previewResult.duplicateCount) === 0)}
                      className="flex items-center gap-2 px-5 py-2.5 rounded-xl bg-emerald-600 hover:bg-emerald-500 disabled:opacity-50 text-white text-xs font-bold transition shadow-lg shadow-emerald-900/30"
                    >
                      {isCommitting ? (
                        <>
                          <RefreshCw className="w-4 h-4 animate-spin" />
                          <span>{isAr ? 'جاري تثبيت الاستيراد داخل Change Set...' : 'Committing batch...'}</span>
                        </>
                      ) : (
                        <>
                          <CheckCircle2 className="w-4 h-4" />
                          <span>{isAr ? 'تأكيد واستيراد المعالم (Step 2: Commit)' : 'Confirm & Commit Import (Step 2)'}</span>
                        </>
                      )}
                    </button>
                  </div>
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
