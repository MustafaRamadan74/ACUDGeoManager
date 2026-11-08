'use client';

import React, { useState, useRef, useEffect, useMemo } from 'react';
import { GeoJsonFeature } from '@/lib/types';
import { useAuth } from '@/lib/authContext';
import { Search, X, MapPin, ChevronRight } from 'lucide-react';

interface SearchBarProps {
  features: GeoJsonFeature[];
  onSelectFeature: (feature: GeoJsonFeature) => void;
  onZoomToFeature: (feature: GeoJsonFeature) => void;
}

export const SearchBar: React.FC<SearchBarProps> = ({
  features,
  onSelectFeature,
  onZoomToFeature,
}) => {
  const { lang } = useAuth();
  const isAr = lang === 'ar';

  const [query, setQuery] = useState('');
  const [isOpen, setIsOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  // Close dropdown on click outside
  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setIsOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  // Filter features based on query
  const searchResults = useMemo(() => {
    if (!query.trim() || query.trim().length < 2) return [];

    const q = query.toLowerCase().trim();
    return features
      .filter((f) => {
        const nameMatch = (f.properties.name || '').toLowerCase().includes(q);
        const typeMatch = (f.properties.featureTypeCode || '').toLowerCase().includes(q);
        const idMatch = f.id.toLowerCase().includes(q);
        const attrMatch = Object.entries(f.properties.attributes || {}).some(
          ([k, v]) => k.toLowerCase().includes(q) || String(v).toLowerCase().includes(q)
        );
        return nameMatch || typeMatch || idMatch || attrMatch;
      })
      .slice(0, 8); // top 8 results
  }, [features, query]);

  const handleSelect = (feature: GeoJsonFeature) => {
    onSelectFeature(feature);
    onZoomToFeature(feature);
    setIsOpen(false);
    setQuery('');
  };

  return (
    <div ref={containerRef} className="relative w-72 md:w-80">
      <div className="relative flex items-center">
        <Search className="w-4 h-4 text-slate-400 absolute right-3 rtl:right-3 rtl:left-auto ltr:left-3 ltr:right-auto pointer-events-none" />
        <input
          type="text"
          value={query}
          onChange={(e) => {
            setQuery(e.target.value);
            setIsOpen(true);
          }}
          onFocus={() => setIsOpen(true)}
          placeholder={isAr ? 'بحث بالاسم، النوع، المعرّف...' : 'Search features, types, IDs...'}
          className="w-full bg-slate-900 border border-slate-700/80 rounded-xl py-1.5 rtl:pr-9 rtl:pl-8 ltr:pl-9 ltr:pr-8 text-xs text-white placeholder-slate-400 focus:outline-none focus:border-sky-500 focus:ring-1 focus:ring-sky-500 transition shadow-inner"
        />
        {query && (
          <button
            onClick={() => {
              setQuery('');
              setIsOpen(false);
            }}
            className="absolute left-2.5 rtl:left-2.5 rtl:right-auto ltr:right-2.5 ltr:left-auto text-slate-400 hover:text-white p-0.5 rounded"
          >
            <X className="w-3.5 h-3.5" />
          </button>
        )}
      </div>

      {/* Autocomplete Dropdown Results */}
      {isOpen && query.trim().length >= 2 && (
        <div className="absolute top-full mt-1.5 right-0 left-0 bg-slate-900 border border-slate-700/90 rounded-xl shadow-2xl overflow-hidden z-50 divide-y divide-slate-800 animate-in fade-in slide-in-from-top-1 max-h-80 overflow-y-auto">
          {searchResults.length === 0 ? (
            <div className="p-4 text-center text-xs text-slate-400 italic">
              {isAr ? 'لا توجد نتائج مطابقة' : 'No matching features found'}
            </div>
          ) : (
            searchResults.map((feature) => {
              const { properties } = feature;
              return (
                <div
                  key={feature.id}
                  onClick={() => handleSelect(feature)}
                  className="p-2.5 hover:bg-slate-800/80 cursor-pointer flex items-center justify-between gap-3 transition"
                >
                  <div className="flex items-center gap-2.5 flex-1 min-w-0">
                    <div className="p-1.5 rounded-lg bg-sky-950/60 border border-sky-800/50 text-sky-400 flex-shrink-0">
                      <MapPin className="w-3.5 h-3.5" />
                    </div>
                    <div className="truncate">
                      <div className="text-xs font-bold text-white truncate">
                        {properties.name}
                      </div>
                      <div className="flex items-center gap-2 text-[10px] text-slate-400 mt-0.5 font-mono">
                        <span className="text-sky-300 font-semibold">{properties.featureTypeCode}</span>
                        <span>•</span>
                        <span
                          className={`${
                            properties.status === 'ACTIVE'
                              ? 'text-emerald-400'
                              : properties.status === 'DAMAGED'
                              ? 'text-red-400'
                              : 'text-amber-400'
                          }`}
                        >
                          {properties.status}
                        </span>
                      </div>
                    </div>
                  </div>

                  <ChevronRight className="w-4 h-4 text-slate-500 flex-shrink-0 rtl:rotate-180" />
                </div>
              );
            })
          )}
        </div>
      )}
    </div>
  );
};

