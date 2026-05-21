<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import {
  fetchPersonalStats,
  generateAIReport,
  generateDailyReport,
  generateWeeklyReport,
  generateMonthlyReport,
  listReports,
  deleteReport,
  fetchReportTemplate,
  saveReportTemplate,
} from '@/services/analytics';
import type { PersonalStatsData, WorkReportItem, ReportTemplateData } from '@/services/analytics';

type Period = 'week' | 'month' | 'year';

const activePeriod = ref<Period>('week');
const stats = ref<PersonalStatsData | null>(null);
const loading = ref(false);
const error = ref('');

const reportLoading = ref(false);
const reportContent = ref('');
const reportError = ref('');
const reportGenerated = ref(false);
const reportType = ref('');

const viewTab = ref<'stats' | 'history'>('stats');
const reports = ref<WorkReportItem[]>([]);
const reportsLoading = ref(false);
const reportsTotal = ref(0);
const reportsPage = ref(1);
const reportsPageSize = 20;
const reportsFilter = ref({
  keyword: '',
  period: '',
  date_from: '',
  date_to: '',
});
const selectedReport = ref<WorkReportItem | null>(null);

const showTemplateModal = ref(false);
const templateLoading = ref(false);
const templateContent = ref('');
const templateSaving = ref(false);

const toastMsg = ref('');
const toastType = ref<'success' | 'error'>('success');
function showToast(type: 'success' | 'error', msg: string) {
  toastMsg.value = msg;
  toastType.value = type;
  setTimeout(() => {
    toastMsg.value = '';
  }, 3000);
}

const periodOptions: { key: Period; label: string; icon: string }[] = [
  { key: 'week', label: '本周', icon: '📅' },
  { key: 'month', label: '本月', icon: '📆' },
  { key: 'year', label: '本年度', icon: '📊' },
];

const periodLabels: Record<string, string> = {
  week: '本周',
  month: '本月',
  year: '本年度',
};

const maxTrendCount = computed(() => {
  if (!stats.value?.daily_trend?.length) return 1;
  return Math.max(...stats.value.daily_trend.map((d) => d.count), 1);
});

async function loadStats() {
  loading.value = true;
  error.value = '';
  try {
    const res = await fetchPersonalStats(activePeriod.value);
    stats.value = res.data as PersonalStatsData;
  } catch {
    error.value = '加载统计数据失败';
    stats.value = null;
  } finally {
    loading.value = false;
  }
}

function switchPeriod(period: Period) {
  activePeriod.value = period;
  reportGenerated.value = false;
  reportContent.value = '';
  loadStats();
}

async function handleGenerateReport() {
  reportLoading.value = true;
  reportError.value = '';
  reportContent.value = '';
  try {
    const res = await generateAIReport(activePeriod.value);
    const data = res.data as { report: string; report_type: string };
    reportContent.value = data.report || '';
    reportType.value = data.report_type || '';
    reportGenerated.value = true;
    showToast('success', '报告生成成功，已保存到历史记录');
    loadReports();
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : 'AI报告生成失败';
    reportError.value = msg;
    showToast('error', msg);
  } finally {
    reportLoading.value = false;
  }
}

const currentReportPeriod = ref<'day' | 'week' | 'month'>('week');

async function handleGeneratePeriodReport(period: 'day' | 'week' | 'month') {
  reportLoading.value = true;
  reportError.value = '';
  reportContent.value = '';
  currentReportPeriod.value = period;
  try {
    let res: { data: unknown };
    if (period === 'day') {
      res = await generateDailyReport();
    } else if (period === 'week') {
      res = await generateWeeklyReport();
    } else {
      res = await generateMonthlyReport();
    }
    const data = res.data as { report: string; report_type: string; period_label: string };
    reportContent.value = data.report || '';
    reportType.value = data.report_type || '';
    reportGenerated.value = true;
    showToast('success', `${data.period_label || ''}报告生成成功，已保存到历史记录`);
    loadReports();
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : '报告生成失败';
    reportError.value = msg;
    showToast('error', msg);
  } finally {
    reportLoading.value = false;
  }
}

