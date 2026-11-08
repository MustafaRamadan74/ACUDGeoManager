'use client';

import React, { useEffect, useRef, useState, useCallback } from 'react';
import dynamic from 'next/dynamic';
import { useRouter } from 'next/navigation';
import { useAuth } from '@/lib/authContext';
import { api } from '@/lib/api';
import { FeatureTypeDto, GeoJsonFeature, NetworkDto } from '@/lib/types';
import { Navbar } from '@/components/Navbar';
import { Sidebar } from '@/components/Sidebar';
import { FeatureDetails } from '@/components/FeatureDetails';
import { FeatureEditModal } from '@/components/FeatureEditModal';
import { DeleteConfirmModal } from '@/components/DeleteConfirmModal';
import { FeatureHistoryModal } from '@/components/FeatureHistoryModal';
import { Loader2 } from 'lucide-react';

// Dynamic import with ssr: false for Leaflet map component
const MapComponent = dynamic(
  () => import('@/components/Map/MapComponent').then((mod) => mod.MapComponent),
  {
    ssr: false,
    loading: () => (
      <div className="w-full h-full flex items-center justify-center bg-slate-950 text-slate-400">
        <div className="flex items-center gap-3">
          <Loader2 className="w-6 h-6 animate-spin text-sky-400" />
          <span>جارٍ تهيئة خريطة العاصمة الإدارية...</span>
        </div>
      </div>
    ),
  }
);

