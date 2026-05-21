<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed } from 'vue';
import { useRouter } from 'vue-router';
import { fetchNoteStats } from '@/services/notes';
import { fetchLedger } from '@/services/admin';
import { getUsers } from '@/services/admin';

const router = useRouter();

const currentTime = ref(new Date());
let timer: ReturnType<typeof setInterval>;

const period = ref<'7' | '30' | '90'>('7');
const loading = ref(true);
const totalNotes = ref(0);
const activeNotes = ref(0);
const trendData = ref<{ date: string; count: number }[]>([]);
const staffCount = ref(0);
const activities = ref<any[]>([]);

const maxCount = computed(() => {
  if (trendData.value.length === 0) return 1;
  return Math.max(...trendData.value.map((d) => d.count), 1);
});

const CHART_X0 = 40;
const CHART_W = 460;
const CHART_Y0 = 15;
const CHART_H = 240;

function buildSmoothPath(pts: { x: number; y: number }[]): string {
  if (pts.length === 0) return '';
  if (pts.length === 1) return `M${pts[0].x},${pts[0].y}`;
  const tension = 0.3;
  const cp: { x: number; y: number }[][] = [];
  for (let i = 0; i < pts.length - 1; i++) {
    const p0 = pts[Math.max(i - 1, 0)];
    const p1 = pts[i];
    const p2 = pts[i + 1];
    const p3 = pts[Math.min(i + 2, pts.length - 1)];
    const dx = p2.x - p1.x;
    cp[i] = [
      { x: p1.x + dx * tension, y: p1.y + (p2.y - p0.y) * tension * 0.5 },
      { x: p2.x - dx * tension, y: p2.y - (p3.y - p1.y) * tension * 0.5 },
    ];
  }
  let d = `M${pts[0].x},${pts[0].y}`;
  for (let i = 0; i < pts.length - 1; i++) {
    const c = cp[i];
    d += ` C${c[0].x},${c[0].y} ${c[1].x},${c[1].y} ${pts[i + 1].x},${pts[i + 1].y}`;
  }
  return d;
}

const linePath = computed(() => {
  if (trendData.value.length === 0) return '';
  const pts = trendData.value.map((d, i) => ({
    x: CHART_X0 + (i / Math.max(trendData.value.length - 1, 1)) * CHART_W,
    y: CHART_Y0 + CHART_H - (d.count / maxCount.value) * CHART_H,
  }));
  return buildSmoothPath(pts);
});

const areaPath = computed(() => {
  if (trendData.value.length === 0) return '';
  const lastX = CHART_X0 + CHART_W;
  const baseY = CHART_Y0 + CHART_H;
  return `${linePath.value} L${lastX},${baseY} L${CHART_X0},${baseY} Z`;
});

const chartPoints = computed(() => {
  if (trendData.value.length === 0) return [];
  return trendData.value.map((d, i) => {
    const x = CHART_X0 + (i / Math.max(trendData.value.length - 1, 1)) * CHART_W;
    const y = CHART_Y0 + CHART_H - (d.count / maxCount.value) * CHART_H;
    return { x, y, ...d };
  });
});

const yTicks = computed(() => {
  const m = maxCount.value;
  const step = m > 10 ? Math.ceil(m / 4) : m > 4 ? Math.ceil(m / 3) : 1;
  const ticks: { y: number; label: string }[] = [];
  for (let v = 0; v <= m; v += step) {
    ticks.push({ y: CHART_Y0 + CHART_H - (v / m) * CHART_H, label: String(v) });
  }
  if (ticks[ticks.length - 1].label !== String(m)) {
    ticks.push({ y: CHART_Y0, label: String(m) });
  }
  return ticks;
});

const xLabels = computed(() => {
  if (trendData.value.length === 0) return [];
  const data = trendData.value;
  if (data.length <= 7) return data.map((d) => d.date);
  const step = Math.ceil(data.length / 7);
  return data.filter((_, i) => i % step === 0).map((d) => d.date);
});

onMounted(async () => {
  timer = setInterval(() => {
    currentTime.value = new Date();
  }, 1000);
  try {
    const [statsRes, ledgerRes, usersRes] = await Promise.all([
      fetchNoteStats({ days: parseInt(period.value) }),
      fetchLedger({ page: 1, page_size: 10 }),
      getUsers({ page: 1, page_size: 1 }),
    ]);
    totalNotes.value = statsRes.data.total_notes || 0;
    activeNotes.value = statsRes.data.active_notes || 0;
    trendData.value = statsRes.data.trend || [];
    activities.value = (ledgerRes.data as unknown as { data: any[] }).data || [];
    staffCount.value = (usersRes.data as unknown as { total: number }).total || 0;
  } catch {
    /* ignore */
  } finally {
    loading.value = false;
  }
});

