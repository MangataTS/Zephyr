<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { getGroupDashboard, type DashboardData, type DashboardColumn } from '@/services/groupNotes';
import { useGroupSocket } from '@/composables/useGroupSocket';

const route = useRoute();
const router = useRouter();
const groupId = route.params.id as string;

const { onNoteUpdated } = useGroupSocket(groupId);

const data = ref<DashboardData | null>(null);
const loading = ref(true);
const error = ref('');
const autoRefresh = ref(true);
const isFullscreen = ref(false);
let refreshTimer: ReturnType<typeof setInterval> | null = null;
let mounted = true;

async function loadDashboard() {
  if (!mounted) return;
  try {
    const res = await getGroupDashboard(groupId);
    data.value = res.data as unknown as DashboardData;
    error.value = '';
  } catch (e: unknown) {
    const err = e as Error;
    if (err.message?.includes('403')) {
      error.value = '您不是该工作组成员，无权访问协作大屏';
    } else if (err.message?.includes('404')) {
      error.value = '工作组不存在或已被删除';
    } else {
      error.value = '数据加载失败，请检查网络后重试';
    }
  } finally {
    if (mounted) loading.value = false;
  }
}

onMounted(() => {
  mounted = true;
  loadDashboard();
  refreshTimer = setInterval(loadDashboard, 5000);
  document.addEventListener('fullscreenchange', onFullscreenChange);
  onNoteUpdated.value = () => {
    loadDashboard();
  };
});

onUnmounted(() => {
  mounted = false;
  if (refreshTimer) clearInterval(refreshTimer);
  document.removeEventListener('fullscreenchange', onFullscreenChange);
  onNoteUpdated.value = null;
});

function toggleAutoRefresh() {
  autoRefresh.value = !autoRefresh.value;
  if (autoRefresh.value) {
    refreshTimer = setInterval(loadDashboard, 5000);
  } else if (refreshTimer) {
    clearInterval(refreshTimer);
    refreshTimer = null;
  }
}

async function toggleFullscreen() {
  if (!document.fullscreenElement) {
    await document.documentElement.requestFullscreen();
    isFullscreen.value = true;
  } else {
    await document.exitFullscreen();
    isFullscreen.value = false;
  }
}

function onFullscreenChange() {
  isFullscreen.value = !!document.fullscreenElement;
}

const totalCompleted = computed(() => {
  if (!data.value) return 0;
  return data.value.columns.reduce((sum, col) => sum + col.items.length, 0);
});

function tagColorHex(color: string) {
  return color || '#64748B';
}
</script>