async function loadReports() {
  reportsLoading.value = true;
  try {
    const res = await listReports({
      page: reportsPage.value,
      page_size: reportsPageSize,
      keyword: reportsFilter.value.keyword || undefined,
      period: reportsFilter.value.period || undefined,
      date_from: reportsFilter.value.date_from || undefined,
      date_to: reportsFilter.value.date_to || undefined,
    });
    const data = res.data as unknown as { data: WorkReportItem[]; total: number };
    reports.value = data.data || [];
    reportsTotal.value = data.total || 0;
  } catch {
    showToast('error', '加载报告列表失败');
  } finally {
    reportsLoading.value = false;
  }
}

function applyReportFilters() {
  reportsPage.value = 1;
  loadReports();
}

function resetReportFilters() {
  reportsFilter.value = { keyword: '', period: '', date_from: '', date_to: '' };
  reportsPage.value = 1;
  loadReports();
}

function viewReport(report: WorkReportItem) {
  selectedReport.value = report;
}

function closeReportDetail() {
  selectedReport.value = null;
}

async function openTemplateEditor() {
  showTemplateModal.value = true;
  templateLoading.value = true;
  try {
    const res = await fetchReportTemplate();
    templateContent.value = (res.data as ReportTemplateData).content || '';
  } catch {
    showToast('error', '加载模板失败');
  } finally {
    templateLoading.value = false;
  }
}

async function handleSaveTemplate() {
  if (!templateContent.value.trim()) {
    showToast('error', '模板内容不能为空');
    return;
  }
  templateSaving.value = true;
  try {
    await saveReportTemplate(templateContent.value);
    showToast('success', '模板保存成功');
    showTemplateModal.value = false;
  } catch {
    showToast('error', '保存模板失败');
  } finally {
    templateSaving.value = false;
  }
}

async function handleDeleteReport(id: string) {
  try {
    await deleteReport(id);
    showToast('success', '报告已删除');
    if (selectedReport.value?.id === id) selectedReport.value = null;
    loadReports();
  } catch {
    showToast('error', '删除报告失败');
  }
}

function formatPercent(v: number): string {
  return v.toFixed(1) + '%';
}

function formatHours(v: number): string {
  if (v < 1) return (v * 60).toFixed(0) + ' 分钟';
  return v.toFixed(1) + ' 小时';
}

function formatTime(dateStr: string): string {
  if (!dateStr) return '-';
  return new Date(dateStr).toLocaleString('zh-CN');
}

function copyReport() {
  if (!reportContent.value) return;
  navigator.clipboard.writeText(reportContent.value);
  showToast('success', '报告已复制到剪贴板');
}

const renderedReport = computed(() => {
  const content = selectedReport.value?.content || reportContent.value;
  if (!content) return '';
  return content
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(
      /^## (.+)$/gm,
      '<h3 class="text-base font-semibold mt-4 mb-2 text-slate-800 dark:text-slate-200">$1</h3>'
    )
    .replace(
      /^### (.+)$/gm,
      '<h4 class="text-sm font-semibold mt-3 mb-1 text-slate-700 dark:text-slate-300">$1</h4>'
    )
    .replace(/\*\*(.+?)\*\*/g, '<strong class="font-semibold">$1</strong>')
    .replace(/^- (.+)$/gm, '<li class="ml-4 text-sm text-slate-600 dark:text-slate-400">$1</li>')
    .replace(/\n/g, '<br>');
});

