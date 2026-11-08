'use client';

import React, { useEffect, useRef } from 'react';
import L from 'leaflet';
import '@geoman-io/leaflet-geoman-free';
import { FeatureTypeDto, GeoJsonFeature } from '@/lib/types';

interface MapComponentProps {
  features: GeoJsonFeature[];
  featureTypes: FeatureTypeDto[];
  visibleTypes: Set<string>;
  selectedFeatureId: string | null;
  canEdit: boolean;
  onSelectFeature: (feature: GeoJsonFeature) => void;
  onBboxChange: (bbox: [number, number, number, number]) => void;
  onDrawingComplete?: (geometry: {
    kind: 'POINT' | 'LINE' | 'POLYGON';
    coordinates: any;
  }) => void;
}

export const MapComponent: React.FC<MapComponentProps> = ({
  features,
  featureTypes,
  visibleTypes,
  selectedFeatureId,
  canEdit,
  onSelectFeature,
  onBboxChange,
  onDrawingComplete,
}) => {
  const mapContainerRef = useRef<HTMLDivElement>(null);
  const mapInstanceRef = useRef<L.Map | null>(null);
  const geojsonLayerRef = useRef<L.GeoJSON | null>(null);
  const debounceTimerRef = useRef<NodeJS.Timeout | null>(null);
  const onDrawingCompleteRef = useRef(onDrawingComplete);

  useEffect(() => {
    onDrawingCompleteRef.current = onDrawingComplete;
  }, [onDrawingComplete]);

  // Initialize Map
  useEffect(() => {
    if (!mapContainerRef.current || mapInstanceRef.current) return;

    // Center on New Administrative Capital (NAC)
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
      // Global snapping options: snap to existing vertices and midpoints within 15px
      pm.setGlobalOptions({
        snapping: true,
        snapDistance: 15,
        allowSelfIntersection: false,
      });

      // Handle drawing completion
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

          // Close polygon ring if needed
          if (ringCoords.length > 0) {
            const first = ringCoords[0];
            const last = ringCoords[ringCoords.length - 1];
            if (first[0] !== last[0] || first[1] !== last[1]) {
              ringCoords.push([first[0], first[1]]);
            }
          }
          coordinates = [ringCoords];
        }

        // Remove temporary Geoman drawing layer from map
        map.removeLayer(layer);

        // Notify parent callback to open Create Feature modal
        if (onDrawingCompleteRef.current) {
          onDrawingCompleteRef.current({ kind, coordinates });
        }
      });
    }

    // Initial bbox emit
    const emitBbox = () => {
      const bounds = map.getBounds();
      onBboxChange([
        bounds.getWest(),
        bounds.getSouth(),
        bounds.getEast(),
        bounds.getNorth(),
      ]);
    };

    // Listen to moveend with debounce
    map.on('moveend', () => {
      if (debounceTimerRef.current) clearTimeout(debounceTimerRef.current);
      debounceTimerRef.current = setTimeout(emitBbox, 250);
    });

    mapInstanceRef.current = map;
    emitBbox();

    return () => {
      if (debounceTimerRef.current) clearTimeout(debounceTimerRef.current);
      map.remove();
      mapInstanceRef.current = null;
    };
  }, []);

  // Update Geoman Drawing Controls based on user role (canEdit)
  useEffect(() => {
    const map = mapInstanceRef.current;
    if (!map) return;
    const pm = (map as any).pm;
    if (!pm) return;

    if (canEdit) {
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
  }, [canEdit]);

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

    // Filter features by visibleTypes
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
          const isSelected = feature.id === selectedFeatureId;

          return L.circleMarker(latlng, {
            radius: isSelected ? (style.radius || 7) + 3 : style.radius || 7,
            fillColor: isSelected ? '#F59E0B' : style.fillColor || style.color || '#3B82F6',
            color: isSelected ? '#FFFFFF' : style.color || '#1E40AF',
            weight: isSelected ? 3 : style.weight || 2,
            opacity: 1,
            fillOpacity: style.fillOpacity || 0.85,
          });
        },
        style: (feature) => {
          if (!feature) return {};
          const typeCode = feature.properties?.featureTypeCode;
          const style = typeStyles.get(typeCode) || {};
          const isSelected = feature.id === selectedFeatureId;

          return {
            color: isSelected ? '#F59E0B' : style.color || '#2563EB',
            weight: isSelected ? (style.weight || 3) + 2 : style.weight || 3,
            opacity: style.opacity || 0.9,
            fillColor: isSelected ? '#FBBF24' : style.fillColor || style.color || '#3B82F6',
            fillOpacity: isSelected ? 0.5 : style.fillOpacity || 0.25,
            dashArray: style.dashArray || undefined,
          };
        },
        onEachFeature: (feature, layer) => {
          // Tooltip on hover
          const name = feature.properties?.name || 'Feature';
          const type = feature.properties?.featureTypeCode || '';
          layer.bindTooltip(
            `<div class="font-bold text-xs">${name}</div><div class="text-[10px] text-slate-500 font-mono">${type}</div>`,
            { direction: 'top', offset: [0, -5], opacity: 0.9 }
          );

          // Click handler to select
          layer.on('click', () => {
            onSelectFeature(feature as any);
          });
        },
      }
    );

    geoJsonLayer.addTo(map);
    geojsonLayerRef.current = geoJsonLayer;
  }, [features, featureTypes, visibleTypes, selectedFeatureId]);

  return (
    <div className="relative w-full h-full">
      <div ref={mapContainerRef} className="w-full h-full z-0" />
    </div>
  );
};
