'use client';

import React, { useState, useMemo } from 'react';
import { FeatureTypeDto, GeoJsonFeature } from '@/lib/types';
import { useAuth } from '@/lib/authContext';
import {
  Table,
  X,
  ChevronUp,
  ChevronDown,
  Download,
  Search,
  Maximize2,
  Minimize2,
  ArrowUpDown,
  LocateFixed,
  Layers,
  Filter,
} from 'lucide-react';

interface AttributeTableProps {
  isOpen: boolean;
  onClose: () => void;
  features: GeoJsonFeature[];
  featureTypes: FeatureTypeDto[];
  selectedFeatureId: string | null;
  onSelectFeature: (feature: GeoJsonFeature) => void;
  onZoomToFeature: (feature: GeoJsonFeature) => void;
}

export const AttributeTable: React.FC<AttributeTableProps> = ({
  isOpen,
  onClose,
  features,
  featureTypes,
  selectedFeatureId,
  onSelectFeature,
  onZoomToFeature,
}) => {
  const { lang } = useAuth();
  const isAr = lang === 'ar';

  const [selectedLayerCode, setSelectedLayerCode] = useState<string>('PIPE');
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState('ALL');
  const [sortField, setSortField] = useState<string>('name');
  const [sortAsc, setSortAsc] = useState<boolean>(true);
  const [currentPage, setCurrentPage] = useState<number>(1);
  const [pageSize, setPageSize] = useState<number>(10);
  const [isExpanded, setIsExpanded] = useState<boolean>(false);

  // If selected layer is not set or features don't have it, default to first available
  const availableLayerCodes = useMemo(() => {
    const set = new Set<string>();
    features.forEach((f) => set.add(f.properties.featureTypeCode));
    return Array.from(set).sort();
  }, [features]);

  const activeLayerCode = useMemo(() => {
    if (availableLayerCodes.includes(selectedLayerCode)) {
      return selectedLayerCode;
    }
    return availableLayerCodes[0] || '';
  }, [availableLayerCodes, selectedLayerCode]);

  const currentTypeDto = featureTypes.find((ft) => ft.code === activeLayerCode);

  // Features for this layer
  const layerFeatures = useMemo(() => {
    return features.filter((f) => f.properties.featureTypeCode === activeLayerCode);
  }, [features, activeLayerCode]);

  // Extract dynamic attribute keys from currentTypeDto schema
  const attributeKeys = useMemo(() => {
    const props = currentTypeDto?.attributeSchema?.properties;
    return props ? Object.keys(props) : [];
  }, [currentTypeDto]);

  // Filtered & Sorted Features
  const processedFeatures = useMemo(() => {
    let result = layerFeatures.filter((f) => {
      // Status filter
      if (statusFilter !== 'ALL' && f.properties.status !== statusFilter) {
        return false;
      }
      // Text search
      if (searchQuery.trim()) {
        const q = searchQuery.toLowerCase().trim();
        const nameMatch = (f.properties.name || '').toLowerCase().includes(q);
        const idMatch = f.id.toLowerCase().includes(q);
        const attrMatch = Object.values(f.properties.attributes || {}).some((v) =>
          String(v).toLowerCase().includes(q)
        );
        return nameMatch || idMatch || attrMatch;
      }
      return true;
    });

    // Sorting
    result.sort((a, b) => {
      let valA: any;
      let valB: any;

      if (sortField === 'name') {
        valA = a.properties.name || '';
        valB = b.properties.name || '';
      } else if (sortField === 'status') {
        valA = a.properties.status || '';
        valB = b.properties.status || '';
      } else if (sortField === 'version') {
        valA = a.properties.version ?? 1;
        valB = b.properties.version ?? 1;
      } else if (sortField === 'diameter') {
        valA = a.properties.diameterMm ?? 0;
        valB = b.properties.diameterMm ?? 0;
      } else if (sortField === 'flowDirection') {
        valA = a.properties.flowDirection || '';
        valB = b.properties.flowDirection || '';
      } else {
        valA = (a.properties.attributes || {})[sortField] ?? '';
        valB = (b.properties.attributes || {})[sortField] ?? '';
      }

      if (valA < valB) return sortAsc ? -1 : 1;
      if (valA > valB) return sortAsc ? 1 : -1;
      return 0;
    });

    return result;
  }, [layerFeatures, statusFilter, searchQuery, sortField, sortAsc]);

  // Pagination
  const totalPages = Math.max(1, Math.ceil(processedFeatures.length / pageSize));
  const pagedFeatures = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return processedFeatures.slice(start, start + pageSize);
  }, [processedFeatures, currentPage, pageSize]);

  const handleSort = (field: string) => {
    if (sortField === field) {
      setSortAsc(!sortAsc);
    } else {
      setSortField(field);
      setSortAsc(true);
    }
  };

  // CSV Export
  const exportToCsv = () => {
    if (processedFeatures.length === 0) return;

    // Headers
    const headers = [
      isAr ? 'المعرّف (ID)' : 'ID',
      isAr ? 'الاسم' : 'Name',
      isAr ? 'النوع' : 'Type',
      isAr ? 'الحالة' : 'Status',
      isAr ? 'الإصدار' : 'Version',
    ];

    if (currentTypeDto?.geometryKind === 'LINE') {
      headers.push(isAr ? 'القطر (مم)' : 'Diameter (mm)');
      headers.push(isAr ? 'اتجاه السريان' : 'Flow Direction');
      headers.push(isAr ? 'المادة' : 'Material');
    }

    attributeKeys.forEach((key) => headers.push(key));

    const rows = processedFeatures.map((f) => {
      const row = [
        `"${f.id}"`,
        `"${(f.properties.name || '').replace(/"/g, '""')}"`,
        `"${f.properties.featureTypeCode}"`,
        `"${f.properties.status}"`,
        `"${f.properties.version ?? 1}"`,
      ];

      if (currentTypeDto?.geometryKind === 'LINE') {
        row.push(`"${f.properties.diameterMm || ''}"`);
        row.push(`"${f.properties.flowDirection || ''}"`);
        row.push(`"${f.properties.material || ''}"`);
      }

      attributeKeys.forEach((key) => {
        const val = (f.properties.attributes || {})[key];
        row.push(`"${val !== undefined && val !== null ? String(val).replace(/"/g, '""') : ''}"`);
      });

      return row.join(',');
    });

    // Add UTF-8 BOM so Excel opens Arabic correctly
    const csvContent = '\uFEFF' + [headers.join(','), ...rows].join('\r\n');
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.setAttribute('href', url);
    link.setAttribute(
      'download',
      `ACUD_${activeLayerCode}_${new Date().toISOString().slice(0, 10)}.csv`
    );
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  };

  if (!isOpen) return null;

  return (
    <div
      className={`fixed bottom-0 left-0 right-0 z-30 bg-slate-900 border-t border-slate-700 shadow-2xl flex flex-col transition-all duration-300 ${
        isExpanded ? 'h-[75vh]' : 'h-80'
      }`}
    >
      {/* Top Header Bar */}
      <div className="bg-slate-950 px-4 py-2 border-b border-slate-800 flex items-center justify-between gap-4">
        {/* Layer selector tabs */}
        <div className="flex items-center gap-2 overflow-x-auto py-1 scrollbar-none">
          <span className="text-xs font-bold text-sky-400 flex items-center gap-1.5 flex-shrink-0">
            <Table className="w-4 h-4" />
            <span>{isAr ? 'جدول البيانات الجغرافية:' : 'Attribute Table:'}</span>
          </span>

          {availableLayerCodes.map((code) => {
            const count = features.filter((f) => f.properties.featureTypeCode === code).length;
            const isSelected = code === activeLayerCode;
            return (
              <button
                key={code}
                onClick={() => {
                  setSelectedLayerCode(code);
                  setCurrentPage(1);
                }}
                className={`px-3 py-1 rounded-lg text-xs font-semibold whitespace-nowrap transition flex items-center gap-1.5 ${
                  isSelected
                    ? 'bg-sky-600 text-white shadow-md shadow-sky-900/50'
                    : 'bg-slate-800 text-slate-300 hover:bg-slate-700 hover:text-white'
                }`}
              >
                <span>{code}</span>
                <span className="px-1.5 py-0.2 rounded-full text-[10px] bg-black/30 font-mono">
                  {count}
                </span>
              </button>
            );
          })}
        </div>

        {/* Window controls */}
        <div className="flex items-center gap-1 flex-shrink-0">
          <button
            onClick={() => setIsExpanded(!isExpanded)}
            className="p-1.5 text-slate-400 hover:text-white rounded-lg hover:bg-slate-800 transition"
            title={isExpanded ? (isAr ? 'تصغير' : 'Minimize') : isAr ? 'تكبير' : 'Maximize'}
          >
            {isExpanded ? <Minimize2 className="w-4 h-4" /> : <Maximize2 className="w-4 h-4" />}
          </button>
          <button
            onClick={onClose}
            className="p-1.5 text-slate-400 hover:text-white rounded-lg hover:bg-slate-800 transition"
            title={isAr ? 'إغلاق الجدول' : 'Close table'}
          >
            <X className="w-4 h-4" />
          </button>
        </div>
      </div>

      {/* Toolbar: Search, Filters & Export */}
      <div className="px-4 py-2 bg-slate-900/90 border-b border-slate-800 flex items-center justify-between gap-3 flex-wrap text-xs">
        {/* Search */}
        <div className="flex items-center gap-2 flex-1 max-w-sm">
          <div className="relative w-full">
            <Search className="w-3.5 h-3.5 text-slate-400 absolute right-3 rtl:right-3 rtl:left-auto ltr:left-3 ltr:right-auto top-2.5" />
            <input
              type="text"
              value={searchQuery}
              onChange={(e) => {
                setSearchQuery(e.target.value);
                setCurrentPage(1);
              }}
              placeholder={isAr ? 'بحث بالاسم، المعرّف، أو الخصائص...' : 'Search by name, ID, or attributes...'}
              className="w-full bg-slate-950 border border-slate-700 rounded-lg py-1.5 rtl:pr-8 rtl:pl-3 ltr:pl-8 ltr:pr-3 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-sky-500"
            />
          </div>
        </div>

        {/* Filter by Status */}
        <div className="flex items-center gap-2">
          <span className="text-slate-400 flex items-center gap-1">
            <Filter className="w-3.5 h-3.5 text-slate-500" />
            <span>{isAr ? 'الحالة:' : 'Status:'}</span>
          </span>
          <select
            value={statusFilter}
            onChange={(e) => {
              setStatusFilter(e.target.value);
              setCurrentPage(1);
            }}
            className="bg-slate-950 border border-slate-700 rounded-lg px-2.5 py-1 text-xs text-white focus:outline-none focus:border-sky-500"
          >
            <option value="ALL">{isAr ? 'الكل (All)' : 'All'}</option>
            <option value="ACTIVE">{isAr ? 'نشط (ACTIVE)' : 'ACTIVE'}</option>
            <option value="PLANNED">{isAr ? 'مخطط (PLANNED)' : 'PLANNED'}</option>
            <option value="DAMAGED">{isAr ? 'تالف (DAMAGED)' : 'DAMAGED'}</option>
            <option value="ABANDONED">{isAr ? 'مهجور (ABANDONED)' : 'ABANDONED'}</option>
            <option value="INACTIVE">{isAr ? 'معطل (INACTIVE)' : 'INACTIVE'}</option>
          </select>
        </div>

        {/* Export CSV & Count */}
        <div className="flex items-center gap-3">
          <span className="text-slate-400 font-mono text-[11px]">
            {isAr ? 'عدد السجلات:' : 'Records:'}{' '}
            <strong className="text-white">{processedFeatures.length}</strong>
          </span>

          <button
            onClick={exportToCsv}
            disabled={processedFeatures.length === 0}
            className="px-3 py-1.5 rounded-lg bg-emerald-600 hover:bg-emerald-500 disabled:opacity-50 text-white font-semibold flex items-center gap-1.5 transition shadow-sm"
          >
            <Download className="w-3.5 h-3.5" />
            <span>{isAr ? 'تصدير CSV' : 'Export CSV'}</span>
          </button>
        </div>
      </div>

      {/* Table Content */}
      <div className="flex-1 overflow-auto">
        <table className="w-full text-left rtl:text-right border-collapse text-xs">
          <thead className="bg-slate-950 text-slate-400 sticky top-0 z-10 select-none border-b border-slate-800">
            <tr>
              <th className="p-2.5 font-semibold text-center w-12">#</th>
              <th className="p-2.5 font-semibold text-center w-16">{isAr ? 'تحديد' : 'Locate'}</th>
              <th
                onClick={() => handleSort('name')}
                className="p-2.5 font-semibold cursor-pointer hover:text-white"
              >
                <div className="flex items-center gap-1">
                  <span>{isAr ? 'الاسم' : 'Name'}</span>
                  <ArrowUpDown className="w-3 h-3 text-slate-500" />
                </div>
              </th>
              <th
                onClick={() => handleSort('status')}
                className="p-2.5 font-semibold cursor-pointer hover:text-white w-28"
              >
                <div className="flex items-center gap-1">
                  <span>{isAr ? 'الحالة' : 'Status'}</span>
                  <ArrowUpDown className="w-3 h-3 text-slate-500" />
                </div>
              </th>
              <th
                onClick={() => handleSort('version')}
                className="p-2.5 font-semibold cursor-pointer hover:text-white w-20 text-center"
              >
                <div className="flex items-center justify-center gap-1">
                  <span>{isAr ? 'الإصدار' : 'Version'}</span>
                  <ArrowUpDown className="w-3 h-3 text-slate-500" />
                </div>
              </th>

              {/* Line specifics */}
              {currentTypeDto?.geometryKind === 'LINE' && (
                <>
                  <th
                    onClick={() => handleSort('diameter')}
                    className="p-2.5 font-semibold cursor-pointer hover:text-white w-28"
                  >
                    <div className="flex items-center gap-1">
                      <span>{isAr ? 'القطر (مم)' : 'Diameter'}</span>
                      <ArrowUpDown className="w-3 h-3 text-slate-500" />
                    </div>
                  </th>
                  <th
                    onClick={() => handleSort('flowDirection')}
                    className="p-2.5 font-semibold cursor-pointer hover:text-white w-36"
                  >
                    <div className="flex items-center gap-1">
                      <span>{isAr ? 'الاتجاه' : 'Flow'}</span>
                      <ArrowUpDown className="w-3 h-3 text-slate-500" />
                    </div>
                  </th>
                </>
              )}

              {/* Dynamic attribute columns */}
              {attributeKeys.map((key) => (
                <th
                  key={key}
                  onClick={() => handleSort(key)}
                  className="p-2.5 font-semibold cursor-pointer hover:text-white font-mono"
                >
                  <div className="flex items-center gap-1">
                    <span>{key}</span>
                    <ArrowUpDown className="w-3 h-3 text-slate-500" />
                  </div>
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-800 text-slate-300">
            {pagedFeatures.length === 0 ? (
              <tr>
                <td
                  colSpan={6 + attributeKeys.length}
                  className="p-8 text-center text-slate-500 italic"
                >
                  {isAr ? 'لا توجد معالم مطابقة للشروط' : 'No matching features found'}
                </td>
              </tr>
            ) : (
              pagedFeatures.map((f, idx) => {
                const isSelected = f.id === selectedFeatureId;
                const rowNum = (currentPage - 1) * pageSize + idx + 1;
                const attrs = f.properties.attributes || {};

                return (
                  <tr
                    key={f.id}
                    onClick={() => onSelectFeature(f)}
                    className={`cursor-pointer transition hover:bg-slate-800/60 ${
                      isSelected ? 'bg-sky-950/70 border-l-2 rtl:border-l-0 rtl:border-r-2 border-sky-400' : ''
                    }`}
                  >
                    <td className="p-2.5 text-center text-slate-500 font-mono text-[11px]">
                      {rowNum}
                    </td>
                    <td className="p-2.5 text-center">
                      <button
                        onClick={(e) => {
                          e.stopPropagation();
                          onZoomToFeature(f);
                        }}
                        className="p-1 rounded hover:bg-sky-600/30 text-sky-400 hover:text-white transition"
                        title={isAr ? 'تكبير والانتقال للمعلم' : 'Zoom and select'}
                      >
                        <LocateFixed className="w-4 h-4" />
                      </button>
                    </td>
                    <td className="p-2.5 font-semibold text-white">
                      {f.properties.name}
                    </td>
                    <td className="p-2.5">
                      <span
                        className={`px-2 py-0.5 rounded-full text-[10px] font-bold ${
                          f.properties.status === 'ACTIVE'
                            ? 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/40'
                            : f.properties.status === 'PLANNED'
                            ? 'bg-sky-500/20 text-sky-300 border border-sky-500/40'
                            : f.properties.status === 'DAMAGED'
                            ? 'bg-red-500/20 text-red-300 border border-red-500/40'
                            : 'bg-amber-500/20 text-amber-300 border border-amber-500/40'
                        }`}
                      >
                        {f.properties.status}
                      </span>
                    </td>
                    <td className="p-2.5 text-center font-mono text-amber-400">
                      v{f.properties.version ?? 1}
                    </td>

                    {/* Line specifics */}
                    {currentTypeDto?.geometryKind === 'LINE' && (
                      <>
                        <td className="p-2.5 font-mono">
                          {f.properties.diameterMm ? `${f.properties.diameterMm} mm` : '-'}
                        </td>
                        <td className="p-2.5 font-mono text-[11px] text-slate-400">
                          {f.properties.flowDirection || '-'}
                        </td>
                      </>
                    )}

                    {/* Dynamic attribute values */}
                    {attributeKeys.map((key) => {
                      const val = attrs[key];
                      return (
                        <td key={key} className="p-2.5 font-mono text-[11px] text-slate-300">
                          {val !== undefined && val !== null ? String(val) : '-'}
                        </td>
                      );
                    })}
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>

      {/* Pagination Footer */}
      <div className="bg-slate-950 px-4 py-2 border-t border-slate-800 flex items-center justify-between text-xs text-slate-400">
        <div className="flex items-center gap-2">
          <span>{isAr ? 'عدد الصفوف:' : 'Rows per page:'}</span>
          <select
            value={pageSize}
            onChange={(e) => {
              setPageSize(Number(e.target.value));
              setCurrentPage(1);
            }}
            className="bg-slate-900 border border-slate-700 rounded px-2 py-0.5 text-xs text-white focus:outline-none"
          >
            <option value="10">10</option>
            <option value="25">25</option>
            <option value="50">50</option>
          </select>
        </div>

        <div className="flex items-center gap-3">
          <span>
            {isAr ? 'الصفحة' : 'Page'} <strong className="text-white">{currentPage}</strong>{' '}
            {isAr ? 'من' : 'of'} <strong className="text-white">{totalPages}</strong>
          </span>

          <div className="flex items-center gap-1">
            <button
              onClick={() => setCurrentPage((p) => Math.max(1, p - 1))}
              disabled={currentPage <= 1}
              className="px-2.5 py-1 rounded bg-slate-800 hover:bg-slate-700 disabled:opacity-40 text-white font-semibold transition"
            >
              {isAr ? 'السابق' : 'Previous'}
            </button>
            <button
              onClick={() => setCurrentPage((p) => Math.min(totalPages, p + 1))}
              disabled={currentPage >= totalPages}
              className="px-2.5 py-1 rounded bg-slate-800 hover:bg-slate-700 disabled:opacity-40 text-white font-semibold transition"
            >
              {isAr ? 'التالي' : 'Next'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};

