'use client';

import React, { useState, useRef, useEffect } from 'react';
import { useAuth } from '@/lib/authContext';
import { NetworkDto } from '@/lib/types';
import {
  Shield,
  LogOut,
  Globe,
  Layers,
  UserCheck,
  Table,
  BarChart3,
  Users,
  Compass,
  Filter,
  Navigation,
  ShieldAlert,
  ChevronDown,
  Pentagon,
  Undo2,
  Redo2,
  History,
  ArrowUpDown,
} from 'lucide-react';

interface NavbarProps {
  networks: NetworkDto[];
  activeNetworkId: string | null;
  onSelectNetwork: (networkId: string | null) => void;
  onToggleTable: () => void;
  isTableOpen: boolean;
  onOpenSummary: () => void;
  onOpenAdmin: () => void;
  onSelectByPolygon: () => void;
  isSelectingPolygon: boolean;
  onOpenSelectByAttribute: () => void;
  onOpenNearestModal: () => void;
  onOpenValidation?: () => void;
  onOpenImportExport?: () => void;
  canUndo?: boolean;
  canRedo?: boolean;
  onUndo?: () => void;
  onRedo?: () => void;
  onOpenHistory?: () => void;
  spatialResultsCount?: number;
  onToggleResultsDrawer?: () => void;
  isResultsDrawerOpen?: boolean;
  searchSlot?: React.ReactNode;
}