onUnmounted(() => clearInterval(timer));

async function changePeriod(p: '7' | '30' | '90') {
  period.value = p;
  try {
    const res = await fetchNoteStats({ days: parseInt(p) });
    trendData.value = res.data.trend || [];
  } catch {
    /* ignore */
  }
}

function formatTime(d: Date) {
  return `${d.getHours().toString().padStart(2, '0')}:${d.getMinutes().toString().padStart(2, '0')}:${d.getSeconds().toString().padStart(2, '0')}`;
}

function formatDate(d: Date) {
  return `${d.getFullYear()}年${d.getMonth() + 1}月${d.getDate()}日`;
}

function fmtNum(n: number): string {
  if (n >= 10000) return (n / 10000).toFixed(1) + 'w';
  if (n >= 1000) return (n / 1000).toFixed(1) + 'k';
  return String(n);
}

function getActionLabel(action: string): string {
  const map: Record<string, string> = {
    create: '创建任务',
    update: '更新任务',
    complete: '完成归档',
    remind: '盯办提醒',
    restore: '恢复任务',
    delete: '删除任务',
  };
  return map[action] || action;
}

function timeAgo(ts: string): string {
  const diff = Date.now() - new Date(ts).getTime();
  if (diff < 60000) return '刚刚';
  if (diff < 3600000) return `${Math.floor(diff / 60000)}分钟前`;
  if (diff < 86400000) return `${Math.floor(diff / 3600000)}小时前`;
  return `${Math.floor(diff / 86400000)}天前`;
}
</script>

