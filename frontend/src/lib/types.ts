export interface UserDto {
  id: string;
  username: string;
  email: string;
  role: 'ADMIN' | 'EDITOR' | 'VIEWER';
  isActive: boolean;
  createdAt: string;
}

export interface AuthResponseDto {
  accessToken: string;
  expiresIn: number;
  user: UserDto;
}

export interface NetworkDto {
  id: string;
  code: string;
  name: string;
  domain: string;
  isDirected: boolean;
  snapToleranceM: number;
}

export interface FeatureTypeStyle {
  color?: string;
  fillColor?: string;
  fillOpacity?: number;
  weight?: number;
  radius?: number;
  opacity?: number;
  dashArray?: string;
  shape?: string;
}

export interface FeatureTypeDto {
  code: string;
  networkDomain: string;
  geometryKind: 'POINT' | 'LINE' | 'POLYGON';
  displayName: string;
  topologyRole: string;
  attributeSchema: Record<string, any>;
  style: FeatureTypeStyle;
  isActive: boolean;
}

export interface GeoJsonGeometry {
  type: 'Point' | 'LineString' | 'Polygon' | string;
  coordinates: any;
}

export interface GeoJsonProperties {
  networkId: string;
  featureTypeCode: string;
  name: string;
  status: string;
  geometryKind: string;
  version?: number;
  isOpen?: boolean | null;
  flowDirection?: string | null;
  diameterMm?: number | null;
  material?: string | null;
  attributes?: Record<string, unknown>;
  [key: string]: unknown;
}

export interface GeoJsonFeature {
  type: 'Feature';
  id: string;
  geometry: GeoJsonGeometry;
  properties: GeoJsonProperties;
}

export interface GeoJsonFeatureCollection {
  type: 'FeatureCollection';
  features: GeoJsonFeature[];
}

export interface ProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
  instance?: string;
}

// --- CRUD Request DTOs ---

export interface CreatePointRequest {
  networkId: string;
  featureTypeCode: string;
  name: string;
  status: string;
  attributes: Record<string, unknown>;
  x: number;
  y: number;
  isOpen?: boolean | null;
}

export interface UpdatePointRequest {
  name?: string;
  status?: string;
  attributes?: Record<string, unknown>;
  x?: number;
  y?: number;
  isOpen?: boolean | null;
  version: number;
}

export interface CreateLineRequest {
  networkId: string;
  featureTypeCode: string;
  name: string;
  status: string;
  attributes: Record<string, unknown>;
  coordinates: [number, number][]; // [lng, lat][]
  flowDirection: string;
  diameterMm?: number | null;
  material?: string | null;
}

export interface UpdateLineRequest {
  name?: string;
  status?: string;
  attributes?: Record<string, unknown>;
  coordinates?: [number, number][];
  flowDirection?: string;
  diameterMm?: number | null;
  material?: string | null;
  version: number;
}

export interface CreatePolygonRequest {
  networkId: string;
  featureTypeCode: string;
  name: string;
  status: string;
  attributes: Record<string, unknown>;
  ringCoordinates: [number, number][][]; // [[[lng, lat], ...]]
}

export interface UpdatePolygonRequest {
  name?: string;
  status?: string;
  attributes?: Record<string, unknown>;
  ringCoordinates?: [number, number][][];
  version: number;
}

// --- Feature History / Audit ---

export interface AuditLogEntry {
  id: number;
  changeSetId?: string | null;
  tableName: string;
  recordId: string;
  action: string;
  oldData?: string | null;
  newData?: string | null;
  userId?: string | null;
  username: string;
  role: string;
  changedAt: string;
  reason: string;
  requestId?: string | null;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

// --- Network Summary ---

export interface TypeStatusCountDto {
  featureTypeCode: string;
  status: string;
  count: number;
}

export interface NetworkSummaryDto {
  networkId: string;
  networkName: string;
  networkDomain: string;
  totalFeatures: number;
  countsByType: Record<string, number>;
  countsByStatus: Record<string, number>;
  details: TypeStatusCountDto[];
}

// --- User Management ---

export interface UserResponse {
  id: string;
  username: string;
  email: string;
  role: 'ADMIN' | 'EDITOR' | 'VIEWER';
  isActive: boolean;
  lastLoginAt?: string | null;
  createdAt: string;
  accessibleNetworkIds: string[];
}

export interface CreateUserRequest {
  username: string;
  email: string;
  password: string;
  role: 'ADMIN' | 'EDITOR' | 'VIEWER';
  networkIds?: string[];
}

export interface UpdateUserRequest {
  email?: string;
  role?: 'ADMIN' | 'EDITOR' | 'VIEWER';
  isActive?: boolean;
  networkIds?: string[];
}