<template>
  <div class="h-screen flex flex-col bg-slate-950 text-slate-100 overflow-hidden">
    <!-- Header -->
    <div
      class="shrink-0 flex items-center justify-between px-6 py-3 border-b border-slate-800 bg-slate-900/80 backdrop-blur"
    >
      <div class="flex items-center gap-4">
        <button
          class="text-slate-400 hover:text-white transition-smooth flex items-center gap-1 text-sm"
          @click="router.push(`/workbench/groups/${groupId}`)"
        >
          <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path
              stroke-linecap="round"
              stroke-linejoin="round"
              stroke-width="2"
              d="M15 19l-7-7 7-7"
            />
          </svg>
          返回
        </button>
        <h1 class="text-xl font-bold tracking-tight">
          {{ data?.group?.name || '加载中...' }}
          <span class="ml-3 text-sm font-normal text-purple-400">协作大屏</span>
        </h1>
      </div>
      <div class="flex items-center gap-4 text-sm">
        <span class="text-slate-400"
          >已完成
          <span class="text-green-400 font-mono text-lg">{{ totalCompleted }}</span> 条</span
        >
        <button
          :class="[
            'px-3 py-1.5 rounded-lg text-xs font-medium transition-smooth',
            autoRefresh
              ? 'bg-green-600/20 text-green-400 ring-1 ring-green-500/50'
              : 'bg-slate-800 text-slate-500',
          ]"
          @click="toggleAutoRefresh"
        >
          {{ autoRefresh ? '⏱ 自动刷新中' : '▶ 自动刷新已关闭' }}
        </button>
        <button
          class="px-3 py-1.5 rounded-lg text-xs font-medium bg-slate-800 text-slate-400 hover:bg-slate-700 transition-smooth"
          @click="loadDashboard()"
        >
          🔄 手动刷新
        </button>
        <button
          class="px-3 py-1.5 rounded-lg text-xs font-medium bg-slate-800 text-slate-400 hover:bg-slate-700 transition-smooth flex items-center gap-1"
          @click="toggleFullscreen"
        >
          <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path
              v-if="!isFullscreen"
              stroke-linecap="round"
              stroke-linejoin="round"
              stroke-width="2"
              d="M4 8V4m0 0h4M4 4l5 5m11-1V4m0 0h-4m4 0l-5 5M4 16v4m0 0h4m-4 0l5-5m11 5l-5-5m5 5v-4m0 4h-4"
            />
            <path
              v-else
              stroke-linecap="round"
              stroke-linejoin="round"
              stroke-width="2"
              d="M6 18L18 6M6 6l12 12"
            />
          </svg>
          {{ isFullscreen ? '退出全屏' : '全屏' }}
        </button>
      </div>
    </div>

    <!-- Loading -->
    <div v-if="loading" class="flex-1 flex items-center justify-center">
      <div class="text-center">
        <div
          class="animate-spin rounded-full h-10 w-10 border-2 border-purple-500 border-t-transparent mx-auto mb-4"
        ></div>
        <p class="text-slate-500 text-sm">加载数据大屏...</p>
      </div>
    </div>
    <div v-else-if="error" class="flex-1 flex items-center justify-center">
      <p class="text-red-400">{{ error }}</p>
    </div>

    <!-- Grid columns -->
    <div v-else-if="data" class="flex-1 overflow-auto p-4">
      <div
        :class="[
          'grid gap-4 h-full',
          data.columns.length <= 1
            ? 'grid-cols-1'
            : data.columns.length === 2
              ? 'grid-cols-2'
              : data.columns.length === 3
                ? 'grid-cols-3'
                : 'grid-cols-4',
        ]"
      >
        <div
          v-for="col in data.columns"
          :key="col.sub_group_name"
          class="flex flex-col rounded-xl border border-slate-800 bg-slate-900/60 backdrop-blur overflow-hidden min-h-0"
        >
          <!-- Column header -->
          <div class="shrink-0 px-4 py-3 border-b border-slate-800 bg-slate-900/80">
            <div class="flex items-center justify-between">
              <h2 class="text-sm font-semibold text-purple-400 tracking-wide">
                {{ col.sub_group_name }}
              </h2>
              <span
                class="text-xs px-2 py-0.5 rounded-full bg-purple-500/20 text-purple-300 font-mono"
                >{{ col.items.length }}</span
              >
            </div>
          </div>

          <!-- Column items -->
          <div class="flex-1 overflow-y-auto p-3 space-y-2">
            <div v-if="col.items.length === 0" class="flex items-center justify-center h-full">
              <p class="text-slate-600 text-xs">暂无完成项</p>
            </div>
            <div
              v-for="item in col.items"
              :key="item.note_id"
              class="p-3 rounded-lg bg-slate-800/60 border border-slate-700/50 hover:border-purple-500/30 hover:bg-slate-800/80 transition-smooth animate-fade-in"
            >
              <div class="flex items-start justify-between mb-1.5">
                <span class="text-xs font-semibold text-slate-200 truncate max-w-[60%]">{{
                  item.user_name
                }}</span>
                <span class="text-[10px] text-slate-500 font-mono whitespace-nowrap">{{
                  item.completed_at
                }}</span>
              </div>
              <p class="text-xs text-slate-400 leading-relaxed mb-2 line-clamp-3">
                {{ item.note_content || item.note_title }}
              </p>
              <div v-if="item.tags && item.tags.length" class="flex flex-wrap gap-1">
                <span
                  v-for="tag in item.tags"
                  :key="tag.id"
                  class="text-[9px] px-1.5 py-0.5 rounded-full text-white font-medium"
                  :style="{ backgroundColor: tagColorHex(tag.color) }"
                  >{{ tag.sub_tag ? tag.name + ' › ' + tag.sub_tag : tag.name }}</span
                >
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
