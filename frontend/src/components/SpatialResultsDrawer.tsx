'use client';

import React, { useState, useMemo } from 'react';
import { FeatureTypeDto, GeoJsonFeature } from '@/lib/types';
import { useAuth } from '@/lib/authContext';
import {
  Compass,
  Download,
  X,
  MapPin,
  Eye,
  Filter,
  Search,
  ChevronDown,
  ChevronUp,
  Trash2,
  Layers,
} from 'lucide-react';

interface SpatialResultsDrawerProps {
  isOpen: boolean;
  onClose: () => void;
  title: string;
  features: GeoJsonFeature[];
  featureTypes: FeatureTypeDto[];
  selectedFeatureId: string | null;
  onSelectFeature: (feature: GeoJsonFeature) => void;
  onZoomToFeature: (feature: GeoJsonFeature) => void;
  onClear: () => void;
}

export const SpatialResultsDrawer: React.FC<SpatialResultsDrawerProps> = ({
  isOpen,
  onClose,
  title,
  features,
  featureTypes,
  selectedFeatureId,
  onSelectFeature,
  onZoomToFeature,
  onClear,
}) => {
  const { lang } = useAuth();
  const isAr = lang === 'ar';

  const [isMinimized, setIsMinimized] = useState(false);
  const [filterText, setFilterText] = useState('');
  const [typeFilter, setTypeFilter] = useState<string>('ALL');

  const typeMap = useMemo(() => {
    const map = new Map<string, FeatureTypeDto>();
    featureTypes.forEach((ft) => map.set(ft.code, ft));
    return map;
  }, [featureTypes]);

  const hasDistance = useMemo(() => {
    return features.some(
      (f) =>
        f.properties.distanceMeters !== undefined ||
        (f.properties as any).distance_meters !== undefined
    );
  }, [features]);

  const filteredFeatures = useMemo(() => {
    return features.filter((f) => {
      if (typeFilter !== 'ALL' && f.properties.featureTypeCode !== typeFilter) {
        return false;
      }
      if (filterText.trim()) {
        const text = filterText.toLowerCase();
        const name = (f.properties.name || '').toLowerCase();
        const code = (f.properties.featureTypeCode || '').toLowerCase();
        const status = (f.properties.status || '').toLowerCase();
        return name.includes(text) || code.includes(text) || status.includes(text);
      }
      return true;
    });
  }, [features, typeFilter, filterText]);

  if (!isOpen || features.length === 0) return null;

  // Export to CSV with UTF-8 BOM
  const handleExportCsv = () => {
    const headers = [
      'ID',
      'Name',
      'FeatureType',
      'GeometryKind',
      'Status',
      hasDistance ? 'Distance_Meters' : '',
      'DiameterMm',
      'FlowDirection',
      'Material',
      'CreatedAt',
    ].filter(Boolean);

    const rows = filteredFeatures.map((f) => {
      const p = f.properties;
      const dist = p.distanceMeters ?? (p as any).distance_meters ?? '';
      return [
        `"${f.id}"`,
        `"${(p.name || '').replace(/"/g, '""')}"`,
        `"${p.featureTypeCode || ''}"`,
        `"${f.geometry?.type || ''}"`,
        `"${p.status || ''}"`,
        hasDistance ? `"${dist}"` : '',
        `"${p.diameterMm ?? ''}"`,
        `"${p.flowDirection ?? ''}"`,
        `"${p.material ?? ''}"`,
        `"${p.createdAt || ''}"`,
      ]
        .filter((val) => val !== '')
        .join(',');
    });

    const csvContent = '\uFEFF' + [headers.join(','), ...rows].join('\r\n');
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.setAttribute('download', `acud_spatial_results_${Date.now()}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  };

  return (
    <aside aria-label="Spatial Results Drawer" className="absolute bottom-0 left-0 right-0 z-30 bg-slate-900/95 backdrop-blur-md border-t border-slate-700/80 shadow-2xl transition-all duration-300 flex flex-col max-h-[48vh]">
      {/* Header Bar */}
      <div className="h-11 px-4 border-b border-slate-800 flex items-center justify-between bg-slate-950/80">
        <div className="flex items-center gap-2.5 overflow-hidden">
          <div className="p-1.5 rounded-lg bg-sky-500/10 border border-sky-500/30 text-sky-400">
            <Compass className="w-4 h-4" />
          </div>
          <div className="flex items-center gap-2 truncate">
            <span className="text-xs font-bold text-white truncate">
              {title || (isAr ? 'نتائج الاستعلام المكاني' : 'Spatial Query Results')}
            </span>
            <span className="px-2 py-0.5 rounded-full text-[10px] font-mono font-bold bg-sky-500/20 text-sky-300 border border-sky-500/40">
              {filteredFeatures.length} / {features.length}
            </span>
          </div>
        </div>

        {/* Header Controls */}
        <div className="flex items-center gap-1.5">
          {/* Export CSV */}
          <button
            onClick={handleExportCsv}
            className="flex items-center gap-1.5 px-2.5 py-1 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 hover:text-white text-xs font-medium border border-slate-700 transition"
            title={isAr ? 'تصدير النتائج إلى CSV' : 'Export to CSV'}
          >
            <Download className="w-3.5 h-3.5 text-emerald-400" />
            <span className="hidden sm:inline">{isAr ? 'تصدير CSV' : 'Export'}</span>
          </button>

          {/* Clear results */}
          <button
            onClick={onClear}
            className="flex items-center gap-1.5 px-2.5 py-1 rounded-lg bg-slate-800 hover:bg-rose-950/40 text-slate-400 hover:text-rose-400 text-xs font-medium border border-slate-700 hover:border-rose-800 transition"
            title={isAr ? 'مسح التحديد' : 'Clear Selection'}
          >
            <Trash2 className="w-3.5 h-3.5" />
            <span className="hidden sm:inline">{isAr ? 'مسح' : 'Clear'}</span>
          </button>

          <div className="h-4 w-px bg-slate-800 mx-1" />

          {/* Minimize / Expand */}
          <button
            onClick={() => setIsMinimized(!isMinimized)}
            className="p-1.5 text-slate-400 hover:text-white rounded-lg hover:bg-slate-800 transition"
            title={isMinimized ? (isAr ? 'توسيع' : 'Expand') : (isAr ? 'تصغير' : 'Minimize')}
          >
            {isMinimized ? <ChevronUp className="w-4 h-4" /> : <ChevronDown className="w-4 h-4" />}
          </button>

          {/* Close */}
          <button
            onClick={onClose}
            className="p-1.5 text-slate-400 hover:text-white rounded-lg hover:bg-slate-800 transition"
            title={isAr ? 'إغلاق' : 'Close'}
          >
            <X className="w-4 h-4" />
          </button>
        </div>
      </div>

      {/* Body Content */}
      {!isMinimized && (
        <div className="flex flex-col flex-1 overflow-hidden">
          {/* Subheader: Filter row */}
          <div className="px-4 py-2 border-b border-slate-800 flex items-center gap-3 bg-slate-900/60 text-xs">
            <div className="relative flex-1 max-w-xs">
              <Search className="w-3.5 h-3.5 text-slate-400 absolute left-2.5 rtl:left-auto rtl:right-2.5 top-2.5 pointer-events-none" />
              <input
                type="text"
                value={filterText}
                onChange={(e) => setFilterText(e.target.value)}
                placeholder={isAr ? 'تصفية النتائج بالاسم أو الكود...' : 'Filter results...'}
                className="w-full bg-slate-950 border border-slate-700 rounded-lg pl-8 rtl:pl-3 rtl:pr-8 pr-3 py-1.5 text-xs text-white focus:outline-none focus:border-sky-500"
              />
            </div>

            <div className="flex items-center gap-1.5">
              <Layers className="w-3.5 h-3.5 text-slate-400" />
              <select
                value={typeFilter}
                onChange={(e) => setTypeFilter(e.target.value)}
                className="bg-slate-950 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-slate-200 focus:outline-none focus:border-sky-500 cursor-pointer"
              >
                <option value="ALL">{isAr ? 'جميع الطبقات' : 'All Types'}</option>
                {featureTypes.map((ft) => (
                  <option key={ft.code} value={ft.code}>
                    {ft.displayName} ({ft.code})
                  </option>
                ))}
              </select>
            </div>
          </div>

          {/* Table Container */}
          <div className="flex-1 overflow-y-auto">
            <table className="w-full text-xs text-left rtl:text-right border-collapse">
              <thead className="sticky top-0 bg-slate-950 text-slate-400 font-semibold border-b border-slate-800 z-10">
                <tr>
                  <th className="p-2.5">{isAr ? 'النوع / الطبقة' : 'Type'}</th>
                  <th className="p-2.5">{isAr ? 'الاسم' : 'Name'}</th>
                  <th className="p-2.5">{isAr ? 'الحالة' : 'Status'}</th>
                  {hasDistance && (
                    <th className="p-2.5 font-mono text-amber-400">
                      {isAr ? 'المسافة (متر)' : 'Distance (m)'}
                    </th>
                  )}
                  <th className="p-2.5">{isAr ? 'الشكل' : 'Geometry'}</th>
                  <th className="p-2.5 text-center">{isAr ? 'الإجراءات' : 'Actions'}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-800/60 text-slate-300">
                {filteredFeatures.map((f) => {
                  const p = f.properties;
                  const ft = typeMap.get(p.featureTypeCode);
                  const isSelected = f.id === selectedFeatureId;
                  const distVal = p.distanceMeters ?? (p as any).distance_meters;

                  return (
                    <tr
                      key={f.id}
                      onClick={() => onSelectFeature(f)}
                      className={`hover:bg-slate-800/50 transition cursor-pointer ${
                        isSelected ? 'bg-sky-950/40 border-l-2 rtl:border-l-0 rtl:border-r-2 border-sky-500' : ''
                      }`}
                    >
                      {/* Type code */}
                      <td className="p-2.5 font-mono text-xs font-medium">
                        <div className="flex items-center gap-1.5">
                          <span
                            className="w-2 h-2 rounded-full flex-shrink-0"
                            style={{
                              backgroundColor: ft?.style?.fillColor || ft?.style?.color || '#38BDF8',
                            }}
                          />
                          <span className="text-slate-200">{p.featureTypeCode}</span>
                        </div>
                      </td>

                      {/* Name */}
                      <td className="p-2.5 font-medium text-white max-w-[200px] truncate">
                        {p.name || <span className="text-slate-500 italic">No name</span>}
                      </td>

                      {/* Status */}
                      <td className="p-2.5">
                        <span
                          className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                            p.status === 'ACTIVE'
                              ? 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/40'
                              : p.status === 'PLANNED'
                              ? 'bg-sky-500/20 text-sky-300 border border-sky-500/40'
                              : p.status === 'DAMAGED'
                              ? 'bg-rose-500/20 text-rose-300 border border-rose-500/40'
                              : 'bg-amber-500/20 text-amber-300 border border-amber-500/40'
                          }`}
                        >
                          {p.status}
                        </span>
                      </td>

                      {/* Distance (if applicable) */}
                      {hasDistance && (
                        <td className="p-2.5 font-mono font-bold text-amber-400">
                          {distVal !== undefined
                            ? `${Number(distVal).toFixed(2)} m`
                            : '—'}
                        </td>
                      )}

                      {/* Geometry */}
                      <td className="p-2.5 text-slate-400 font-mono text-[11px]">
                        {f.geometry?.type}
                      </td>

                      {/* Actions */}
                      <td className="p-2.5 text-center">
                        <div className="inline-flex items-center gap-1">
                          <button
                            type="button"
                            onClick={(e) => {
                              e.stopPropagation();
                              onZoomToFeature(f);
                            }}
                            className="p-1 rounded hover:bg-slate-700 text-sky-400 hover:text-white transition"
                            title={isAr ? 'تكبير وانتقال للمعلم' : 'Zoom to Feature'}
                          >
                            <MapPin className="w-3.5 h-3.5" />
                          </button>
                          <button
                            type="button"
                            onClick={(e) => {
                              e.stopPropagation();
                              onSelectFeature(f);
                            }}
                            className="p-1 rounded hover:bg-slate-700 text-slate-400 hover:text-white transition"
                            title={isAr ? 'عرض التفاصيل' : 'View Details'}
                          >
                            <Eye className="w-3.5 h-3.5" />
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </aside>
  );
};

