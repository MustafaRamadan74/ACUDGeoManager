'use client';

import { useEffect, useRef, useState, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import { getAccessToken } from './api';
import {
  UserPresenceDto,
  FeatureChangeEvent,
  FeatureEditingEvent,
  ValidationCompletedEvent,
} from './types';

interface UseWorkspaceCollaborationOptions {
  workspaceId: string | null;
  onFeatureChanged?: (ev: FeatureChangeEvent) => void;
  onValidationCompleted?: (ev: ValidationCompletedEvent) => void;
}

export function useWorkspaceCollaboration({
  workspaceId,
  onFeatureChanged,
  onValidationCompleted,
}: UseWorkspaceCollaborationOptions) {
  const [isConnected, setIsConnected] = useState(false);
  const [onlineUsers, setOnlineUsers] = useState<UserPresenceDto[]>([]);
  const [activeEditors, setActiveEditors] = useState<Map<string, { username: string; timestamp: number }>>(new Map());

  const hubConnectionRef = useRef<signalR.HubConnection | null>(null);
  const currentWorkspaceRef = useRef<string | null>(workspaceId);
  currentWorkspaceRef.current = workspaceId;

  const onFeatureChangedRef = useRef(onFeatureChanged);
  onFeatureChangedRef.current = onFeatureChanged;

  const onValidationCompletedRef = useRef(onValidationCompleted);
  onValidationCompletedRef.current = onValidationCompleted;

  useEffect(() => {
    const rawApiUrl = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api/v1';
    const hubBase = rawApiUrl.replace(/\/api\/v1\/?$/, '');
    const hubUrl = `${hubBase}/hubs/workspace`;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => getAccessToken() || '',
        skipNegotiation: false,
        transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.None)
      .build();

    // Event Handlers
    connection.on('PresenceUpdated', (users: UserPresenceDto[]) => {
      setOnlineUsers(users);
    });

    connection.on('UserJoined', (user: UserPresenceDto) => {
      setOnlineUsers((prev) => {
        if (prev.some((u) => u.userId === user.userId)) return prev;
        return [...prev, user];
      });
    });

    connection.on('UserLeft', (user: UserPresenceDto) => {
      setOnlineUsers((prev) => prev.filter((u) => u.userId !== user.userId));
    });

    connection.on('FeatureChanged', (ev: FeatureChangeEvent) => {
      if (onFeatureChangedRef.current) {
        onFeatureChangedRef.current(ev);
      }
    });

    connection.on('FeatureEditingChanged', (ev: FeatureEditingEvent) => {
      setActiveEditors((prev) => {
        const next = new Map(prev);
        if (ev.isEditing) {
          next.set(ev.featureId, { username: ev.username, timestamp: Date.now() });
        } else {
          next.delete(ev.featureId);
        }
        return next;
      });
    });

    connection.on('ValidationCompleted', (ev: ValidationCompletedEvent) => {
      if (onValidationCompletedRef.current) {
        onValidationCompletedRef.current(ev);
      }
    });

    connection.onreconnecting(() => {
      setIsConnected(false);
    });

    connection.onreconnected(() => {
      setIsConnected(true);
      if (currentWorkspaceRef.current) {
        connection.invoke('JoinWorkspace', currentWorkspaceRef.current).catch(() => {});
      }
    });

    connection.onclose(() => {
      setIsConnected(false);
    });

    // Start connection gracefully (non-blocking)
    connection
      .start()
      .then(() => {
        setIsConnected(true);
        if (currentWorkspaceRef.current) {
          connection.invoke('JoinWorkspace', currentWorkspaceRef.current).catch(() => {});
        }
      })
      .catch(() => {
        // Silent catch: if SignalR fails to connect, application continues to function normally
        setIsConnected(false);
      });

    hubConnectionRef.current = connection;

    return () => {
      if (connection.state === signalR.HubConnectionState.Connected && currentWorkspaceRef.current) {
        connection.invoke('LeaveWorkspace', currentWorkspaceRef.current).catch(() => {});
      }
      connection.stop().catch(() => {});
      hubConnectionRef.current = null;
    };
  }, []);

  // Workspace change effect
  useEffect(() => {
    const conn = hubConnectionRef.current;
    if (!conn || conn.state !== signalR.HubConnectionState.Connected) return;

    if (workspaceId) {
      conn.invoke('JoinWorkspace', workspaceId).catch(() => {});
    }

    return () => {
      if (workspaceId && conn.state === signalR.HubConnectionState.Connected) {
        conn.invoke('LeaveWorkspace', workspaceId).catch(() => {});
      }
    };
  }, [workspaceId]);

  const notifyEditingFeature = useCallback(
    (featureTable: string, featureId: string, isEditing: boolean) => {
      const conn = hubConnectionRef.current;
      if (!conn || conn.state !== signalR.HubConnectionState.Connected || !workspaceId) return;

      conn.invoke('NotifyEditingFeature', workspaceId, featureTable, featureId, isEditing).catch(() => {});
    },
    [workspaceId]
  );

  return {
    isConnected,
    onlineUsers,
    activeEditors,
    notifyEditingFeature,
  };
}

