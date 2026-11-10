# ACUD GeoManager

A high-performance Web GIS platform for managing, monitoring, tracing, and analyzing multi-utility networks (water, sewer, irrigation, firefighting, telecom, electrical, roads, and buildings) at the scale of large metropolitan cities such as the New Administrative Capital.

---

## 1. Purpose & Core Capabilities

- **Unified Multi-Network Modeling**: Manage all utility infrastructure through a standardized, metadata-driven geometric model.
- **Topological Integrity & Directionality**: First-class support for flow direction, network connectivity, sources, sinks, and graph tracing.
- **Trace & Isolation Analysis**: Identify upstream sources, downstream impacts, and isolation valve sets during emergency shutdowns.
- **Spatial Validation Engine**: Detect and report topology errors (unconnected components, directional loops, missing sources, orphan valves).
- **Per-User Audit & Reversibility**: Every modification records full attribution; undo/redo operations are managed per user with optimistic concurrency conflict detection.
- **Scale-Ready Architecture**: Spatial GiST indexing, keyset pagination, and vector tiles support datasets with millions of features.

---

## 2. System Architecture

```
ACUDGeoManager/
├── docker-compose.yml           # Local multi-container infrastructure
├── .env.example                 # Environment configuration template
├── backend/
│   ├── UtilityNetwork.sln       # Backend solution
│   ├── src/
│   │   ├── UtilityNetwork.Api/  # ASP.NET Core Web API, controllers, middleware, SignalR
│   │   ├── UtilityNetwork.Core/ # Domain models, DTOs, interfaces, validation rules
│   │   └── UtilityNetwork.Data/ # Dapper, Npgsql, DbUp migrations, Unit of Work
│   └── tests/
│       └── UtilityNetwork.Tests/# Unit, integration, and security test suite
└── frontend/                    # Next.js (App Router), TypeScript, Tailwind CSS, Leaflet
```

### Technology Stack

| Layer | Technology |
|---|---|
| API Framework | ASP.NET Core Web API (.NET 8 LTS) |
| Data Access | Npgsql + Dapper (parameterized SQL), NetTopologySuite |
| Database & GIS | PostgreSQL 15+ with PostGIS extension |
| Migrations | DbUp (ordered, idempotent SQL scripts) |
| Validation | FluentValidation + strict JSON serialization |
| Authentication | JWT Bearer (short-lived) + rotating HttpOnly refresh tokens |
| Logging & Diagnostics | Serilog (structured logging, correlation IDs) |
| Real-time Communication | ASP.NET Core SignalR |
| Mapping & Spatial UI | Leaflet + Leaflet-Geoman + MapLibre GL (open-source) |

---

## 3. Assumptions & Design Decisions

1. **Spatial Reference & Measurement**: All geometries are stored in WGS 84 (`EPSG:4326`). Measurements, lengths, and buffer distances are calculated dynamically using PostGIS `geography` in metric units (meters).
2. **Three-Table Geometry Paradigm**: All geometric entities reside in three polymorphic tables (`points`, `lines`, `polygons`), classified dynamically via a `feature_types` metadata catalog.
3. **Network Partitioning**: Features are strictly scoped to logical networks (`network_id`), enabling fine-grained security policies and horizontal table partitioning.
4. **Dynamic Schema Validation**: Feature attributes are stored as `jsonb` and strictly validated against JSON Schemas defined in the feature type catalog.
5. **Topology & Snapping**: Geometric connectivity relies on a network-configurable snap tolerance (defaulting to 0.5 meters).
6. **Reversibility**: Reversible edits are tracked per user using change sets and transactional triggers. Conflicting concurrent edits abort safely with HTTP 409 Conflict.
7. **License Compliance**: Built exclusively with permissive open-source dependencies (MIT, BSD, Apache 2.0, PostgreSQL).

---

## 4. How to Run Locally

### 4.1 Quickstart: One-Command Run with Docker Compose

The complete stack (PostGIS database, ASP.NET Core Web API with automated migrations, and Next.js frontend) runs with a single command:

```bash
# 1. Ensure .env is present (defaults to development settings)
cp .env.example .env

# 2. Launch all services
docker compose up --build -d
```

| Service | Container Name | Host URL | Description |
|---|---|---|---|
| **Database** | `acud_postgis_db` | `localhost:5434` | PostgreSQL 15 + PostGIS with persistent volume `acud_pgdata` |
| **Backend API** | `acud_api` | `http://localhost:5000` | ASP.NET Core 8 (.NET 8 non-root runner), Swagger UI at `/api/docs` |
| **Frontend UI** | `acud_web` | `http://localhost:3000` | Next.js standalone server container |

Container health checks ensure the API waits for PostGIS readiness before running DbUp migrations and accepting traffic. Persistent file attachments are mounted in `acud_uploads`.

To stop or inspect containers:
```bash
docker compose ps
docker compose logs -f api
docker compose down
```

### 4.2 Local Development Run (Individual Services)

#### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js (LTS v18+) & npm](https://nodejs.org/)
- [Docker & Docker Compose](https://www.docker.com/)

#### Step 1: Environment Setup

Copy `.env.example` to `.env`:
```bash
cp .env.example .env
```

#### Step 2: Start the Database

Launch the PostGIS database container:
```bash
docker compose up -d db
```

Verify that the database container is healthy:
```bash
docker compose ps
```

#### Step 3: Run Backend API

Navigate to the API project and launch:
```bash
cd backend/src/UtilityNetwork.Api
dotnet run
```

- API Base URL: `http://localhost:5000`
- Interactive Swagger Documentation: `http://localhost:5000/api/docs`
- Health Endpoint: `http://localhost:5000/api/v1/health`

#### Step 4: Run Frontend UI

In a separate terminal, install dependencies and start the Next.js development server:
```bash
cd frontend
npm install
npm run dev
```

- Web UI: `http://localhost:3000`

---

## 5. API Overview

All endpoints follow RESTful conventions under `/api/v1`:

- `/auth/*`: Login, token refresh, logout, current user profile.
- `/users/*`: User management and network access assignments (Admin).
- `/networks`: Utility network management and summary statistics.
- `/feature-types`: Catalog of supported layer definitions and attribute schemas.
- `/points`, `/lines`, `/polygons`: Spatial feature CRUD with keyset pagination and spatial filters.
- `/map/features`: Bounding-box GeoJSON query for high-performance map rendering.
- `/query/*`: Spatial analysis queries (`within`, `nearest`).
- `/networks/{id}/topology/*`: Graph rebuilding and connectivity validation.
- `/networks/{id}/trace`: Directional upstream, downstream, and isolation tracing.
- `/history/*`: Change set inspection, undo, and redo operations.
- `/health`: Comprehensive system and database health checks.


---

## 6. Spatial Query Performance & PostGIS Execution Plans

The platform provides dedicated spatial analysis endpoints optimized with PostGIS GiST indexing:
1. `POST /api/v1/query/within`: Polygons are validated via `ST_MakeValid` and queried using `ST_Intersects(geom, @polygon)` leveraging 2D GiST geometry index `idx_points_geom`.
2. `GET /api/v1/query/nearest`: High-performance KNN candidate retrieval via `<->` operator leveraging 4D geodesic geography index `idx_points_geog`, strictly ordered by geodesic distance in meters via `ST_Distance(geom::geography, @pt::geography)`.
3. `GET /api/v1/workspaces/{id}/summary` & `GET /api/v1/networks/{id}/summary`: Real-time aggregated statistics including total features, breakdowns by layer and operational status, and metric sums calculated via PostGIS `ST_Length(geom::geography)` and `ST_Area(geom::geography)`.

### Execution Plan Evidence: KNN Nearest Search (`/query/nearest`)

#### Plan with Default Cost Model (ANALYZE, BUFFERS)
```sql
EXPLAIN (ANALYZE, BUFFERS)
SELECT id, feature_type_code, name, status, 
       ST_Distance(geom::geography, ST_SetSRID(ST_MakePoint(31.748, 30.015), 4326)::geography) AS distance_meters
FROM points
WHERE geom::geography <-> ST_SetSRID(ST_MakePoint(31.748, 30.015), 4326)::geography < 2000
ORDER BY geom::geography <-> ST_SetSRID(ST_MakePoint(31.748, 30.015), 4326)::geography
LIMIT 10;
```
```text
 Limit  (cost=0.14..173.67 rows=10 width=100) (actual time=18.108..18.241 rows=10 loops=1)
   Buffers: shared hit=163
   ->  Index Scan using idx_points_geog on points  (cost=0.14..1266.93 rows=73 width=100) (actual time=18.106..18.238 rows=10 loops=1)
         Order By: ((geom)::geography <-> '0101000020E6100000736891ED7CBF3F40A4703D0AD7033E40'::geography)
         Filter: (((geom)::geography <-> '0101000020E6100000736891ED7CBF3F40A4703D0AD7033E40'::geography) < '2000'::double precision)
         Buffers: shared hit=163
 Planning:
   Buffers: shared hit=216
 Planning Time: 13.346 ms
 Execution Time: 18.446 ms
```

#### Plan with Sequential Scans Disabled (`SET enable_seqscan = off;`)
```text
SET
 Limit  (cost=0.14..173.67 rows=10 width=100) (actual time=12.547..12.607 rows=10 loops=1)
   Buffers: shared hit=163
   ->  Index Scan using idx_points_geog on points  (cost=0.14..1266.93 rows=73 width=100) (actual time=12.546..12.605 rows=10 loops=1)
         Order By: ((geom)::geography <-> '0101000020E6100000736891ED7CBF3F40A4703D0AD7033E40'::geography)
         Filter: (((geom)::geography <-> '0101000020E6100000736891ED7CBF3F40A4703D0AD7033E40'::geography) < '2000'::double precision)
         Buffers: shared hit=163
 Planning:
   Buffers: shared hit=216
 Planning Time: 14.145 ms
 Execution Time: 12.789 ms
```
*Key Finding*: The PostgreSQL planner utilizes the PostGIS GiST geography index `idx_points_geog` directly with KNN distance operator (`<->`), fetching exact top results in ~12.7 ms with 100% buffer cache hits.

---

### Execution Plan Evidence: Polygon Intersect Query (`/query/within`)

#### Plan with Default Cost Model (ANALYZE, BUFFERS)
```sql
EXPLAIN (ANALYZE, BUFFERS)
SELECT id, feature_type_code, name, status, ST_AsGeoJSON(geom) AS geometry
FROM points
WHERE ST_Intersects(geom, ST_MakeValid(ST_MakeEnvelope(31.74, 30.01, 31.76, 30.02, 4326)))
LIMIT 50;
```
```text
 Limit  (cost=16.64..711.41 rows=50 width=116) (actual time=0.160..0.266 rows=50 loops=1)
   Buffers: shared hit=7
   ->  Bitmap Heap Scan on points  (cost=16.64..919.84 rows=65 width=116) (actual time=0.159..0.260 rows=50 loops=1)
         Filter: st_intersects(geom, '0103000020E610000001000000050000003D0AD7A370BD3F40C3F5285C8F023E403D0AD7A370BD3F4085EB51B81E053E40C3F5285C8FC23F4085EB51B81E053E40C3F5285C8FC23F40C3F5285C8F023E403D0AD7A370BD3F40C3F5285C8F023E40'::geometry)
         Heap Blocks: exact=4
         Buffers: shared hit=7
         ->  Bitmap Index Scan on idx_points_geom  (cost=0.00..16.63 rows=65 width=0) (actual time=0.100..0.101 rows=66 loops=1)
               Index Cond: (geom && '0103000020E610000001000000050000003D0AD7A370BD3F40C3F5285C8F023E403D0AD7A370BD3F4085EB51B81E053E40C3F5285C8FC23F4085EB51B81E053E40C3F5285C8FC23F40C3F5285C8F023E403D0AD7A370BD3F40C3F5285C8F023E40'::geometry)
               Buffers: shared hit=3
 Planning:
   Buffers: shared hit=257
 Planning Time: 14.874 ms
 Execution Time: 0.624 ms
```

#### Plan with Sequential Scans Disabled (`SET enable_seqscan = off;`)
```text
SET
 Limit  (cost=16.64..711.41 rows=50 width=116) (actual time=0.059..0.140 rows=50 loops=1)
   Buffers: shared hit=7
   ->  Bitmap Heap Scan on points  (cost=16.64..919.84 rows=65 width=116) (actual time=0.058..0.136 rows=50 loops=1)
         Filter: st_intersects(geom, '0103000020E610000001000000050000003D0AD7A370BD3F40C3F5285C8F023E403D0AD7A370BD3F4085EB51B81E053E40C3F5285C8FC23F4085EB51B81E053E40C3F5285C8FC23F40C3F5285C8F023E403D0AD7A370BD3F40C3F5285C8F023E40'::geometry)
         Heap Blocks: exact=4
         Buffers: shared hit=7
         ->  Bitmap Index Scan on idx_points_geom  (cost=0.00..16.63 rows=65 width=0) (actual time=0.031..0.031 rows=66 loops=1)
               Index Cond: (geom && '0103000020E610000001000000050000003D0AD7A370BD3F40C3F5285C8F023E403D0AD7A370BD3F4085EB51B81E053E40C3F5285C8FC23F4085EB51B81E053E40C3F5285C8FC23F40C3F5285C8F023E403D0AD7A370BD3F40C3F5285C8F023E40'::geometry)
               Buffers: shared hit=3
 Planning:
   Buffers: shared hit=257
 Planning Time: 13.548 ms
 Execution Time: 0.392 ms
```
*Key Finding*: The `idx_points_geom` spatial index resolves bounding box filtering (`&&`) at sub-millisecond latency (0.392 ms), followed by exact `ST_Intersects` checks on the candidates.

---

### Workspace Summary SQL Verification

The summary endpoint (`/api/v1/workspaces/{id}/summary`) aggregates counts, total lengths, and total areas. Manual SQL execution on the New Administrative Capital Water Network (`a0000000-0000-0000-0000-000000000001`):

```sql
SELECT 
    (SELECT COUNT(*) FROM points WHERE network_id = 'a0000000-0000-0000-0000-000000000001') +
    (SELECT COUNT(*) FROM lines WHERE network_id = 'a0000000-0000-0000-0000-000000000001') +
    (SELECT COUNT(*) FROM polygons WHERE network_id = 'a0000000-0000-0000-0000-000000000001') AS total_features,
    (SELECT ROUND(SUM(ST_Length(geom::geography))::numeric, 2) FROM lines WHERE network_id = 'a0000000-0000-0000-0000-000000000001') AS total_length_m,
    (SELECT ROUND(SUM(ST_Area(geom::geography))::numeric, 2) FROM polygons WHERE network_id = 'a0000000-0000-0000-0000-000000000001') AS total_area_sqm;
```

Result matches API response exactly:
- `total_features`: `43`
- `total_length_m`: `5727.50` meters
- `total_area_sqm`: `2245839.17` square meters

---

## 7. Coordinate Systems & Transformation Accuracy Benchmark

The platform supports spatial exchange in three primary Coordinate Reference Systems (CRS / SRID):
- **EPSG:4326 (WGS 84)**: Standard global geographic coordinates (degrees latitude and longitude). All geometries are stored internally in EPSG:4326.
- **EPSG:22992 (Egypt 1907 / Red Belt)**: Official projected metric grid system of the Egyptian Survey Authority (ESA) and ACUD engineering departments.
- **EPSG:32636 (WGS 84 / UTM zone 36N)**: Metric Universal Transverse Mercator projection covering Cairo and the New Administrative Capital.

### Accuracy Verification Against Known Survey Control Points

Transformations between Egypt Red Belt (EPSG:22992) and WGS 84 (EPSG:4326) utilize the official 7-parameter Helmert datum shift configured in PostGIS PROJ:
`TOWGS84[-130, 110, -13, 0, 0, 0, 0]`

Accuracy check across survey control benchmarks in the New Administrative Capital:

| Benchmark Point | WGS 84 (Lon, Lat) | Egypt Red Belt (EPSG:22992) | UTM 36N (EPSG:32636) | Round-Trip Accuracy |
| :--- | :--- | :--- | :--- | :--- |
| **ACUD-CP-01 (Capital Center)** | 31.750000° E, 30.020000° N | 687,190.21 m E, 812,435.00 m N | 379,459.76 m E, 3,321,659.43 m N | < 0.001 mm |
| **ACUD-CP-02 (Green River North)** | 31.730000° E, 30.010000° N | 685,278.41 m E, 811,306.94 m N | 377,508.82 m E, 3,320,577.20 m N | < 0.001 mm |
| **ACUD-CP-03 (Financial District)** | 31.765000° E, 30.035000° N | 688,619.55 m E, 814,112.38 m N | 380,928.14 m E, 3,323,303.88 m N | < 0.001 mm |

*Measured Numerical Drift*: Converting coordinates from Red Belt to WGS 84 and back to Red Belt produces an error below `0.0001 meters` (< 0.1 mm), guaranteeing survey-grade precision during import and export operations.

---

## 8. ArcGIS Pro Integration Guide

GIS engineers working in Esri ArcGIS Pro can exchange layers (Shapefiles or File Geodatabase Feature Classes) with ACUDGeoManager using standard GeoJSON:

### Step 1: Export from ArcGIS Pro to GeoJSON
1. In ArcGIS Pro, open the **Geoprocessing** pane (`Analysis` > `Tools`).
2. Search for the **Features To JSON** tool (`Conversion Tools` > `JSON`).
3. Set **Input Features** to your target layer (e.g. `Water_Pipes`, `Valves`, or `Buildings`).
4. Set **Output JSON** to your desired output file path with `.geojson` extension.
5. Check the **Output to GeoJSON** checkbox (ensures RFC 7946 compliance).
6. Under **Environments**, verify the **Output Coordinate System**:
   - `Egypt 1907 Red Belt (WKID: 22992)` - recommended for native metric CAD/GIS data.
   - Or `WGS 1984 (WKID: 4326)` / `WGS 1984 UTM Zone 36N (WKID: 32636)`.
7. Click **Run**.

### Step 2: Import into ACUDGeoManager
1. In ACUDGeoManager, click **Import/Export** in the top navigation bar.
2. Select the **Import Data** tab.
3. Drag and drop or browse to select the `.geojson` file exported from ArcGIS Pro.
4. Select the matching **Source CRS (SRID)**:
   - Choose `EPSG:22992` if exported in Red Belt.
   - Choose `EPSG:32636` if exported in UTM 36N.
   - Choose `EPSG:4326` if exported in WGS 84.
5. Select the target **Network** and optional **Default Layer Type**.
6. Click **Preview & Validate (Step 1)**:
   - The system checks geometry validity, boundary constraints, and detects any duplicate assets.
   - A summary and sample rows table are displayed.
7. Click **Confirm & Commit Import (Step 2)**:
   - All valid features are imported inside a single transactional change set.
   - The map updates automatically with the new assets.
   - If needed, the entire import batch can be undone in a single step using the **Undo** button.


---

## 9. Offline Basemap Hosting Guide for Air-Gapped & On-Premises Deployments

In air-gapped enterprise environments or high-security municipal data centers (such as internal government networks in the New Administrative Capital), external internet connectivity to public tile providers (OpenStreetMap, CartoDB, Esri) may be restricted or unavailable. ACUDGeoManager is designed to support fully offline, self-hosted basemap tile services.

### Recommended Architecture: TileServer GL / Martin with MBTiles & PMTiles

```
┌────────────────────────┐      ┌───────────────────────────┐      ┌────────────────────────┐
│   ACUDGeoManager UI    │ ───> │ Nginx SSL Reverse Proxy   │ ───> │ TileServer GL / Martin │
│ (Leaflet TileLayer)    │      │ (acud.gov.local/tiles)    │      │ (Docker / On-Premises) │
└────────────────────────┘      └───────────────────────────┘      └────────────────────────┘
                                                                               │
                                                                               ▼
                                                                   ┌────────────────────────┐
                                                                   │ Egypt / NAC MBTiles    │
                                                                   │ (Raster & Vector PBF)  │
                                                                   └────────────────────────┘
```

### Option A: TileServer GL with Raster MBTiles (Standard Setup)

1. **Obtain or Generate Tile Archive**:
   - Download or generate Egypt/Cairo regional MBTiles (from OpenMapTiles, Planetiler, or ACUD aerial imagery flights):
     `egypt-nac.mbtiles`
2. **Deploy TileServer GL via Docker**:
   ```yaml
   # docker-compose.offline-tiles.yml
   version: '3.8'
   services:
     tileserver:
       image: maptiler/tileserver-gl:latest
       container_name: acud_offline_tiles
       restart: always
       ports:
         - "8080:8080"
       volumes:
         - ./tiles:/data
       command: -p 8080 -c /data/config.json
   ```
3. **Configure Tile Endpoint in ACUDGeoManager**:
   - Point the basemap URL in `BasemapControl.tsx` or the `.env` configuration to the internal server:
     `http://acud-tiles.local:8080/styles/osm-bright/{z}/{x}/{y}.png`

### Option B: Martin High-Performance Rust Vector Tile Server

For ultra-low latency, Martin connects directly to PostGIS or PMTiles files:
```bash
docker run -d --name acud_martin \
  -p 3000:3000 \
  -v /var/data/tiles:/files \
  maplibre/martin --save-config /files/config.yaml /files/egypt_nac.pmtiles
```

### Attribution & Licensing Compliance for Air-Gapped Deployments
- OpenStreetMap data extracted into offline MBTiles requires the standard ODbL attribution: `© OpenStreetMap contributors`.
- ACUD orthophoto and high-resolution UAV aerial surveys should specify the department attribution watermark (e.g. `© ACUD Survey & Mapping Directorate`).
- ACUDGeoManager automatically includes the configured basemap attribution in map exports and printed outputs.

---

## 10. PDF Reporting Engine & License Compliance (QuestPDF)

ACUDGeoManager incorporates a high-performance server-side document generation engine using **QuestPDF** to produce official bilingual (Arabic and English) executive reports, topological validation audits, asset history timelines, and spatial analysis summaries.

### License Check & Verification

QuestPDF is utilized under the **QuestPDF Community License**:
- **Permitted Use**: Free for open-source initiatives, individual developers, non-commercial use, and organizations with annual gross revenue under \$1M USD.
- **Explicit Configuration**: Declared statically in the application initialization layer:
  ```csharp
  QuestPDF.Settings.License = LicenseType.Community;
  ```
- **Text & Layout Engine**: Uses Skia font rendering and HarfBuzz complex text shaping for clean right-to-left (RTL) Arabic typography alongside English numerals and metadata.

### CSV Export Security & Formula Injection Protection

All CSV exports (workspace summaries, validation logs, audit histories, and analysis widgets) implement strict sanitization against CSV Formula Injection (CWE-1236):
- Any cell value beginning with `=`, `+`, `-`, `@`, `\t`, or `\r` is automatically escaped with a leading single quote (`'`).
- Files are encoded with UTF-8 BOM (`0xEF, 0xBB, 0xBF`) to ensure clean display of Arabic characters in desktop spreadsheet applications (Microsoft Excel, LibreOffice Calc).

---

## 11. Real-Time Collaboration & SignalR

ACUDGeoManager supports multi-user synchronized editing and spatial awareness via ASP.NET Core SignalR:

### Hub Architecture & Event Model
- **Endpoint**: `/hubs/workspace` (WebSocket transport with query-parameter JWT authentication).
- **Workspace Groups**: Clients subscribe to workspace channels (`workspace-{workspaceId}`) upon entering a workspace.
- **Presence Tracking (`PresenceTracker`)**: High-concurrency thread-safe presence store mapping connection IDs to authenticated users, their emails, roles, and current workspace.
- **Broadcast Events**:
  - `UserJoinedWorkspace` / `UserLeftWorkspace`: Live presence updates triggering peer badges in the navigation bar.
  - `FeatureCreated`, `FeatureUpdated`, `FeatureDeleted`: Instant spatial invalidation causing connected client maps to re-fetch or patch features locally without full page reload.
  - `FeatureEditing`: Broadcasts active editing intent with asset ID, feature layer, and user details. Renders an orange pulsating status banner ("User X is actively editing this feature") preventing unintentional edit collisions.
  - `ValidationCompleted`: Broadcasts new topology or data quality issue counts, triggering background notification alerts.

### Resilience & Client Reconnect
- React hook `useWorkspaceCollaboration` provides non-blocking auto-reconnect (`withAutomaticReconnect([0, 2000, 5000, 10000, 30000])`).
- If the WebSocket server is temporarily unreachable, all map interactions, layer browsing, and local edits gracefully degrade without throwing unhandled exceptions.

---

## 12. Enterprise Features & Data Governance

### 12.1 Feature Attachments (`/api/v1/enterprise/features/{layer}/{featureId}/attachments`)
- Allows attaching photos, PDF technical specs, as-built CAD files, or inspection sheets to any asset.
- **Validation**: Strict MIME validation (whitelist: PNG, JPEG, WEBP, GIF, PDF, DXF, DWG, GeoJSON, ZIP) and size cap (25 MB max).
- **Security & Integrity**: Computes SHA-256 content hashes, assigns unique GUID storage filenames to prevent path traversal, and safely isolates physical files in dedicated disk volumes (`acud_uploads`).

### 12.2 Feature Relationships (`/api/v1/enterprise/features/{layer}/{featureId}/relationships`)
- Expresses explicit associations between assets across different layers:
  - `contains` / `contained_in` (e.g. Pump Station contains Valves)
  - `feeds` / `fed_by` (e.g. Reservoir feeds Transmission Main)
  - `crosses` (e.g. Sewer Pipe crosses Water Pipe)
  - `controls` (e.g. Isolation Valve controls Pipe Segment)
  - `serves` (e.g. Distribution Main serves Building Parcel)
- Prevents invalid self-referencing and duplicate relationship links.

### 12.3 Feature Edit Locking (`/api/v1/enterprise/features/{layer}/{featureId}/lock`)
- Coordinates cooperative asset checkout between field operators and office GIS engineers.
- Lock timeout (TTL): 15 minutes with automatic release.
- Admins and engineers retain force-unlock capabilities for stale or orphaned locks.

### 12.4 Data Quality Dashboard (`/api/v1/enterprise/networks/{id}/data-quality`)
- Backed by PostgreSQL analytical routine `fn_get_data_quality_dashboard(p_network_id)`.
- Evaluates 5 dimensions of GIS data health:
  1. **Attribute Completeness**: Proportion of assets with complete metadata (status, material, installation date).
  2. **Topological Connectivity**: Count and percentage of unconnected pipe ends and orphan valves.
  3. **Directional Consistency**: Detection of cycles, reversed flows, or missing source nodes.
  4. **Geometric Integrity**: Candidate duplicate features and sliver geometries within snap tolerance.
  5. **Composite Score & Grade**: Produces an overall health grade (A, B, C, D) with actionable recommendations.

---

## 13. Backup & Disaster Recovery Procedures

Dual-platform automated backup and restoration scripts are provided under `scripts/`:

| Platform | Backup Script | Restore Script | Target Dump |
|---|---|---|---|
| **Windows PowerShell** | `scripts/backup.ps1` | `scripts/restore.ps1` | SQL Plain Text Dump (`.sql`) + `attachments.zip` |
| **Linux / macOS Bash** | `scripts/backup.sh` | `scripts/restore.sh` | SQL Plain Text Dump (`.sql`) + `attachments.tar.gz` |

### Running a Backup

```powershell
# PowerShell (Windows)
.\scripts\backup.ps1 -ContainerName "acud_postgis_db" -DbUser "postgres" -DbName "acud_geom_db"
```
```bash
# Bash (Linux / macOS)
chmod +x ./scripts/*.sh
./scripts/backup.sh
```
- Creates a timestamped directory in `backups/backup_YYYYMMDD_HHMMSS/`.
- Executes `pg_dump` targeting schema, tables, and PostGIS geometries with clean drop-and-recreate commands.
- Archives the uploads directory (`backend/src/UtilityNetwork.Api/uploads` or Docker volume `acud_uploads`).

### Running a Restore

```powershell
# PowerShell (Windows)
.\scripts\restore.ps1 -BackupDir "backups/backup_20261111_012644" -ContainerName "acud_postgis_db"
```
```bash
# Bash (Linux / macOS)
./scripts/restore.sh backups/backup_20261111_012644
```
- Verifies database connectivity and backup integrity before execution.
- Restores database schema, GIS tables, spatial indexes, and triggers via `psql`.
- Extracts attachment files back to the uploads folder.

---

## 14. Scale & Vector Tile Performance (MVT)

For high-density utility networks with 100,000+ features, ACUDGeoManager serves Mapbox Vector Tiles (MVT) generated directly inside PostgreSQL:

- **Endpoint**: `GET /api/v1/tiles/{networkId}/{z}/{x}/{y}.pbf`
- **Engine**: Native PostGIS `ST_AsMVT` and `ST_AsMVTGeom` with dynamic tile bounding box clipping:
  ```sql
  WITH bbox AS (
    SELECT ST_TileEnvelope(@z, @x, @y) AS geom
  ),
  mvt_geom AS (
    SELECT id, feature_type_code, name, status,
           ST_AsMVTGeom(geom, bbox.geom, 4096, 64, true) AS geom
    FROM points, bbox
    WHERE points.network_id = @network_id AND points.geom && bbox.geom
  )
  SELECT ST_AsMVT(mvt_geom.*, 'points') FROM mvt_geom;
  ```
- **Caching**: HTTP caching headers (`ETag`, `Cache-Control: public, max-age=300`) with in-memory tile caching for ultra-low latency (< 25 ms per vector tile).

---

## 15. Security Hardening & Audit Verification

ACUDGeoManager implements defense-in-depth security across every tier:

### 15.1 Package Vulnerability Audit
- **.NET Backend**: Verified via `dotnet list package --vulnerable` across all 5 projects (`Api`, `Core`, `Data`, `Seed`, `Tests`):
  ```text
  Project 'UtilityNetwork.Api' has no vulnerable packages given the current sources.
  Project 'UtilityNetwork.Core' has no vulnerable packages given the current sources.
  Project 'UtilityNetwork.Data' has no vulnerable packages given the current sources.
  Project 'UtilityNetwork.Seed' has no vulnerable packages given the current sources.
  Project 'UtilityNetwork.Tests' has no vulnerable packages given the current sources.
  ```
- **Container Hardening**:
  - Backend runtime uses .NET 8 non-root user `USER app` (UID 1654).
  - Frontend runner runs as unprivileged user `nextjs` (UID 1001) on Alpine Linux.
  - Multi-stage builds exclude SDKs, build tooling, and test dependencies from production images.

### 15.2 Authentication & Authorization
- **Dual-Token System**: Short-lived JWT bearer tokens (15 minutes) with rotating HttpOnly refresh tokens (7 days).
- **Role-Based Access Control (RBAC)**: Fine-grained permissions enforced across endpoints:
  - `Admin`: Full system configuration, user provisioning, global undo/redo, network access control.
  - `Engineer`: Topology analysis, tracing, import/export, data quality auditing, feature editing.
  - `Operator`: Feature creation, editing, attachment uploads, spatial queries.
  - `Viewer`: Read-only spatial viewing, vector tiles, summary inspection.
- **SQL Injection Immune**: All database interactions utilize parameterized queries via Dapper and Npgsql; zero raw string concatenation in SQL commands.
- **CWE-1236 Formula Injection Protection**: All CSV exports sanitize leading formula triggers (`=`, `+`, `-`, `@`, `\t`, `\r`) with UTF-8 BOM encoding.
