export interface UserDto {
  id: string;
  username: string;
  email: string;
  role: 'ADMIN' | 'APPROVER' | 'EDITOR' | 'VIEWER';
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
  requiresApproval?: boolean;
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
  role: 'ADMIN' | 'APPROVER' | 'EDITOR' | 'VIEWER';
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
  role: 'ADMIN' | 'APPROVER' | 'EDITOR' | 'VIEWER';
  department?: string | null;
  networkIds?: string[];
}

export interface UpdateUserRequest {
  email?: string;
  role?: 'ADMIN' | 'APPROVER' | 'EDITOR' | 'VIEWER';
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

export interface ExportFilterRequest {
  networkId?: string;
  featureTypeCode?: string;
  bbox?: string;
  srid?: number;
}

export interface ImportRowErrorDto {
  rowIndex: number;
  field: string;
  error: string;
}

export interface ImportPreviewRowDto {
  rowIndex: number;
  name: string;
  featureTypeCode: string;
  geometryKind: string;
  geometrySummary: string;
  status: string;
  isValid: boolean;
  isDuplicate: boolean;
  duplicateReason?: string | null;
  attributes?: string | null;
}

export interface ImportPreviewResultDto {
  previewToken: string;
  totalFeatures: number;
  validCount: number;
  invalidCount: number;
  duplicateCount: number;
  errors: ImportRowErrorDto[];
  sampleRows: ImportPreviewRowDto[];
}

export interface ImportCommitRequest {
  previewToken: string;
  networkId: string;
  skipDuplicates?: boolean;
}

export interface ImportCommitResultDto {
  changeSetId: string;
  importedCount: number;
  skippedCount: number;
  featureIds: string[];
}

export interface BookmarkDto {
  id: string;
  userId: string;
  name: string;
  centerLon: number;
  centerLat: number;
  zoom: number;
  bbox?: string | null;
  createdAt: string;
}

export interface CreateBookmarkRequest {
  name: string;
  centerLon: number;
  centerLat: number;
  zoom: number;
  bbox?: string | null;
}




// --- Analysis & Reports Types ---

export interface IntersectingFeatureItem {
  id: string;
  featureTypeCode: string;
  name: string;
  geometryKind: string;
  geometryGeoJson: string;
  status: string;
  lengthMeters?: number | null;
  areaSqM?: number | null;
}

export interface BufferAnalysisRequest {
  geometryGeoJson?: string | null;
  featureId?: string | null;
  distanceMeters: number;
  networkId?: string | null;
  featureTypeCodes?: string[] | null;
}

export interface BufferAnalysisResultDto {
  bufferPolygonGeoJson: string;
  distanceMeters: number;
  intersectingFeaturesCount: number;
  totalPipeLengthMeters: number;
  totalAreaSqM: number;
  intersectingFeatures: IntersectingFeatureItem[];
}

export interface ServiceAreaRingDto {
  radiusMeters: number;
  polygonGeoJson: string;
  featuresCount: number;
  valvesCount: number;
  pipeLengthMeters: number;
}

export interface ServiceAreaRequest {
  centerLon: number;
  centerLat: number;
  radiiMeters: number[];
  networkId?: string | null;
}

export interface ServiceAreaResultDto {
  centerLon: number;
  centerLat: number;
  rings: ServiceAreaRingDto[];
}

export interface SpatialJoinItemDto {
  polygonId: string;
  polygonName: string;
  containedCount: number;
  totalContainedLengthMeters: number;
  geometryGeoJson: string;
}

export interface SpatialJoinRequest {
  polygonTypeCode: string;
  targetTypeCode: string;
  networkId?: string | null;
}

export interface SpatialJoinResultDto {
  polygonTypeCode: string;
  targetTypeCode: string;
  totalPolygons: number;
  totalContainedCount: number;
  items: SpatialJoinItemDto[];
}

export interface TypeStatsDto {
  featureTypeCode: string;
  displayName: string;
  geometryKind: string;
  count: number;
  totalLengthMeters: number;
  totalAreaSqM: number;
}

export interface StatusStatsDto {
  status: string;
  count: number;
}

export interface PipeDiameterStatsDto {
  diameterMm: number;
  count: number;
  totalLengthMeters: number;
}

export interface ValveStateStatsDto {
  state: string;
  count: number;
}

export interface NetworkStatisticsDto {
  networkId: string;
  totalFeatures: number;
  totalPipeLengthMeters: number;
  totalAreaSqM: number;
  byType: TypeStatsDto[];
  byStatus: StatusStatsDto[];
  pipesByDiameter: PipeDiameterStatsDto[];
  valvesByState: ValveStateStatsDto[];
}

export type ReportFormat = 'Pdf' | 'Csv';

export interface WorkspaceSummaryReportRequest {
  networkId: string;
  mapSnapshotBase64?: string | null;
  format?: ReportFormat;
}

export interface ValidationReportRequest {
  networkId?: string | null;
  severity?: string | null;
  mapSnapshotBase64?: string | null;
  format?: ReportFormat;
}

export interface FeatureHistoryReportRequest {
  featureId: string;
  mapSnapshotBase64?: string | null;
  format?: ReportFormat;
}

export interface UserActivityReportRequest {
  fromDate?: string | null;
  toDate?: string | null;
  format?: ReportFormat;
}

export interface AnalysisWidgetExportRequest {
  title: string;
  analysisType: string;
  description?: string | null;
  summaryJson?: string | null;
  headers: string[];
  rows: string[][];
  mapSnapshotBase64?: string | null;
  format?: ReportFormat;
}

// --- Approval Workflow Models ---
export type ReviewStatus = 'PENDING' | 'APPROVED' | 'REJECTED';

export interface PendingChangeDto {
  id: string;
  networkId: string;
  featureTypeCode: string;
  geometryKind: 'POINT' | 'LINE' | 'POLYGON';
  featureId?: string | null;
  operation: 'INSERT' | 'UPDATE' | 'DELETE';
  name: string;
  status: string;
  geometryGeoJson?: string | null;
  attributesJson: string;
  isOpen?: boolean | null;
  flowDirection?: string | null;
  diameterMm?: number | null;
  material?: string | null;
  targetVersion?: number | null;
  proposerUserId: string;
  proposerUsername: string;
  proposedAt: string;
  reviewStatus: ReviewStatus;
  reviewerUserId?: string | null;
  reviewerUsername?: string | null;
  reviewedAt?: string | null;
  rejectionReason?: string | null;
}

export interface RejectPendingChangeRequest {
  reason: string;
}

export interface PendingChangeSubmittedDto {
  pendingChangeId: string;
  isPendingApproval: boolean;
  operation: string;
  featureTypeCode: string;
  message: string;
}

export interface ApprovalResultDto {
  pendingChangeId: string;
  reviewStatus: string;
  appliedFeatureId?: string | null;
  message: string;
}

export interface UpdateLayerApprovalFlagRequest {
  requiresApproval: boolean;
}

// --- Topology Models ---
export interface TopologyStatusDto {
  networkId: string;
  isDirty: boolean;
  lastRebuiltAt?: string | null;
  nodeCount: number;
  edgeCount: number;
  dirtyFeatureCount: number;
}

export interface RebuildTopologyResultDto {
  networkId: string;
  isDirty: boolean;
  nodeCount: number;
  edgeCount: number;
  rebuiltAt: string;
  durationMs: number;
  message: string;
}

// --- Network Trace Models ---
export type TraceMode = 'UPSTREAM' | 'DOWNSTREAM' | 'ISOLATION';

export interface NetworkTraceRequest {
  networkId: string;
  mode: TraceMode;
  startFeatureId: string;
  startKind: 'LINE' | 'POINT';
  maxDepth?: number;
  maxEdges?: number;
}

export interface TraceSummaryDto {
  totalLengthM: number;
  edgeCount: number;
  nodeCount: number;
  valvesToCloseCount: number;
  affectedZonesCount: number;
  executionMs: number;
}

export interface TraceEdgeDto {
  id: number;
  line_id: string;
  name?: string | null;
  diameter_mm?: number | null;
  material?: string | null;
  direction?: string | null;
  length_m: number;
  geojson?: any;
}

export interface TraceNodeDto {
  id: number;
  node_role?: string | null;
  elevation_m?: number | null;
  geojson?: any;
}

export interface TraceValveDto {
  id: string;
  name: string;
  valve_type?: string | null;
  is_open: boolean;
  status?: string | null;
  coordinates: [number, number];
  geojson?: any;
}

export interface TraceAffectedPolygonDto {
  id: string;
  name: string;
  feature_type_code: string;
  geojson?: any;
}

export interface NetworkTraceResultDto {
  networkId: string;
  mode: TraceMode;
  startFeatureId: string;
  startKind: 'LINE' | 'POINT';
  error?: string | null;
  summary: TraceSummaryDto;
  edges: TraceEdgeDto[];
  nodes: TraceNodeDto[];
  valvesToClose: TraceValveDto[];
  affectedPolygons: TraceAffectedPolygonDto[];
}

export interface TraceReportRequest {
  traceResult: NetworkTraceResultDto;
  networkName: string;
  format?: 'PDF' | 'CSV';
}

// --- Enterprise Capabilities ---

export interface FeatureAttachmentDto {
  id: string;
  featureId: string;
  featureTable: string;
  fileName: string;
  contentType: string;
  fileSizeBytes: number;
  filePath: string;
  description?: string | null;
  uploadedBy: string;
  uploadedAt: string;
}

export interface FeatureRelationshipDto {
  id: string;
  sourceFeatureId: string;
  sourceTable: string;
  targetFeatureId: string;
  targetTable: string;
  relationshipType: 'CONTAINS' | 'SERVES' | 'ATTACHED_TO' | 'FEEDS' | 'ASSOCIATED_WITH' | string;
  description?: string | null;
  createdBy: string;
  createdAt: string;
  sourceFeatureName?: string | null;
  targetFeatureName?: string | null;
  sourceFeatureTypeCode?: string | null;
  targetFeatureTypeCode?: string | null;
}

export interface CreateRelationshipRequest {
  sourceFeatureId: string;
  sourceTable: string;
  targetFeatureId: string;
  targetTable: string;
  relationshipType: string;
  description?: string | null;
}

export interface FeatureLockDto {
  featureId: string;
  featureTable: string;
  lockedByUserId: string;
  lockedByUsername: string;
  lockedAt: string;
  expiresAt: string;
  clientSessionId?: string | null;
  isLocked: boolean;
  isLockedByCurrentUser: boolean;
}

export interface LayerDataQualityDto {
  featureTypeCode: string;
  displayName: string;
  geometryKind: string;
  totalCount: number;
  validGeomCount: number;
  openIssuesCount: number;
  completenessPct: number;
  attachmentsCount: number;
  relationshipsCount: number;
}

export interface DataQualityDashboardDto {
  networkId?: string | null;
  totalFeatures: number;
  totalValidGeometries: number;
  totalOpenIssues: number;
  overallCompletenessPct: number;
  totalAttachments: number;
  totalRelationships: number;
  layers: LayerDataQualityDto[];
}

