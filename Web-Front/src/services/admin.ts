import { get, post, put, del } from './api'
import type { User, Department, LoginCredentials, LoginResponse, UserProfile, WorkTypeOption } from '@/types'

export function login(credentials: LoginCredentials) {
  return post<LoginResponse>('/api/v1/auth/login', credentials)
}

export function getCurrentUser() {
  return get<User>('/api/v1/auth/me')
}

// ---- Departments ----
export function getDepartments(flat?: boolean) {
  return get<Department[]>('/api/v1/departments', { flat: flat ? 'true' : 'false' })
}

export function createDepartment(payload: { name: string; parent_id?: string; level?: number }) {
  return post<Department>('/api/v1/departments', payload)
}

export function updateDepartment(id: string, payload: { name?: string; parent_id?: string; leader_id?: string; level?: number }) {
  return put<Department>(`/api/v1/departments/${id}`, payload)
}

export function deleteDepartment(id: string) {
  return del<{ deleted: string; name: string }>(`/api/v1/departments/${id}`)
}

// ---- Users ----
export function getUsers(params: { dept_id?: string; role?: string; keyword?: string; page?: number; page_size?: number }) {
  return get<{ data: User[]; total: number; page: number; page_size: number }>('/api/v1/users', params as Record<string, unknown>)
}

export function getVisibleUsers() {
  return get<User[]>('/api/v1/users/visible')
}

export function createUser(payload: { username: string; name: string; password: string; role?: string; rank?: string; position?: string; skills?: string; phone?: string; email?: string; avatar?: string; dept_id?: string }) {
  return post<User>('/api/v1/users', payload)
}

export function updateUser(id: string, payload: { name?: string; role?: string; rank?: string; position?: string; skills?: string; phone?: string; email?: string; avatar?: string; dept_id?: string; is_active?: boolean }) {
  return put<User>(`/api/v1/users/${id}`, payload)
}

export function deleteUser(id: string) {
  return del<{ success: boolean }>(`/api/v1/users/${id}`)
}

export function getUserProfile(id: string) {
  return get<UserProfile>(`/api/v1/users/${id}/profile`)
}

export function getUsersWithStats(params: { dept_id?: string; role?: string; keyword?: string; page?: number; page_size?: number }) {
  return get<{ data: (User & { work_type_stats?: { work_type: string; group_count: number }[] })[]; total: number; page: number; page_size: number }>('/api/v1/users/with-stats', params as Record<string, unknown>)
}

export function recommendUsers(params: { work_type: string; exclude_user_id?: string; limit?: number }) {
  return get<User[]>('/api/v1/users/recommend', params as Record<string, unknown>)
}

export function getWorkTypeOptions() {
  return get<WorkTypeOption[]>('/api/v1/users/work-type-options')
}

// ---- Ledger ----
export function fetchLedger(params?: { page?: number; page_size?: number; dept_id?: string }) {
  return get<{ data: any[]; total: number }>('/api/v1/ledger', params as Record<string, unknown>)
}
