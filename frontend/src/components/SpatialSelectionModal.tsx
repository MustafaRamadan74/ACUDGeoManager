'use client';

import React, { useState } from 'react';
import { FeatureTypeDto, GeoJsonFeature } from '@/lib/types';
import { useAuth } from '@/lib/authContext';
import { Filter, X, Search, Check, Layers, Sparkles } from 'lucide-react';

interface SpatialSelectionModalProps {
  isOpen: boolean;
  onClose: () => void;
  featureTypes: FeatureTypeDto[];
  features: GeoJsonFeature[];
  onResults: (results: GeoJsonFeature[], queryDesc: string) => void;
}

export const SpatialSelectionModal: React.FC<SpatialSelectionModalProps> = ({
  isOpen,
  onClose,
  featureTypes,
  features,
  onResults,
}) => {
  const { lang } = useAuth();
  const isAr = lang === 'ar';

  const [selectedLayer, setSelectedLayer] = useState<string>(featureTypes[0]?.code || 'PIPE');
  const [selectedField, setSelectedField] = useState<string>('status');
  const [operator, setOperator] = useState<string>('=');
  const [filterValue, setFilterValue] = useState<string>('ACTIVE');

  if (!isOpen) return null;

  const currentType = featureTypes.find((ft) => ft.code === selectedLayer);
  const schemaProps = (currentType?.attributeSchema?.properties as Record<string, any>) || {};

  // Available fields for this layer
  const availableFields: { id: string; label: string; isAttr?: boolean }[] = [
    { id: 'name', label: isAr ? 'الاسم (Name)' : 'Name' },
    { id: 'status', label: isAr ? 'الحالة (Status)' : 'Status' },
    { id: 'version', label: isAr ? 'الإصدار (Version)' : 'Version' },
  ];

  if (currentType?.geometryKind === 'LINE') {
    availableFields.push({ id: 'diameterMm', label: isAr ? 'القطر (مم)' : 'Diameter (mm)' });
    availableFields.push({ id: 'flowDirection', label: isAr ? 'اتجاه السريان' : 'Flow Direction' });
    availableFields.push({ id: 'material', label: isAr ? 'المادة' : 'Material' });
  }

  Object.keys(schemaProps).forEach((propKey) => {
    availableFields.push({ id: propKey, label: `${propKey} (خاصية)`, isAttr: true });
  });

  const handleExecute = (e: React.FormEvent) => {
    e.preventDefault();

    const layerFeatures = features.filter((f) => f.properties.featureTypeCode === selectedLayer);

    const matches = layerFeatures.filter((f) => {
      let val: any;
      if (selectedField === 'name') val = f.properties.name || '';
      else if (selectedField === 'status') val = f.properties.status || '';
      else if (selectedField === 'version') val = f.properties.version ?? 1;
      else if (selectedField === 'diameterMm') val = f.properties.diameterMm ?? 0;
      else if (selectedField === 'flowDirection') val = f.properties.flowDirection || '';
      else if (selectedField === 'material') val = f.properties.material || '';
      else val = (f.properties.attributes || {})[selectedField];

      if (val === undefined || val === null) return false;

      const strVal = String(val).toLowerCase();
      const strTarget = filterValue.trim().toLowerCase();
      const numVal = Number(val);
      const numTarget = Number(filterValue);

      switch (operator) {
        case '=':
          return strVal === strTarget;
        case '!=':
          return strVal !== strTarget;
        case 'contains':
          return strVal.includes(strTarget);
        case '>':
          return !isNaN(numVal) && !isNaN(numTarget) && numVal > numTarget;
        case '<':
          return !isNaN(numVal) && !isNaN(numTarget) && numVal < numTarget;
        case '>=':
          return !isNaN(numVal) && !isNaN(numTarget) && numVal >= numTarget;
        case '<=':
          return !isNaN(numVal) && !isNaN(numTarget) && numVal <= numTarget;
        default:
          return strVal === strTarget;
      }
    });

    const queryDesc = `${selectedLayer}.${selectedField} ${operator} "${filterValue}" (${matches.length} ${isAr ? 'معلم' : 'features'})`;
    onResults(matches, queryDesc);
    onClose();
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-sm animate-in fade-in duration-200">
      <div className="bg-slate-900 border border-slate-700/80 rounded-2xl max-w-lg w-full shadow-2xl flex flex-col overflow-hidden">
        {/* Header */}
        <div className="p-4 border-b border-slate-800 flex items-center justify-between bg-slate-950/80">
          <div className="flex items-center gap-2.5">
            <div className="p-2 rounded-lg bg-sky-500/10 border border-sky-500/30 text-sky-400">
              <Filter className="w-5 h-5" />
            </div>
            <div>
              <h3 className="text-sm font-bold text-white flex items-center gap-2">
                <span>{isAr ? 'تحديد المعالم بالخصائص (Select by Attribute)' : 'Select by Attribute'}</span>
              </h3>
              <p className="text-[11px] text-slate-400">
                {isAr
                  ? 'بناء استعلام منطقي على طبقة جغرافية لعزل وتحديد المعالم المطابقة'
                  : 'Build attribute query condition to select and isolate matching features'}
              </p>
            </div>
          </div>

          <button
            onClick={onClose}
            className="p-1.5 text-slate-400 hover:text-white rounded-lg hover:bg-slate-800 transition"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Form Body */}
        <form onSubmit={handleExecute} className="p-5 space-y-4 text-xs">
          {/* Layer Selection */}
          <div className="space-y-1.5">
            <label className="text-slate-300 font-semibold flex items-center gap-1.5">
              <Layers className="w-3.5 h-3.5 text-sky-400" />
              <span>{isAr ? 'الطبقة المستهدفة (Target Layer)' : 'Target Layer'}</span>
            </label>
            <select
              value={selectedLayer}
              onChange={(e) => {
                setSelectedLayer(e.target.value);
                setSelectedField('status');
              }}
              className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white focus:outline-none focus:border-sky-500"
            >
              {featureTypes.map((ft) => (
                <option key={ft.code} value={ft.code}>
                  {ft.displayName} ({ft.code})
                </option>
              ))}
            </select>
          </div>

          {/* Condition Row: Field + Operator + Value */}
          <div className="grid grid-cols-1 md:grid-cols-3 gap-2 bg-slate-950/60 p-3.5 rounded-xl border border-slate-800">
            {/* Field */}
            <div className="space-y-1">
              <label className="text-slate-400 text-[11px] font-semibold">{isAr ? 'الحقل / الخاصية' : 'Field'}</label>
              <select
                value={selectedField}
                onChange={(e) => setSelectedField(e.target.value)}
                className="w-full bg-slate-900 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-white focus:outline-none focus:border-sky-500"
              >
                {availableFields.map((f) => (
                  <option key={f.id} value={f.id}>
                    {f.label}
                  </option>
                ))}
              </select>
            </div>

            {/* Operator */}
            <div className="space-y-1">
              <label className="text-slate-400 text-[11px] font-semibold">{isAr ? 'المعامل' : 'Operator'}</label>
              <select
                value={operator}
                onChange={(e) => setOperator(e.target.value)}
                className="w-full bg-slate-900 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-white focus:outline-none focus:border-sky-500 font-mono"
              >
                <option value="=">=</option>
                <option value="!=">!=</option>
                <option value="contains">{isAr ? 'يحتوي (contains)' : 'contains'}</option>
                <option value=">">&gt;</option>
                <option value="<">&lt;</option>
                <option value=">=">&gt;=</option>
                <option value="<=">&lt;=</option>
              </select>
            </div>

            {/* Value */}
            <div className="space-y-1">
              <label className="text-slate-400 text-[11px] font-semibold">{isAr ? 'القيمة' : 'Value'}</label>
              <input
                type="text"
                value={filterValue}
                onChange={(e) => setFilterValue(e.target.value)}
                placeholder="ACTIVE / 250 / ..."
                className="w-full bg-slate-900 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-white focus:outline-none focus:border-sky-500"
              />
            </div>
          </div>

          {/* Quick status shortcut hints */}
          <div className="flex items-center gap-1.5 text-[11px] text-slate-400">
            <span>{isAr ? 'اختصارات سريعة:' : 'Quick values:'}</span>
            {['ACTIVE', 'PLANNED', 'DAMAGED', 'ABANDONED'].map((st) => (
              <button
                key={st}
                type="button"
                onClick={() => {
                  setSelectedField('status');
                  setOperator('=');
                  setFilterValue(st);
                }}
                className="px-2 py-0.5 rounded bg-slate-800 hover:bg-slate-700 text-slate-300 text-[10px] font-mono transition"
              >
                {st}
              </button>
            ))}
          </div>

          {/* Footer Actions */}
          <div className="pt-3 border-t border-slate-800 flex justify-end gap-3">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 rounded-xl text-xs font-semibold text-slate-300 hover:text-white bg-slate-800 hover:bg-slate-700 transition"
            >
              {isAr ? 'إلغاء' : 'Cancel'}
            </button>
            <button
              type="submit"
              className="px-5 py-2 rounded-xl text-xs font-semibold text-white bg-sky-600 hover:bg-sky-500 transition flex items-center gap-2 shadow-lg shadow-sky-950/50"
            >
              <Sparkles className="w-4 h-4" />
              <span>{isAr ? 'تنفيذ التحديد' : 'Apply Selection'}</span>
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

