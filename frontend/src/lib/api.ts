import {
  AuthResponseDto,
  FeatureTypeDto,
  GeoJsonFeatureCollection,
  NetworkDto,
  ProblemDetails,
  UserDto,
  CreatePointRequest,
  UpdatePointRequest,
  CreateLineRequest,
  UpdateLineRequest,
  CreatePolygonRequest,
  UpdatePolygonRequest,
  AuditLogEntry,
  PagedResult,
  NetworkSummaryDto,
  RichWorkspaceSummaryDto,
  UserResponse,
  CreateUserRequest,
  UpdateUserRequest,
  QueryWithinRequest,
  NearestQueryParams,
} from './types';

let inMemoryToken: string | null = null;
let isRefreshing = false;
let failedQueue: Array<{
  resolve: (token: string) => void;
  reject: (err: unknown) => void;
}> = [];

const processQueue = (error: unknown, token: string | null = null) => {
  failedQueue.forEach((prom) => {
    if (error) {
      prom.reject(error);
    } else {
      prom.resolve(token!);
    }
  });
  failedQueue = [];
};

export const setAccessToken = (token: string | null) => {
  inMemoryToken = token;
};

export const getAccessToken = (): string | null => {
  return inMemoryToken;
};

const BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api/v1';

export class ApiError extends Error {
  public status: number;
  public problem?: ProblemDetails;

  constructor(status: number, message: string, problem?: ProblemDetails) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.problem = problem;
  }
}

