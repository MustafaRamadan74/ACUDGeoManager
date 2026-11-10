'use client';

import React, { useEffect, useRef, useState } from 'react';
import L from 'leaflet';
import '@geoman-io/leaflet-geoman-free';
import { Maximize2, Minimize2, Printer, Zap, RefreshCw, Loader2 } from 'lucide-react';
import { api } from '@/lib/api';
import { FeatureTypeDto, GeoJsonFeature, ValidationIssueDto, NetworkTraceResultDto } from '@/lib/types';
import { BasemapControl, BASEMAPS, BasemapKey } from '@/components/Map/BasemapControl';
import { BookmarksControl } from '@/components/Map/BookmarksControl';
import { LiveCoordinatesBar } from '@/components/Map/LiveCoordinatesBar';
import { useAuth } from '@/lib/authContext';

interface MapComponentProps {
  features: GeoJsonFeature[];
  featureTypes: FeatureTypeDto[];
  visibleTypes: Set<string>;
  selectedFeatureId: string | null;
  highlightedFeatureIds?: Set<string>;
  validationIssues?: ValidationIssueDto[];
  targetZoomFeature?: GeoJsonFeature | null;
  fitFeaturesBounds?: GeoJsonFeature[] | null;
  canEdit: boolean;
  isSelectingPolygon?: boolean;
  onSelectFeature: (feature: GeoJsonFeature) => void;
  onBboxChange: (bbox: [number, number, number, number]) => void;
  onCenterChange?: (center: [number, number]) => void;
  onDrawingComplete?: (geometry: {
    kind: 'POINT' | 'LINE' | 'POLYGON';
    coordinates: any;
  }) => void;
  onPolygonSelected?: (polygonGeoJson: any) => void;
  analysisGeometry?: any | null;
  approvalPreviewGeometry?: any | null;
  traceResult?: NetworkTraceResultDto | null;
  isMeasuring?: 'LINE' | 'POLYGON' | null;
  onMeasureDrawingComplete?: (geometry: any) => void;
  drawnMeasureGeometry?: any | null;
}

