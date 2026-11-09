'use client';

import React, { useEffect, useRef } from 'react';
import L from 'leaflet';
import '@geoman-io/leaflet-geoman-free';
import { FeatureTypeDto, GeoJsonFeature, ValidationIssueDto } from '@/lib/types';

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
  onSelectFeature,
  onBboxChange,
  onCenterChange,
  onDrawingComplete,
  onPolygonSelected,
}) => {
  const mapContainerRef = useRef<HTMLDivElement>(null);
  const mapInstanceRef = useRef<L.Map | null>(null);
  const geojsonLayerRef = useRef<L.GeoJSON | null>(null);
  const validationLayerRef = useRef<L.GeoJSON | null>(null);
  const debounceTimerRef = useRef<NodeJS.Timeout | null>(null);

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
    onCenterChangeRef.current = onCenterChange;
  }, [onCenterChange]);

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

    // OpenStreetMap Basemap Layer
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors | ACUD',
      maxZoom: 19,
    }).addTo(map);

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

  // Update GeoJSON features on map
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

          let fillColor = style.fillColor || style.color || '#3B82F6';
          let strokeColor = style.color || '#1E40AF';
          let radius = style.radius || 7;
          let weight = style.weight || 2;

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

          let strokeColor = style.color || '#2563EB';
          let fillColor = style.fillColor || style.color || '#3B82F6';
          let weight = style.weight || 3;
          let fillOpacity = style.fillOpacity || 0.25;

          if (isHighlighted) {
            strokeColor = '#06B6D4';
            fillColor = '#22D3EE';
            weight = 5;
            fillOpacity = 0.55;
          } else if (isSelected) {
            strokeColor = '#F59E0B';
            fillColor = '#FBBF24';
            weight = 5;
            fillOpacity = 0.5;
          }

          return {
            color: strokeColor,
            weight,
            opacity: 0.95,
            fillColor,
            fillOpacity,
            dashArray: style.dashArray || undefined,
          };
        },
        onEachFeature: (feature, layer) => {
          const name = feature.properties?.name || 'Feature';
          const type = feature.properties?.featureTypeCode || '';
          const dist = feature.properties?.distanceMeters ?? (feature.properties as any)?.distance_meters;
          const distStr = dist !== undefined ? `<div class="text-[10px] text-amber-400 font-mono font-bold mt-0.5">🎯 ${Number(dist).toFixed(1)}m</div>` : '';

          layer.bindTooltip(
            `<div class="font-bold text-xs">${name}</div><div class="text-[10px] text-slate-500 font-mono">${type}</div>${distStr}`,
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
    <div className="relative w-full h-full">
      <div ref={mapContainerRef} className="w-full h-full z-0" />

      {/* Floating Banner when Polygon Selection is active */}
      {isSelectingPolygon && (
        <div className="absolute top-4 left-1/2 -translate-x-1/2 z-20 bg-sky-950/90 backdrop-blur-md border border-sky-500/50 text-white text-xs px-4 py-2 rounded-xl shadow-xl flex items-center gap-3 animate-pulse">
          <div className="w-2.5 h-2.5 rounded-full bg-sky-400 animate-ping" />
          <span className="font-medium">
            انقر على الخريطة لرسم مضلع التحديد المكاني (Double click to finish)
          </span>
        </div>
      )}
    </div>
  );
};
