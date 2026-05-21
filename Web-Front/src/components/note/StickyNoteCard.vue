<script setup lang="ts">
import { ref, computed } from 'vue';
import type { Note } from '@/types';

const props = withDefaults(
  defineProps<{
    note: Note;
    mode?: 'desktop' | 'web';
    archived?: boolean;
    editingBy?: string | null;
  }>(),
  {
    mode: 'web',
    archived: false,
    editingBy: null,
  }
);

const emit = defineEmits<{
  click: [note: Note];
  'context-menu': [event: MouseEvent, note: Note];
  complete: [note: Note];
  remind: [note: Note];
  restore: [note: Note];
  export: [note: Note];
}>();

const expanded = ref(false);

const isRed = computed(() => props.note.color_status === 'red');
const isBlue = computed(() => props.note.color_status === 'blue');
const isGreen = computed(() => props.note.color_status === 'green');
const isArchived = computed(() => props.archived || props.note.is_archived);

const displayTags = computed(() => {
  const max = 2;
  const tags = (props.note.tags || []).map((t: any) => {
    if (typeof t === 'string') return { id: t, name: t, sub_tag: '', color: '#64748B' };
    return t;
  });
  const visible = tags.slice(0, max);
  const remaining = tags.length - max;
  return { visible, remaining };
});

function tagLabel(tag: any): string {
  return tag.sub_tag ? `${tag.name} › ${tag.sub_tag}` : tag.name;
}

function handleClick() {
  emit('click', props.note);
}

function handleContextMenu(e: MouseEvent) {
  e.preventDefault();
  emit('context-menu', e, props.note);
}

function toggleExpand() {
  expanded.value = !expanded.value;
}
</script>

<template>
  <div
    class="relative rounded-card p-5 transition-smooth cursor-pointer select-none"
    :class="{
      'opacity-80': isArchived,
      'ring-2 ring-purple-400 ring-offset-2 shadow-lg shadow-purple-200/50': !!props.editingBy,
    }"
    :style="{
      background: isRed ? '#FEE2E2' : isBlue ? '#DBEAFE' : isGreen ? '#DCFCE7' : '#FEF3C7',
      borderLeft: isRed
        ? '1px solid #DC2626'
        : isBlue
          ? '1px solid #2563EB'
          : isGreen
            ? '1px solid #16A34A'
            : '4px solid #D97706',
      border: isRed ? '1px solid #DC2626' : isBlue ? '1px solid #2563EB' : '',
      animation: isRed ? 'pulse-alert 2s ease-in-out infinite' : 'none',
    }"
    @click="handleClick"
    @contextmenu="handleContextMenu"
    draggable="true"
  >
    <div
      v-if="props.editingBy"
      class="absolute -top-3 left-3 flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-[10px] font-medium animate-fade-in z-10"
      style="
        background: linear-gradient(135deg, #8b5cf6, #3b82f6);
        color: #fff;
        box-shadow: 0 2px 8px rgba(139, 92, 246, 0.3);
      "
    >
      <span class="w-3.5 h-3.5 rounded-full bg-white/30 flex items-center justify-center text-[8px]"
        >✎</span
      >
      <span>{{ props.editingBy }}</span>
      <span class="inline-block w-1 h-3 bg-white/60 rounded-sm animate-pulse ml-0.5"></span>
    </div>

    <!-- 盯办徽章 -->
    <span v-if="isRed && !isArchived" class="badge-corner bg-red-500 text-white">
      盯办{{ note.remind_count > 0 ? note.remind_count : '' }}
    </span>
    <!-- 协作标识 -->
    <span v-if="isBlue && !isArchived" class="badge-corner bg-blue-500 text-white"> 协作 </span>
    <!-- 已归档水印 -->
    <span v-if="isArchived" class="watermark-archived">已归档</span>

    <h3 class="text-base font-semibold text-slate-900 mb-1 line-clamp-1">
      {{ note.title || '无标题' }}
    </h3>
    <p v-if="note.sub_tag" class="text-xs text-slate-400 mb-2 line-clamp-1">
      {{ note.sub_tag }}
    </p>
    <div
      :class="[
        'text-sm text-slate-500 transition-all duration-300 overflow-hidden',
        expanded ? 'note-content-expanded' : 'note-content-mask',
        expanded ? '' : 'max-h-[72px]',
      ]"
    >
      {{ note.content || '暂无内容' }}
    </div>

    <button
      v-if="(note.content?.length || 0) > 100"
      class="text-xs text-slate-400 hover:text-slate-600 mt-1 transition-smooth"
      @click.stop="toggleExpand"
    >
      {{ expanded ? '收起' : '展开全文' }}
    </button>

    <!-- 标签区 -->
    <div v-if="(note.tags || []).length" class="flex items-center gap-1.5 mt-3 flex-wrap">
      <span
        v-for="tag in displayTags.visible"
        :key="tag.id"
        class="tag-capsule text-white text-[11px]"
        :style="{ backgroundColor: tag.color || '#64748B' }"
      >
        {{ tagLabel(tag) }}
      </span>
      <span v-if="displayTags.remaining > 0" class="text-xs text-slate-400">
        +{{ displayTags.remaining }}
      </span>
    </div>

    <!-- 底部信息 -->
    <div class="flex items-center justify-between mt-4 pt-3 border-t border-slate-200/50">
      <span class="text-xs text-slate-400">{{ note.created_at?.slice(0, 10) }}</span>
      <span v-if="note.due_time && !isArchived" class="text-xs text-slate-400">
        截止 {{ note.due_time.slice(0, 10) }}
      </span>
      <span v-else-if="isArchived && note.archive_time" class="text-xs text-slate-300">
        归档于 {{ note.archive_time.slice(0, 10) }}
      </span>
    </div>

    <!-- 操作栏：黄/红状态 → 完成并归档 + 盯办 -->
    <div v-if="!isArchived" class="flex gap-2 mt-3 pt-3 border-t border-slate-200/50">
      <button
        class="text-xs px-2.5 py-1 rounded-btn bg-green-100 text-green-700 hover:bg-green-200 transition-smooth"
        @click.stop="$emit('complete', note)"
      >
        完成并归档
      </button>
      <button
        v-if="!isRed"
        class="text-xs px-2.5 py-1 rounded-btn bg-red-100 text-red-700 hover:bg-red-200 transition-smooth"
        @click.stop="$emit('remind', note)"
      >
        盯办
      </button>
    </div>

    <div v-if="isArchived" class="flex gap-2 mt-3 pt-3 border-t border-slate-200/50">
      <button
        class="text-xs px-2.5 py-1 rounded-btn bg-blue-100 text-blue-700 hover:bg-blue-200 transition-smooth"
        @click.stop="$emit('restore', note)"
      >
        恢复
      </button>
      <button
        class="text-xs px-2.5 py-1 rounded-btn bg-slate-100 text-slate-600 hover:bg-slate-200 transition-smooth"
        @click.stop="$emit('export', note)"
      >
        导出
      </button>
    </div>
  </div>
</template>
