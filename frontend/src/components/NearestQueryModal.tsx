'use client';

import React, { useState, useEffect } from 'react';
import { FeatureTypeDto, GeoJsonFeatureCollection, GeoJsonFeature } from '@/lib/types';
import { api } from '@/lib/api';
import { useAuth } from '@/lib/authContext';
import { Navigation, X, Crosshair, Loader2, Sparkles, AlertCircle } from 'lucide-react';

interface NearestQueryModalProps {
  isOpen: boolean;
  onClose: () => void;
  activeNetworkId: string | null;
  featureTypes: FeatureTypeDto[];
  currentCenter?: [number, number] | null; // [lng, lat]
  onResults: (results: GeoJsonFeature[], description: string) => void;
}

export const NearestQueryModal: React.FC<NearestQueryModalProps> = ({
  isOpen,
  onClose,
  activeNetworkId,
  featureTypes,
  currentCenter,
  onResults,
}) => {
  const { lang } = useAuth();
  const isAr = lang === 'ar';

  const [x, setX] = useState<string>('31.748000');
  const [y, setY] = useState<string>('30.015000');
  const [selectedType, setSelectedType] = useState<string>('');
  const [selectedStatus, setSelectedStatus] = useState<string>('');
  const [maxDistance, setMaxDistance] = useState<string>('1000');
  const [limit, setLimit] = useState<number>(10);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Sync with current center if opened and center is available
  useEffect(() => {
    if (isOpen && currentCenter) {
      setX(currentCenter[0].toFixed(6));
      setY(currentCenter[1].toFixed(6));
    }
  }, [isOpen, currentCenter]);

  if (!isOpen) return null;

  const handleUseCenter = () => {
    if (currentCenter) {
      setX(currentCenter[0].toFixed(6));
      setY(currentCenter[1].toFixed(6));
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    const numX = parseFloat(x);
    const numY = parseFloat(y);

    if (isNaN(numX) || isNaN(numY)) {
      setError(isAr ? 'يرجى إدخال إحداثيات صحيحة' : 'Please enter valid coordinates');
      return;
    }

    if (numX < -180 || numX > 180 || numY < -90 || numY > 90) {
      setError(isAr ? 'الإحداثيات خارج النطاق الجغرافي المسموح' : 'Coordinates out of bounds');
      return;
    }

    const dist = maxDistance ? parseFloat(maxDistance) : undefined;
    if (dist !== undefined && (isNaN(dist) || dist <= 0)) {
      setError(isAr ? 'أقصى مسافة يجب أن تكون رقماً موجباً' : 'Max distance must be a positive number');
      return;
    }

    setIsLoading(true);

    try {
      const collection = await api.queryNearest({
        x: numX,
        y: numY,
        networkId: activeNetworkId || undefined,
        type: selectedType || undefined,
        status: selectedStatus || undefined,
        maxDistanceM: dist,
        limit: limit > 0 ? limit : 10,
      });

      const count = collection.features ? collection.features.length : 0;
      const typeLabel = selectedType || (isAr ? 'جميع الطبقات' : 'All Layers');
      const desc = isAr
        ? `أقرب ${count} معالم (${typeLabel}) للنقطة [${numX.toFixed(4)}, ${numY.toFixed(4)}]`
        : `Nearest ${count} features (${typeLabel}) to [${numX.toFixed(4)}, ${numY.toFixed(4)}]`;

      onResults(collection.features || [], desc);
      onClose();
    } catch (err: any) {
      console.error('Failed to run nearest query:', err);
      setError(err?.message || (isAr ? 'فشل تنفيذ استعلام الأقرب' : 'Nearest query failed'));
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-sm animate-in fade-in duration-200">
      <div className="bg-slate-900 border border-slate-700/80 rounded-2xl max-w-md w-full shadow-2xl flex flex-col overflow-hidden">
        {/* Header */}
        <div className="p-4 border-b border-slate-800 flex items-center justify-between bg-slate-950/80">
          <div className="flex items-center gap-2.5">
            <div className="p-2 rounded-lg bg-amber-500/10 border border-amber-500/30 text-amber-400">
              <Navigation className="w-5 h-5" />
            </div>
            <div>
              <h3 className="text-sm font-bold text-white flex items-center gap-2">
                <span>{isAr ? 'البحث عن أقرب أصول (KNN Nearest)' : 'Find Nearest Assets'}</span>
              </h3>
              <p className="text-[11px] text-slate-400">
                {isAr
                  ? 'البحث فائق السرعة عبر PostGIS KNN Operator (<->) وحساب المسافات الدقيقة بالمتر'
                  : 'Fast PostGIS KNN (<->) search ordered by exact geodesic distance in metres'}
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
        <form onSubmit={handleSubmit} className="p-5 space-y-4 text-xs">
          {error && (
            <div className="p-3 rounded-xl bg-rose-950/50 border border-rose-800 text-rose-200 text-xs flex items-center gap-2">
              <AlertCircle className="w-4 h-4 flex-shrink-0" />
              <span>{error}</span>
            </div>
          )}

          {/* Coordinates (X, Y) */}
          <div className="space-y-1.5">
            <div className="flex items-center justify-between">
              <label className="text-slate-300 font-semibold">
                {isAr ? 'إحداثيات نقطة البحث (WGS84)' : 'Search Point Coordinates'}
              </label>
              {currentCenter && (
                <button
                  type="button"
                  onClick={handleUseCenter}
                  className="text-[11px] text-amber-400 hover:text-amber-300 flex items-center gap-1 font-medium transition"
                >
                  <Crosshair className="w-3 h-3" />
                  <span>{isAr ? 'مركز الخريطة' : 'Map Center'}</span>
                </button>
              )}
            </div>

            <div className="grid grid-cols-2 gap-2">
              <div>
                <span className="text-[10px] text-slate-400 font-mono mb-0.5 block">
                  {isAr ? 'خط الطول (Longitude X)' : 'Longitude (X)'}
                </span>
                <input
                  type="number"
                  step="any"
                  required
                  value={x}
                  onChange={(e) => setX(e.target.value)}
                  className="w-full bg-slate-950 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-white font-mono focus:outline-none focus:border-amber-500"
                />
              </div>
              <div>
                <span className="text-[10px] text-slate-400 font-mono mb-0.5 block">
                  {isAr ? 'خط العرض (Latitude Y)' : 'Latitude (Y)'}
                </span>
                <input
                  type="number"
                  step="any"
                  required
                  value={y}
                  onChange={(e) => setY(e.target.value)}
                  className="w-full bg-slate-950 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-white font-mono focus:outline-none focus:border-amber-500"
                />
              </div>
            </div>
          </div>

          {/* Target Layer Filter */}
          <div className="space-y-1">
            <label className="text-slate-300 font-semibold">{isAr ? 'نوع المعلم / الطبقة' : 'Asset Layer'}</label>
            <select
              value={selectedType}
              onChange={(e) => setSelectedType(e.target.value)}
              className="w-full bg-slate-950 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-white focus:outline-none focus:border-amber-500"
            >
              <option value="">{isAr ? '🌐 جميع الطبقات (All Layers)' : '🌐 All Layers'}</option>
              {featureTypes.map((ft) => (
                <option key={ft.code} value={ft.code}>
                  {ft.displayName} ({ft.code})
                </option>
              ))}
            </select>
          </div>

          {/* Status & Max Distance */}
          <div className="grid grid-cols-2 gap-2">
            <div className="space-y-1">
              <label className="text-slate-300 font-semibold">{isAr ? 'الحالة التشغيلية' : 'Status'}</label>
              <select
                value={selectedStatus}
                onChange={(e) => setSelectedStatus(e.target.value)}
                className="w-full bg-slate-950 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-white focus:outline-none focus:border-amber-500"
              >
                <option value="">{isAr ? 'الكل' : 'All'}</option>
                <option value="ACTIVE">ACTIVE</option>
                <option value="PLANNED">PLANNED</option>
                <option value="DAMAGED">DAMAGED</option>
                <option value="ABANDONED">ABANDONED</option>
              </select>
            </div>

            <div className="space-y-1">
              <label className="text-slate-300 font-semibold">
                {isAr ? 'أقصى نطاق (متر)' : 'Max Radius (m)'}
              </label>
              <input
                type="number"
                min="1"
                step="10"
                value={maxDistance}
                onChange={(e) => setMaxDistance(e.target.value)}
                placeholder="1000"
                className="w-full bg-slate-950 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-white font-mono focus:outline-none focus:border-amber-500"
              />
            </div>
          </div>

          {/* Limit / Max count */}
          <div className="space-y-1">
            <div className="flex items-center justify-between">
              <label className="text-slate-300 font-semibold">{isAr ? 'أقصى عدد للنتائج' : 'Max Results Limit'}</label>
              <span className="font-mono text-amber-400 font-bold">{limit}</span>
            </div>
            <input
              type="range"
              min="1"
              max="100"
              step="1"
              value={limit}
              onChange={(e) => setLimit(parseInt(e.target.value, 10))}
              className="w-full accent-amber-500 cursor-pointer"
            />
          </div>

          {/* Footer Actions */}
          <div className="pt-3 border-t border-slate-800 flex justify-end gap-3">
            <button
              type="button"
              onClick={onClose}
              disabled={isLoading}
              className="px-4 py-2 rounded-xl text-xs font-semibold text-slate-300 hover:text-white bg-slate-800 hover:bg-slate-700 transition"
            >
              {isAr ? 'إلغاء' : 'Cancel'}
            </button>
            <button
              type="submit"
              disabled={isLoading}
              className="px-5 py-2 rounded-xl text-xs font-semibold text-white bg-gradient-to-r from-amber-600 to-amber-500 hover:from-amber-500 hover:to-amber-400 transition flex items-center gap-2 shadow-lg shadow-amber-950/50 disabled:opacity-50"
            >
              {isLoading ? (
                <>
                  <Loader2 className="w-4 h-4 animate-spin" />
                  <span>{isAr ? 'جارٍ البحث...' : 'Searching...'}</span>
                </>
              ) : (
                <>
                  <Sparkles className="w-4 h-4" />
                  <span>{isAr ? 'بدء البحث عن الأقرب' : 'Find Nearest'}</span>
                </>
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