export default function GisPage() {
  const router = useRouter();
  const { isAuthenticated, isLoading: authLoading, canEdit, lang } = useAuth();
  const isAr = lang === 'ar';

  const [networks, setNetworks] = useState<NetworkDto[]>([]);
  const [featureTypes, setFeatureTypes] = useState<FeatureTypeDto[]>([]);
  const [activeNetworkId, setActiveNetworkId] = useState<string | null>(null);
  const [visibleTypes, setVisibleTypes] = useState<Set<string>>(new Set());

  const [features, setFeatures] = useState<GeoJsonFeature[]>([]);
  const [selectedFeature, setSelectedFeature] = useState<GeoJsonFeature | null>(null);
  const [isLoadingFeatures, setIsLoadingFeatures] = useState(false);
  const [isLimitReached, setIsLimitReached] = useState(false);

  // Modals state
  const [createGeometry, setCreateGeometry] = useState<{
    kind: 'POINT' | 'LINE' | 'POLYGON';
    coordinates: any;
  } | null>(null);
  const [editingFeature, setEditingFeature] = useState<GeoJsonFeature | null>(null);
  const [deletingFeature, setDeletingFeature] = useState<GeoJsonFeature | null>(null);
  const [historyFeature, setHistoryFeature] = useState<GeoJsonFeature | null>(null);

  const abortControllerRef = useRef<AbortController | null>(null);
  const currentBboxRef = useRef<[number, number, number, number] | null>(null);

  // Authentication check
  useEffect(() => {
    if (!authLoading && !isAuthenticated) {
      router.push('/login');
    }
  }, [authLoading, isAuthenticated, router]);

  // Load Catalog: Networks and Feature Types
  useEffect(() => {
    if (!isAuthenticated) return;

    Promise.all([api.getNetworks(), api.getFeatureTypes()])
      .then(([netList, typeList]) => {
        setNetworks(netList);
        setFeatureTypes(typeList);
        // By default make all active types visible
        setVisibleTypes(new Set(typeList.map((t) => t.code)));
      })
      .catch((err) => {
        console.error('Failed to load GIS catalog:', err);
      });
  }, [isAuthenticated]);

  // Fetch Features for BBox
  const fetchFeatures = useCallback(
    (bbox: [number, number, number, number]) => {
      if (abortControllerRef.current) {
        abortControllerRef.current.abort();
      }

      const abortController = new AbortController();
      abortControllerRef.current = abortController;
      setIsLoadingFeatures(true);

      const netIds = activeNetworkId ? [activeNetworkId] : undefined;

      api
        .getMapFeatures(bbox, netIds, undefined, 1000, abortController.signal)
        .then((collection) => {
          setFeatures(collection.features || []);
          setIsLimitReached((collection.features || []).length >= 1000);

          // Update selected feature reference if it's currently selected
          if (selectedFeature) {
            const found = (collection.features || []).find((f) => f.id === selectedFeature.id);
            if (found) {
              setSelectedFeature(found);
            }
          }
        })
        .catch((err) => {
          if (err.name !== 'AbortError') {
            console.error('Failed to fetch map features:', err);
          }
        })
        .finally(() => {
          setIsLoadingFeatures(false);
        });
    },
    [activeNetworkId, selectedFeature]
  );

  const handleBboxChange = (bbox: [number, number, number, number]) => {
    currentBboxRef.current = bbox;
    fetchFeatures(bbox);
  };

  // Refetch when active network changes
  useEffect(() => {
    if (currentBboxRef.current) {
      fetchFeatures(currentBboxRef.current);
    }
  }, [activeNetworkId, fetchFeatures]);

  const handleToggleType = (typeCode: string) => {
    setVisibleTypes((prev) => {
      const next = new Set(prev);
      if (next.has(typeCode)) {
        next.delete(typeCode);
      } else {
        next.add(typeCode);
      }
      return next;
    });
  };

  const handleToggleAll = (selectAll: boolean) => {
    if (selectAll) {
      setVisibleTypes(new Set(featureTypes.map((f) => f.code)));
    } else {
      setVisibleTypes(new Set());
    }
  };

  // Callback when a new feature is drawn using Geoman
  const handleDrawingComplete = (geometry: {
    kind: 'POINT' | 'LINE' | 'POLYGON';
    coordinates: any;
  }) => {
    if (!canEdit) return;
    setCreateGeometry(geometry);
  };

  // Callback after feature is created or edited
  const handleFeatureSaved = () => {
    if (currentBboxRef.current) {
      fetchFeatures(currentBboxRef.current);
    }
  };

  // Callback after feature is deleted
  const handleFeatureDeleted = (deletedId: string) => {
    if (selectedFeature?.id === deletedId) {
      setSelectedFeature(null);
    }
    if (currentBboxRef.current) {
      fetchFeatures(currentBboxRef.current);
    }
  };

  if (authLoading || !isAuthenticated) {
    return (
      <div className="h-screen w-screen flex items-center justify-center bg-slate-950 text-slate-300">
        <Loader2 className="w-8 h-8 animate-spin text-sky-400" />
      </div>
    );
  }

  return (
    <div className="flex flex-col h-screen w-screen overflow-hidden bg-slate-950 select-none">
      {/* Top Navbar */}
      <Navbar
        networks={networks}
        activeNetworkId={activeNetworkId}
        onSelectNetwork={setActiveNetworkId}
      />

      {/* Main Workspace: Sidebar + Map + FeatureDetails */}
      <div className="flex-1 flex overflow-hidden relative">
        {/* Layer Tree Sidebar */}
        <Sidebar
          networks={networks}
          featureTypes={featureTypes}
          visibleTypes={visibleTypes}
          onToggleType={handleToggleType}
          onToggleAll={handleToggleAll}
          featuresCount={features.length}
          isLimitReached={isLimitReached}
          isLoading={isLoadingFeatures}
        />

        {/* Map Center */}
        <main className="flex-1 relative h-full">
          {isLoadingFeatures && (
            <div className="absolute top-4 right-4 z-10 bg-slate-900/90 backdrop-blur border border-slate-700 text-sky-400 text-xs px-3 py-1.5 rounded-lg shadow-lg flex items-center gap-2">
              <Loader2 className="w-3.5 h-3.5 animate-spin" />
              <span>{isAr ? 'جارٍ تحديث المعالم...' : 'Updating features...'}</span>
            </div>
          )}

          <MapComponent
            features={features}
            featureTypes={featureTypes}
            visibleTypes={visibleTypes}
            selectedFeatureId={selectedFeature?.id || null}
            canEdit={canEdit}
            onSelectFeature={setSelectedFeature}
            onBboxChange={handleBboxChange}
            onDrawingComplete={handleDrawingComplete}
          />
        </main>

        {/* Feature Details Right Panel */}
        {selectedFeature && (
          <FeatureDetails
            feature={selectedFeature}
            onClose={() => setSelectedFeature(null)}
            onEdit={(f) => setEditingFeature(f)}
            onDelete={(f) => setDeletingFeature(f)}
            onShowHistory={(f) => setHistoryFeature(f)}
          />
        )}
      </div>

      {/* Feature Create Modal (opened after Leaflet-Geoman drawing) */}
      {createGeometry && (
        <FeatureEditModal
          mode="create"
          initialGeometry={createGeometry}
          networks={networks}
          featureTypes={featureTypes}
          onClose={() => setCreateGeometry(null)}
          onSaved={handleFeatureSaved}
        />
      )}

      {/* Feature Edit Modal */}
      {editingFeature && (
        <FeatureEditModal
          mode="edit"
          feature={editingFeature}
          networks={networks}
          featureTypes={featureTypes}
          onClose={() => setEditingFeature(null)}
          onSaved={handleFeatureSaved}
        />
      )}

      {/* Delete Confirmation Modal */}
      {deletingFeature && (
        <DeleteConfirmModal
          feature={deletingFeature}
          onClose={() => setDeletingFeature(null)}
          onDeleted={handleFeatureDeleted}
        />
      )}

      {/* Feature Audit History Modal */}
      {historyFeature && (
        <FeatureHistoryModal
          feature={historyFeature}
          onClose={() => setHistoryFeature(null)}
        />
      )}
    </div>
  );
}
