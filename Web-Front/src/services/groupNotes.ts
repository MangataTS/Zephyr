import { get, post } from './api';
import type { ApiResponse, PaginatedData } from '@/types/api';
import type { Note } from '@/types';

export function getGroupNotes(
  groupId: string,
  params?: Record<string, unknown>
): Promise<ApiResponse<PaginatedData<Note>>> {
  return get(`/api/v1/groups/${groupId}/notes`, params);
}

export function createGroupNote(
  groupId: string,
  payload: {
    title: string;
    content: string;
    owner_id?: string;
    due_time?: string;
    tag_ids?: string[];
  }
): Promise<ApiResponse<Note>> {
  return post(`/api/v1/groups/${groupId}/notes`, payload);
}

export interface DashboardItem {
  user_name: string;
  note_id: string;
  note_title: string;
  note_content: string;
  tags: { id: string; name: string; sub_tag?: string; color: string }[];
  completed_at: string;
}

export interface DashboardColumn {
  sub_group_name: string;
  items: DashboardItem[];
}

export interface DashboardData {
  group: { id: string; name: string; members: { user_id: string; sub_group_name: string }[] };
  columns: DashboardColumn[];
}

export function getGroupDashboard(groupId: string): Promise<ApiResponse<DashboardData>> {
  return get(`/api/v1/groups/${groupId}/dashboard`);
}

export interface RoomCanvasData {
  columns: number;
  canvas_data: Record<number, string>;
  version: number;
}

export function getRoomCanvas(noteId: string): Promise<ApiResponse<RoomCanvasData>> {
  return get(`/api/v1/rooms/${noteId}/canvas`);
}
