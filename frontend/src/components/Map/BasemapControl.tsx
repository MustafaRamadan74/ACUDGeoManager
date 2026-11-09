'use client';

import React, { useState, useEffect } from 'react';
import { Layers, Check, X } from 'lucide-react';

export type BasemapKey = 'osm' | 'carto_dark' | 'carto_light' | 'satellite';

export interface BasemapDef {
  key: BasemapKey;
  nameAr: string;
  nameEn: string;
  url: string;
  attribution: string;
  maxZoom: number;
}

export const BASEMAPS: Record<BasemapKey, BasemapDef> = {
  osm: {
    key: 'osm',
    nameAr: 'خريطة الشوارع (OpenStreetMap)',
    nameEn: 'Streets (OpenStreetMap)',
    url: 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright" target="_blank">OpenStreetMap</a> contributors',
    maxZoom: 19,
  },
  carto_dark: {
    key: 'carto_dark',
    nameAr: 'الوضع الداكن (Carto Dark Matter)',
    nameEn: 'Dark Matter (CartoDB)',
    url: 'https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png',
    attribution: '&copy; <a href="https://carto.com/" target="_blank">CARTO</a>, &copy; OpenStreetMap contributors',
    maxZoom: 20,
  },
  carto_light: {
    key: 'carto_light',
    nameAr: 'الوضع الفاتح (Carto Positron)',
    nameEn: 'Positron Light (CartoDB)',
    url: 'https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png',
    attribution: '&copy; <a href="https://carto.com/" target="_blank">CARTO</a>, &copy; OpenStreetMap contributors',
    maxZoom: 20,
  },
  satellite: {
    key: 'satellite',
    nameAr: 'صور الأقمار الصناعية (Esri Satellite)',
    nameEn: 'Satellite (Esri World Imagery)',
    url: 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',
    attribution: 'Tiles &copy; Esri, Maxar, Earthstar Geographics, USDA, USGS, AeroGRID, IGN, and the GIS User Community',
    maxZoom: 19,
  },
};

interface BasemapControlProps {
  currentBasemap: BasemapKey;
  onSelectBasemap: (key: BasemapKey) => void;
  isAr?: boolean;
}

export const BasemapControl: React.FC<BasemapControlProps> = ({
  currentBasemap,
  onSelectBasemap,
  isAr = true,
}) => {
  const [isOpen, setIsOpen] = useState(false);

  return (
    <div className="relative">
      <button
        type="button"
        onClick={() => setIsOpen(!isOpen)}
        className={`p-2 rounded-xl backdrop-blur-md shadow-lg border transition flex items-center gap-1.5 ${
          isOpen
            ? 'bg-sky-600 text-white border-sky-400'
            : 'bg-slate-900/85 hover:bg-slate-800 text-slate-200 border-slate-700/80 hover:border-slate-600'
        }`}
        title={isAr ? 'تغيير خريطة الأساس' : 'Change Basemap Layer'}
      >
        <Layers className="w-4 h-4 text-sky-400" />
        <span className="text-xs font-semibold hidden sm:inline">{isAr ? 'الخلفية' : 'Basemap'}</span>
      </button>

      {isOpen && (
        <div
          className={`absolute top-11 ${
            isAr ? 'left-0' : 'right-0'
          } z-30 w-64 bg-slate-900/95 backdrop-blur-md border border-slate-700/90 rounded-2xl shadow-2xl p-3 text-slate-200 animate-in fade-in zoom-in-95 duration-150`}
          dir={isAr ? 'rtl' : 'ltr'}
        >
          <div className="flex items-center justify-between pb-2 mb-2 border-b border-slate-800">
            <div className="flex items-center gap-2">
              <Layers className="w-4 h-4 text-sky-400" />
              <span className="text-xs font-bold text-white">
                {isAr ? 'اختيار خريطة الأساس' : 'Select Basemap'}
              </span>
            </div>
            <button
              onClick={() => setIsOpen(false)}
              className="p-1 rounded-md text-slate-400 hover:text-white hover:bg-slate-800 transition"
            >
              <X className="w-3.5 h-3.5" />
            </button>
          </div>

          <div className="space-y-1.5">
            {Object.values(BASEMAPS).map((b) => (
              <button
                key={b.key}
                type="button"
                onClick={() => {
                  onSelectBasemap(b.key);
                  setIsOpen(false);
                }}
                className={`w-full flex items-center justify-between p-2 rounded-xl text-xs font-medium transition border ${
                  currentBasemap === b.key
                    ? 'border-sky-500 bg-sky-950/40 text-white shadow-sm'
                    : 'border-transparent bg-slate-800/40 hover:bg-slate-800 text-slate-300'
                }`}
              >
                <span>{isAr ? b.nameAr : b.nameEn}</span>
                {currentBasemap === b.key && <Check className="w-3.5 h-3.5 text-sky-400" />}
              </button>
            ))}
          </div>
        </div>
      )}
    </div>
  );
};

