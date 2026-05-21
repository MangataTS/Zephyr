<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { fetchNoteStats, fetchHeatmap } from '@/services/notes';
import { fetchNotes } from '@/services/notes';
import { updateUser } from '@/services/admin';
import type { NoteFilters } from '@/types';

const auth = useAuthStore();

const loading = ref(true);
const loadError = ref('');

const totalNotes = ref(0);
const activeNotes = ref(0);
const archivedNotes = ref(0);
const completedNotes = ref(0);
const trendData = ref<{ date: string; count: number }[]>([]);

const savingProfile = ref(false);
const profileSaved = ref(false);
const profileError = ref('');
const editName = ref('');
const editPhone = ref('');
const editEmail = ref('');
const editRank = ref('');

const thisYear = new Date().getFullYear();
const yearOptions = computed(() => {
  const years: number[] = [];
  for (let y = thisYear; y >= thisYear - 3; y--) years.push(y);
  return years;
});
const selectedYear = ref(thisYear);
const hoveredCell = ref<{ date: string; count: number } | null>(null);

interface HeatCell {
  date: string;
  dayOfWeek: number;
  week: number;
  count: number;
  level: number;
}

function generateHeatData(): HeatCell[] {
  const cells: HeatCell[] = [];
  const year = selectedYear.value;
  const start = new Date(year, 0, 1);
  const end = new Date(year, 11, 31);
  let w = 0;
  let prevDow = -1;
  for (const d = new Date(start); d <= end; d.setDate(d.getDate() + 1)) {
    const dow = d.getDay();
    if (dow <= prevDow) w++;
    prevDow = dow;
    const dateStr = d.toISOString().slice(0, 10);
    const trendPoint = trendData.value.find((t) => t.date === dateStr);
    const count = trendPoint?.count || 0;
    let level = 0;
    if (count > 0) level = 1;
    if (count > 2) level = 2;
    if (count > 5) level = 3;
    if (count > 10) level = 4;
    cells.push({ date: dateStr, dayOfWeek: dow, week: w, count, level });
  }
  return cells;
}

const heatData = computed(() => generateHeatData());

const weekGroups = computed(() => {
  const groups: HeatCell[][] = [];
  const data = heatData.value;
  let cur: HeatCell[] = [];
  let curWeek = -1;
  for (const cell of data) {
    if (cell.week !== curWeek) {
      if (cur.length) groups.push(cur);
      cur = [];
      curWeek = cell.week;
    }
    cur.push(cell);
  }
  if (cur.length) groups.push(cur);
  return groups;
});

const monthLabels = computed(() => {
  const labels: { col: number; label: string }[] = [];
  const groups = weekGroups.value;
  if (groups.length === 0) return labels;
  let prevMonth = -1;
  groups.forEach((week, colIdx) => {
    const d = new Date(week[0]?.date || '');
    const m = d.getMonth();
    if (m !== prevMonth) {
      labels.push({ col: colIdx, label: `${m + 1}月` });
      prevMonth = m;
    }
  });
  return labels;
});

function getCellColor(level: number): string {
  const colors = ['#F1F5F9', '#93C5FD', '#60A5FA', '#3B82F6', '#1D4ED8'];
  return colors[level] || colors[0];
}

function getCellTitle(cell: HeatCell): string {
  if (cell.count === 0) return `${cell.date} · 无活动`;
  return `${cell.date} · ${cell.count} 条任务`;
}

onMounted(loadData);