function downloadReport() {
  if (!reportContent.value) return;
  const blob = new Blob([reportContent.value], { type: 'text/markdown;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `工作成效报告_${new Date().toISOString().slice(0, 10)}.md`;
  a.click();
  URL.revokeObjectURL(url);
}

onMounted(() => {
  loadStats();
  loadReports();
});
</script>

<template>
  <div class="h-full flex flex-col bg-white dark:bg-slate-900 transition-colors duration-300">
    <!-- Header -->
    <div class="shrink-0 px-6 py-4 border-b border-slate-200 dark:border-slate-700">
      <h1 class="text-xl font-semibold text-slate-900 dark:text-slate-100">工作成效分析</h1>
      <p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">
        个人工作数据统计、趋势分析与AI智能报告
      </p>
    </div>

    <!-- Sub tabs -->
    <div class="shrink-0 px-6 pt-4">
      <div class="flex gap-2">
        <button
          v-for="tab in [
            { key: 'stats', label: '📊 数据统计' },
            { key: 'history', label: '📋 报告历史' },
          ]"
          :key="tab.key"
          :class="[
            'px-4 py-2 rounded-lg text-sm font-medium transition-smooth',
            viewTab === tab.key
              ? 'bg-blue-50 dark:bg-blue-900/40 text-blue-600 dark:text-blue-400 shadow-sm'
              : 'text-slate-500 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800',
          ]"
          @click="viewTab = tab.key"
        >
          {{ tab.label }}
        </button>
        <button
          class="px-3 py-2 rounded-lg text-sm font-medium text-slate-500 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 transition-smooth ml-2 border border-dashed border-slate-300 dark:border-slate-600"
          title="编辑报告模板"
          @click="openTemplateEditor()"
        >
          📝 编辑模板
        </button>
      </div>
    </div>

    <!-- Toast -->
    <div
      v-if="toastMsg"
      :class="[
        'fixed top-4 right-4 z-50 px-4 py-3 rounded-lg text-sm shadow-lg transition-all',
        toastType === 'success' ? 'bg-green-600 text-white' : 'bg-red-600 text-white',
      ]"
    >
      {{ toastMsg }}
    </div>

    <div class="flex-1 overflow-auto p-6">
      <!-- ===================== 数据统计 Tab ===================== -->
      <template v-if="viewTab === 'stats'">
        <!-- Period Tabs -->
        <div class="flex gap-2 mb-4">
          <button
            v-for="opt in periodOptions"
            :key="opt.key"
            :class="[
              'flex items-center gap-1.5 px-4 py-2 rounded-lg text-sm font-medium transition-smooth',
              activePeriod === opt.key
                ? 'bg-blue-50 dark:bg-blue-900/40 text-blue-600 dark:text-blue-400 shadow-sm'
                : 'text-slate-500 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800',
            ]"
            @click="switchPeriod(opt.key)"
          >
            <span>{{ opt.icon }}</span>
            <span>{{ opt.label }}</span>
          </button>
        </div>

        <div
          v-if="error"
          class="mb-4 px-4 py-3 rounded-lg text-sm bg-red-50 dark:bg-red-900/30 text-red-700 dark:text-red-400 border border-red-200 dark:border-red-800"
        >
          {{ error }}
        </div>

        <div v-if="loading" class="flex items-center justify-center py-16">
          <div
            class="animate-spin rounded-full h-8 w-8 border-2 border-blue-500 border-t-transparent"
          ></div>
        </div>

        <template v-else-if="stats">
          <div class="grid grid-cols-2 lg:grid-cols-5 gap-3 mb-6">
            <div
              class="p-4 rounded-xl bg-blue-50 dark:bg-blue-900/20 border border-blue-100 dark:border-blue-800"
            >
              <p
                class="text-[11px] font-medium text-blue-500 dark:text-blue-400 uppercase tracking-wide mb-1"
              >
                创建任务
              </p>
              <p class="text-2xl font-bold text-blue-700 dark:text-blue-300">
                {{ stats.total_created }}
              </p>
            </div>
            <div
              class="p-4 rounded-xl bg-green-50 dark:bg-green-900/20 border border-green-100 dark:border-green-800"
            >
              <p
                class="text-[11px] font-medium text-green-500 dark:text-green-400 uppercase tracking-wide mb-1"
              >
                已完成
              </p>
              <p class="text-2xl font-bold text-green-700 dark:text-green-300">
                {{ stats.total_completed }}
              </p>
            </div>
            <div
              class="p-4 rounded-xl bg-amber-50 dark:bg-amber-900/20 border border-amber-100 dark:border-amber-800"
            >
              <p
                class="text-[11px] font-medium text-amber-500 dark:text-amber-400 uppercase tracking-wide mb-1"
              >
                完成率
              </p>
              <p class="text-2xl font-bold text-amber-700 dark:text-amber-300">
                {{ formatPercent(stats.completion_rate) }}
              </p>
            </div>
            <div
              class="p-4 rounded-xl bg-red-50 dark:bg-red-900/20 border border-red-100 dark:border-red-800"
            >
              <p
                class="text-[11px] font-medium text-red-500 dark:text-red-400 uppercase tracking-wide mb-1"
              >
                被盯办
              </p>
              <p class="text-2xl font-bold text-red-700 dark:text-red-300">
                {{ stats.remind_received }}
              </p>
            </div>
            <div
              class="p-4 rounded-xl bg-purple-50 dark:bg-purple-900/20 border border-purple-100 dark:border-purple-800"
            >
              <p
                class="text-[11px] font-medium text-purple-500 dark:text-purple-400 uppercase tracking-wide mb-1"
              >
                平均耗时
              </p>
              <p class="text-2xl font-bold text-purple-700 dark:text-purple-300">
                {{ formatHours(stats.avg_completion_hours) }}
              </p>
            </div>
          </div>

          <div class="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-6">
            <div
              class="p-5 rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800/40"
            >
              <h3 class="text-sm font-semibold text-slate-700 dark:text-slate-300 mb-4">
                每日任务趋势
              </h3>
              <div
                v-if="stats.daily_trend.length === 0"
                class="text-center py-8 text-slate-400 text-sm"
              >
                暂无数据
              </div>
              <div v-else class="flex items-end gap-1 h-32">
                <div
                  v-for="(d, i) in stats.daily_trend"
                  :key="i"
                  class="flex-1 flex flex-col items-center gap-1 group relative"
                >
                  <div
                    class="w-full bg-blue-400 dark:bg-blue-500 rounded-t-sm transition-all hover:bg-blue-500 dark:hover:bg-blue-400 min-h-[4px]"
                    :style="{ height: Math.max((d.count / maxTrendCount) * 100, 4) + '%' }"
                  ></div>
                  <span
                    class="text-[9px] text-slate-400 dark:text-slate-500 truncate w-full text-center"
                    >{{ d.date.slice(5) }}</span
                  >
                  <div
                    class="absolute -top-6 opacity-0 group-hover:opacity-100 bg-slate-800 text-white text-[10px] px-1.5 py-0.5 rounded whitespace-nowrap transition-opacity pointer-events-none"
                  >
                    {{ d.date }}：{{ d.count }} 条
                  </div>
                </div>
              </div>
            </div>

            <div
              class="p-5 rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800/40"
            >
              <h3 class="text-sm font-semibold text-slate-700 dark:text-slate-300 mb-4">
                标签使用分布
              </h3>
              <div
                v-if="stats.tag_breakdown.length === 0"
                class="text-center py-8 text-slate-400 text-sm"
              >
                暂无数据
              </div>
              <div v-else class="space-y-2.5">
                <div
                  v-for="t in stats.tag_breakdown"
                  :key="t.tag_name"
                  class="flex items-center gap-3"
                >
                  <span
                    class="text-xs text-slate-600 dark:text-slate-400 w-20 truncate"
                    :title="t.tag_name"
                    >{{ t.tag_name }}</span
                  >
                  <div
                    class="flex-1 h-5 bg-slate-200 dark:bg-slate-700 rounded-full overflow-hidden"
                  >
                    <div
                      class="h-full bg-gradient-to-r from-blue-400 to-blue-500 rounded-full transition-all"
                      :style="{
                        width:
                          (t.count / Math.max(...stats.tag_breakdown.map((x) => x.count), 1)) *
                            100 +
                          '%',
                      }"
                    ></div>
                  </div>
                  <span
                    class="text-xs font-mono text-slate-500 dark:text-slate-400 w-8 text-right"
                    >{{ t.count }}</span
                  >
                </div>
              </div>
            </div>
          </div>

          <div
            class="rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800/40 p-5"
          >
            <div class="flex items-center justify-between mb-4">
              <div>
                <h3 class="text-sm font-semibold text-slate-700 dark:text-slate-300">
                  🤖 AI 智能报告
                </h3>
                <p class="text-xs text-slate-400 dark:text-slate-500 mt-0.5">
                  选择日报/周报/月报快速生成，或按周期生成综合分析报告
                  <span class="text-blue-500">（无AI配置时自动使用模板生成）</span>
                </p>
              </div>
              <div class="flex items-center gap-2">
                <button
                  v-if="!reportGenerated"
                  class="px-3 py-2 text-xs font-medium text-blue-600 dark:text-blue-400 bg-blue-50 dark:bg-blue-900/30 hover:bg-blue-100 dark:hover:bg-blue-900/50 rounded-lg transition-smooth disabled:opacity-50 border border-blue-200 dark:border-blue-800"
                  :disabled="reportLoading"
                  @click="handleGeneratePeriodReport('day')"
                  title="生成今日工作成效报告"
                >
                  📅 日报
                </button>
                <button
                  v-if="!reportGenerated"
                  class="px-3 py-2 text-xs font-medium text-purple-600 dark:text-purple-400 bg-purple-50 dark:bg-purple-900/30 hover:bg-purple-100 dark:hover:bg-purple-900/50 rounded-lg transition-smooth disabled:opacity-50 border border-purple-200 dark:border-purple-800"
                  :disabled="reportLoading"
                  @click="handleGeneratePeriodReport('week')"
                  title="生成本周工作成效报告"
                >
                  📆 周报
                </button>
                <button
                  v-if="!reportGenerated"
                  class="px-3 py-2 text-xs font-medium text-teal-600 dark:text-teal-400 bg-teal-50 dark:bg-teal-900/30 hover:bg-teal-100 dark:hover:bg-teal-900/50 rounded-lg transition-smooth disabled:opacity-50 border border-teal-200 dark:border-teal-800"
                  :disabled="reportLoading"
                  @click="handleGeneratePeriodReport('month')"
                  title="生成本月工作成效报告"
                >
                  📊 月报
                </button>
                <button
                  v-if="!reportGenerated"
                  class="px-4 py-2 text-sm font-medium text-white bg-gradient-to-r from-blue-500 to-purple-500 hover:from-blue-600 hover:to-purple-600 rounded-lg transition-smooth disabled:opacity-50 flex items-center gap-2"
                  :disabled="reportLoading"
                  @click="handleGenerateReport()"
                >
                  <span
                    v-if="reportLoading"
                    class="animate-spin rounded-full h-4 w-4 border-2 border-white border-t-transparent"
                  ></span>
                  <span>{{ reportLoading ? '生成中...' : '生成报告' }}</span>
                </button>
                <template v-if="reportGenerated && reportContent">
                  <span
                    v-if="reportType === 'ai'"
                    class="text-[10px] px-1.5 py-0.5 rounded bg-purple-100 dark:bg-purple-900/50 text-purple-600 dark:text-purple-400 font-medium"
                    >AI生成</span
                  >
                  <span
                    v-else
                    class="text-[10px] px-1.5 py-0.5 rounded bg-amber-100 dark:bg-amber-900/50 text-amber-600 dark:text-amber-400 font-medium"
                    >模板生成</span
                  >
                  <button
                    class="px-3 py-2 text-xs font-medium text-slate-600 dark:text-slate-400 bg-white dark:bg-slate-700 hover:bg-slate-100 dark:hover:bg-slate-600 rounded-lg border border-slate-200 dark:border-slate-600 transition-smooth"
                    @click="copyReport()"
                  >
                    📋 复制
                  </button>
                  <button
                    class="px-3 py-2 text-xs font-medium text-slate-600 dark:text-slate-400 bg-white dark:bg-slate-700 hover:bg-slate-100 dark:hover:bg-slate-600 rounded-lg border border-slate-200 dark:border-slate-600 transition-smooth"
                    @click="downloadReport()"
                  >
                    ⬇️ 下载
                  </button>
                </template>
              </div>
            </div>

            <div
              v-if="reportError"
              class="px-4 py-3 rounded-lg text-sm bg-red-50 dark:bg-red-900/30 text-red-700 dark:text-red-400 border border-red-200 dark:border-red-800 mb-3"
            >
              {{ reportError }}
            </div>

            <div
              v-if="!reportGenerated && !reportLoading"
              class="text-center py-8 text-slate-400 dark:text-slate-500"
            >
              <p class="text-2xl mb-2">🤖</p>
              <p class="text-sm">点击"生成报告"按钮</p>
              <p class="text-xs mt-1">AI将基于当前数据自动分析并生成工作报告</p>
            </div>

            <div
              v-if="reportContent"
              class="prose prose-sm dark:prose-invert max-w-none bg-white dark:bg-slate-900 rounded-lg p-5 border border-slate-200 dark:border-slate-700"
            >
              <div v-html="renderedReport"></div>
            </div>
          </div>
        </template>
      </template>

      <!-- ===================== 报告历史 Tab ===================== -->
      <template v-if="viewTab === 'history'">
        <!-- Report filters -->
        <div
          class="mb-4 p-4 rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800/40"
        >
          <div class="flex flex-wrap items-center gap-3">
            <input
              v-model="reportsFilter.keyword"
              type="text"
              class="px-3 py-1.5 text-sm border border-slate-200 dark:border-slate-700 rounded-lg bg-white dark:bg-slate-900 text-slate-900 dark:text-slate-100 focus:outline-none focus:ring-1 focus:ring-blue-400 placeholder-slate-400 w-48"
              placeholder="🔍 关键词搜索..."
              @keyup.enter="applyReportFilters()"
            />
            <select
              v-model="reportsFilter.period"
              class="px-3 py-1.5 text-sm border border-slate-200 dark:border-slate-700 rounded-lg bg-white dark:bg-slate-900 text-slate-900 dark:text-slate-100 focus:outline-none focus:ring-1 focus:ring-blue-400"
              @change="applyReportFilters()"
            >
              <option value="">全部周期</option>
              <option value="week">本周</option>
              <option value="month">本月</option>
              <option value="year">本年度</option>
            </select>
            <input
              v-model="reportsFilter.date_from"
              type="date"
              class="px-3 py-1.5 text-sm border border-slate-200 dark:border-slate-700 rounded-lg bg-white dark:bg-slate-900 text-slate-900 dark:text-slate-100 focus:outline-none focus:ring-1 focus:ring-blue-400"
              @change="applyReportFilters()"
            />
            <span class="text-xs text-slate-400">至</span>
            <input
              v-model="reportsFilter.date_to"
              type="date"
              class="px-3 py-1.5 text-sm border border-slate-200 dark:border-slate-700 rounded-lg bg-white dark:bg-slate-900 text-slate-900 dark:text-slate-100 focus:outline-none focus:ring-1 focus:ring-blue-400"
              @change="applyReportFilters()"
            />
            <button
              class="px-3 py-1.5 text-xs text-slate-500 dark:text-slate-400 hover:text-slate-700 dark:hover:text-slate-300 bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 rounded-lg transition-smooth"
              @click="resetReportFilters()"
            >
              🔄 重置
            </button>
          </div>
        </div>

        <div v-if="reportsLoading" class="flex items-center justify-center py-12">
          <div
            class="animate-spin rounded-full h-8 w-8 border-2 border-blue-500 border-t-transparent"
          ></div>
        </div>

        <div
          v-else-if="reports.length === 0"
          class="text-center py-16 text-slate-400 dark:text-slate-500"
        >
          <p class="text-2xl mb-2">📋</p>
          <p class="text-sm">暂无报告记录</p>
          <p class="text-xs mt-1">在"数据统计"中生成报告后，将自动出现在这里</p>
        </div>

        <!-- Report cards -->
        <div v-else class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          <div
            v-for="report in reports"
            :key="report.id"
            class="p-4 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 hover:shadow-md transition-smooth cursor-pointer group"
            @click="viewReport(report)"
          >
            <div class="flex items-start justify-between mb-2">
              <div class="flex-1 min-w-0">
                <h4 class="text-sm font-semibold text-slate-800 dark:text-slate-200 truncate">
                  {{ report.title }}
                </h4>
                <p class="text-[11px] text-slate-400 dark:text-slate-500 mt-0.5">
                  {{ formatTime(report.created_at) }}
                </p>
              </div>
              <span
                :class="[
                  'text-[10px] px-1.5 py-0.5 rounded font-medium shrink-0 ml-2',
                  report.report_type === 'ai'
                    ? 'bg-purple-100 dark:bg-purple-900/50 text-purple-600 dark:text-purple-400'
                    : 'bg-amber-100 dark:bg-amber-900/50 text-amber-600 dark:text-amber-400',
                ]"
                >{{ report.report_type === 'ai' ? 'AI' : '模板' }}</span
              >
            </div>
            <div
              class="flex items-center gap-2 text-[11px] text-slate-500 dark:text-slate-400 mb-2"
            >
              <span>{{ periodLabels[report.period] || report.period_label }}</span>
              <span>·</span>
              <span>{{ report.user_name }}</span>
            </div>
            <p class="text-xs text-slate-500 dark:text-slate-400 line-clamp-3">
              {{ report.content.slice(0, 150) }}...
            </p>
            <div
              class="flex items-center justify-end gap-2 mt-3 pt-2 border-t border-slate-100 dark:border-slate-700"
            >
              <button
                class="text-[11px] text-red-400 hover:text-red-600 dark:hover:text-red-300 transition-smooth opacity-0 group-hover:opacity-100"
                @click.stop="handleDeleteReport(report.id)"
              >
                🗑 删除
              </button>
            </div>
          </div>
        </div>

        <!-- Pagination -->
        <div v-if="reportsTotal > reportsPageSize" class="flex items-center justify-between mt-6">
          <span class="text-xs text-slate-400 dark:text-slate-500"
            >共 {{ reportsTotal }} 份报告</span
          >
          <div class="flex items-center gap-2">
            <button
              class="px-3 py-1 text-xs font-medium text-slate-600 dark:text-slate-400 bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 rounded transition-smooth disabled:opacity-40"
              :disabled="reportsPage <= 1"
              @click="
                reportsPage--;
                loadReports();
              "
            >
              上一页
            </button>
            <button
              class="px-3 py-1 text-xs font-medium text-slate-600 dark:text-slate-400 bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 rounded transition-smooth disabled:opacity-40"
              :disabled="reportsPage * reportsPageSize >= reportsTotal"
              @click="
                reportsPage++;
                loadReports();
              "
            >
              下一页
            </button>
          </div>
        </div>
      </template>
    </div>

    <!-- Report detail modal -->
    <Teleport to="body">
      <div
        v-if="selectedReport"
        class="fixed inset-0 z-50 flex items-center justify-center bg-black/40"
        @click.self="closeReportDetail()"
      >
        <div
          class="bg-white dark:bg-slate-900 rounded-2xl shadow-2xl w-full max-w-3xl max-h-[80vh] flex flex-col mx-4 overflow-hidden"
        >
          <div
            class="shrink-0 flex items-center justify-between px-6 py-4 border-b border-slate-200 dark:border-slate-700"
          >
            <div>
              <h3 class="text-base font-semibold text-slate-900 dark:text-slate-100">
                {{ selectedReport.title }}
              </h3>
              <p class="text-xs text-slate-400 dark:text-slate-500 mt-0.5">
                {{ formatTime(selectedReport.created_at) }}
                ·
                {{ periodLabels[selectedReport.period] || selectedReport.period_label }}
                ·
                <span
                  :class="
                    selectedReport.report_type === 'ai' ? 'text-purple-500' : 'text-amber-500'
                  "
                >
                  {{ selectedReport.report_type === 'ai' ? 'AI生成' : '模板生成' }}
                </span>
              </p>
            </div>
            <button
              class="text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 text-xl leading-none"
              @click="closeReportDetail()"
            >
              ✕
            </button>
          </div>
          <div class="flex-1 overflow-auto p-6">
            <div class="prose prose-sm dark:prose-invert max-w-none" v-html="renderedReport"></div>
          </div>
          <div
            class="shrink-0 flex items-center justify-end gap-2 px-6 py-3 border-t border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800/40"
          >
            <button
              class="px-3 py-1.5 text-xs font-medium text-red-500 hover:bg-red-50 dark:hover:bg-red-900/30 rounded-lg transition-smooth"
              @click="
                handleDeleteReport(selectedReport.id);
                closeReportDetail();
              "
            >
              🗑 删除
            </button>
            <button
              class="px-3 py-1.5 text-xs font-medium text-slate-600 dark:text-slate-400 bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 rounded-lg transition-smooth"
              @click="
                navigator.clipboard.writeText(selectedReport.content);
                showToast('success', '已复制到剪贴板');
              "
            >
              📋 复制
            </button>
            <button
              class="px-4 py-1.5 text-xs font-medium text-white bg-blue-500 hover:bg-blue-600 rounded-lg transition-smooth"
              @click="closeReportDetail()"
            >
              关闭
            </button>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- Template editor modal -->
    <Teleport to="body">
      <div
        v-if="showTemplateModal"
        class="fixed inset-0 z-50 flex items-center justify-center bg-black/40"
        @click.self="showTemplateModal = false"
      >
        <div
          class="bg-white dark:bg-slate-900 rounded-2xl shadow-2xl w-full max-w-3xl max-h-[85vh] flex flex-col mx-4 overflow-hidden"
        >
          <div
            class="shrink-0 flex items-center justify-between px-6 py-4 border-b border-slate-200 dark:border-slate-700"
          >
            <div>
              <h3 class="text-base font-semibold text-slate-900 dark:text-slate-100">
                📝 编辑报告模板
              </h3>
              <p class="text-xs text-slate-400 dark:text-slate-500 mt-0.5">
                使用
                <code class="text-blue-500 bg-blue-50 dark:bg-blue-900/30 px-1 rounded">{{
                  变量名
                }}</code>
                作为占位符，生成报告时自动替换
              </p>
            </div>
            <button
              class="text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 text-xl leading-none"
              @click="showTemplateModal = false"
            >
              ✕
            </button>
          </div>
          <div
            class="shrink-0 px-6 py-2 border-b border-slate-100 dark:border-slate-800 flex flex-wrap gap-1.5"
          >
            <span class="text-[10px] text-slate-400 dark:text-slate-500">可用变量：</span>
            <code
              v-for="v in [
                '{{userName}}',
                '{{periodLabel}}',
                '{{totalCreated}}',
                '{{totalCompleted}}',
                '{{completionRate}}',
                '{{completionDesc}}',
                '{{remindDesc}}',
                '{{remindReceived}}',
                '{{avgCompletionHours}}',
                '{{tagList}}',
                '{{dailyTrend}}',
                '{{activeTagMsg}}',
              ]"
              :key="v"
              class="text-[10px] bg-blue-50 dark:bg-blue-900/30 text-blue-600 dark:text-blue-400 px-1.5 py-0.5 rounded cursor-pointer hover:bg-blue-100 dark:hover:bg-blue-800/50"
              @click="templateContent += v"
              >{{ v }}</code
            >
          </div>
          <div v-if="templateLoading" class="flex items-center justify-center py-16">
            <div
              class="animate-spin rounded-full h-8 w-8 border-2 border-blue-500 border-t-transparent"
            ></div>
          </div>
          <div v-else class="flex-1 overflow-hidden p-4">
            <textarea
              v-model="templateContent"
              class="w-full h-full min-h-[300px] p-4 text-sm font-mono border border-slate-200 dark:border-slate-700 rounded-xl bg-white dark:bg-slate-800 text-slate-900 dark:text-slate-100 resize-none focus:outline-none focus:ring-2 focus:ring-blue-400 placeholder-slate-400"
              placeholder="输入 Markdown 格式的报告模板..."
            ></textarea>
          </div>
          <div
            class="shrink-0 flex items-center justify-between px-6 py-3 border-t border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800/40"
          >
            <span class="text-[11px] text-slate-400 dark:text-slate-500"
              >💡 模板使用 Markdown 格式，点击上方变量快速插入</span
            >
            <div class="flex items-center gap-2">
              <button
                class="px-3 py-1.5 text-xs font-medium text-slate-600 dark:text-slate-400 bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 rounded-lg transition-smooth"
                @click="showTemplateModal = false"
              >
                取消
              </button>
              <button
                class="px-4 py-1.5 text-xs font-medium text-white bg-blue-500 hover:bg-blue-600 rounded-lg transition-smooth disabled:opacity-50"
                :disabled="templateSaving"
                @click="handleSaveTemplate()"
              >
                <span
                  v-if="templateSaving"
                  class="animate-spin rounded-full h-3 w-3 border-2 border-white border-t-transparent inline-block mr-1"
                ></span>
                保存
              </button>
            </div>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>