export const Navbar: React.FC<NavbarProps> = ({
  networks,
  activeNetworkId,
  onSelectNetwork,
  onToggleTable,
  isTableOpen,
  onOpenSummary,
  onOpenAdmin,
  onSelectByPolygon,
  isSelectingPolygon,
  onOpenSelectByAttribute,
  onOpenNearestModal,
  onOpenValidation,
  onOpenImportExport,
  canUndo = false,
  canRedo = false,
  onUndo,
  onRedo,
  onOpenHistory,
  spatialResultsCount = 0,
  onToggleResultsDrawer,
  isResultsDrawerOpen = false,
  searchSlot,
}) => {
  const { user, logout, lang, setLang } = useAuth();
  const isAr = lang === 'ar';
  const isAdmin = user?.role === 'ADMIN';

  const [isSpatialMenuOpen, setIsSpatialMenuOpen] = useState(false);
  const spatialMenuRef = useRef<HTMLDivElement>(null);

  // Close dropdown on outside click
  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (spatialMenuRef.current && !spatialMenuRef.current.contains(event.target as Node)) {
        setIsSpatialMenuOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const getRoleBadge = (role?: string) => {
    switch (role) {
      case 'ADMIN':
        return (
          <span className="px-2 py-0.5 rounded-full text-xs font-semibold bg-amber-500/20 text-amber-300 border border-amber-500/40">
            👑 Admin
          </span>
        );
      case 'EDITOR':
        return (
          <span className="px-2 py-0.5 rounded-full text-xs font-semibold bg-emerald-500/20 text-emerald-300 border border-emerald-500/40">
            ✏️ Editor
          </span>
        );
      default:
        return (
          <span className="px-2 py-0.5 rounded-full text-xs font-semibold bg-purple-500/20 text-purple-300 border border-purple-500/40">
            👁️ Viewer
          </span>
        );
    }
  };

  return (
    <header className="h-14 bg-slate-900 border-b border-slate-800 px-4 flex items-center justify-between z-20 shadow-md gap-3">
      {/* Brand & Network switcher */}
      <div className="flex items-center gap-3">
        <div className="flex items-center gap-2.5">
          <div className="w-8 h-8 rounded-lg bg-gradient-to-tr from-sky-500 to-amber-500 flex items-center justify-center text-white shadow-md shadow-sky-500/20">
            <Shield className="w-5 h-5" />
          </div>
          <div>
            <div className="font-bold text-sm tracking-tight text-white flex items-center gap-2">
              ACUDGeoManager
              <span className="hidden sm:inline-block text-[11px] font-medium text-slate-400 border border-slate-700 rounded px-1.5 py-0.2">
                NAC GIS
              </span>
            </div>
          </div>
        </div>

        {/* Network Switcher Dropdown */}
        <div className="h-6 w-px bg-slate-800 mx-1 hidden sm:block" />
        <div className="flex items-center gap-2">
          <Layers className="w-4 h-4 text-sky-400 hidden sm:block" />
          <select
            value={activeNetworkId || ''}
            onChange={(e) => onSelectNetwork(e.target.value ? e.target.value : null)}
            className="bg-slate-800 text-slate-200 text-xs font-medium rounded-lg px-2.5 py-1.5 border border-slate-700 focus:outline-none focus:ring-1 focus:ring-sky-500 cursor-pointer"
          >
            <option value="">{isAr ? '🌐 جميع شبكات العاصمة' : '🌐 All Networks'}</option>
            {networks.map((net) => (
              <option key={net.id} value={net.id}>
                {net.name} ({net.code})
              </option>
            ))}
          </select>
        </div>
      </div>

      {/* Center Search Slot */}
      <div className="hidden md:flex items-center justify-center flex-1 max-w-sm">
        {searchSlot}
      </div>

      {/* Spatial Tools, Table, Summary, Admin */}
      <div className="flex items-center gap-2">
        {/* Spatial Query Tools Dropdown */}
        <div className="relative" ref={spatialMenuRef}>
          <button
            onClick={() => setIsSpatialMenuOpen(!isSpatialMenuOpen)}
            className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-semibold transition border ${
              isSelectingPolygon || isSpatialMenuOpen
                ? 'bg-sky-600 text-white border-sky-500 shadow-md shadow-sky-950/50'
                : 'bg-slate-800 hover:bg-slate-700 text-slate-300 border-slate-700'
            }`}
            title={isAr ? 'أدوات الاستعلام المكاني' : 'Spatial Query Tools'}
          >
            <Compass className="w-3.5 h-3.5 text-sky-400" />
            <span className="hidden lg:inline">{isAr ? 'الاستعلام المكاني' : 'Spatial Query'}</span>
            <ChevronDown className="w-3 h-3 text-slate-400" />
          </button>

          {isSpatialMenuOpen && (
            <div className="absolute right-0 rtl:right-auto rtl:left-0 mt-2 w-56 bg-slate-900 border border-slate-700 rounded-xl shadow-2xl py-1.5 z-50 text-xs">
              <button
                type="button"
                onClick={() => {
                  setIsSpatialMenuOpen(false);
                  onSelectByPolygon();
                }}
                className="w-full px-3 py-2 text-left rtl:text-right hover:bg-slate-800 text-slate-200 flex items-center gap-2.5 transition"
              >
                <Pentagon className="w-4 h-4 text-sky-400" />
                <div>
                  <div className="font-semibold">{isAr ? 'تحديد بمضلع (Within Polygon)' : 'Select by Polygon'}</div>
                  <div className="text-[10px] text-slate-400">
                    {isAr ? 'رسم مضلع واستخراج المعالم بداخله' : 'Draw polygon to query features inside'}
                  </div>
                </div>
              </button>

              <button
                type="button"
                onClick={() => {
                  setIsSpatialMenuOpen(false);
                  onOpenSelectByAttribute();
                }}
                className="w-full px-3 py-2 text-left rtl:text-right hover:bg-slate-800 text-slate-200 flex items-center gap-2.5 transition"
              >
                <Filter className="w-4 h-4 text-emerald-400" />
                <div>
                  <div className="font-semibold">{isAr ? 'تحديد بالخصائص (By Attribute)' : 'Select by Attribute'}</div>
                  <div className="text-[10px] text-slate-400">
                    {isAr ? 'تصفية طبقة بشروط منطقية' : 'Filter layer by SQL condition'}
                  </div>
                </div>
              </button>

              <button
                type="button"
                onClick={() => {
                  setIsSpatialMenuOpen(false);
                  onOpenNearestModal();
                }}
                className="w-full px-3 py-2 text-left rtl:text-right hover:bg-slate-800 text-slate-200 flex items-center gap-2.5 transition"
              >
                <Navigation className="w-4 h-4 text-amber-400" />
                <div>
                  <div className="font-semibold">{isAr ? 'البحث عن الأقرب (KNN Nearest)' : 'Find Nearest'}</div>
                  <div className="text-[10px] text-slate-400">
                    {isAr ? 'أقرب أصول لموقع أو مركز الخريطة' : 'Find nearest assets with distance in meters'}
                  </div>
                </div>
              </button>
            </div>
          )}
        </div>

        {/* Spatial Results Pill Button (if results exist) */}
        {spatialResultsCount > 0 && onToggleResultsDrawer && (
          <button
            onClick={onToggleResultsDrawer}
            className={`flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg text-xs font-semibold transition border ${
              isResultsDrawerOpen
                ? 'bg-amber-600 text-white border-amber-500 shadow-md shadow-amber-950/50'
                : 'bg-amber-950/60 hover:bg-amber-900/60 text-amber-300 border-amber-800/80 animate-pulse'
            }`}
            title={isAr ? 'عرض نتائج الاستعلام المكاني' : 'Toggle Spatial Results'}
          >
            <span>🎯</span>
            <span className="font-mono font-bold">{spatialResultsCount}</span>
            <span className="hidden xl:inline">{isAr ? 'نتائج' : 'selected'}</span>
          </button>
        )}

        {/* Undo / Redo & History Stack Buttons */}
        <div className="flex items-center bg-slate-800/80 rounded-lg p-0.5 border border-slate-700/80">
          <button
            onClick={onUndo}
            disabled={!canUndo}
            className={`p-1.5 rounded-md transition ${
              canUndo
                ? 'text-amber-400 hover:text-amber-300 hover:bg-slate-700'
                : 'text-slate-600 cursor-not-allowed'
            }`}
            title={isAr ? 'تراجع (Ctrl+Z)' : 'Undo (Ctrl+Z)'}
          >
            <Undo2 className="w-3.5 h-3.5" />
          </button>

          <button
            onClick={onRedo}
            disabled={!canRedo}
            className={`p-1.5 rounded-md transition ${
              canRedo
                ? 'text-sky-400 hover:text-sky-300 hover:bg-slate-700'
                : 'text-slate-600 cursor-not-allowed'
            }`}
            title={isAr ? 'إعادة (Ctrl+Y)' : 'Redo (Ctrl+Y)'}
          >
            <Redo2 className="w-3.5 h-3.5" />
          </button>

          <div className="h-4 w-px bg-slate-700 mx-0.5" />

          <button
            onClick={onOpenHistory}
            className="p-1.5 rounded-md text-slate-400 hover:text-slate-200 hover:bg-slate-700 transition"
            title={isAr ? 'سجل التعديلات ومكدس العمليات' : 'History & Change Stack'}
          >
            <History className="w-3.5 h-3.5" />
          </button>
        </div>

        {/* Toggle Attribute Table */}
        <button
          onClick={onToggleTable}
          className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-semibold transition border ${
            isTableOpen
              ? 'bg-sky-600 text-white border-sky-500 shadow-md shadow-sky-950/50'
              : 'bg-slate-800 hover:bg-slate-700 text-slate-300 border-slate-700'
          }`}
          title={isAr ? 'فتح/إغلاق جدول البيانات' : 'Toggle Attribute Table'}
        >
          <Table className="w-3.5 h-3.5" />
          <span className="hidden lg:inline">{isAr ? 'جدول البيانات' : 'Table'}</span>
        </button>

        {/* Network Summary */}
        <button
          onClick={onOpenSummary}
          className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 text-xs font-semibold border border-slate-700 transition"
          title={isAr ? 'ملخص الشبكة والإحصائيات' : 'Network Summary'}
        >
          <BarChart3 className="w-3.5 h-3.5 text-amber-400" />
          <span className="hidden lg:inline">{isAr ? 'ملخص الشبكة' : 'Summary'}</span>
        </button>

        {/* Validation Engine Dashboard */}
        {onOpenValidation && (
          <button
            onClick={onOpenValidation}
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-rose-950/40 hover:bg-rose-900/60 text-rose-300 text-xs font-semibold border border-rose-800/60 transition shadow-sm"
            title={isAr ? 'لوحة تدقيق ومطابقة قواعد البيانات الجغرافية' : 'Validation Dashboard'}
          >
            <ShieldAlert className="w-3.5 h-3.5 text-rose-400" />
            <span className="hidden lg:inline">{isAr ? 'التدقيق' : 'Validation'}</span>
          </button>
        )}

        {/* Import & Export */}
        {onOpenImportExport && (
          <button
            onClick={onOpenImportExport}
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-emerald-950/40 hover:bg-emerald-900/60 text-emerald-300 text-xs font-semibold border border-emerald-800/60 transition shadow-sm"
            title={isAr ? 'استيراد وتصدير البيانات المكانية (EPSG: 4326/22992/32636)' : 'Spatial Data Import & Export (EPSG: 4326/22992/32636)'}
          >
            <ArrowUpDown className="w-3.5 h-3.5 text-emerald-400" />
            <span className="hidden lg:inline">{isAr ? 'استيراد/تصدير' : 'Import/Export'}</span>
          </button>
        )}

        {/* User Admin (Admin Only) */}
        {isAdmin && (
          <button
            onClick={onOpenAdmin}
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-amber-950/40 hover:bg-amber-950/60 text-amber-300 text-xs font-semibold border border-amber-800/60 transition shadow-sm"
            title={isAr ? 'إدارة المستخدمين' : 'User Administration'}
          >
            <Users className="w-3.5 h-3.5" />
            <span className="hidden lg:inline">{isAr ? 'المستخدمين' : 'Users'}</span>
          </button>
        )}

        <div className="h-6 w-px bg-slate-800 mx-1" />

        {/* Language switcher */}
        <button
          onClick={() => setLang(isAr ? 'en' : 'ar')}
          className="flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 text-xs font-medium border border-slate-700 transition"
          title={isAr ? 'Switch to English' : 'التحويل للعربية'}
        >
          <Globe className="w-3.5 h-3.5 text-sky-400" />
          <span>{isAr ? 'EN' : 'عربي'}</span>
        </button>

        {/* User Info */}
        {user && (
          <div className="flex items-center gap-2 pl-2 rtl:pl-0 rtl:pr-2 border-l rtl:border-l-0 rtl:border-r border-slate-800">
            <div className="w-7 h-7 rounded-full bg-slate-800 border border-slate-700 flex items-center justify-center text-slate-300">
              <UserCheck className="w-4 h-4" />
            </div>
            <div className="hidden xl:block text-right rtl:text-right ltr:text-left">
              <div className="text-xs font-semibold text-slate-200">{user.username}</div>
            </div>
            {getRoleBadge(user.role)}
          </div>
        )}

        {/* Logout button */}
        <button
          onClick={() => logout()}
          className="p-1.5 rounded-lg text-slate-400 hover:text-red-400 hover:bg-slate-800/80 transition"
          title={isAr ? 'تسجيل الخروج' : 'Sign Out'}
        >
          <LogOut className="w-4 h-4" />
        </button>
      </div>
    </header>
  );
};
