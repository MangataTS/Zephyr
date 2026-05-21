import type { Tag } from './tag'
import type { UserBrief } from './user'

export type NoteStatus = 'active' | 'completed' | 'archived'
export type NoteSourceType = 'self' | 'assigned' | 'collaboration'

export interface Note {
  id: string
  title: string
  sub_tag?: string
  content: string
  content_delta?: string
  color_status: 'yellow' | 'red' | 'green' | 'blue'
  source_type: NoteSourceType
  owner_id: string
  creator_id: string
  is_archived: boolean
  tags: Tag[]
  assignees: UserBrief[]
  group_id?: string
  dept_id?: string
  template_type?: string
  due_time?: string
  completed_at?: string
  archive_time?: string
  remind_count: number
  serial_no?: string
  created_at: string
  updated_at: string
}

export const NOTE_STATUS_LABEL: Record<string, string> = {
  active: '待办',
  completed: '已完成',
  archived: '已归档',
}

export const NOTE_COLOR_STATUS: Record<string, string> = {
  yellow: '#FEF3C7',
  red: '#FEE2E2',
  green: '#DCFCE7',
  blue: '#DBEAFE',
}

export const NOTE_COLOR_BORDER: Record<string, string> = {
  yellow: '#D97706',
  red: '#DC2626',
  green: '#16A34A',
  blue: '#2563EB',
}

export interface CreateNotePayload {
  title: string
  sub_tag?: string
  content: string
  content_delta?: string
  tags: string[]
  source_type: NoteSourceType
  due_time?: string
  owner_id?: string
  template_type?: string
  assignees?: string[]
}

export interface UpdateNotePayload {
  title?: string
  sub_tag?: string
  content?: string
  content_delta?: string
  tags?: string[]
  due_time?: string
  color_status?: string
  owner_id?: string
}

export interface CompleteNotePayload {
  feedback_content?: string
  attachments?: string[]
}

export interface RemindPayload {
  remind_type?: 'urgent' | 'normal'
  message?: string
  target_id: string
}

export interface NoteFilters {
  status?: string
  tag_ids?: string[]
  department_id?: string
  owner_id?: string
  keyword?: string
  page?: number
  page_size?: number
}

export interface ArchiveFilters {
  date_from?: string
  date_to?: string
  tag_ids?: string[]
  user_id?: string
  department_id?: string
  keyword?: string
  page?: number
  page_size?: number
}
