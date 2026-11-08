'use client';

import React from 'react';
import { useAuth } from '@/lib/authContext';
import { FeatureTypeDto, NetworkDto } from '@/lib/types';
import { Layers, Eye, EyeOff, Droplet, Building2, CheckSquare, Square } from 'lucide-react';

interface SidebarProps {
  networks: NetworkDto[];
  featureTypes: FeatureTypeDto[];
  visibleTypes: Set<string>;
  onToggleType: (typeCode: string) => void;
  onToggleAll: (selectAll: boolean) => void;
  featuresCount: number;
  isLimitReached: boolean;
  isLoading: boolean;
}

export const Sidebar: React.FC<SidebarProps> = ({
  featureTypes,
  visibleTypes,
  onToggleType,
  onToggleAll,
  featuresCount,
  isLimitReached,
  isLoading,
}) => {
  const { lang } = useAuth();
  const isAr = lang === 'ar';

  const waterTypes = featureTypes.filter((f) => f.networkDomain === 'WATER');
  const urbanTypes = featureTypes.filter((f) => f.networkDomain === 'URBAN');

  const allSelected = featureTypes.every((f) => visibleTypes.has(f.code));

  return (
    <aside className="w-72 bg-slate-900 border-r rtl:border-r-0 rtl:border-l border-slate-800 flex flex-col h-full select-none z-10 shadow-lg">
      {/* Sidebar Header */}
      <div className="p-3.5 border-b border-slate-800 flex items-center justify-between">
        <div className="flex items-center gap-2 text-slate-200 font-semibold text-xs uppercase tracking-wider">
          <Layers className="w-4 h-4 text-sky-400" />
          <span>{isAr ? 'طبقات المعالم الجغرافية' : 'Feature Layers'}</span>
        </div>
        <button
          onClick={() => onToggleAll(!allSelected)}
          className="text-[11px] font-medium text-sky-400 hover:text-sky-300 transition flex items-center gap-1"
        >
          {allSelected ? (
            <>
              <Square className="w-3.5 h-3.5" />
              <span>{isAr ? 'إلغاء الكل' : 'Clear'}</span>
            </>
          ) : (
            <>
              <CheckSquare className="w-3.5 h-3.5" />
              <span>{isAr ? 'تحديد الكل' : 'All'}</span>
            </>
          )}
        </button>
      </div>

      {/* Layer Tree List */}
      <div className="flex-1 overflow-y-auto p-3 space-y-4">
        {/* Water Group */}
        {waterTypes.length > 0 && (
          <div>
            <div className="flex items-center gap-1.5 px-2 py-1 text-xs font-bold text-sky-400 uppercase tracking-wider mb-1">
              <Droplet className="w-3.5 h-3.5" />
              <span>{isAr ? 'شبكة مياه الشرب' : 'Water Network'}</span>
            </div>
            <div className="space-y-1">
              {waterTypes.map((ft) => {
                const isVisible = visibleTypes.has(ft.code);
                const color = ft.style.color || ft.style.fillColor || '#3B82F6';

                return (
                  <button
                    key={ft.code}
                    onClick={() => onToggleType(ft.code)}
                    className={`w-full flex items-center justify-between px-2.5 py-1.5 rounded-lg text-xs font-medium transition text-right rtl:text-right ltr:text-left ${
                      isVisible
                        ? 'bg-slate-800/80 text-slate-200 hover:bg-slate-700/80'
                        : 'text-slate-500 hover:bg-slate-800/40'
                    }`}
                  >
                    <div className="flex items-center gap-2">
                      <span
                        className="w-3 h-3 rounded-full flex-shrink-0 border border-slate-700"
                        style={{ backgroundColor: color }}
                      />
                      <span className="truncate">{ft.displayName}</span>
                    </div>
                    {isVisible ? (
                      <Eye className="w-3.5 h-3.5 text-sky-400" />
                    ) : (
                      <EyeOff className="w-3.5 h-3.5 text-slate-600" />
                    )}
                  </button>
                );
              })}
            </div>
          </div>
        )}

        {/* Urban Assets Group */}
        {urbanTypes.length > 0 && (
          <div>
            <div className="flex items-center gap-1.5 px-2 py-1 text-xs font-bold text-amber-400 uppercase tracking-wider mb-1">
              <Building2 className="w-3.5 h-3.5" />
              <span>{isAr ? 'الأصول العمرانية' : 'Urban Assets'}</span>
            </div>
            <div className="space-y-1">
              {urbanTypes.map((ft) => {
                const isVisible = visibleTypes.has(ft.code);
                const color = ft.style.color || ft.style.fillColor || '#F59E0B';

                return (
                  <button
                    key={ft.code}
                    onClick={() => onToggleType(ft.code)}
                    className={`w-full flex items-center justify-between px-2.5 py-1.5 rounded-lg text-xs font-medium transition text-right rtl:text-right ltr:text-left ${
                      isVisible
                        ? 'bg-slate-800/80 text-slate-200 hover:bg-slate-700/80'
                        : 'text-slate-500 hover:bg-slate-800/40'
                    }`}
                  >
                    <div className="flex items-center gap-2">
                      <span
                        className="w-3 h-3 rounded-full flex-shrink-0 border border-slate-700"
                        style={{ backgroundColor: color }}
                      />
                      <span className="truncate">{ft.displayName}</span>
                    </div>
                    {isVisible ? (
                      <Eye className="w-3.5 h-3.5 text-amber-400" />
                    ) : (
                      <EyeOff className="w-3.5 h-3.5 text-slate-600" />
                    )}
                  </button>
                );
              })}
            </div>
          </div>
        )}
      </div>

      {/* Footer Info Badge */}
      <div className="p-3 border-t border-slate-800 bg-slate-950/60 text-xs">
        <div className="flex items-center justify-between text-slate-400 mb-1">
          <span>{isAr ? 'المعالم المعروضة:' : 'Visible features:'}</span>
          <span className="font-semibold text-slate-200">
            {isLoading ? '...' : featuresCount}
          </span>
        </div>
        {isLimitReached && (
          <div className="text-[11px] text-amber-400 font-medium leading-tight mt-1 bg-amber-950/40 p-1.5 rounded border border-amber-800/50">
            ⚠️ {isAr ? 'تم الوصول للحد الأقصى للعرض. قرّب الخريطة لرؤية المزيد.' : 'Max display limit reached. Zoom in for details.'}
          </div>
        )}
      </div>
    </aside>
  );
};