async function loadData() {
  loading.value = true;
  loadError.value = '';
  try {
    const [statsRes, heatmapRes] = await Promise.all([
      fetchNoteStats({ days: 30 }),
      fetchHeatmap(selectedYear.value),
    ]);
    totalNotes.value = statsRes.data.total_notes || 0;
    activeNotes.value = statsRes.data.active_notes || 0;
    trendData.value = heatmapRes.data.daily || [];
    archivedNotes.value = heatmapRes.data.total_archived || 0;

    try {
      const completedRes = await fetchNotes({
        status: 'completed' as any,
        page: 1,
        page_size: 1,
      } as NoteFilters);
      completedNotes.value = (completedRes.data as unknown as { total: number }).total || 0;
    } catch {
      completedNotes.value = 0;
    }

    if (auth.user) {
      editName.value = auth.user.name || '';
      editPhone.value = auth.user.phone || '';
      editEmail.value = auth.user.email || '';
      editRank.value = auth.user.rank || '';
    }
  } catch {
    loadError.value = '加载数据失败';
  } finally {
    loading.value = false;
  }
}

function fmtNum(n: number): string {
  if (n >= 10000) return (n / 10000).toFixed(1) + 'w';
  if (n >= 1000) return (n / 1000).toFixed(1) + 'k';
  return String(n);
}

const roleLabel = computed(() => {
  const map: Record<string, string> = {
    super_admin: '系统管理员',
    dept_admin: '部门管理员',
    group_leader: '组长',
    user: '普通员工',
    screen_role: '大屏角色',
  };
  return map[auth.user?.role || ''] || '—';
});

async function handleSaveProfile() {
  if (!auth.user) return;
  savingProfile.value = true;
  profileSaved.value = false;
  profileError.value = '';
  try {
    await updateUser(auth.user.id, {
      name: editName.value.trim(),
      phone: editPhone.value.trim() || '',
      email: editEmail.value.trim() || '',
      rank: editRank.value.trim() || '',
    });
    auth.user.name = editName.value.trim();
    auth.user.phone = editPhone.value.trim();
    auth.user.email = editEmail.value.trim();
    auth.user.rank = editRank.value.trim();
    localStorage.setItem('auth_user', JSON.stringify(auth.user));
    profileSaved.value = true;
    setTimeout(() => {
      profileSaved.value = false;
    }, 2000);
  } catch (e: unknown) {
    const err = e as { response?: { data?: { message?: string } } };
    profileError.value = err?.response?.data?.message || '保存失败';
  } finally {
    savingProfile.value = false;
  }
}
</script>

