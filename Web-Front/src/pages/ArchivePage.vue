<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useNoteStore } from '@/stores/notes';
import type { Note } from '@/types';
import StickyNoteCard from '@/components/note/StickyNoteCard.vue';

const noteStore = useNoteStore();
const viewMode = ref<'timeline' | 'card'>('card');
const keyword = ref('');
const dateFrom = ref('');
const dateTo = ref('');
const showDetailPanel = ref(false);
const selectedNote = ref<Note | null>(null);
const restoring = ref(false);

onMounted(() => {
  noteStore.fetchArchivedNotes({});
});

function handleSearch() {
  noteStore.fetchArchivedNotes({
    keyword: keyword.value || undefined,
    date_from: dateFrom.value || undefined,
    date_to: dateTo.value || undefined,
  });
}

function handleClear() {
  keyword.value = '';
  dateFrom.value = '';
  dateTo.value = '';
  noteStore.fetchArchivedNotes({});
}

function openDetail(note: Note) {
  selectedNote.value = note;
  showDetailPanel.value = true;
}

function closeDetail() {
  showDetailPanel.value = false;
  selectedNote.value = null;
}

async function handleRestore(note: Note) {
  restoring.value = true;
  try {
    await noteStore.restoreNote(note.id);
    if (showDetailPanel.value && selectedNote.value?.id === note.id) {
      closeDetail();
    }
  } catch {
    // ignore
  } finally {
    restoring.value = false;
  }
}

function groupNotesByMonth(notes: Note[]) {
  const groups: { month: string; notes: Note[] }[] = [];
  const sorted = [...notes].sort(
    (a, b) =>
      new Date(b.archive_time || b.created_at).getTime() -
      new Date(a.archive_time || a.created_at).getTime()
  );
  for (const note of sorted) {
    const d = new Date(note.archive_time || note.created_at);
    const key = `${d.getFullYear()}年${d.getMonth() + 1}月`;
    let group = groups.find((g) => g.month === key);
    if (!group) {
      group = { month: key, notes: [] };
      groups.push(group);
    }
    group.notes.push(note);
  }
  return groups;
}
</script>

