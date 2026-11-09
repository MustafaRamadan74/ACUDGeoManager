'use client';

import React, { useEffect, useRef, useState, useCallback, useMemo } from 'react';
import dynamic from 'next/dynamic';
import { useRouter } from 'next/navigation';
import { useAuth } from '@/lib/authContext';
import { api } from '@/lib/api';
import { FeatureTypeDto, GeoJsonFeature, NetworkDto, ValidationIssueDto, UndoRedoStackDto } from '@/lib/types';
import { Navbar } from '@/components/Navbar';
import { Sidebar } from '@/components/Sidebar';
import { FeatureDetails } from '@/components/FeatureDetails';
import { FeatureEditModal } from '@/components/FeatureEditModal';
import { DeleteConfirmModal } from '@/components/DeleteConfirmModal';
import { FeatureHistoryModal } from '@/components/FeatureHistoryModal';
import { AttributeTable } from '@/components/AttributeTable';
import { SearchBar } from '@/components/SearchBar';
import { SummaryModal } from '@/components/SummaryModal';
import { UserAdminModal } from '@/components/UserAdminModal';
import { SpatialSelectionModal } from '@/components/SpatialSelectionModal';
import { NearestQueryModal } from '@/components/NearestQueryModal';
import { SpatialResultsDrawer } from '@/components/SpatialResultsDrawer';
import { ValidationDashboardModal } from '@/components/ValidationDashboardModal';
import { HistoryPanelModal } from '@/components/HistoryPanelModal';
import { ImportExportModal } from '@/components/ImportExportModal';
import { AnalysisModal } from '@/components/AnalysisModal';
import { ApprovalsModal } from '@/components/ApprovalsModal';
import { useToast } from '@/components/Toast';
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
  const { isAuthenticated, isLoading: authLoading, canEdit, lang, user } = useAuth();
  const { showSuccess, showError, showInfo } = useToast();
  const isAr = lang === 'ar';
  const isApproverOrAdmin = user?.role === 'APPROVER' || user?.role === 'ADMIN';

  const [networks, setNetworks] = useState<NetworkDto[]>([]);
  const [featureTypes, setFeatureTypes] = useState<FeatureTypeDto[]>([]);
  const [activeNetworkId, setActiveNetworkId] = useState<string | null>(null);
  const [visibleTypes, setVisibleTypes] = useState<Set<string>>(new Set());

  const [features, setFeatures] = useState<GeoJsonFeature[]>([]);
  const [selectedFeature, setSelectedFeature] = useState<GeoJsonFeature | null>(null);
  const [targetZoomFeature, setTargetZoomFeature] = useState<GeoJsonFeature | null>(null);
  const [isLoadingFeatures, setIsLoadingFeatures] = useState(false);
  const [isLimitReached, setIsLimitReached] = useState(false);

  // Panel & Modals state
  const [isTableOpen, setIsTableOpen] = useState(false);
  const [isSummaryOpen, setIsSummaryOpen] = useState(false);
  const [isAdminOpen, setIsAdminOpen] = useState(false);
  const [isValidationOpen, setIsValidationOpen] = useState(false);
  const [isHistoryOpen, setIsHistoryOpen] = useState(false);
  const [isImportExportOpen, setIsImportExportOpen] = useState(false);
  const [isAnalysisOpen, setIsAnalysisOpen] = useState(false);
  const [isApprovalsOpen, setIsApprovalsOpen] = useState(false);
  const [pendingApprovalsCount, setPendingApprovalsCount] = useState(0);
  const [approvalPreviewGeometry, setApprovalPreviewGeometry] = useState<any | null>(null);
  const [analysisGeometry, setAnalysisGeometry] = useState<any | null>(null);
  const [measuringMode, setMeasuringMode] = useState<'LINE' | 'POLYGON' | null>(null);
  const [drawnMeasureGeometry, setDrawnMeasureGeometry] = useState<any | null>(null);
  const [historyStack, setHistoryStack] = useState<UndoRedoStackDto | null>(null);
  const [validationIssues, setValidationIssues] = useState<ValidationIssueDto[]>([]);

  // Spatial Selection & Query State
  const [isAttributeModalOpen, setIsAttributeModalOpen] = useState(false);
  const [isNearestModalOpen, setIsNearestModalOpen] = useState(false);
  const [isSelectingPolygon, setIsSelectingPolygon] = useState(false);
  const [isResultsDrawerOpen, setIsResultsDrawerOpen] = useState(false);
  const [spatialResults, setSpatialResults] = useState<GeoJsonFeature[]>([]);
  const [spatialResultsTitle, setSpatialResultsTitle] = useState<string>('');
  const [fitFeaturesBounds, setFitFeaturesBounds] = useState<GeoJsonFeature[] | null>(null);
  const [currentMapCenter, setCurrentMapCenter] = useState<[number, number]>([31.748, 30.015]);

  const [createGeometry, setCreateGeometry] = useState<{
    kind: 'POINT' | 'LINE' | 'POLYGON';
    coordinates: any;
  } | null>(null);
  const [editingFeature, setEditingFeature] = useState<GeoJsonFeature | null>(null);
  const [deletingFeature, setDeletingFeature] = useState<GeoJsonFeature | null>(null);
  const [historyFeature, setHistoryFeature] = useState<GeoJsonFeature | null>(null);

  const abortControllerRef = useRef<AbortController | null>(null);
  const currentBboxRef = useRef<[number, number, number, number] | null>(null);

  // Current active network object for summary
  const activeNetwork = useMemo(() => {
    if (activeNetworkId) {
      return networks.find((n) => n.id === activeNetworkId) || null;
    }
    return networks[0] || null;
  }, [networks, activeNetworkId]);

  // Derived set of highlighted feature IDs for spatial results
  const highlightedFeatureIds = useMemo(() => {
    return new Set(spatialResults.map((f) => f.id));
  }, [spatialResults]);

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

  // Zoom to feature handler (from table, drawer or search)
  const handleZoomToFeature = (feature: GeoJsonFeature) => {
    setSelectedFeature(feature);
    setTargetZoomFeature(feature);
  };

  // Callback when a new feature is drawn using Geoman
  const handleDrawingComplete = (geometry: {
    kind: 'POINT' | 'LINE' | 'POLYGON';
    coordinates: any;
  }) => {
    if (!canEdit) return;
    setCreateGeometry(geometry);
  };

  // Polygon Selection Handler
  const handleSelectByPolygonToggle = () => {
    if (isSelectingPolygon) {
      setIsSelectingPolygon(false);
    } else {
      setIsSelectingPolygon(true);
      showInfo(
        isAr ? 'وضع التحديد بمضلع نشط' : 'Polygon selection active',
        isAr ? 'انقر على الخريطة لرسم مضلع الاستعلام' : 'Click on the map to draw selection polygon'
      );
    }
  };

  const handlePolygonSelected = async (polygonGeoJson: any) => {
    setIsSelectingPolygon(false);
    try {
      showInfo(
        isAr ? 'استعلام مكاني' : 'Spatial Query',
        isAr ? 'جارٍ تنفيذ الاستعلام المكاني (ST_Intersects)...' : 'Executing spatial within query...'
      );
      const result = await api.queryWithin({
        networkId: activeNetworkId || undefined,
        polygon: polygonGeoJson,
        limit: 500,
      });

      const resFeatures = result.features || [];
      setSpatialResults(resFeatures);
      const title = isAr
        ? `المعالم داخل المضلع المحدد (${resFeatures.length} معلم)`
        : `Features within polygon (${resFeatures.length} found)`;
      setSpatialResultsTitle(title);
      setIsResultsDrawerOpen(true);
      setFitFeaturesBounds(resFeatures);

      if (resFeatures.length > 0) {
        showSuccess(
          isAr ? 'تم استخراج المعالم' : 'Features Found',
          isAr
            ? `تم العثور على ${resFeatures.length} معلماً داخل المضلع`
            : `Found ${resFeatures.length} features within polygon`
        );
      } else {
        showInfo(
          isAr ? 'نتيجة فارغة' : 'No Results',
          isAr ? 'لا توجد أصول داخل المضلع المحدد' : 'No assets found within drawn polygon'
        );
      }
    } catch (err: any) {
      console.error('Spatial within query failed:', err);
      showError(
        isAr ? 'خطأ في الاستعلام' : 'Query Error',
        err?.message || (isAr ? 'فشل تنفيذ استعلام المضلع' : 'Within polygon query failed')
      );
    }
  };

  // Spatial Results Handlers
  const handleAttributeResults = (results: GeoJsonFeature[], desc: string) => {
    setSpatialResults(results);
    setSpatialResultsTitle(desc);
    setIsResultsDrawerOpen(true);
    setFitFeaturesBounds(results);
    showSuccess(
      isAr ? 'تحديد بالخصائص' : 'Select by Attribute',
      isAr
        ? `تم تحديد ${results.length} معلماً بنجاح`
        : `Selected ${results.length} features successfully`
    );
  };

  const handleNearestResults = (results: GeoJsonFeature[], desc: string) => {
    setSpatialResults(results);
    setSpatialResultsTitle(desc);
    setIsResultsDrawerOpen(true);
    setFitFeaturesBounds(results);
    showSuccess(
      isAr ? 'البحث عن الأقرب' : 'Find Nearest',
      isAr
        ? `تم العثور على أقرب ${results.length} أصول بنجاح`
        : `Found nearest ${results.length} assets successfully`
    );
  };

  const handleClearSpatialSelection = () => {
    setSpatialResults([]);
    setIsResultsDrawerOpen(false);
    setFitFeaturesBounds(null);
    showInfo(
      isAr ? 'مسح التحديد' : 'Selection Cleared',
      isAr ? 'تم مسح نتائج الاستعلام المكاني' : 'Spatial selection cleared'
    );
  };

  // History & Undo / Redo handlers
  const refreshHistoryStack = useCallback(async () => {
    try {
      const res = await api.getHistoryStack();
      setHistoryStack(res);
    } catch {
      // ignore
    }
  }, []);

  useEffect(() => {
    if (isAuthenticated) {
      refreshHistoryStack();
    }
  }, [isAuthenticated, refreshHistoryStack]);

  const refreshPendingApprovalsCount = useCallback(async () => {
    if (!isApproverOrAdmin) return;
    try {
      const pending = await api.getPendingApprovals();
      setPendingApprovalsCount(pending.length);
    } catch {
      // ignore
    }
  }, [isApproverOrAdmin]);

  useEffect(() => {
    if (isAuthenticated && isApproverOrAdmin) {
      refreshPendingApprovalsCount();
      const interval = setInterval(refreshPendingApprovalsCount, 30000);
      return () => clearInterval(interval);
    }
  }, [isAuthenticated, isApproverOrAdmin, refreshPendingApprovalsCount]);

  const handleUndo = useCallback(async () => {
    try {
      const res = await api.undoLatest();
      showSuccess(
        isAr ? 'تراجع ناجح' : 'Undo Successful',
        isAr ? `تم التراجع عن "${res.description}"` : `Undid "${res.description}"`
      );
      if (currentBboxRef.current) {
        fetchFeatures(currentBboxRef.current);
      }
      refreshHistoryStack();
    } catch (err: any) {
      if (err?.status === 409) {
        showError(
          isAr ? 'تعارض في التراجع' : 'Undo Conflict',
          isAr
            ? 'تعذر التراجع لوجود تعديلات لاحقة على المعلم المعني'
            : 'Cannot undo because affected features have conflicts'
        );
      } else {
        showError(isAr ? 'خطأ' : 'Error', err?.message || 'Undo failed');
      }
    }
  }, [isAr, fetchFeatures, refreshHistoryStack, showError, showSuccess]);

  const handleRedo = useCallback(async () => {
    try {
      const res = await api.redoLatest();
      showSuccess(
        isAr ? 'إعادة ناجحة' : 'Redo Successful',
        isAr ? `تمت إعادة تطبيق "${res.description}"` : `Redid "${res.description}"`
      );
      if (currentBboxRef.current) {
        fetchFeatures(currentBboxRef.current);
      }
      refreshHistoryStack();
    } catch (err: any) {
      if (err?.status === 409) {
        showError(
          isAr ? 'تعارض في الإعادة' : 'Redo Conflict',
          isAr
            ? 'تعذر الإعادة لوجود تعارض في البيانات المعنية'
            : 'Cannot redo because affected features have conflicts'
        );
      } else {
        showError(isAr ? 'خطأ' : 'Error', err?.message || 'Redo failed');
      }
    }
  }, [isAr, fetchFeatures, refreshHistoryStack, showError, showSuccess]);

  // Global hotkeys for Undo (Ctrl+Z) and Redo (Ctrl+Y / Ctrl+Shift+Z)
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      const tag = (e.target as HTMLElement)?.tagName?.toLowerCase();
      if (tag === 'input' || tag === 'textarea' || (e.target as HTMLElement)?.isContentEditable) {
        return;
      }

      if (e.ctrlKey || e.metaKey) {
        if (e.key === 'z' && !e.shiftKey) {
          e.preventDefault();
          if (historyStack?.canUndo) {
            handleUndo();
          }
        } else if (e.key === 'y' || (e.key === 'z' && e.shiftKey) || (e.key === 'Z' && e.shiftKey)) {
          e.preventDefault();
          if (historyStack?.canRedo) {
            handleRedo();
          }
        }
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [historyStack, handleUndo, handleRedo]);

  // Callback after feature is created or edited
  const handleFeatureSaved = () => {
    if (currentBboxRef.current) {
      fetchFeatures(currentBboxRef.current);
    }
    refreshHistoryStack();
    refreshPendingApprovalsCount();
  };

  // Callback after feature is deleted
  const handleFeatureDeleted = (deletedId: string) => {
    if (selectedFeature?.id === deletedId) {
      setSelectedFeature(null);
    }
    if (currentBboxRef.current) {
      fetchFeatures(currentBboxRef.current);
    }
    setSpatialResults((prev) => prev.filter((f) => f.id !== deletedId));
    refreshHistoryStack();
    refreshPendingApprovalsCount();
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
        onToggleTable={() => setIsTableOpen(!isTableOpen)}
        isTableOpen={isTableOpen}
        onOpenSummary={() => setIsSummaryOpen(true)}
        onOpenAdmin={() => setIsAdminOpen(true)}
        onSelectByPolygon={handleSelectByPolygonToggle}
        isSelectingPolygon={isSelectingPolygon}
        onOpenSelectByAttribute={() => setIsAttributeModalOpen(true)}
        onOpenNearestModal={() => setIsNearestModalOpen(true)}
        onOpenValidation={() => setIsValidationOpen(true)}
        onOpenImportExport={() => setIsImportExportOpen(true)}
        onOpenAnalysis={() => setIsAnalysisOpen(true)}
        onOpenApprovals={() => setIsApprovalsOpen(true)}
        pendingApprovalsCount={pendingApprovalsCount}
        canUndo={historyStack?.canUndo ?? false}
        canRedo={historyStack?.canRedo ?? false}
        onUndo={handleUndo}
        onRedo={handleRedo}
        onOpenHistory={() => setIsHistoryOpen(true)}
        spatialResultsCount={spatialResults.length}
        onToggleResultsDrawer={() => setIsResultsDrawerOpen(!isResultsDrawerOpen)}
        isResultsDrawerOpen={isResultsDrawerOpen}
        searchSlot={
          <SearchBar
            features={features}
            onSelectFeature={setSelectedFeature}
            onZoomToFeature={handleZoomToFeature}
          />
        }
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
            highlightedFeatureIds={highlightedFeatureIds}
            validationIssues={validationIssues}
            targetZoomFeature={targetZoomFeature}
            fitFeaturesBounds={fitFeaturesBounds}
            canEdit={canEdit}
            isSelectingPolygon={isSelectingPolygon}
            analysisGeometry={analysisGeometry}
            approvalPreviewGeometry={approvalPreviewGeometry}
            isMeasuring={measuringMode}
            onMeasureDrawingComplete={(geom) => {
              setDrawnMeasureGeometry(geom);
              setMeasuringMode(null);
              setIsAnalysisOpen(true);
            }}
            drawnMeasureGeometry={drawnMeasureGeometry}
            onSelectFeature={setSelectedFeature}
            onBboxChange={handleBboxChange}
            onCenterChange={setCurrentMapCenter}
            onDrawingComplete={handleDrawingComplete}
            onPolygonSelected={handlePolygonSelected}
          />

          {/* Bottom Spatial Results Drawer */}
          <SpatialResultsDrawer
            isOpen={isResultsDrawerOpen}
            onClose={() => setIsResultsDrawerOpen(false)}
            title={spatialResultsTitle}
            features={spatialResults}
            featureTypes={featureTypes}
            selectedFeatureId={selectedFeature?.id || null}
            onSelectFeature={setSelectedFeature}
            onZoomToFeature={handleZoomToFeature}
            onClear={handleClearSpatialSelection}
          />

          {/* Bottom Attribute Table Drawer */}
          <AttributeTable
            isOpen={isTableOpen}
            onClose={() => setIsTableOpen(false)}
            features={features}
            featureTypes={featureTypes}
            selectedFeatureId={selectedFeature?.id || null}
            onSelectFeature={setSelectedFeature}
            onZoomToFeature={handleZoomToFeature}
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

      {/* Select by Attribute Modal */}
      <SpatialSelectionModal
        isOpen={isAttributeModalOpen}
        onClose={() => setIsAttributeModalOpen(false)}
        featureTypes={featureTypes}
        features={features}
        onResults={handleAttributeResults}
      />

      {/* KNN Nearest Search Modal */}
      <NearestQueryModal
        isOpen={isNearestModalOpen}
        onClose={() => setIsNearestModalOpen(false)}
        activeNetworkId={activeNetworkId}
        featureTypes={featureTypes}
        currentCenter={currentMapCenter}
        onResults={handleNearestResults}
      />

      {/* Feature Create Modal */}
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

      {/* Network Summary Modal */}
      {isSummaryOpen && (
        <SummaryModal
          network={activeNetwork}
          onClose={() => setIsSummaryOpen(false)}
        />
      )}

      {/* User Administration Modal (Admin Only) */}
      {isAdminOpen && (
        <UserAdminModal
          networks={networks}
          featureTypes={featureTypes}
          onClose={() => setIsAdminOpen(false)}
        />
      )}

      {/* Validation Quality Dashboard Modal */}
      <ValidationDashboardModal
        isOpen={isValidationOpen}
        onClose={() => setIsValidationOpen(false)}
        networks={networks}
        activeNetworkId={activeNetworkId}
        onSelectNetwork={setActiveNetworkId}
        onZoomToIssue={handleZoomToFeature}
        onIssuesUpdated={setValidationIssues}
      />

      {/* Undo / Redo & History Stack Modal */}
      <HistoryPanelModal
        isOpen={isHistoryOpen}
        onClose={() => setIsHistoryOpen(false)}
        onStackChanged={() => {
          refreshHistoryStack();
          if (currentBboxRef.current) {
            fetchFeatures(currentBboxRef.current);
          }
        }}
      />

      {/* Import & Export Modal */}
      <ImportExportModal
        isOpen={isImportExportOpen}
        onClose={() => setIsImportExportOpen(false)}
        networks={networks}
        activeNetworkId={activeNetworkId}
        featureTypes={featureTypes}
        currentBbox={
          currentBboxRef.current
            ? `${currentBboxRef.current[0]},${currentBboxRef.current[1]},${currentBboxRef.current[2]},${currentBboxRef.current[3]}`
            : undefined
        }
        isAr={isAr}
        onImportSuccess={() => {
          refreshHistoryStack();
          if (currentBboxRef.current) {
            fetchFeatures(currentBboxRef.current);
          }
        }}
      />

      {/* Analysis & Reports Modal */}
      <AnalysisModal
        isOpen={isAnalysisOpen}
        onClose={() => setIsAnalysisOpen(false)}
        networks={networks}
        activeNetworkId={activeNetworkId}
        featureTypes={featureTypes}
        selectedFeature={selectedFeature}
        currentMapCenter={currentMapCenter}
        onSetAnalysisGeometry={setAnalysisGeometry}
        onStartMeasureDrawing={(kind) => {
          setMeasuringMode(kind);
          setIsAnalysisOpen(false);
        }}
        drawnMeasureGeometry={drawnMeasureGeometry}
        onClearMeasureDrawing={() => setDrawnMeasureGeometry(null)}
        isAr={isAr}
      />

      {/* Approvals Workflow Modal */}
      <ApprovalsModal
        isOpen={isApprovalsOpen}
        onClose={() => {
          setIsApprovalsOpen(false);
          setApprovalPreviewGeometry(null);
        }}
        networks={networks}
        activeNetworkId={activeNetworkId}
        featureTypes={featureTypes}
        onFeatureTypesUpdated={setFeatureTypes}
        onRefreshData={() => {
          refreshPendingApprovalsCount();
          if (currentBboxRef.current) {
            fetchFeatures(currentBboxRef.current);
          }
        }}
        onPreviewGeometry={setApprovalPreviewGeometry}
      />
    </div>
  );
}