<template>
  <div class="w-full">
    <h2 class="text-lg font-semibold text-slate-900 dark:text-slate-100 mb-6">个人中心</h2>

    <div v-if="loading" class="space-y-4">
      <div class="skeleton h-24 rounded-card" />
      <div class="skeleton h-16 rounded-card" />
      <div class="skeleton h-64 rounded-card" />
    </div>

    <div v-else-if="loadError" class="text-center py-16 text-sm text-red-400">
      {{ loadError }}
      <button class="block mx-auto mt-2 text-blue-500 hover:underline" @click="loadData">
        重试
      </button>
    </div>

    <template v-else>
      <!-- 用户信息 + 统计卡片合并行 -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-5 mb-6">
        <!-- 用户信息卡 -->
        <div
          class="bg-white dark:bg-slate-800 rounded-card border border-slate-100 dark:border-slate-700 p-6 transition-colors duration-300 flex items-center gap-4"
        >
          <div
            class="w-14 h-14 rounded-full bg-blue-500 flex items-center justify-center text-lg font-semibold text-white shrink-0"
          >
            {{ auth.user?.name?.charAt(0) || '用' }}
          </div>
          <div class="min-w-0">
            <h3 class="text-base font-semibold text-slate-900 dark:text-slate-100 truncate">
              {{ auth.user?.name || '未登录' }}
            </h3>
            <p class="text-xs text-slate-500 dark:text-slate-400">
              {{ auth.user?.dept_name || '' }}
            </p>
            <p class="text-xs text-slate-400 dark:text-slate-500 mt-0.5">{{ roleLabel }}</p>
          </div>
        </div>

        <!-- 统计卡片 -->
        <div class="col-span-2 grid grid-cols-4 gap-3">
          <div
            class="rounded-card p-4 text-center bg-amber-50 dark:bg-amber-900/20 transition-colors duration-300"
          >
            <div class="text-2xl font-bold text-amber-700 dark:text-amber-400 tabular-nums">
              {{ fmtNum(activeNotes) }}
            </div>
            <div class="text-xs text-amber-600 dark:text-amber-500 mt-1">活跃任务</div>
          </div>
          <div
            class="rounded-card p-4 text-center bg-green-50 dark:bg-green-900/20 transition-colors duration-300"
          >
            <div class="text-2xl font-bold text-green-700 dark:text-green-400 tabular-nums">
              {{ fmtNum(completedNotes) }}
            </div>
            <div class="text-xs text-green-600 dark:text-green-500 mt-1">已完成</div>
          </div>
          <div
            class="rounded-card p-4 text-center bg-red-50 dark:bg-red-900/20 transition-colors duration-300"
          >
            <div class="text-2xl font-bold text-red-700 dark:text-red-400 tabular-nums">
              {{ fmtNum(archivedNotes) }}
            </div>
            <div class="text-xs text-red-600 dark:text-red-500 mt-1">已归档</div>
          </div>
          <div
            class="rounded-card p-4 text-center bg-slate-50 dark:bg-slate-800 transition-colors duration-300"
          >
            <div class="text-2xl font-bold text-slate-700 dark:text-slate-300 tabular-nums">
              {{ fmtNum(totalNotes) }}
            </div>
            <div class="text-xs text-slate-500 dark:text-slate-400 mt-1">任务总数</div>
          </div>
        </div>
      </div>

      <!-- 活动热力图 (全宽) -->
      <div class="mb-5">
        <div
          class="bg-white dark:bg-slate-800 rounded-card border border-slate-100 dark:border-slate-700 p-6 transition-colors duration-300"
        >
          <div class="flex items-center justify-between mb-4">
            <div>
              <h4 class="text-sm font-semibold text-slate-900 dark:text-slate-100">
                归档活动热力图
              </h4>
              <span v-if="hoveredCell" class="text-xs text-slate-500 dark:text-slate-400 ml-2">
                {{ hoveredCell.date }} · {{ hoveredCell.count }}条归档
              </span>
            </div>
            <div class="flex items-center gap-3">
              <select
                v-model="selectedYear"
                class="input-field !w-auto !py-1 !text-xs"
                @change="loadData"
              >
                <option v-for="y in yearOptions" :key="y" :value="y">{{ y }}年</option>
              </select>
              <div class="hidden sm:flex items-center gap-1">
                <div class="w-3 h-3 rounded-[2px]" style="background-color: #f1f5f9" title="0" />
                <div class="w-3 h-3 rounded-[2px]" style="background-color: #93c5fd" title="1-2" />
                <div class="w-3 h-3 rounded-[2px]" style="background-color: #60a5fa" title="3-5" />
                <div class="w-3 h-3 rounded-[2px]" style="background-color: #3b82f6" title="6-10" />
                <div class="w-3 h-3 rounded-[2px]" style="background-color: #1d4ed8" title="10+" />
              </div>
            </div>
          </div>

          <div class="flex gap-1">
            <!-- 星期标签 -->
            <div class="flex flex-col gap-1 pt-3 mr-1">
              <span class="text-[9px] text-slate-400 dark:text-slate-600 h-3 leading-3">一</span>
              <span class="text-[9px] text-slate-400 dark:text-slate-600 h-3 leading-3" />
              <span class="text-[9px] text-slate-400 dark:text-slate-600 h-3 leading-3">三</span>
              <span class="text-[9px] text-slate-400 dark:text-slate-600 h-3 leading-3" />
              <span class="text-[9px] text-slate-400 dark:text-slate-600 h-3 leading-3">五</span>
              <span class="text-[9px] text-slate-400 dark:text-slate-600 h-3 leading-3" />
              <span class="text-[9px] text-slate-400 dark:text-slate-600 h-3 leading-3">日</span>
            </div>

            <div class="flex-1 overflow-x-auto scrollbar-thin">
              <!-- 月份标签 -->
              <div class="flex gap-1 mb-1" :style="{ paddingLeft: '0px' }">
                <div v-if="monthLabels.length === 0" class="flex-1" />
                <template v-for="(ml, idx) in monthLabels" :key="idx">
                  <div v-if="idx === 0" :style="{ width: '0px', flexShrink: 0 }" />
                  <div
                    v-else
                    :style="{
                      width: `${(ml.col - monthLabels[idx - 1].col) * 14}px`,
                      flexShrink: 0,
                    }"
                  />
                  <span
                    class="text-[9px] text-slate-400 dark:text-slate-600 whitespace-nowrap shrink-0"
                    >{{ ml.label }}</span
                  >
                </template>
              </div>

              <!-- 色块网格 -->
              <div class="flex gap-[2px]">
                <div v-for="(week, wi) in weekGroups" :key="wi" class="flex flex-col gap-[2px]">
                  <div
                    v-for="(cell, di) in week"
                    :key="di"
                    class="w-3 h-3 rounded-[2px] transition-colors duration-150 cursor-pointer hover:ring-1 hover:ring-slate-400"
                    :style="{ backgroundColor: getCellColor(cell.level) }"
                    :title="getCellTitle(cell)"
                    @mouseenter="hoveredCell = cell"
                    @mouseleave="hoveredCell = null"
                  />
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- 个人信息编辑表单 -->
      <div class="mb-6">
        <div
          class="bg-white dark:bg-slate-800 rounded-card border border-slate-100 dark:border-slate-700 p-6 transition-colors duration-300 max-w-lg"
        >
          <h4 class="text-sm font-semibold text-slate-900 dark:text-slate-100 mb-4">个人信息</h4>

          <form @submit.prevent="handleSaveProfile" class="space-y-3">
            <div>
              <span class="text-xs text-slate-400 dark:text-slate-500 mb-1 block">姓名</span>
              <input v-model="editName" name="name" class="input-field !py-1.5 !text-sm" placeholder="姓名" />
            </div>
            <div>
              <span class="text-xs text-slate-400 dark:text-slate-500 mb-1 block">部门</span>
              <input
                :value="auth.user?.dept_name || ''"
                class="input-field !py-1.5 !text-sm bg-slate-50 dark:bg-slate-900 text-slate-400 dark:text-slate-500"
                disabled
              />
            </div>
            <div>
              <span class="text-xs text-slate-400 dark:text-slate-500 mb-1 block">警衔/职级</span>
              <input
                v-model="editRank"
                class="input-field !py-1.5 !text-sm"
                placeholder="如：二级警督"
              />
            </div>
            <div>
              <span class="text-xs text-slate-400 dark:text-slate-500 mb-1 block">手机号</span>
              <input
                v-model="editPhone"
                class="input-field !py-1.5 !text-sm"
                placeholder="手机号"
              />
            </div>
            <div>
              <span class="text-xs text-slate-400 dark:text-slate-500 mb-1 block">邮箱</span>
              <input v-model="editEmail" name="email" class="input-field !py-1.5 !text-sm" placeholder="邮箱" />
            </div>
            <div>
              <span class="text-xs text-slate-400 dark:text-slate-500 mb-1 block">角色</span>
              <input
                :value="roleLabel"
                class="input-field !py-1.5 !text-sm bg-slate-50 dark:bg-slate-900 text-slate-400 dark:text-slate-500"
                disabled
              />
            </div>

            <p v-if="profileError" class="text-xs text-red-500">{{ profileError }}</p>
            <p v-if="profileSaved" class="text-xs text-green-500">✓ 已保存</p>

            <button
              type="submit"
              class="w-full btn-primary text-sm !py-2 disabled:opacity-50"
              :disabled="savingProfile"
            >
              {{ savingProfile ? '保存中...' : '保存修改' }}
            </button>
          </form>
        </div>
      </div>
    </template>
  </div>
</template>
