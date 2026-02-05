import { apiClient } from './client';

export interface Waypoint {
    title: string;
    color: string;
    icon: string;
    x: number;
    y: number;
    pinned: boolean;
    owner: string;
}

export interface WaypointsResponse {
    waypoints: Waypoint[];
    count: number;
}

/**
 * Get all server waypoints
 */
export async function getWaypoints(): Promise<WaypointsResponse> {
    const response = await apiClient.get('/waypoints');
    return response as any as WaypointsResponse;
}
