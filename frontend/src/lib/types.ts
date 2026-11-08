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
  attributeSchema: Record<string, unknown>;
  style: FeatureTypeStyle;
  isActive: boolean;
}

export interface GeoJsonGeometry {
  type: string;
  coordinates: any;
}

export interface GeoJsonProperties {
  networkId: string;
  featureTypeCode: string;
  name: string;
  status: string;
  geometryKind: string;
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

