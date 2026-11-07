-- 0005_seed_water_catalog.sql
-- Seed the primary water network and its feature types catalog

INSERT INTO networks (id, code, name, domain, is_directed, snap_tolerance_m)
VALUES (
    'a0000000-0000-0000-0000-000000000001'::uuid,
    'WATER_DISTRIBUTION',
    'New Administrative Capital Water Network',
    'WATER',
    true,
    0.5
)
ON CONFLICT (code) DO UPDATE
SET name = EXCLUDED.name,
    domain = EXCLUDED.domain,
    is_directed = EXCLUDED.is_directed,
    snap_tolerance_m = EXCLUDED.snap_tolerance_m;

-- Initialize topology_state for the water network
INSERT INTO topology_state (network_id, is_dirty)
VALUES ('a0000000-0000-0000-0000-000000000001'::uuid, true)
ON CONFLICT (network_id) DO NOTHING;

-- FEATURE TYPES
INSERT INTO feature_types (code, network_domain, geometry_kind, display_name, topology_role, attribute_schema, style, is_active)
VALUES
(
    'VALVE',
    'WATER',
    'POINT',
    'Control Valve',
    'CONTROL',
    '{
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "type": "object",
        "properties": {
            "valve_type": { "type": "string", "enum": ["GATE", "BUTTERFLY", "CHECK", "PRESSURE_REDUCING", "AIR_RELEASE"] },
            "depth_m": { "type": "number", "minimum": 0, "maximum": 20 },
            "turns_to_close": { "type": "integer", "minimum": 1 }
        },
        "additionalProperties": false
    }'::jsonb,
    '{
        "shape": "circle",
        "color": "#1E40AF",
        "radius": 7,
        "weight": 2,
        "fillColor": "#3B82F6",
        "fillOpacity": 0.9
    }'::jsonb,
    true
),
(
    'HYDRANT',
    'WATER',
    'POINT',
    'Fire Hydrant',
    'TERMINAL',
    '{
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "type": "object",
        "properties": {
            "flow_rate_lps": { "type": "number", "minimum": 0 },
            "outlet_diameter_mm": { "type": "integer", "minimum": 50 }
        },
        "additionalProperties": false
    }'::jsonb,
    '{
        "shape": "circle",
        "color": "#991B1B",
        "radius": 8,
        "weight": 2,
        "fillColor": "#EF4444",
        "fillOpacity": 0.9
    }'::jsonb,
    true
),
(
    'FITTING',
    'WATER',
    'POINT',
    'Pipe Fitting',
    'JUNCTION',
    '{
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "type": "object",
        "properties": {
            "fitting_type": { "type": "string", "enum": ["TEE", "ELBOW", "CROSS", "REDUCER", "CAP"] }
        },
        "additionalProperties": false
    }'::jsonb,
    '{
        "shape": "square",
        "color": "#374151",
        "radius": 5,
        "weight": 1.5,
        "fillColor": "#6B7280",
        "fillOpacity": 0.85
    }'::jsonb,
    true
),
(
    'RESERVOIR',
    'WATER',
    'POINT',
    'Water Reservoir',
    'SOURCE',
    '{
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "type": "object",
        "properties": {
            "capacity_m3": { "type": "number", "minimum": 0 },
            "water_level_m": { "type": "number", "minimum": 0 }
        },
        "additionalProperties": false
    }'::jsonb,
    '{
        "shape": "pentagon",
        "color": "#0369A1",
        "radius": 12,
        "weight": 2.5,
        "fillColor": "#0EA5E9",
        "fillOpacity": 0.9
    }'::jsonb,
    true
),
(
    'PUMP_STATION',
    'WATER',
    'POINT',
    'Booster Pump Station',
    'SOURCE',
    '{
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "type": "object",
        "properties": {
            "power_kw": { "type": "number", "minimum": 0 },
            "pumps_count": { "type": "integer", "minimum": 1 }
        },
        "additionalProperties": false
    }'::jsonb,
    '{
        "shape": "diamond",
        "color": "#0F766E",
        "radius": 11,
        "weight": 2,
        "fillColor": "#14B8A6",
        "fillOpacity": 0.9
    }'::jsonb,
    true
),
(
    'PIPE',
    'WATER',
    'LINE',
    'Water Main Pipe',
    'NONE',
    '{
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "type": "object",
        "properties": {
            "installation_year": { "type": "integer", "minimum": 1900, "maximum": 2100 },
            "pressure_bar": { "type": "number", "minimum": 0, "maximum": 50 }
        },
        "additionalProperties": false
    }'::jsonb,
    '{
        "color": "#2563EB",
        "weight": 3.5,
        "opacity": 0.85
    }'::jsonb,
    true
),
(
    'SERVICE_ZONE',
    'WATER',
    'POLYGON',
    'Pressure Service Zone',
    'NONE',
    '{
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "type": "object",
        "properties": {
            "target_pressure_bar": { "type": "number", "minimum": 0 },
            "population_served": { "type": "integer", "minimum": 0 }
        },
        "additionalProperties": false
    }'::jsonb,
    '{
        "fillColor": "#3B82F6",
        "fillOpacity": 0.2,
        "color": "#1D4ED8",
        "weight": 2,
        "dashArray": "4, 4"
    }'::jsonb,
    true
)
ON CONFLICT (code) DO UPDATE
SET display_name = EXCLUDED.display_name,
    topology_role = EXCLUDED.topology_role,
    attribute_schema = EXCLUDED.attribute_schema,
    style = EXCLUDED.style,
    is_active = EXCLUDED.is_active;