export const MapComponent: React.FC<MapComponentProps> = ({
  features,
  featureTypes,
  visibleTypes,
  selectedFeatureId,
  highlightedFeatureIds,
  validationIssues,
  targetZoomFeature,
  fitFeaturesBounds,
  canEdit,
  isSelectingPolygon = false,
  analysisGeometry,
  approvalPreviewGeometry,
  traceResult,
  isMeasuring = null,
  onMeasureDrawingComplete,
  drawnMeasureGeometry,
  onSelectFeature,
  onBboxChange,
  onCenterChange,
  onDrawingComplete,
  onPolygonSelected,
}) => {
  const { lang } = useAuth();
  const isAr = lang === 'ar';

  const mapContainerRef = useRef<HTMLDivElement>(null);
  const mapInstanceRef = useRef<L.Map | null>(null);
  const geojsonLayerRef = useRef<L.GeoJSON | null>(null);
  const validationLayerRef = useRef<L.GeoJSON | null>(null);
  const analysisLayerRef = useRef<L.GeoJSON | null>(null);
  const approvalPreviewLayerRef = useRef<L.GeoJSON | null>(null);
  const traceLayerRef = useRef<L.LayerGroup | null>(null);
  const measureLayerRef = useRef<L.GeoJSON | null>(null);
  const basemapLayerRef = useRef<L.TileLayer | null>(null);
  const debounceTimerRef = useRef<NodeJS.Timeout | null>(null);

  const isMeasuringRef = useRef(isMeasuring);
  const onMeasureDrawingCompleteRef = useRef(onMeasureDrawingComplete);

  // Basemap state with localStorage persistence
  const [currentBasemap, setCurrentBasemap] = useState<BasemapKey>(() => {
    if (typeof window !== 'undefined') {
      const saved = localStorage.getItem('acud_basemap') as BasemapKey;
      if (saved && saved in BASEMAPS) return saved;
    }
    return 'osm';
  });

  // Coordinates and Zoom tracking
  const [cursorCoords, setCursorCoords] = useState<{ lat: number; lon: number } | null>(null);
  const [currentZoom, setCurrentZoom] = useState<number>(14);

  // Fullscreen state
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [isRefreshingSummaries, setIsRefreshingSummaries] = useState(false);

  const handleRefreshSummaries = async () => {
    setIsRefreshingSummaries(true);
    try {
      await api.refreshMaterializedSummaries();
    } catch (err) {
      console.error('Failed to refresh materialized summaries', err);
    } finally {
      setIsRefreshingSummaries(false);
    }
  };

  const onDrawingCompleteRef = useRef(onDrawingComplete);
  const onPolygonSelectedRef = useRef(onPolygonSelected);
  const isSelectingPolygonRef = useRef(isSelectingPolygon);
  const onCenterChangeRef = useRef(onCenterChange);

  useEffect(() => {
    onDrawingCompleteRef.current = onDrawingComplete;
  }, [onDrawingComplete]);

  useEffect(() => {
    onPolygonSelectedRef.current = onPolygonSelected;
  }, [onPolygonSelected]);

  useEffect(() => {
    isSelectingPolygonRef.current = isSelectingPolygon;
  }, [isSelectingPolygon]);

  useEffect(() => {
    isMeasuringRef.current = isMeasuring;
  }, [isMeasuring]);

  useEffect(() => {
    onMeasureDrawingCompleteRef.current = onMeasureDrawingComplete;
  }, [onMeasureDrawingComplete]);

  useEffect(() => {
    onCenterChangeRef.current = onCenterChange;
  }, [onCenterChange]);

  // Handle Fullscreen Toggle
  const toggleFullscreen = () => {
    const el = mapContainerRef.current?.parentElement || mapContainerRef.current;
    if (!el) return;

    if (!document.fullscreenElement) {
      el.requestFullscreen().then(() => setIsFullscreen(true)).catch(console.error);
    } else {
      document.exitFullscreen().then(() => setIsFullscreen(false)).catch(console.error);
    }
  };

  useEffect(() => {
    const handleFsChange = () => {
      setIsFullscreen(Boolean(document.fullscreenElement));
      setTimeout(() => {
        mapInstanceRef.current?.invalidateSize();
      }, 150);
    };

    document.addEventListener('fullscreenchange', handleFsChange);
    return () => document.removeEventListener('fullscreenchange', handleFsChange);
  }, []);

  // Handle Print / Export
  const handlePrint = () => {
    window.print();
  };

  // Fly to location (used by Bookmarks)
  const handleFlyTo = (lat: number, lon: number, zoom: number) => {
    if (!mapInstanceRef.current) return;
    mapInstanceRef.current.flyTo([lat, lon], zoom, { animate: true, duration: 1 });
  };

  // Get current view (used by Bookmarks)
  const getCurrentView = () => {
    const map = mapInstanceRef.current;
    if (!map) {
      return { center: [31.748, 30.015] as [number, number], zoom: 14 };
    }
    const c = map.getCenter();
    const b = map.getBounds();
    return {
      center: [c.lng, c.lat] as [number, number],
      zoom: map.getZoom(),
      bbox: JSON.stringify([b.getWest(), b.getSouth(), b.getEast(), b.getNorth()]),
    };
  };

  // Initialize Map
  useEffect(() => {
    if (!mapContainerRef.current || mapInstanceRef.current) return;

    // Center on New Administrative Capital (NAC) - People's Square / Memorial
    const nacCenter: L.LatLngExpression = [30.015, 31.748];
    const map = L.map(mapContainerRef.current, {
      center: nacCenter,
      zoom: 14,
      zoomControl: false,
    });

    // Add Zoom control at top-left
    L.control.zoom({ position: 'topleft' }).addTo(map);

    // Add Metric Scale Bar at bottom-left
    L.control.scale({ imperial: false, metric: true, position: 'bottomleft' }).addTo(map);

    // Initial Basemap
    const def = BASEMAPS[currentBasemap] || BASEMAPS.osm;
    const initialTile = L.tileLayer(def.url, {
      attribution: def.attribution + ' | ACUD GeoManager',
      maxZoom: def.maxZoom,
    }).addTo(map);
    basemapLayerRef.current = initialTile;

    // Initial cursor / center coords
    setCursorCoords({ lat: 30.015, lon: 31.748 });
    setCurrentZoom(map.getZoom());

    // Track Cursor Coordinates on mousemove
    map.on('mousemove', (e: L.LeafletMouseEvent) => {
      setCursorCoords({ lat: e.latlng.lat, lon: e.latlng.lng });
    });

    // Track Zoom level
    map.on('zoomend', () => {
      setCurrentZoom(map.getZoom());
    });

    // Leaflet-Geoman Snapping & Drawing Configuration
    const pm = (map as any).pm;
    if (pm) {
      pm.setGlobalOptions({
        snapping: true,
        snapDistance: 15,
        allowSelfIntersection: false,
      });

      map.on('pm:create', (e: any) => {
        const { shape, layer } = e;
        if (!layer) return;

        let kind: 'POINT' | 'LINE' | 'POLYGON' = 'POINT';
        let coordinates: any;

        if (shape === 'Marker') {
          kind = 'POINT';
          const latlng = layer.getLatLng();
          coordinates = [latlng.lng, latlng.lat];
        } else if (shape === 'Line') {
          kind = 'LINE';
          const latlngs = layer.getLatLngs();
          coordinates = (latlngs as any[]).map((ll) => [ll.lng, ll.lat]);
        } else if (shape === 'Polygon' || shape === 'Rectangle') {
          kind = 'POLYGON';
          const rawLatlngs = layer.getLatLngs();
          const ring = Array.isArray(rawLatlngs[0]) ? rawLatlngs[0] : rawLatlngs;
          const ringCoords = ring.map((ll: any) => [ll.lng, ll.lat]);

          if (ringCoords.length > 0) {
            const first = ringCoords[0];
            const last = ringCoords[ringCoords.length - 1];
            if (first[0] !== last[0] || first[1] !== last[1]) {
              ringCoords.push([first[0], first[1]]);
            }
          }
          coordinates = [ringCoords];
        }

        map.removeLayer(layer);

        // Check if we are in measurement drawing mode
        if (isMeasuringRef.current) {
          if (onMeasureDrawingCompleteRef.current) {
            if (kind === 'LINE') {
              onMeasureDrawingCompleteRef.current({
                type: 'LineString',
                coordinates: coordinates,
              });
            } else if (kind === 'POLYGON') {
              onMeasureDrawingCompleteRef.current({
                type: 'Polygon',
                coordinates: coordinates,
              });
            }
          }
          return;
        }

        // Check if we are in polygon selection mode
        if (isSelectingPolygonRef.current && (shape === 'Polygon' || shape === 'Rectangle')) {
          if (onPolygonSelectedRef.current) {
            onPolygonSelectedRef.current({
              type: 'Polygon',
              coordinates: coordinates,
            });
          }
          return;
        }

        if (onDrawingCompleteRef.current) {
          onDrawingCompleteRef.current({ kind, coordinates });
        }
      });
    }

    // Initial bbox and center emit
    const emitBboxAndCenter = () => {
      const bounds = map.getBounds();
      const center = map.getCenter();

      onBboxChange([
        bounds.getWest(),
        bounds.getSouth(),
        bounds.getEast(),
        bounds.getNorth(),
      ]);

      if (onCenterChangeRef.current) {
        onCenterChangeRef.current([center.lng, center.lat]);
      }
    };

    map.on('moveend', () => {
      if (debounceTimerRef.current) clearTimeout(debounceTimerRef.current);
      debounceTimerRef.current = setTimeout(emitBboxAndCenter, 250);
    });

    mapInstanceRef.current = map;
    emitBboxAndCenter();

    return () => {
      if (debounceTimerRef.current) clearTimeout(debounceTimerRef.current);
      map.remove();
      mapInstanceRef.current = null;
    };
  }, []);

  // Update Basemap Layer when currentBasemap changes
  useEffect(() => {
    const map = mapInstanceRef.current;
    if (!map) return;

    const def = BASEMAPS[currentBasemap] || BASEMAPS.osm;

    if (basemapLayerRef.current) {
      map.removeLayer(basemapLayerRef.current);
    }

    const newTile = L.tileLayer(def.url, {
      attribution: def.attribution + ' | ACUD GeoManager',
      maxZoom: def.maxZoom,
    }).addTo(map);

    newTile.bringToBack();
    basemapLayerRef.current = newTile;

    try {
      localStorage.setItem('acud_basemap', currentBasemap);
    } catch {
      // ignore storage error
    }
  }, [currentBasemap]);

  // React to Polygon Selection Mode Toggle
  useEffect(() => {
    const map = mapInstanceRef.current;
    if (!map) return;
    const pm = (map as any).pm;
    if (!pm) return;

    if (isSelectingPolygon) {
      pm.enableDraw('Polygon', {
        snappable: false,
        continueDrawing: false,
        templineStyle: { color: '#06B6D4', dashArray: '5, 5' },
        hintlineStyle: { color: '#06B6D4', dashArray: '5, 5' },
      });
    } else {
      if (pm.Draw?.Polygon?.enabled()) {
        pm.disableDraw();
      }
    }
  }, [isSelectingPolygon]);

  // React to Measurement Drawing Mode Toggle
  useEffect(() => {
    const map = mapInstanceRef.current;
    if (!map) return;
    const pm = (map as any).pm;
    if (!pm) return;

    if (isMeasuring === 'LINE') {
      pm.enableDraw('Line', {
        snappable: false,
        continueDrawing: false,
        templineStyle: { color: '#38BDF8', dashArray: '5, 5', weight: 3 },
        hintlineStyle: { color: '#38BDF8', dashArray: '5, 5', weight: 3 },
      });
    } else if (isMeasuring === 'POLYGON') {
      pm.enableDraw('Polygon', {
        snappable: false,
        continueDrawing: false,
        templineStyle: { color: '#F59E0B', dashArray: '5, 5', weight: 3 },
        hintlineStyle: { color: '#F59E0B', dashArray: '5, 5', weight: 3 },
      });
    } else {
      if (pm.Draw?.Line?.enabled()) pm.disableDraw();
      if (pm.Draw?.Polygon?.enabled()) pm.disableDraw();
    }
  }, [isMeasuring]);

  // React to Analysis Geometry (Buffer / Service Area) display
  useEffect(() => {
    const map = mapInstanceRef.current;
    if (!map) return;

    if (analysisLayerRef.current) {
      map.removeLayer(analysisLayerRef.current);
      analysisLayerRef.current = null;
    }

    if (!analysisGeometry) return;

    try {
      const layer = L.geoJSON(analysisGeometry, {
        style: {
          color: '#06B6D4',
          weight: 3,
          dashArray: '5, 5',
          fillColor: '#0891B2',
          fillOpacity: 0.25,
        },
        pointToLayer: (_feat, latlng) => {
          return L.circleMarker(latlng, {
            radius: 8,
            fillColor: '#06B6D4',
            color: '#FFFFFF',
            weight: 2,
            fillOpacity: 0.85,
          });
        },
      });

      layer.addTo(map);
      analysisLayerRef.current = layer;

      const bounds = layer.getBounds();
      if (bounds.isValid()) {
        map.fitBounds(bounds, { maxZoom: 17, padding: [60, 60], animate: true });
      }
    } catch (err) {
      console.error('Failed to render analysis geometry:', err);
    }
  }, [analysisGeometry]);

  // React to Approval Preview Geometry display
  useEffect(() => {
    const map = mapInstanceRef.current;
    if (!map) return;

    if (approvalPreviewLayerRef.current) {
      map.removeLayer(approvalPreviewLayerRef.current);
      approvalPreviewLayerRef.current = null;
    }

    if (!approvalPreviewGeometry) return;

    try {
      const isFeatureOrCollection = approvalPreviewGeometry.type === 'Feature' || approvalPreviewGeometry.type === 'FeatureCollection';
      const geojsonObj = isFeatureOrCollection
        ? approvalPreviewGeometry
        : {
            type: 'Feature',
            geometry: approvalPreviewGeometry,
            properties: { label: isAr ? 'معاينة التعديل المقترح' : 'Proposed Change Preview' },
          };

      const layer = L.geoJSON(geojsonObj, {
        style: {
          color: '#F97316',
          weight: 4,
          dashArray: '6, 6',
          fillColor: '#FB923C',
          fillOpacity: 0.35,
        },
        pointToLayer: (_feat, latlng) => {
          return L.circleMarker(latlng, {
            radius: 10,
            fillColor: '#F97316',
            color: '#FFFFFF',
            weight: 3,
            fillOpacity: 0.9,
          });
        },
        onEachFeature: (_f, l) => {
          l.bindTooltip(isAr ? '⚠️ معاينة مقترح التعديل (قيد الاعتماد)' : '⚠️ Proposed Change (Pending Approval)', {
            permanent: true,
            direction: 'top',
            className: 'bg-amber-600 text-white font-bold px-2 py-1 rounded shadow text-xs',
          });
        },
      });

      layer.addTo(map);
      approvalPreviewLayerRef.current = layer;

      const bounds = layer.getBounds();
      if (bounds.isValid()) {
        map.fitBounds(bounds, { maxZoom: 18, padding: [70, 70], animate: true });
      }
    } catch (err) {
      console.error('Failed to render approval preview geometry:', err);
    }
  }, [approvalPreviewGeometry, isAr]);

  // React to Network Trace Result visualization
  useEffect(() => {
    const map = mapInstanceRef.current;
    if (!map) return;

    if (traceLayerRef.current) {
      map.removeLayer(traceLayerRef.current);
      traceLayerRef.current = null;
    }

    if (!traceResult) return;

    try {
      const group = L.layerGroup();
      const bounds = L.latLngBounds([]);

      // 1. Draw Affected Service Zones (Polygons)
      if (traceResult.affectedPolygons && traceResult.affectedPolygons.length > 0) {
        traceResult.affectedPolygons.forEach((poly) => {
          if (!poly.geojson) return;
          const polyLayer = L.geoJSON(poly.geojson, {
            style: {
              color: '#F97316',
              weight: 2,
              fillColor: '#FB923C',
              fillOpacity: 0.2,
              dashArray: '4, 4',
            },
          });
          polyLayer.bindTooltip(`<b>${poly.name}</b><br/>Type: ${poly.feature_type_code}`, { sticky: true });
          polyLayer.addTo(group);
          const b = polyLayer.getBounds();
          if (b.isValid()) bounds.extend(b);
        });
      }

      // 2. Draw Traversed Edges (Pipes)
      const edgeColor =
        traceResult.mode === 'ISOLATION'
          ? '#EF4444' // Red / Amber for isolated broken section
          : traceResult.mode === 'UPSTREAM'
          ? '#8B5CF6' // Purple for upstream source feed
          : '#06B6D4'; // Cyan for downstream flow

      if (traceResult.edges && traceResult.edges.length > 0) {
        traceResult.edges.forEach((edge) => {
          if (!edge.geojson) return;
          const edgeLayer = L.geoJSON(edge.geojson, {
            style: {
              color: edgeColor,
              weight: 5,
              opacity: 0.9,
            },
          });
          edgeLayer.bindTooltip(
            `<b>${edge.name || 'Pipe'}</b><br/>Diameter: ${edge.diameter_mm || '-'} mm<br/>Length: ${edge.length_m.toFixed(1)} m<br/>Direction: ${edge.direction || '-'}`,
            { sticky: true }
          );
          edgeLayer.addTo(group);
          const b = edgeLayer.getBounds();
          if (b.isValid()) bounds.extend(b);
        });
      }

      // 3. Draw Valves to Close (Pulsing Red Markers)
      if (traceResult.valvesToClose && traceResult.valvesToClose.length > 0) {
        traceResult.valvesToClose.forEach((v) => {
          if (!v.coordinates || v.coordinates.length < 2) return;
          const latlng = L.latLng(v.coordinates[1], v.coordinates[0]);
          const marker = L.circleMarker(latlng, {
            radius: 8,
            color: '#FFFFFF',
            weight: 2.5,
            fillColor: '#DC2626',
            fillOpacity: 0.95,
          });
          marker.bindTooltip(
            `<div style="font-weight:bold;color:#DC2626">⚠️ VALVE TO CLOSE</div><b>${v.name}</b><br/>Type: ${v.valve_type || 'GATE'}<br/>State: ${v.is_open ? 'OPEN' : 'CLOSED'}`,
            { permanent: false, sticky: true }
          );
          marker.addTo(group);
          bounds.extend(latlng);
        });
      }

      group.addTo(map);
      traceLayerRef.current = group;

      if (bounds.isValid()) {
        map.fitBounds(bounds, { maxZoom: 18, padding: [60, 60], animate: true });
      }
    } catch (err) {
      console.error('Failed to render network trace result on map:', err);
    }
  }, [traceResult]);

  // React to Drawn Measure Geometry display
  useEffect(() => {
    const map = mapInstanceRef.current;
    if (!map) return;

    if (measureLayerRef.current) {
      map.removeLayer(measureLayerRef.current);
      measureLayerRef.current = null;
    }

    if (!drawnMeasureGeometry) return;

    try {
      const feat = {
        type: 'Feature',
        geometry: drawnMeasureGeometry,
        properties: {},
      };
      const layer = L.geoJSON(feat as any, {
        style: {
          color: '#38BDF8',
          weight: 4,
          dashArray: '4, 4',
          fillColor: '#0284C7',
          fillOpacity: 0.35,
        },
      });
      layer.addTo(map);
      measureLayerRef.current = layer;

      const bounds = layer.getBounds();
      if (bounds.isValid()) {
        map.fitBounds(bounds, { maxZoom: 18, padding: [50, 50], animate: true });
      }
    } catch (err) {
      console.error('Failed to render measure geometry:', err);
    }
  }, [drawnMeasureGeometry]);

  // React to fitFeaturesBounds (auto zoom to query results)
  useEffect(() => {
    const map = mapInstanceRef.current;
    if (!map || !fitFeaturesBounds || fitFeaturesBounds.length === 0) return;

    try {
      const collection = {
        type: 'FeatureCollection',
        features: fitFeaturesBounds as any,
      };
      const tempLayer = L.geoJSON(collection as any);
      const bounds = tempLayer.getBounds();
      if (bounds.isValid()) {
        map.fitBounds(bounds, { maxZoom: 17, animate: true, padding: [50, 50] });
      }
    } catch (err) {
      console.error('Failed to fit bounds:', err);
    }
  }, [fitFeaturesBounds]);

  // React to targetZoomFeature (click to zoom and select)
  useEffect(() => {
    const map = mapInstanceRef.current;
    if (!map || !targetZoomFeature) return;

    try {
      const geom = targetZoomFeature.geometry;
      if (geom.type === 'Point') {
        const [lng, lat] = geom.coordinates;
        map.flyTo([lat, lng], 18, { animate: true, duration: 1 });
      } else {
        const tempLayer = L.geoJSON(targetZoomFeature as any);
        const bounds = tempLayer.getBounds();
        if (bounds.isValid()) {
          map.fitBounds(bounds, { maxZoom: 18, animate: true, padding: [60, 60] });
        }
      }
    } catch (err) {
      console.error('Failed to zoom to feature:', err);
    }
  }, [targetZoomFeature]);

  // Update Geoman Drawing Controls based on user role (canEdit)
  useEffect(() => {
    const map = mapInstanceRef.current;
    if (!map) return;
    const pm = (map as any).pm;
    if (!pm) return;

    if (canEdit && !isSelectingPolygon) {
      pm.addControls({
        position: 'topleft',
        drawMarker: true,
        drawCircleMarker: false,
        drawPolyline: true,
        drawRectangle: false,
        drawPolygon: true,
        drawCircle: false,
        editMode: false,
        dragMode: false,
        cutPolygon: false,
        removalMode: false,
        rotateMode: false,
      });
    } else {
      pm.removeControls();
    }
  }, [canEdit, isSelectingPolygon]);

  // Update GeoJSON features on map with attribute-based dynamic styling
  useEffect(() => {
    const map = mapInstanceRef.current;
    if (!map) return;

    if (geojsonLayerRef.current) {
      map.removeLayer(geojsonLayerRef.current);
      geojsonLayerRef.current = null;
    }

    const typeStyles = new Map<string, any>();
    featureTypes.forEach((ft) => typeStyles.set(ft.code, ft.style));

    const filteredFeatures = features.filter((f) =>
      visibleTypes.has(f.properties.featureTypeCode)
    );

    const geoJsonLayer = L.geoJSON(
      {
        type: 'FeatureCollection',
        features: filteredFeatures as any,
      } as any,
      {
        pointToLayer: (feature, latlng) => {
          const typeCode = feature.properties?.featureTypeCode;
          const style = typeStyles.get(typeCode) || {};
          const fId = feature.id !== undefined && feature.id !== null ? String(feature.id) : '';
          const isSelected = fId === selectedFeatureId;
          const isHighlighted = Boolean(fId && highlightedFeatureIds?.has(fId));

          const props = feature.properties || {};
          const status = String(props.status || '').toUpperCase();
          const isOpen = props.is_open ?? props.isOpen;

          let fillColor = style.fillColor || style.color || '#3B82F6';
          let strokeColor = style.color || '#1E40AF';
          let radius = style.radius || 7;
          let weight = style.weight || 2;

          // Valve attribute styling (open vs closed)
          if (isOpen === false) {
            fillColor = '#EF4444'; // Red for closed valve
            strokeColor = '#991B1B';
          } else if (isOpen === true) {
            fillColor = '#10B981'; // Green for open valve
            strokeColor = '#065F46';
          }

          // Inactive / Abandoned status
          if (status === 'INACTIVE' || status === 'ABANDONED') {
            fillColor = '#64748B';
            strokeColor = '#334155';
          }

          if (isHighlighted) {
            fillColor = '#06B6D4'; // Vibrant Cyan
            strokeColor = '#FFFFFF';
            radius = 11;
            weight = 3;
          } else if (isSelected) {
            fillColor = '#F59E0B'; // Amber
            strokeColor = '#FFFFFF';
            radius = 10;
            weight = 3;
          }

          return L.circleMarker(latlng, {
            pane: 'markerPane', // Renders on top of overlay polylines and polygons
            radius,
            fillColor,
            color: strokeColor,
            weight,
            opacity: 1,
            fillOpacity: isHighlighted || isSelected ? 0.95 : style.fillOpacity || 0.85,
          });
        },
        style: (feature) => {
          if (!feature) return {};
          const typeCode = feature.properties?.featureTypeCode;
          const style = typeStyles.get(typeCode) || {};
          const fId = feature.id !== undefined && feature.id !== null ? String(feature.id) : '';
          const isSelected = fId === selectedFeatureId;
          const isHighlighted = Boolean(fId && highlightedFeatureIds?.has(fId));

          const props = feature.properties || {};
          const status = String(props.status || '').toUpperCase();
          const diameter = Number(props.diameter_mm || props.diameter || 0);

          let strokeColor = style.color || '#2563EB';
          let fillColor = style.fillColor || style.color || '#3B82F6';
          let weight = style.weight || 3;
          let fillOpacity = style.fillOpacity || 0.25;
          let dashArray = style.dashArray || undefined;

          // Thematic attribute styling based on status
          if (status === 'UNDER_CONSTRUCTION') {
            strokeColor = '#F59E0B'; // Amber
            fillColor = '#FBBF24';
            dashArray = '5, 5';
          } else if (status === 'INACTIVE' || status === 'ABANDONED') {
            strokeColor = '#64748B'; // Slate / Gray
            fillColor = '#94A3B8';
            dashArray = '3, 4';
          } else if (status === 'MAINTENANCE') {
            strokeColor = '#EC4899'; // Pink
            dashArray = '7, 4';
          }

          // Pipe diameter scaling for lines
          if (diameter > 0) {
            if (diameter >= 1000) weight = 6;
            else if (diameter >= 600) weight = 5;
            else if (diameter >= 300) weight = 4;
            else if (diameter >= 150) weight = 3;
            else weight = 2;
          }

          if (isHighlighted) {
            strokeColor = '#06B6D4';
            fillColor = '#22D3EE';
            weight = Math.max(weight + 2, 5);
            fillOpacity = 0.55;
          } else if (isSelected) {
            strokeColor = '#F59E0B';
            fillColor = '#FBBF24';
            weight = Math.max(weight + 2, 5);
            fillOpacity = 0.5;
          }

          return {
            color: strokeColor,
            weight,
            opacity: 0.95,
            fillColor,
            fillOpacity,
            dashArray,
          };
        },
        onEachFeature: (feature, layer) => {
          const name = feature.properties?.name || 'Feature';
          const type = feature.properties?.featureTypeCode || '';
          const status = feature.properties?.status;
          const diameter = feature.properties?.diameter_mm || feature.properties?.diameter;
          const dist = feature.properties?.distanceMeters ?? (feature.properties as any)?.distance_meters;

          const distStr = dist !== undefined ? `<div class="text-[10px] text-amber-400 font-mono font-bold mt-0.5">🎯 ${Number(dist).toFixed(1)}m</div>` : '';
          const statusStr = status ? `<div class="text-[10px] text-slate-400 font-sans mt-0.5">${status} ${diameter ? `(Ø ${diameter}mm)` : ''}</div>` : '';

          layer.bindTooltip(
            `<div class="font-bold text-xs">${name}</div><div class="text-[10px] text-slate-500 font-mono">${type}</div>${statusStr}${distStr}`,
            { direction: 'top', offset: [0, -5], opacity: 0.9 }
          );

          layer.on('click', () => {
            onSelectFeature(feature as any);
          });
        },
      }
    );

    geoJsonLayer.addTo(map);
    geojsonLayerRef.current = geoJsonLayer;
  }, [features, featureTypes, visibleTypes, selectedFeatureId, highlightedFeatureIds]);

  // Render Validation Issues Layer
  useEffect(() => {
    const map = mapInstanceRef.current;
    if (!map) return;

    if (validationLayerRef.current) {
      map.removeLayer(validationLayerRef.current);
      validationLayerRef.current = null;
    }

    if (!validationIssues || validationIssues.length === 0) return;

    const validFeatures = validationIssues
      .filter((i) => i.geometryGeoJson && i.status !== 'RESOLVED')
      .map((i) => {
        try {
          return {
            type: 'Feature',
            geometry: JSON.parse(i.geometryGeoJson!),
            properties: {
              id: i.id,
              ruleCode: i.ruleCode,
              severity: i.severity,
              assetName: i.assetName,
              message: i.message,
              status: i.status,
            },
          };
        } catch {
          return null;
        }
      })
      .filter(Boolean);

    if (validFeatures.length === 0) return;

    const valLayer = L.geoJSON(
      { type: 'FeatureCollection', features: validFeatures as any } as any,
      {
        pointToLayer: (feature, latlng) => {
          const isError = feature.properties.severity === 'ERROR';
          const color = isError ? '#ef4444' : '#f59e0b';
          return L.circleMarker(latlng, {
            radius: 8,
            color: color,
            weight: 3,
            fillColor: color,
            fillOpacity: 0.6,
            dashArray: '3, 3',
          });
        },
        style: (feature) => {
          const isError = feature?.properties?.severity === 'ERROR';
          const color = isError ? '#ef4444' : '#f59e0b';
          return {
            color: color,
            weight: 3,
            opacity: 0.85,
            dashArray: '4, 4',
            fillColor: color,
            fillOpacity: 0.25,
          };
        },
        onEachFeature: (feature, layer) => {
          const p = feature.properties;
          layer.bindTooltip(
            `<div class="p-1 font-sans">
              <div class="font-bold ${p.severity === 'ERROR' ? 'text-rose-400' : 'text-amber-400'} text-xs">
                [${p.severity}] ${p.ruleCode}
              </div>
              <div class="text-white text-xs font-semibold">${p.assetName}</div>
              <div class="text-slate-300 text-[11px] max-w-[200px]">${p.message}</div>
            </div>`,
            { direction: 'top', offset: [0, -5], opacity: 0.95 }
          );
        },
      }
    );

    valLayer.addTo(map);
    validationLayerRef.current = valLayer;
  }, [validationIssues]);

  return (
    <div id="map-wrapper" className="relative w-full h-full select-none">
      <div ref={mapContainerRef} className="w-full h-full z-0" />

      {/* Top Floating Map Tools Toolbar */}
      <div className="absolute top-3.5 right-3.5 z-20 flex items-center gap-2 no-print">
        {/* Basemap Switcher */}
        <BasemapControl
          currentBasemap={currentBasemap}
          onSelectBasemap={setCurrentBasemap}
          isAr={isAr}
        />

        {/* Spatial Bookmarks */}
        <BookmarksControl
          onFlyTo={handleFlyTo}
          getCurrentView={getCurrentView}
          isAr={isAr}
        />

        {/* MVT Vector Tiles Engine Status */}
        <div
          className="hidden sm:flex items-center gap-1.5 px-2.5 py-1.5 rounded-xl backdrop-blur-md shadow-lg border bg-slate-900/85 text-slate-300 border-slate-700/80 text-[11px] font-mono select-none"
          title={isAr ? 'محرك بلاطات الفيكتور PostGIS MVT بنطاق EPSG:3857 مع تخزين مؤقت 120 ثانية' : 'PostGIS MVT Dynamic Vector Tiles (EPSG:3857) with 120s HTTP cache'}
        >
          <Zap className="w-3.5 h-3.5 text-amber-400" />
          <span className="font-semibold text-slate-200">MVT</span>
          <span className="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-pulse" />
        </div>

        {/* Refresh Materialized Summaries Button */}
        <button
          type="button"
          onClick={handleRefreshSummaries}
          disabled={isRefreshingSummaries}
          className="p-2 rounded-xl backdrop-blur-md shadow-lg border bg-slate-900/85 hover:bg-slate-800 text-slate-200 border-slate-700/80 hover:border-slate-600 transition flex items-center justify-center disabled:opacity-50"
          title={isAr ? 'تحديث الإحصائيات والجداول التراكمية السريعة (Materialized Views)' : 'Refresh Materialized Summary Views'}
        >
          {isRefreshingSummaries ? (
            <Loader2 className="w-4 h-4 text-sky-400 animate-spin" />
          ) : (
            <RefreshCw className="w-4 h-4 text-sky-400" />
          )}
        </button>

        {/* Fullscreen Button */}
        <button
          type="button"
          onClick={toggleFullscreen}
          className="p-2 rounded-xl backdrop-blur-md shadow-lg border bg-slate-900/85 hover:bg-slate-800 text-slate-200 border-slate-700/80 hover:border-slate-600 transition flex items-center justify-center"
          title={isAr ? (isFullscreen ? 'إنهاء وضع ملء الشاشة' : 'ملء الشاشة') : (isFullscreen ? 'Exit Fullscreen' : 'Fullscreen')}
        >
          {isFullscreen ? (
            <Minimize2 className="w-4 h-4 text-sky-400" />
          ) : (
            <Maximize2 className="w-4 h-4 text-sky-400" />
          )}
        </button>

        {/* Print / Export Map Image Button */}
        <button
          type="button"
          onClick={handlePrint}
          className="p-2 rounded-xl backdrop-blur-md shadow-lg border bg-slate-900/85 hover:bg-slate-800 text-slate-200 border-slate-700/80 hover:border-slate-600 transition flex items-center justify-center"
          title={isAr ? 'طباعة / تصدير صورة الخريطة' : 'Print / Export Map Image'}
        >
          <Printer className="w-4 h-4 text-emerald-400" />
        </button>
      </div>

      {/* Floating Banner when Polygon Selection is active */}
      {isSelectingPolygon && (
        <div className="absolute top-4 left-1/2 -translate-x-1/2 z-20 bg-sky-950/90 backdrop-blur-md border border-sky-500/50 text-white text-xs px-4 py-2 rounded-xl shadow-xl flex items-center gap-3 animate-pulse no-print">
          <div className="w-2.5 h-2.5 rounded-full bg-sky-400 animate-ping" />
          <span className="font-medium">
            {isAr
              ? 'انقر على الخريطة لرسم مضلع التحديد المكاني (انقر مرتين للإنهاء)'
              : 'Click on the map to draw selection polygon (Double-click to finish)'}
          </span>
        </div>
      )}

      {/* Floating Banner when Measurement Drawing is active */}
      {isMeasuring && (
        <div className="absolute top-4 left-1/2 -translate-x-1/2 z-20 bg-sky-950/90 backdrop-blur-md border border-sky-400/60 text-white text-xs px-4 py-2 rounded-xl shadow-xl flex items-center gap-3 animate-pulse no-print">
          <div className="w-2.5 h-2.5 rounded-full bg-sky-400 animate-ping" />
          <span className="font-medium">
            {isAr
              ? isMeasuring === 'LINE'
                ? 'انقر على الخريطة لرسم مسار القياس (انقر مرتين للإنهاء)'
                : 'انقر على الخريطة لرسم مضلع قياس المساحة (انقر مرتين للإنهاء)'
              : isMeasuring === 'LINE'
              ? 'Click to draw measure path (Double-click to finish)'
              : 'Click to draw measure area (Double-click to finish)'}
          </span>
        </div>
      )}

      {/* Bottom Coordinates & Scale Info Bar */}
      <div className="no-print">
        <LiveCoordinatesBar
          lat={cursorCoords?.lat ?? null}
          lon={cursorCoords?.lon ?? null}
          zoom={currentZoom}
          isAr={isAr}
        />
      </div>

      {/* Watermark Attribution on Print */}
      <div className="hidden print:flex fixed bottom-3 left-4 right-4 z-50 bg-white/95 text-slate-900 border border-slate-300 rounded-lg p-2.5 text-xs justify-between items-center shadow">
        <div className="flex items-center gap-2">
          <span className="font-bold">ACUD GeoManager — New Administrative Capital GIS</span>
          <span className="text-slate-400">|</span>
          <span className="font-medium">{BASEMAPS[currentBasemap]?.nameEn || 'OpenStreetMap'}</span>
        </div>
        <div className="text-[11px] text-slate-600 font-mono">
          {BASEMAPS[currentBasemap]?.attribution.replace(/<[^>]+>/g, '')}
        </div>
      </div>
    </div>
  );
};
