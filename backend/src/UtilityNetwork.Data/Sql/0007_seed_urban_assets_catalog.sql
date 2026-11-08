-- 0007_seed_urban_assets_catalog.sql
-- Seed the Urban Assets network and its feature types catalog

INSERT INTO networks (id, code, name, domain, is_directed, snap_tolerance_m)
VALUES (
    'a0000000-0000-0000-0000-000000000002'::uuid,
    'URBAN_ASSETS',
    'New Administrative Capital Urban Assets',
    'URBAN',
    false,
    0.5
)
ON CONFLICT (code) DO UPDATE
SET name = EXCLUDED.name,
    domain = EXCLUDED.domain,
    is_directed = EXCLUDED.is_directed,
    snap_tolerance_m = EXCLUDED.snap_tolerance_m;

-- Initialize topology_state for the urban assets network
INSERT INTO topology_state (network_id, is_dirty)
VALUES ('a0000000-0000-0000-0000-000000000002'::uuid, false)
ON CONFLICT (network_id) DO NOTHING;

-- FEATURE TYPES: BUILDING, ROAD, TREE, SMART_POLE, PARCEL
INSERT INTO feature_types (code, network_domain, geometry_kind, display_name, topology_role, attribute_schema, style, is_active)
VALUES
(
    'BUILDING',
    'URBAN',
    'POLYGON',
    'Building',
    'NONE',
    '{
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "type": "object",
        "properties": {
            "building_type": { "type": "string", "enum": ["RESIDENTIAL", "COMMERCIAL", "ADMINISTRATIVE", "GOVERNMENTAL", "EDUCATIONAL", "HEALTHCARE"] },
            "floors_count": { "type": "integer", "minimum": 1, "maximum": 120 },
            "construction_year": { "type": "integer", "minimum": 1900, "maximum": 2100 },
            "height_m": { "type": "number", "minimum": 0 }
        },
        "additionalProperties": false
    }'::jsonb,
    '{
        "fillColor": "#F59E0B",
        "fillOpacity": 0.4,
        "color": "#B45309",
        "weight": 2
    }'::jsonb,
    true
),
(
    'ROAD',
    'URBAN',
    'LINE',
    'Road Network',
    'NONE',
    '{
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "type": "object",
        "properties": {
            "road_class": { "type": "string", "enum": ["HIGHWAY", "ARTERIAL", "COLLECTOR", "LOCAL", "PEDESTRIAN"] },
            "lanes_count": { "type": "integer", "minimum": 1, "maximum": 16 },
            "speed_limit_kmh": { "type": "integer", "minimum": 10, "maximum": 160 },
            "width_m": { "type": "number", "minimum": 2, "maximum": 100 }
        },
        "additionalProperties": false
    }'::jsonb,
    '{
        "color": "#4B5563",
        "weight": 4,
        "opacity": 0.9
    }'::jsonb,
    true
),
(
    'TREE',
    'URBAN',
    'POINT',
    'Landscape Tree',
    'NONE',
    '{
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "type": "object",
        "properties": {
            "species": { "type": "string" },
            "height_m": { "type": "number", "minimum": 0, "maximum": 50 },
            "crown_diameter_m": { "type": "number", "minimum": 0, "maximum": 30 }
        },
        "additionalProperties": false
    }'::jsonb,
    '{
        "shape": "circle",
        "color": "#15803D",
        "radius": 5,
        "weight": 1.5,
        "fillColor": "#22C55E",
        "fillOpacity": 0.85
    }'::jsonb,
    true
),
(
    'SMART_POLE',
    'URBAN',
    'POINT',
    'Smart Lighting Pole',
    'NONE',
    '{
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "type": "object",
        "properties": {
            "pole_type": { "type": "string", "enum": ["STREET_LIGHT", "CCTV", "WIFI_HOTSPOT", "ENVIRONMENTAL_SENSOR", "INTEGRATED_SMART_POLE"] },
            "height_m": { "type": "number", "minimum": 3, "maximum": 30 },
            "has_cctv": { "type": "boolean" },
            "has_wifi": { "type": "boolean" }
        },
        "additionalProperties": false
    }'::jsonb,
    '{
        "shape": "circle",
        "color": "#D97706",
        "radius": 6,
        "weight": 2,
        "fillColor": "#FBBF24",
        "fillOpacity": 0.9
    }'::jsonb,
    true
),
(
    'PARCEL',
    'URBAN',
    'POLYGON',
    'Land Parcel',
    'NONE',
    '{
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "type": "object",
        "properties": {
            "land_use": { "type": "string", "enum": ["MIXED_USE", "RESIDENTIAL", "COMMERCIAL", "PUBLIC_UTILITY", "GREEN_SPACE"] },
            "zoning_code": { "type": "string" },
            "area_sqm": { "type": "number", "minimum": 0 }
        },
        "additionalProperties": false
    }'::jsonb,
    '{
        "fillColor": "#10B981",
        "fillOpacity": 0.15,
        "color": "#047857",
        "weight": 1.5,
        "dashArray": "3, 3"
    }'::jsonb,
    true
)
ON CONFLICT (code) DO UPDATE
SET display_name = EXCLUDED.display_name,
    topology_role = EXCLUDED.topology_role,
    attribute_schema = EXCLUDED.attribute_schema,
    style = EXCLUDED.style,
    is_active = EXCLUDED.is_active;
