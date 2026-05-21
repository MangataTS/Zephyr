import { get, post, put, del } from './api';
import type { ApiResponse, PaginatedData } from '@/types/api';

export interface AISuggestedMember {
  user_id: string;
  name: string;
  role: string;
  reason: string;
}

export interface AISuggestedGroup {
  name: string;
  responsibility: string;
  members: AISuggestedMember[];
}

export interface AISuggestGroupsResult {
  analysis: string;
  suggested_name: string;
  suggested_template: string;
  sub_groups: AISuggestedGroup[];
  source: string;
}

export function aiSuggestGroups(
  description: string,
  groupName?: string
): Promise<ApiResponse<AISuggestGroupsResult>> {
  return post('/api/v1/groups/ai-suggest', { description, group_name: groupName || '' }, { timeout: 190000 });
}

export interface WorkGroupData {
  id: string;
  name: string;
  description: string;
  initiator_id: string;
  initiator?: { id: string; name: string; username: string };
  template_type: string;
  status: string;
  due_time?: string;
  members: WorkGroupMemberData[];
  created_at: string;
  updated_at: string;
}

export interface WorkGroupMemberData {
  group_id: string;
  user_id: string;
  user?: {
    id: string;
    name: string;
    username: string;
    avatar: string;
    role: string;
    department?: { name: string };
  };
  role: string;
  sub_group_name: string;
}

export interface CreateWorkGroupPayload {
  name: string;
  description: string;
  template_type: string;
  due_time?: string;
  preset_id?: string;
  members: { user_id: string; role: string; sub_group_name: string }[];
  tags?: string[];
}

export interface GroupSearchQuery {
  page: number;
  page_size: number;
  keyword?: string;
  user_id?: string;
  date_from?: string;
  date_to?: string;
}

export function searchGroups(
  query: GroupSearchQuery
): Promise<ApiResponse<PaginatedData<WorkGroupData>>> {
  return get('/api/v1/groups', query as Record<string, unknown>);
}

export function getMyGroups(): Promise<ApiResponse<WorkGroupData[]>> {
  return get('/api/v1/groups/mine');
}

export function getWorkGroupDetail(id: string): Promise<ApiResponse<WorkGroupData>> {
  return get(`/api/v1/groups/${id}`);
}

export function createWorkGroup(
  payload: CreateWorkGroupPayload
): Promise<ApiResponse<WorkGroupData>> {
  return post('/api/v1/groups', payload);
}

export function getWorkGroupMembers(id: string): Promise<ApiResponse<WorkGroupMemberData[]>> {
  return get(`/api/v1/groups/${id}/members`);
}

export function addWorkGroupMember(
  id: string,
  payload: { user_id: string; role?: string; sub_group_name?: string }
): Promise<ApiResponse<{ success: boolean }>> {
  return post(`/api/v1/groups/${id}/members`, payload);
}

export function updateWorkGroupMember(
  id: string,
  userId: string,
  payload: { role?: string; sub_group_name?: string }
): Promise<ApiResponse<{ success: boolean }>> {
  return put(`/api/v1/groups/${id}/members/${userId}`, payload);
}

export function removeWorkGroupMember(
  id: string,
  userId: string
): Promise<ApiResponse<{ success: boolean }>> {
  return del(`/api/v1/groups/${id}/members/${userId}`);
}

export function deleteWorkGroup(id: string): Promise<ApiResponse<null>> {
  return del(`/api/v1/groups/${id}`);
}

export interface WorkGroupReport {
  id: string
  user_id: string
  user_name: string
  group_id: string
  report_type: string
  title: string
  content: string
  stats_summary: string
  created_at: string
}

export interface GenerateReportResult {
  report_id: string
  report_type: string
  report: string
  generated_at: string
}

export function generateGroupReport(groupId: string): Promise<ApiResponse<GenerateReportResult>> {
  return post(`/api/v1/groups/${groupId}/reports`, undefined, { timeout: 190000 })
}

export function listGroupReports(
  groupId: string,
  params?: Record<string, unknown>
): Promise<ApiResponse<PaginatedData<WorkGroupReport>>> {
  return get(`/api/v1/groups/${groupId}/reports`, params)
}

export function getGroupReport(groupId: string, reportId: string): Promise<ApiResponse<WorkGroupReport>> {
  return get(`/api/v1/groups/${groupId}/reports/${reportId}`)
}

export function deleteGroupReport(groupId: string, reportId: string): Promise<ApiResponse<null>> {
  return del(`/api/v1/groups/${groupId}/reports/${reportId}`)
}

export function exportGroupReport(groupId: string, reportId: string, format: string): Promise<Blob> {
  const token = localStorage.getItem('auth_token')
  return fetch(`/api/v1/groups/${groupId}/reports/${reportId}/export?format=${format}`, {
    headers: token ? { Authorization: `Bearer ${token}` } : {},
  }).then(r => r.blob())
}