<template>
  <div class="fixed inset-0 flex flex-col overflow-hidden bg-white">
    <!-- 背景网格纹理 -->
    <div
      class="absolute inset-0 pointer-events-none opacity-[0.04]"
      style="
        background-image: radial-gradient(circle, #3b82f6 1px, transparent 1px);
        background-size: 28px 28px;
      "
    />

    <!-- 顶部信息条 -->
    <div
      class="h-16 border-b border-slate-100 flex items-center justify-between px-8 shrink-0 bg-white/80 backdrop-blur-sm"
    >
      <div class="flex items-center gap-6">
        <div
          class="text-2xl font-bold tabular-nums text-slate-800"
          style="font-family: 'JetBrains Mono', 'Consolas', 'Courier New', monospace"
        >
          {{ formatTime(currentTime) }}
        </div>
        <span class="text-slate-500 text-sm">{{ formatDate(currentTime) }}</span>
      </div>
      <div class="flex items-center gap-4">
        <h1 class="text-slate-900 font-semibold text-lg tracking-wide">轻燕工作台 · 数据大屏</h1>
        <button
          class="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-blue-600 dark:text-blue-400 bg-blue-50 dark:bg-blue-900/30 hover:bg-blue-100 dark:hover:bg-blue-800/40 border border-blue-200 dark:border-blue-800 rounded-lg transition-smooth"
          @click="router.push('/workbench')"
        >
          ← 返回工作台
        </button>
        <div
          class="flex items-center gap-2 px-3 py-1 rounded-full border border-green-300 bg-green-50"
        >
          <span
            class="w-2 h-2 rounded-full bg-green-500 shadow-lg shadow-green-400/50 animate-pulse"
          />
          <span class="text-green-600 text-xs font-medium">实时</span>
        </div>
      </div>
    </div>

    <!-- 主内容区 -->
    <div class="flex-1 p-6 overflow-auto">
      <div v-if="loading" class="flex items-center justify-center h-full">
        <div class="flex flex-col items-center gap-4">
          <div
            class="w-10 h-10 border-2 border-blue-200 border-t-blue-500 rounded-full animate-spin"
          />
          <span class="text-slate-400 text-sm">加载数据中...</span>
        </div>
      </div>

      <template v-else>
        <!-- 指标卡片行 -->
        <div class="grid grid-cols-4 gap-5 mb-6">
          <div
            class="relative overflow-hidden rounded-2xl border border-blue-100 bg-white shadow-card p-5 flex flex-col"
          >
            <div
              class="absolute top-0 right-0 w-24 h-24 opacity-[0.06]"
              style="background: radial-gradient(circle, #3b82f6, transparent 70%)"
            />
            <span class="text-slate-400 text-xs mb-2 tracking-wider uppercase">任务总数</span>
            <span
              class="text-4xl font-bold text-slate-900 tabular-nums"
              style="font-family: 'JetBrains Mono', 'Consolas', 'Courier New', monospace"
              >{{ fmtNum(totalNotes) }}</span
            >
            <span class="text-blue-500 text-xs mt-1 font-medium">系统累计</span>
          </div>

          <div
            class="relative overflow-hidden rounded-2xl border border-green-100 bg-white shadow-card p-5 flex flex-col"
          >
            <div
              class="absolute top-0 right-0 w-24 h-24 opacity-[0.06]"
              style="background: radial-gradient(circle, #22c55e, transparent 70%)"
            />
            <span class="text-slate-400 text-xs mb-2 tracking-wider uppercase">活跃任务</span>
            <span
              class="text-4xl font-bold text-slate-900 tabular-nums"
              style="font-family: 'JetBrains Mono', 'Consolas', 'Courier New', monospace"
              >{{ fmtNum(activeNotes) }}</span
            >
            <span class="text-green-500 text-xs mt-1 font-medium">进行中</span>
          </div>

          <div
            class="relative overflow-hidden rounded-2xl border border-purple-100 bg-white shadow-card p-5 flex flex-col"
          >
            <div
              class="absolute top-0 right-0 w-24 h-24 opacity-[0.06]"
              style="background: radial-gradient(circle, #a855f7, transparent 70%)"
            />
            <span class="text-slate-400 text-xs mb-2 tracking-wider uppercase">归档任务</span>
            <span
              class="text-4xl font-bold text-slate-900 tabular-nums"
              style="font-family: 'JetBrains Mono', 'Consolas', 'Courier New', monospace"
              >{{ fmtNum(totalNotes - activeNotes) }}</span
            >
            <span class="text-purple-500 text-xs mt-1 font-medium">已完成 & 已归档</span>
          </div>

          <div
            class="relative overflow-hidden rounded-2xl border border-amber-100 bg-white shadow-card p-5 flex flex-col"
          >
            <div
              class="absolute top-0 right-0 w-24 h-24 opacity-[0.06]"
              style="background: radial-gradient(circle, #fbbf24, transparent 70%)"
            />
            <span class="text-slate-400 text-xs mb-2 tracking-wider uppercase">人员总数</span>
            <span
              class="text-4xl font-bold text-slate-900 tabular-nums"
              style="font-family: 'JetBrains Mono', 'Consolas', 'Courier New', monospace"
              >{{ fmtNum(staffCount) }}</span
            >
            <span class="text-amber-500 text-xs mt-1 font-medium">全部门在职</span>
          </div>
        </div>

        <!-- 趋势图 + 时间轴 -->
        <div class="grid grid-cols-3 gap-5">
          <!-- 趋势图 占 2/3 -->
          <div
            class="col-span-2 rounded-2xl border border-slate-100 bg-white shadow-card p-6 flex flex-col"
          >
            <div class="flex items-center justify-between mb-3">
              <h3 class="text-slate-900 font-semibold text-sm">任务增长趋势</h3>
              <div class="flex gap-1 bg-slate-100 rounded-btn p-0.5">
                <button
                  v-for="p in [
                    { v: '7', l: '周' },
                    { v: '30', l: '月' },
                    { v: '90', l: '季' },
                  ] as const"
                  :key="p.v"
                  :class="[
                    'px-3 py-1 rounded-md text-xs transition-all',
                    period === p.v
                      ? 'bg-blue-500 text-white shadow-sm'
                      : 'text-slate-500 hover:text-slate-700',
                  ]"
                  @click="changePeriod(p.v)"
                >
                  近一{{ p.l }}
                </button>
              </div>
            </div>

            <div class="h-[280px]">
              <svg
                v-if="trendData.length > 0"
                viewBox="0 0 520 280"
                class="w-full h-full"
                preserveAspectRatio="xMidYMid meet"
              >
                <defs>
                  <linearGradient id="areaGrad" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stop-color="#3B82F6" stop-opacity="0.18" />
                    <stop offset="100%" stop-color="#3B82F6" stop-opacity="0.01" />
                  </linearGradient>
                </defs>
                <!-- Y轴网格线 + 刻度标签 -->
                <g v-for="(v, i) in yTicks" :key="'y' + i">
                  <line :x1="40" :y1="v.y" :x2="500" :y2="v.y" stroke="#F1F5F9" stroke-width="1" />
                  <text :x="36" :y="v.y + 4" text-anchor="end" class="text-[10px]" fill="#94A3B8">
                    {{ v.label }}
                  </text>
                </g>
                <!-- 面积 -->
                <path :d="areaPath" fill="url(#areaGrad)" />
                <!-- 线条 -->
                <path
                  :d="linePath"
                  fill="none"
                  stroke="#3B82F6"
                  stroke-width="2"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                />
                <!-- 数据点 -->
                <circle
                  v-for="(p, i) in chartPoints"
                  :key="i"
                  :cx="p.x"
                  :cy="p.y"
                  r="4"
                  fill="white"
                  stroke="#3B82F6"
                  stroke-width="2.5"
                  class="cursor-pointer"
                >
                  <title>{{ p.date }} · {{ p.count }}条</title>
                </circle>
                <!-- X轴日期标签 -->
                <text
                  v-for="(d, i) in xLabels"
                  :key="'x' + i"
                  :x="40 + i * (460 / Math.max(xLabels.length - 1, 1))"
                  :y="270"
                  text-anchor="middle"
                  class="text-[10px]"
                  fill="#94A3B8"
                >
                  {{ d.slice(5) }}
                </text>
              </svg>
              <div v-else class="h-full flex items-center justify-center text-slate-300 text-sm">
                暂无趋势数据
              </div>
            </div>
          </div>

          <!-- 动态时间轴 占 1/3 -->
          <div
            class="rounded-2xl border border-slate-100 bg-white shadow-card p-5 flex flex-col overflow-hidden"
          >
            <h3 class="text-slate-900 font-semibold text-sm mb-4 shrink-0">最新动态</h3>
            <div class="flex-1 overflow-auto scrollbar-thin space-y-0 relative pl-5">
              <div class="absolute left-[7px] top-1 bottom-1 w-px bg-slate-200" />

              <div
                v-for="(act, idx) in activities.slice(0, 10)"
                :key="idx"
                class="relative pb-4 group cursor-default"
              >
                <div
                  class="absolute left-[-17px] top-1.5 w-2.5 h-2.5 rounded-full border-2 border-white"
                  :style="{
                    backgroundColor:
                      (
                        {
                          create: '#22C55E',
                          complete: '#3B82F6',
                          remind: '#EF4444',
                          update: '#F59E0B',
                        } as any
                      )[act.action] || '#94A3B8',
                  }"
                />
                <div
                  class="bg-slate-50 rounded-lg p-3 border border-slate-50 transition-all group-hover:bg-blue-50/50 group-hover:border-blue-100 group-hover:translate-x-1"
                >
                  <div class="flex items-center gap-2 mb-1">
                    <span class="text-xs font-medium text-slate-900">{{
                      act.user?.name || '系统'
                    }}</span>
                    <span
                      class="text-[10px] px-1.5 py-0.5 rounded-full font-medium"
                      :style="{
                        backgroundColor:
                          (
                            {
                              create: '#DCFCE7',
                              complete: '#DBEAFE',
                              remind: '#FEE2E2',
                              update: '#FEF3C7',
                            } as any
                          )[act.action] || '#F1F5F9',
                        color:
                          (
                            {
                              create: '#16A34A',
                              complete: '#2563EB',
                              remind: '#DC2626',
                              update: '#D97706',
                            } as any
                          )[act.action] || '#64748B',
                      }"
                    >
                      {{ getActionLabel(act.action) }}
                    </span>
                  </div>
                  <p class="text-xs text-slate-500 truncate">{{ act.action_detail || '—' }}</p>
                  <span class="text-[10px] text-slate-400 mt-1 block">{{
                    timeAgo(act.created_at)
                  }}</span>
                </div>
              </div>

              <div v-if="activities.length === 0" class="text-center py-8 text-slate-300 text-xs">
                暂无动态记录
              </div>
            </div>
          </div>
        </div>
      </template>
    </div>

    <!-- 底部状态 -->
    <div class="h-10 border-t border-slate-100 flex items-center px-8 shrink-0 gap-4 bg-white">
      <span class="text-slate-400 text-xs flex items-center gap-1.5">
        <span class="w-1.5 h-1.5 rounded-full bg-green-500" />
        系统运行中
      </span>
      <span class="text-slate-300 text-xs">|</span>
      <span class="text-slate-400 text-xs">数据刷新间隔: 实时</span>
      <span class="flex-1" />
      <span class="text-slate-300 text-xs">轻燕工作台 v1.0</span>
    </div>
  </div>
</template>
