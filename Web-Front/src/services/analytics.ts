import { get, post, del, put } from './api'
import type { ApiResponse, PaginatedData } from '@/types/api'

export interface PersonalStatsData {
  total_created: number
  total_completed: number
  completion_rate: number
  remind_received: number
  avg_completion_hours: number
  daily_trend: { date: string; count: number }[]
  tag_breakdown: { tag_name: string; count: number }[]
}

export interface AIReportData {
  report_id: string
  period: string
  period_label: string
  report_type: string
  stats: PersonalStatsData
  report: string
  generated_at: string
}

export interface WorkReportItem {
  id: string
  user_id: string
  user_name: string
  period: string
  period_label: string
  report_type: string
  title: string
  content: string
  created_at: string
}

export interface ReportListQuery {
  page: number
  page_size: number
  period?: string
  keyword?: string
  date_from?: string
  date_to?: string
}

const AI_REQUEST_TIMEOUT = 190000

export function fetchPersonalStats(period: 'week' | 'month' | 'year'): Promise<ApiResponse<PersonalStatsData>> {
  return get('/api/v1/analytics/personal-stats', { period })
}

export function generateAIReport(period: 'week' | 'month' | 'year'): Promise<ApiResponse<AIReportData>> {
  return post('/api/v1/analytics/ai-report', { period }, { timeout: AI_REQUEST_TIMEOUT })
}

export function generateDailyReport(): Promise<ApiResponse<AIReportData>> {
  return post('/api/v1/analytics/daily-report', undefined, { timeout: AI_REQUEST_TIMEOUT })
}

export function generateWeeklyReport(): Promise<ApiResponse<AIReportData>> {
  return post('/api/v1/analytics/weekly-report', undefined, { timeout: AI_REQUEST_TIMEOUT })
}

export function generateMonthlyReport(): Promise<ApiResponse<AIReportData>> {
  return post('/api/v1/analytics/monthly-report', undefined, { timeout: AI_REQUEST_TIMEOUT })
}

export function listReports(query: ReportListQuery): Promise<ApiResponse<PaginatedData<WorkReportItem>>> {
  return get('/api/v1/analytics/reports', query as Record<string, unknown>)
}

export function getReport(id: string): Promise<ApiResponse<WorkReportItem>> {
  return get(`/api/v1/analytics/reports/${id}`)
}

export function deleteReport(id: string): Promise<ApiResponse<null>> {
  return del(`/api/v1/analytics/reports/${id}`)
}

export interface ReportTemplateData {
  id: string
  name: string
  content: string
  updated_at: string
}

export function fetchReportTemplate(): Promise<ApiResponse<ReportTemplateData>> {
  return get('/api/v1/analytics/report-template')
}

export function saveReportTemplate(content: string): Promise<ApiResponse<ReportTemplateData>> {
  return put('/api/v1/analytics/report-template', { content })
}
