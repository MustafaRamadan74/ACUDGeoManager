export interface UserDto {
  id: string;
  username: string;
  email: string;
  role: 'ADMIN' | 'EDITOR' | 'VIEWER';
  isActive: boolean;
  department?: string | null;
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
  distanceMeters?: number;
  distance_meters?: number;
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

// --- Network & Workspace Summary ---

export interface TypeStatusCountDto {
  featureTypeCode: string;
  status: string;
  count: number;
}

export interface RichWorkspaceSummaryDto {
  workspaceId: string;
  workspaceName: string;
  domain: string;
  totalFeatures: number;
  totalLengthM: number;
  totalAreaSqm: number;
  countsByType: Record<string, number>;
  countsByStatus: Record<string, number>;
  lengthsByTypeM: Record<string, number>;
  areasByTypeSqm: Record<string, number>;
  details: TypeStatusCountDto[];
}

export type NetworkSummaryDto = RichWorkspaceSummaryDto;

// --- User Management ---

export interface UserResponse {
  id: string;
  username: string;
  email: string;
  role: 'ADMIN' | 'EDITOR' | 'VIEWER';
  isActive: boolean;
  department?: string | null;
  lastLoginAt?: string | null;
  createdAt: string;
  accessibleNetworkIds: string[];
}

export interface CreateUserRequest {
  username: string;
  email: string;
  password: string;
  role: 'ADMIN' | 'EDITOR' | 'VIEWER';
  department?: string | null;
  networkIds?: string[];
}

export interface UpdateUserRequest {
  email?: string;
  role?: 'ADMIN' | 'EDITOR' | 'VIEWER';
  isActive?: boolean;
  department?: string | null;
  networkIds?: string[];
}

// --- Layer Permissions ---

export interface LayerPermissionDto {
  id?: string | null;
  userId: string;
  featureTypeCode: string;
  featureTypeName?: string | null;
  canRead: boolean;
  canCreate: boolean;
  canUpdate: boolean;
  canDelete: boolean;
}

export interface UpdateLayerPermissionRequest {
  featureTypeCode: string;
  canRead: boolean;
  canCreate: boolean;
  canUpdate: boolean;
  canDelete: boolean;
}

export interface UserLayerPermissionsUpdateRequest {
  permissions: UpdateLayerPermissionRequest[];
}

// --- Spatial Queries ---

export interface QueryWithinRequest {
  networkId?: string | null;
  featureTypes?: string[] | null;
  status?: string | null;
  polygon: any;
  limit?: number;
}

export interface NearestQueryParams {
  x: number;
  y: number;
  networkId?: string | null;
  type?: string | null;
  status?: string | null;
  maxDistanceM?: number | null;
  limit?: number;
}

// --- Validation Engine ---

export type ValidationSeverity = 'ERROR' | 'WARNING' | 'INFO';
export type IssueStatus = 'OPEN' | 'ACKNOWLEDGED' | 'RESOLVED';

export interface ValidationSummaryDto {
  totalIssues: number;
  errorsCount: number;
  warningsCount: number;
  infosCount: number;
  durationMs: number;
  rulesExecuted: string[];
}

export interface ValidationRunDto {
  id: string;
  networkId: string;
  startedBy: string;
  startedAt: string;
  finishedAt?: string | null;
  summary: ValidationSummaryDto;
}

export interface ValidationIssueDto {
  id: string;
  runId: string;
  ruleCode: string;
  severity: ValidationSeverity;
  featureTable: string;
  featureId: string;
  assetName: string;
  message: string;
  geometryGeoJson?: string | null;
  status: IssueStatus;
  acknowledgedBy?: string | null;
  acknowledgedAt?: string | null;
  resolvedBy?: string | null;
  resolvedAt?: string | null;
}

export interface ValidationIssuePageResult {
  items: ValidationIssueDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ValidationIssueQuery {
  runId?: string;
  networkId?: string;
  status?: IssueStatus;
  severity?: ValidationSeverity;
  ruleCode?: string;
  featureTable?: string;
  page?: number;
  pageSize?: number;
}

// --- Undo / Redo & History ---
export interface ChangeSetSummaryDto {
  id: string;
  userId?: string | null;
  description: string;
  status: 'APPLIED' | 'UNDONE';
  createdAt: string;
  undoneAt?: string | null;
  actionsCount: number;
  affectedFeatures: string[];
}

export interface UndoRedoStackDto {
  canUndo: boolean;
  canRedo: boolean;
  undoStack: ChangeSetSummaryDto[];
  redoStack: ChangeSetSummaryDto[];
}

export interface UndoResultDto {
  changeSetId: string;
  description: string;
  revertedActionsCount: number;
  status: string;
}

export interface RedoResultDto {
  changeSetId: string;
  description: string;
  reappliedActionsCount: number;
  status: string;
}

export interface ConflictingFeatureDto {
  tableName: string;
  recordId: string;
  featureName: string;
  expectedVersion: number;
  currentVersion?: number | null;
  reason: string;
}

