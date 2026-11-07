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

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js (LTS v18+) & npm](https://nodejs.org/)
- [Docker & Docker Compose](https://www.docker.com/)

### Step 1: Environment Setup

Copy `.env.example` to `.env`:
```bash
cp .env.example .env
```

### Step 2: Start the Database

Launch the PostGIS database container:
```bash
docker compose up -d db
```

Verify that the database container is healthy:
```bash
docker compose ps
```

### Step 3: Run Backend API

Navigate to the API project and launch:
```bash
cd backend/src/UtilityNetwork.Api
dotnet run
```

- API Base URL: `http://localhost:5000`
- Interactive Swagger Documentation: `http://localhost:5000/api/docs`
- Health Endpoint: `http://localhost:5000/api/v1/health`

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