<template>
  <div>
    <!-- 筛选栏 -->
    <div
      class="bg-white dark:bg-slate-800 rounded-card p-4 mb-6 flex flex-wrap items-center gap-3 border border-slate-100 dark:border-slate-700 transition-colors duration-300"
    >
      <div class="flex items-center gap-2">
        <input v-model="dateFrom" name="date_from" type="date" class="input-field !w-auto" />
        <span class="text-slate-400 text-sm">至</span>
        <input v-model="dateTo" name="date_to" type="date" class="input-field !w-auto" />
      </div>
      <input
        v-model="keyword"
        class="input-field !w-40"
        placeholder="关键词搜索"
        @keyup.enter="handleSearch"
      />
      <button class="btn-primary text-sm !py-2" @click="handleSearch">搜索</button>
      <button
        class="px-4 py-2 text-sm text-slate-500 hover:text-slate-700 transition-smooth"
        @click="handleClear"
      >
        清空
      </button>
    </div>

    <!-- 视图切换 -->
    <div class="flex items-center justify-between mb-6">
      <span class="text-xs text-slate-400">共 {{ noteStore.totalCount }} 条归档记录</span>
      <div class="flex bg-slate-100 rounded-btn p-0.5">
        <button
          :class="[
            'px-4 py-1.5 rounded-md text-sm font-medium transition-smooth',
            viewMode === 'timeline' ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-500',
          ]"
          @click="viewMode = 'timeline'"
        >
          时间轴
        </button>
        <button
          :class="[
            'px-4 py-1.5 rounded-md text-sm font-medium transition-smooth',
            viewMode === 'card' ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-500',
          ]"
          @click="viewMode = 'card'"
        >
          卡片
        </button>
      </div>
    </div>

    <!-- 加载 -->
    <div
      v-if="noteStore.loading"
      class="grid grid-cols-[repeat(auto-fill,minmax(280px,1fr))] gap-5"
    >
      <div v-for="n in 6" :key="n" class="skeleton h-44 rounded-card" />
    </div>

    <!-- 空态 -->
    <div
      v-else-if="noteStore.archivedNotes.length === 0"
      class="flex flex-col items-center justify-center py-24"
    >
      <div class="w-24 h-24 bg-slate-100 rounded-3xl flex items-center justify-center mb-6">
        <svg class="w-12 h-12 text-slate-300" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path
            stroke-linecap="round"
            stroke-linejoin="round"
            stroke-width="1.5"
            d="M5 8h14M5 8a2 2 0 110-4h14a2 2 0 110 4M5 8v10a2 2 0 002 2h10a2 2 0 002-2V8m-9 4h4"
          />
        </svg>
      </div>
      <p class="text-slate-400 text-sm">暂无归档任务</p>
      <p class="text-slate-300 text-xs mt-1">在工作台完成任务后，会自动归档到这里</p>
    </div>

    <!-- 时间轴视图 -->
    <div v-else-if="viewMode === 'timeline'" class="relative pl-8">
      <div class="absolute left-3 top-0 bottom-0 w-0.5 bg-slate-200" />
      <div
        v-for="group in groupNotesByMonth(noteStore.archivedNotes)"
        :key="group.month"
        class="mb-8"
      >
        <div class="flex items-center gap-3 mb-4">
          <div class="w-2.5 h-2.5 rounded-full bg-slate-300 -ml-[32px] ring-4 ring-white" />
          <span class="text-sm font-semibold text-slate-700">{{ group.month }}</span>
          <span class="text-xs text-slate-400">{{ group.notes.length }}条</span>
        </div>
        <div class="space-y-3">
          <div v-for="note in group.notes" :key="note.id" class="flex items-start gap-4">
            <span class="text-xs text-slate-400 w-10 shrink-0 pt-1"
              >{{ (note.archive_time || note.created_at)?.slice(8, 10) }}日</span
            >
            <div
              class="flex-1 bg-white dark:bg-slate-800 rounded-card border border-slate-100 dark:border-slate-700 p-4 relative hover:shadow-note transition-smooth cursor-pointer"
              @click="openDetail(note)"
            >
              <h4 class="text-sm font-medium text-slate-900 mb-1 truncate">
                {{ note.title || '无标题' }}
              </h4>
              <p class="text-xs text-slate-400 line-clamp-2">{{ note.content || '暂无内容' }}</p>
              <div class="flex items-center gap-2 mt-2">
                <span
                  v-for="tag in note.tags?.slice(0, 3)"
                  :key="tag.id"
                  class="tag-capsule text-white text-[10px]"
                  :style="{ backgroundColor: tag.color || '#94A3B8' }"
                  >{{ tag.sub_tag ? tag.name + ' › ' + tag.sub_tag : tag.name }}</span
                >
              </div>
              <span class="watermark-archived">已归档</span>
            </div>
            <div class="flex flex-col gap-1 shrink-0 pt-1">
              <button
                class="text-xs px-2 py-1 bg-blue-50 text-blue-600 rounded hover:bg-blue-100 transition-smooth"
                @click="handleRestore(note)"
              >
                恢复
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- 卡片视图 -->
    <div v-else class="grid grid-cols-[repeat(auto-fill,minmax(280px,1fr))] gap-5">
      <StickyNoteCard
        v-for="note in noteStore.archivedNotes"
        :key="note.id"
        :note="note"
        mode="web"
        :archived="true"
        class="animate-spring-enter"
        @click="openDetail(note)"
        @restore="handleRestore"
      />
    </div>

    <!-- 详情侧滑面板 -->
    <Teleport to="body">
      <div v-if="showDetailPanel && selectedNote">
        <div class="overlay-backdrop" @click="closeDetail" />
        <div class="slide-panel">
          <div class="p-6 h-full flex flex-col">
            <div class="flex items-center justify-between mb-6">
              <div class="flex items-center gap-2">
                <h2 class="text-lg font-semibold text-slate-900">归档详情</h2>
                <span class="text-xs px-2 py-0.5 bg-green-100 text-green-700 rounded-tag"
                  >已归档</span
                >
              </div>
              <button
                class="p-1 rounded-lg hover:bg-slate-100 transition-smooth"
                @click="closeDetail"
              >
                <svg
                  class="w-5 h-5 text-slate-400"
                  fill="none"
                  viewBox="0 0 24 24"
                  stroke="currentColor"
                >
                  <path
                    stroke-linecap="round"
                    stroke-linejoin="round"
                    stroke-width="2"
                    d="M6 18L18 6M6 6l12 12"
                  />
                </svg>
              </button>
            </div>

            <div class="flex-1 overflow-auto space-y-5">
              <div>
                <span class="text-xs text-slate-400 mb-1 block">标题</span>
                <p class="text-sm font-semibold text-slate-900">{{ selectedNote.title }}</p>
              </div>
              <div>
                <span class="text-xs text-slate-400 mb-1 block">内容</span>
                <p class="text-sm text-slate-700 whitespace-pre-wrap">
                  {{ selectedNote.content || '暂无内容' }}
                </p>
              </div>
              <div v-if="selectedNote.tags?.length" class="flex flex-wrap gap-2">
                <span
                  v-for="tag in selectedNote.tags"
                  :key="tag.id"
                  class="tag-capsule text-white"
                  :style="{ backgroundColor: tag.color || '#64748B' }"
                  >{{ tag.sub_tag ? tag.name + ' › ' + tag.sub_tag : tag.name }}</span
                >
              </div>
              <div
                class="bg-slate-50 dark:bg-slate-900 rounded-card p-4 space-y-2 text-xs transition-colors duration-300"
              >
                <div class="flex justify-between">
                  <span class="text-slate-400">创建时间</span
                  ><span class="text-slate-700">{{
                    selectedNote.created_at?.slice(0, 16).replace('T', ' ')
                  }}</span>
                </div>
                <div class="flex justify-between">
                  <span class="text-slate-400">完成时间</span
                  ><span class="text-slate-700">{{
                    selectedNote.completed_at?.slice(0, 16).replace('T', ' ') || '—'
                  }}</span>
                </div>
                <div class="flex justify-between">
                  <span class="text-slate-400">归档时间</span
                  ><span class="text-slate-700">{{
                    selectedNote.archive_time?.slice(0, 16).replace('T', ' ') || '—'
                  }}</span>
                </div>
              </div>
            </div>

            <div class="flex gap-3 pt-4 border-t border-slate-100 mt-4">
              <button
                class="flex-1 py-2.5 btn-primary text-sm disabled:opacity-50"
                :disabled="restoring"
                @click="handleRestore(selectedNote!)"
              >
                {{ restoring ? '恢复中...' : '恢复任务' }}
              </button>
              <button class="flex-1 py-2.5 btn-secondary text-sm" @click="closeDetail">关闭</button>
            </div>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>
