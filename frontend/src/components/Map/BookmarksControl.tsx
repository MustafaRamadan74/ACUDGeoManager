'use client';

import React, { useState, useEffect } from 'react';
import { Bookmark, Plus, Trash2, MapPin, X, Navigation } from 'lucide-react';
import { api } from '@/lib/api';
import { BookmarkDto } from '@/lib/types';

interface BookmarksControlProps {
  onFlyTo: (lat: number, lon: number, zoom: number) => void;
  getCurrentView: () => { center: [number, number]; zoom: number; bbox?: string };
  isAr?: boolean;
}

export const BookmarksControl: React.FC<BookmarksControlProps> = ({
  onFlyTo,
  getCurrentView,
  isAr = true,
}) => {
  const [isOpen, setIsOpen] = useState(false);
  const [bookmarks, setBookmarks] = useState<BookmarkDto[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [isAdding, setIsAdding] = useState(false);
  const [newName, setNewName] = useState('');
  const [error, setError] = useState<string | null>(null);

  const fetchBookmarks = async () => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.getBookmarks();
      setBookmarks(data);
    } catch (err: any) {
      setError(err.message || 'Failed to load bookmarks');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    if (isOpen) {
      fetchBookmarks();
    }
  }, [isOpen]);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newName.trim()) return;

    try {
      const current = getCurrentView();
      const created = await api.createBookmark({
        name: newName.trim(),
        centerLon: current.center[0],
        centerLat: current.center[1],
        zoom: current.zoom,
        bbox: current.bbox,
      });

      setBookmarks((prev) => [created, ...prev]);
      setNewName('');
      setIsAdding(false);
    } catch (err: any) {
      setError(err.message || 'Failed to save bookmark');
    }
  };

  const handleDelete = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try {
      await api.deleteBookmark(id);
      setBookmarks((prev) => prev.filter((b) => b.id !== id));
    } catch (err: any) {
      setError(err.message || 'Failed to delete bookmark');
    }
  };

  return (
    <div className="relative">
      {/* Floating Toggle Button */}
      <button
        type="button"
        onClick={() => setIsOpen(!isOpen)}
        className={`p-2 rounded-xl backdrop-blur-md shadow-lg border transition flex items-center gap-1.5 ${
          isOpen
            ? 'bg-sky-600 text-white border-sky-400'
            : 'bg-slate-900/85 hover:bg-slate-800 text-slate-200 border-slate-700/80 hover:border-slate-600'
        }`}
        title={isAr ? 'الإشارات المرجعية المحفوظة' : 'Saved Bookmarks'}
      >
        <Bookmark className="w-4 h-4 text-amber-400" />
        <span className="text-xs font-semibold hidden sm:inline">{isAr ? 'علاماتي' : 'Bookmarks'}</span>
      </button>

      {/* Dropdown Panel */}
      {isOpen && (
        <div
          className={`absolute top-11 ${
            isAr ? 'left-0' : 'right-0'
          } z-30 w-72 bg-slate-900/95 backdrop-blur-md border border-slate-700/90 rounded-2xl shadow-2xl p-3 text-slate-200 animate-in fade-in zoom-in-95 duration-150`}
          dir={isAr ? 'rtl' : 'ltr'}
        >
          <div className="flex items-center justify-between pb-2 mb-2 border-b border-slate-800">
            <div className="flex items-center gap-2">
              <Bookmark className="w-4 h-4 text-amber-400" />
              <span className="text-xs font-bold text-white">
                {isAr ? 'المواقع والإشارات المرجعية' : 'Saved Bookmarks'}
              </span>
            </div>
            <button
              onClick={() => setIsOpen(false)}
              className="p-1 rounded-md text-slate-400 hover:text-white hover:bg-slate-800 transition"
            >
              <X className="w-3.5 h-3.5" />
            </button>
          </div>

          {error && (
            <div className="mb-2 p-1.5 rounded bg-rose-950/50 border border-rose-800 text-[11px] text-rose-300">
              {error}
            </div>
          )}

          {/* Add Current View Button / Form */}
          {!isAdding ? (
            <button
              type="button"
              onClick={() => setIsAdding(true)}
              className="w-full flex items-center justify-center gap-1.5 py-1.5 px-3 rounded-xl bg-amber-500/10 hover:bg-amber-500/20 text-amber-300 border border-amber-500/30 text-xs font-semibold transition mb-2"
            >
              <Plus className="w-3.5 h-3.5" />
              <span>{isAr ? 'حفظ مشهد الخريطة الحالي' : 'Save Current View'}</span>
            </button>
          ) : (
            <form onSubmit={handleCreate} className="mb-2 space-y-2 bg-slate-800/60 p-2 rounded-xl border border-slate-700">
              <input
                type="text"
                value={newName}
                onChange={(e) => setNewName(e.target.value)}
                placeholder={isAr ? 'اسم العلامة المرجعية...' : 'Bookmark name...'}
                className="w-full bg-slate-900 border border-slate-700 rounded-lg px-2.5 py-1 text-xs text-white focus:outline-none focus:border-amber-400"
                autoFocus
              />
              <div className="flex items-center justify-end gap-1.5">
                <button
                  type="button"
                  onClick={() => setIsAdding(false)}
                  className="px-2 py-1 rounded text-[11px] text-slate-400 hover:text-white"
                >
                  {isAr ? 'إلغاء' : 'Cancel'}
                </button>
                <button
                  type="submit"
                  disabled={!newName.trim()}
                  className="px-3 py-1 rounded-lg bg-amber-600 hover:bg-amber-500 disabled:opacity-50 text-white text-xs font-bold transition"
                >
                  {isAr ? 'حفظ' : 'Save'}
                </button>
              </div>
            </form>
          )}

          {/* Bookmarks List */}
          <div className="max-h-56 overflow-y-auto space-y-1">
            {isLoading ? (
              <div className="text-center py-4 text-xs text-slate-400">
                {isAr ? 'جاري التحميل...' : 'Loading...'}
              </div>
            ) : bookmarks.length === 0 ? (
              <div className="text-center py-4 text-xs text-slate-500">
                {isAr ? 'لا توجد إشارات محفوظة بعد' : 'No saved bookmarks yet'}
              </div>
            ) : (
              bookmarks.map((b) => (
                <div
                  key={b.id}
                  onClick={() => {
                    onFlyTo(b.centerLat, b.centerLon, b.zoom);
                    setIsOpen(false);
                  }}
                  className="flex items-center justify-between p-2 rounded-xl hover:bg-slate-800/80 cursor-pointer transition border border-transparent hover:border-slate-700 group"
                >
                  <div className="flex items-center gap-2 overflow-hidden">
                    <MapPin className="w-3.5 h-3.5 text-sky-400 shrink-0" />
                    <div className="truncate">
                      <div className="text-xs font-medium text-slate-200 group-hover:text-white truncate">
                        {b.name}
                      </div>
                      <div className="text-[10px] text-slate-400">
                        Zoom: {b.zoom} | {new Date(b.createdAt).toLocaleDateString()}
                      </div>
                    </div>
                  </div>

                  <button
                    type="button"
                    onClick={(e) => handleDelete(b.id, e)}
                    className="p-1 rounded text-slate-500 hover:text-rose-400 hover:bg-rose-950/40 opacity-0 group-hover:opacity-100 transition"
                    title={isAr ? 'حذف العلامة' : 'Delete'}
                  >
                    <Trash2 className="w-3.5 h-3.5" />
                  </button>
                </div>
              ))
            )}
          </div>
        </div>
      )}
    </div>
  );
};