async function request<T>(
  path: string,
  options: RequestInit = {},
  isRetry = false
): Promise<T> {
  const url = path.startsWith('http') ? path : `${BASE_URL}${path}`;
  const headers = new Headers(options.headers || {});

  if (!headers.has('Content-Type') && !(options.body instanceof FormData)) {
    headers.set('Content-Type', 'application/json');
  }

  if (inMemoryToken) {
    headers.set('Authorization', `Bearer ${inMemoryToken}`);
  }

  const fetchOptions: RequestInit = {
    ...options,
    headers,
    credentials: 'include', // for HttpOnly refresh_token cookie
  };

  const response = await fetch(url, fetchOptions);

  if (response.status === 401 && !isRetry && !path.includes('/auth/login') && !path.includes('/auth/refresh')) {
    if (isRefreshing) {
      return new Promise<string>((resolve, reject) => {
        failedQueue.push({ resolve, reject });
      }).then((token) => {
        headers.set('Authorization', `Bearer ${token}`);
        return request<T>(path, { ...options, headers }, true);
      });
    }

    isRefreshing = true;

    try {
      const refreshResponse = await fetch(`${BASE_URL}/auth/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
      });

      if (!refreshResponse.ok) {
        throw new Error('Refresh token invalid or expired');
      }

      const refreshData = (await refreshResponse.json()) as AuthResponseDto;
      setAccessToken(refreshData.accessToken);
      processQueue(null, refreshData.accessToken);

      headers.set('Authorization', `Bearer ${refreshData.accessToken}`);
      return await request<T>(path, { ...options, headers }, true);
    } catch (refreshErr) {
      processQueue(refreshErr, null);
      setAccessToken(null);
      if (typeof window !== 'undefined' && !window.location.pathname.includes('/login')) {
        window.location.href = '/login';
      }
      throw new ApiError(401, 'Session expired. Please sign in again.');
    } finally {
      isRefreshing = false;
    }
  }

  if (!response.ok) {
    let problem: ProblemDetails | undefined;
    try {
      problem = await response.json();
    } catch {
      // not json problem details
    }

    const message = problem?.detail || problem?.title || `Request failed with status ${response.status}`;
    throw new ApiError(response.status, message, problem);
  }

  if (response.status === 204) {
    return {} as T;
  }

  return (await response.json()) as T;
}

export const api = {
  // Auth
  login: async (username: string, password: string): Promise<AuthResponseDto> => {
    const data = await request<AuthResponseDto>('/auth/login', {
      method: 'POST',
      body: JSON.stringify({ username, password }),
    });
    setAccessToken(data.accessToken);
    return data;
  },

  refresh: async (): Promise<AuthResponseDto> => {
    const data = await request<AuthResponseDto>('/auth/refresh', {
      method: 'POST',
    });
    setAccessToken(data.accessToken);
    return data;
  },

  logout: async (): Promise<void> => {
    try {
      await request('/auth/logout', { method: 'POST' });
    } finally {
      setAccessToken(null);
    }
  },

  getMe: async (): Promise<UserDto> => {
    return await request<UserDto>('/auth/me');
  },

  // Catalog
  getNetworks: async (): Promise<NetworkDto[]> => {
    return await request<NetworkDto[]>('/networks');
  },

  getFeatureTypes: async (domain?: string): Promise<FeatureTypeDto[]> => {
    const path = domain ? `/feature-types?domain=${encodeURIComponent(domain)}` : '/feature-types';
    return await request<FeatureTypeDto[]>(path);
  },

  getNetworkSummary: async (networkId: string): Promise<RichWorkspaceSummaryDto> => {
    return await request<RichWorkspaceSummaryDto>(`/networks/${networkId}/summary`);
  },

  getWorkspaceSummary: async (workspaceId: string): Promise<RichWorkspaceSummaryDto> => {
    return await request<RichWorkspaceSummaryDto>(`/workspaces/${workspaceId}/summary`);
  },

  // Map Features
  getMapFeatures: async (
    bbox: [number, number, number, number],
    networks?: string[],
    types?: string[],
    limit = 1000,
    signal?: AbortSignal
  ): Promise<GeoJsonFeatureCollection> => {
    const params = new URLSearchParams();
    params.set('bbox', bbox.join(','));
    params.set('limit', limit.toString());

    if (networks && networks.length > 0) {
      params.set('networks', networks.join(','));
    }

    if (types && types.length > 0) {
      params.set('types', types.join(','));
    }

    return await request<GeoJsonFeatureCollection>(`/map/features?${params.toString()}`, {
      signal,
    });
  },

  // Spatial Queries
  queryWithin: async (data: QueryWithinRequest): Promise<GeoJsonFeatureCollection> => {
    return await request<GeoJsonFeatureCollection>('/query/within', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  },

  queryNearest: async (params: NearestQueryParams): Promise<GeoJsonFeatureCollection> => {
    const urlParams = new URLSearchParams();
    urlParams.set('x', params.x.toString());
    urlParams.set('y', params.y.toString());
    if (params.networkId) urlParams.set('networkId', params.networkId);
    if (params.type) urlParams.set('type', params.type);
    if (params.status) urlParams.set('status', params.status);
    if (params.maxDistanceM) urlParams.set('maxDistanceM', params.maxDistanceM.toString());
    if (params.limit) urlParams.set('limit', params.limit.toString());

    return await request<GeoJsonFeatureCollection>(`/query/nearest?${urlParams.toString()}`);
  },

  // Feature Points CRUD
  createPoint: async (data: CreatePointRequest): Promise<any> => {
    return await request<any>('/points', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  },

  updatePoint: async (id: string, data: UpdatePointRequest): Promise<any> => {
    return await request<any>(`/points/${id}`, {
      method: 'PATCH',
      body: JSON.stringify(data),
    });
  },

  deletePoint: async (id: string, version: number): Promise<void> => {
    await request<void>(`/points/${id}?version=${version}`, {
      method: 'DELETE',
    });
  },

  // Feature Lines CRUD
  createLine: async (data: CreateLineRequest): Promise<any> => {
    return await request<any>('/lines', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  },

  updateLine: async (id: string, data: UpdateLineRequest): Promise<any> => {
    return await request<any>(`/lines/${id}`, {
      method: 'PATCH',
      body: JSON.stringify(data),
    });
  },

  deleteLine: async (id: string, version: number): Promise<void> => {
    await request<void>(`/lines/${id}?version=${version}`, {
      method: 'DELETE',
    });
  },

  // Feature Polygons CRUD
  createPolygon: async (data: CreatePolygonRequest): Promise<any> => {
    return await request<any>('/polygons', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  },

  updatePolygon: async (id: string, data: UpdatePolygonRequest): Promise<any> => {
    return await request<any>(`/polygons/${id}`, {
      method: 'PATCH',
      body: JSON.stringify(data),
    });
  },

  deletePolygon: async (id: string, version: number): Promise<void> => {
    await request<void>(`/polygons/${id}?version=${version}`, {
      method: 'DELETE',
    });
  },

  // Feature History / Audit
  getFeatureHistory: async (
    featureId: string,
    page = 1,
    pageSize = 50
  ): Promise<PagedResult<AuditLogEntry>> => {
    return await request<PagedResult<AuditLogEntry>>(
      `/history?featureId=${featureId}&page=${page}&pageSize=${pageSize}`
    );
  },

  // User Administration (Admin Role)
  getUsers: async (): Promise<UserResponse[]> => {
    return await request<UserResponse[]>('/users');
  },

  getUserById: async (id: string): Promise<UserResponse> => {
    return await request<UserResponse>(`/users/${id}`);
  },

  createUser: async (data: CreateUserRequest): Promise<UserResponse> => {
    return await request<UserResponse>('/users', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  },

  updateUser: async (id: string, data: UpdateUserRequest): Promise<UserResponse> => {
    return await request<UserResponse>(`/users/${id}`, {
      method: 'PATCH',
      body: JSON.stringify(data),
    });
  },

  deleteUser: async (id: string): Promise<void> => {
    await request<void>(`/users/${id}`, {
      method: 'DELETE',
    });
  },

  getUserNetworks: async (id: string): Promise<string[]> => {
    return await request<string[]>(`/users/${id}/networks`);
  },

  assignUserNetworks: async (id: string, networkIds: string[]): Promise<void> => {
    await request<void>(`/users/${id}/networks`, {
      method: 'PUT',
      body: JSON.stringify({ networkIds }),
    });
  },

  getUserActivity: async (
    userId: string,
    page = 1,
    pageSize = 50
  ): Promise<PagedResult<AuditLogEntry>> => {
    return await request<PagedResult<AuditLogEntry>>(
      `/history?userId=${userId}&page=${page}&pageSize=${pageSize}`
    );
  },
};
