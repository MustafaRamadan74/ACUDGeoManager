'use client';

import React from 'react';
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
} from 'lucide-react';

interface NavbarProps {
  networks: NetworkDto[];
  activeNetworkId: string | null;
  onSelectNetwork: (networkId: string | null) => void;
  onToggleTable: () => void;
  isTableOpen: boolean;
  onOpenSummary: () => void;
  onOpenAdmin: () => void;
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
  searchSlot,
}) => {
  const { user, logout, lang, setLang } = useAuth();
  const isAr = lang === 'ar';
  const isAdmin = user?.role === 'ADMIN';

  const getRoleBadge = (role?: string) => {
    switch (role) {
      case 'ADMIN':
        return <span className="px-2 py-0.5 rounded-full text-xs font-semibold bg-amber-500/20 text-amber-300 border border-amber-500/40">👑 Admin</span>;
      case 'EDITOR':
        return <span className="px-2 py-0.5 rounded-full text-xs font-semibold bg-emerald-500/20 text-emerald-300 border border-emerald-500/40">✏️ Editor</span>;
      default:
        return <span className="px-2 py-0.5 rounded-full text-xs font-semibold bg-purple-500/20 text-purple-300 border border-purple-500/40">👁️ Viewer</span>;
    }
  };

  return (
    <header className="h-14 bg-slate-900 border-b border-slate-800 px-4 flex items-center justify-between z-20 shadow-md gap-3">
      {/* Brand & Workspace switcher */}
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

      {/* Workspace Tools: Table, Summary, Admin */}
      <div className="flex items-center gap-2">
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
