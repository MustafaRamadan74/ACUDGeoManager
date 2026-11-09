'use client';

import React, { useState } from 'react';
import { Compass, Copy, Check } from 'lucide-react';
import { wgs84ToRedBelt } from '@/lib/coords';

interface LiveCoordinatesBarProps {
  lat: number | null;
  lon: number | null;
  zoom: number;
  isAr?: boolean;
}

export const LiveCoordinatesBar: React.FC<LiveCoordinatesBarProps> = ({
  lat,
  lon,
  zoom,
  isAr = true,
}) => {
  const [copied, setCopied] = useState(false);

  if (lat === null || lon === null) return null;

  const redBelt = wgs84ToRedBelt(lat, lon);

  const wgsText = `${lat >= 0 ? lat.toFixed(5) + '° N' : Math.abs(lat).toFixed(5) + '° S'}, ${
    lon >= 0 ? lon.toFixed(5) + '° E' : Math.abs(lon).toFixed(5) + '° W'
  }`;

  const redBeltText = `E: ${redBelt.easting.toLocaleString('en-US', { minimumFractionDigits: 1, maximumFractionDigits: 1 })} m | N: ${redBelt.northing.toLocaleString('en-US', { minimumFractionDigits: 1, maximumFractionDigits: 1 })} m`;

  const copyToClipboard = () => {
    const fullText = `WGS84: ${lat.toFixed(6)}, ${lon.toFixed(6)} | RedBelt (22992): E ${redBelt.easting}, N ${redBelt.northing}`;
    navigator.clipboard.writeText(fullText);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div
      onClick={copyToClipboard}
      className="absolute bottom-2 right-2 z-20 bg-slate-900/90 hover:bg-slate-900 backdrop-blur-md border border-slate-700/80 hover:border-sky-500/60 rounded-xl px-3 py-1.5 shadow-xl text-slate-300 text-[11px] font-mono cursor-pointer transition select-none flex items-center gap-3"
      title={isAr ? 'اضغط لنسخ الإحداثيات' : 'Click to copy coordinates'}
    >
      <div className="flex items-center gap-1.5 text-sky-400 font-sans font-bold">
        <Compass className="w-3.5 h-3.5" />
        <span className="hidden sm:inline">NAC</span>
      </div>

      <div className="flex items-center gap-2 border-r border-l border-slate-700/80 px-2.5">
        <span className="text-slate-400 font-sans text-[10px]">WGS84:</span>
        <span className="text-white font-medium">{wgsText}</span>
      </div>

      <div className="flex items-center gap-2 pr-1">
        <span className="text-slate-400 font-sans text-[10px]">Red Belt (22992):</span>
        <span className="text-emerald-400 font-semibold">{redBeltText}</span>
      </div>

      <div className="hidden md:flex items-center gap-1 text-[10px] text-slate-400 font-sans border-r border-slate-700/80 pr-2">
        <span>Z:</span>
        <span className="text-amber-400 font-bold">{zoom}</span>
      </div>

      {copied ? (
        <Check className="w-3 h-3 text-emerald-400 shrink-0" />
      ) : (
        <Copy className="w-3 h-3 text-slate-500 hover:text-slate-300 shrink-0" />
      )}
    </div>
  );
};

