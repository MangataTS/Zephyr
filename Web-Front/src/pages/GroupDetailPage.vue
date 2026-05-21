<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import {
  getWorkGroupDetail,
  deleteWorkGroup,
  addWorkGroupMember,
  updateWorkGroupMember,
  removeWorkGroupMember,
  type WorkGroupData,
  type WorkGroupMemberData,
} from '@/services/workgroup';
import { getGroupNotes, createGroupNote } from '@/services/groupNotes';
import { updateNote } from '@/services/notes';
import { generateGroupReport, type WorkGroupReport } from '@/services/workgroup';
import { useNoteStore } from '@/stores/notes';
import { useAuthStore } from '@/stores/auth';
import StickyNoteCard from '@/components/note/StickyNoteCard.vue';
import TagSelector from '@/components/common/TagSelector.vue';
import UserPicker from '@/components/common/UserPicker.vue';
import { useGroupSocket } from '@/composables/useGroupSocket';
import type { Note } from '@/types';

const route = useRoute();
const router = useRouter();
const noteStore = useNoteStore();
const auth = useAuthStore();
const groupId = route.params.id as string;

const { editingNotes, onNoteUpdated, sendEditing, sendIdle, sendNoteUpdated } =
  useGroupSocket(groupId);

const group = ref<WorkGroupData | null>(null);
const loading = ref(true);
const notes = ref<Note[]>([]);
const notesLoading = ref(false);
const notesTotal = ref(0);
const notesPage = ref(1);
const notesPageSize = 20;
const error = ref('');

const showNoteModal = ref(false);
const noteTitle = ref('');
const noteSubTag = ref('');
const noteContent = ref('');
const noteOwnerId = ref('');
const noteDueDate = ref('');
const selectedTagIds = ref<string[]>([]);
const noteCreating = ref(false);
const noteError = ref('');

const showDetailPanel = ref(false);
const selectedDetailNote = ref<Note | null>(null);
const editingTitle = ref('');
const editingSubTag = ref('');
const editingContent = ref('');
const saving = ref(false);
const completing = ref(false);
const selectedEditingTagIds = ref<string[]>([]);
const tagSaving = ref(false);
const tagError = ref('');

const generatingReport = ref(false);

async function handleGenerateReport() {
  generatingReport.value = true;
  try {
    const res = await generateGroupReport(groupId);
    const data = res.data as unknown as { report_id: string; report: string; report_type: string };
    router.push(`/workbench/groups/${groupId}/reports/${data.report_id}`);
  } catch {
    alert('生成报告失败，请确认已配置AI大模型');
  } finally {
    generatingReport.value = false;
  }
}

const showMemberManager = ref(false);
const addMemberUserIds = ref<string[]>([]);
const addingMembers = ref(false);
const memberError = ref('');
const editingMemberId = ref<string | null>(null);
const editMemberRole = ref('');
const editMemberSubGroup = ref('');

const isCreator = computed(() => {
  if (!group.value || !auth.user) return false;
  return group.value.initiator_id === auth.user.id;
});

function statusLabel(s: string) {
  const m: Record<string, string> = { active: '进行中', completed: '已完成', archived: '已归档' };
  return m[s] || s;
}
function templateLabel(t: string) {
  const m: Record<string, string> = {
    default: '日常任务',
    data_analysis: '数据分析',
    special_project: '专项行动',
    emergency_canvas: '紧急协查',
    collaborative_writing: '协同作战',
  };
  return m[t] || t;
}
function roleLabel(r: string) {
  const m: Record<string, string> = { leader: '组长', sub_leader: '副组长', member: '组员' };
  return m[r] || r;
}
function formatTime(d: string) {
  return d ? new Date(d).toLocaleString('zh-CN') : '-';
}

const membersBySubGroup = computed(() => {
  const map: Record<string, WorkGroupMemberData[]> = {};
  group.value?.members?.forEach((m) => {
    const key = m.sub_group_name || '未分组';
    if (!map[key]) map[key] = [];
    map[key].push(m);
  });
  return map;
});

const existingMemberIds = computed(
  () => new Set(group.value?.members?.map((m) => m.user_id) || [])
);

onMounted(async () => {
  onNoteUpdated.value = () => {
    loadNotes();
  };
  try {
    const res = await getWorkGroupDetail(groupId);
    group.value = res.data as unknown as WorkGroupData;
  } catch {
    error.value = '加载专项行动失败';
  } finally {
    loading.value = false;
  }
  loadNotes();
});

async function loadNotes() {
  notesLoading.value = true;
  try {
    const res = await getGroupNotes(groupId, { page: notesPage.value, page_size: notesPageSize });
    const d = res.data as unknown as { data: Note[]; total: number };
    notes.value = d.data || [];
    notesTotal.value = d.total || 0;
  } catch {
    /* ignore */
  } finally {
    notesLoading.value = false;
  }
}

async function reloadGroup() {
  try {
    const res = await getWorkGroupDetail(groupId);
    group.value = res.data as unknown as WorkGroupData;
  } catch {
    /* ignore */
  }
}

async function handleDeleteGroup() {
  try {
    await deleteWorkGroup(groupId);
    router.push('/workbench');
  } catch {
    /* ignore */
  }
}

function openNoteModal() {
  noteTitle.value = '';
  noteSubTag.value = '';
  noteContent.value = '';
  noteOwnerId.value = '';
  noteDueDate.value = '';
  selectedTagIds.value = [];
  noteError.value = '';
  showNoteModal.value = true;
}
async function handleCreateNote() {
  if (!noteTitle.value.trim()) {
    noteError.value = '请输入标题';
    return;
  }
  noteCreating.value = true;
  noteError.value = '';
  try {
    const note = await createGroupNote(groupId, {
      title: noteTitle.value.trim(),
      sub_tag: noteSubTag.value.trim(),
      content: noteContent.value,
      owner_id: noteOwnerId.value || undefined,
      due_time: noteDueDate.value ? new Date(noteDueDate.value).toISOString() : undefined,
      tag_ids: selectedTagIds.value.length > 0 ? selectedTagIds.value : undefined,
    });
    const noteData = (note as unknown as { data: { id: string } }).data;
    showNoteModal.value = false;
    if (noteData?.id) sendNoteUpdated(noteData.id, 'created');
    loadNotes();
  } catch (e: unknown) {
    noteError.value =
      (e as { response?: { data?: { message?: string } } })?.response?.data?.message || '创建失败';
  } finally {
    noteCreating.value = false;
  }
}

function openDetail(note: Note) {
  if (selectedDetailNote.value?.id) sendIdle(selectedDetailNote.value.id);
  selectedDetailNote.value = note;
  editingTitle.value = note.title || '';
  editingSubTag.value = note.sub_tag || '';
  editingContent.value = note.content || '';
  selectedEditingTagIds.value = (note.tags || []).map((t) => t.id);
  tagError.value = '';
  showDetailPanel.value = true;
  sendEditing(note.id);
}
function closeDetail() {
  if (selectedDetailNote.value?.id) sendIdle(selectedDetailNote.value.id);
  showDetailPanel.value = false;
  selectedDetailNote.value = null;
  completing.value = false;
}
async function handleSaveDetail() {
  if (!selectedDetailNote.value) return;
  const noteId = selectedDetailNote.value.id;
  saving.value = true;
  try {
    await updateNote(selectedDetailNote.value.id, {
      title: editingTitle.value.trim(),
      sub_tag: editingSubTag.value.trim(),
      content: editingContent.value,
      tags: selectedEditingTagIds.value,
    });
    sendNoteUpdated(noteId, 'updated');
    closeDetail();
    loadNotes();
  } catch {
    /* ignore */
  } finally {
    saving.value = false;
  }
}
async function handleUpdateTags(tagIds: string[]) {
  if (!selectedDetailNote.value) return;
  selectedEditingTagIds.value = tagIds;
  tagSaving.value = true;
  tagError.value = '';
  try {
    await noteStore.updateNoteTags(selectedDetailNote.value.id, tagIds);
    const noteId = selectedDetailNote.value.id;
    const idx = notes.value.findIndex((n) => n.id === noteId);
    if (idx >= 0) {
      notes.value[idx] = {
        ...notes.value[idx],
        tags: noteStore.activeNotes.find((n) => n.id === noteId)?.tags || [],
      };
    }
    if (selectedDetailNote.value?.id === noteId) {
      selectedDetailNote.value = {
        ...selectedDetailNote.value,
        tags: noteStore.activeNotes.find((n) => n.id === noteId)?.tags || [],
      };
    }
    sendNoteUpdated(noteId, 'updated');
  } catch {
    tagError.value = '标签更新失败，请重试';
    selectedEditingTagIds.value = (selectedDetailNote.value.tags || []).map((t) => t.id);
  } finally {
    tagSaving.value = false;
  }
}
async function handleComplete(note: Note) {
  await noteStore.completeNote(note.id);
  sendNoteUpdated(note.id, 'completed');
  loadNotes();
  if (showDetailPanel.value && selectedDetailNote.value?.id === note.id) closeDetail();
}
async function handleRemind(note: Note) {
  await noteStore.remindNote(note.id, note.owner_id, '请尽快处理');
  sendNoteUpdated(note.id, 'reminded');
  const idx = notes.value.findIndex((n) => n.id === note.id);
  if (idx >= 0) notes.value[idx] = { ...notes.value[idx], color_status: 'red' };
  if (showDetailPanel.value && selectedDetailNote.value?.id === note.id)
    selectedDetailNote.value = { ...selectedDetailNote.value!, color_status: 'red' as const };
}

function openMemberManager() {
  addMemberUserIds.value = [];
  memberError.value = '';
  showMemberManager.value = true;
}
async function handleAddMembers() {
  if (addMemberUserIds.value.length === 0) {
    memberError.value = '请选择要添加的成员';
    return;
  }
  addingMembers.value = true;
  memberError.value = '';
  try {
    for (const uid of addMemberUserIds.value) {
      await addWorkGroupMember(groupId, { user_id: uid, role: 'member' });
    }
    showMemberManager.value = false;
    await reloadGroup();
  } catch (e: unknown) {
    memberError.value =
      (e as { response?: { data?: { message?: string } } })?.response?.data?.message ||
      '添加成员失败';
  } finally {
    addingMembers.value = false;
  }
}

function startEditMember(m: WorkGroupMemberData) {
  editingMemberId.value = m.user_id;
  editMemberRole.value = m.role;
  editMemberSubGroup.value = m.sub_group_name;
}
function cancelEditMember() {
  editingMemberId.value = null;
}
async function saveEditMember(m: WorkGroupMemberData) {
  try {
    await updateWorkGroupMember(groupId, m.user_id, {
      role: editMemberRole.value || undefined,
      sub_group_name: editMemberSubGroup.value || undefined,
    });
    editingMemberId.value = null;
    await reloadGroup();
  } catch {
    /* ignore */
  }
}
async function handleRemoveMember(m: WorkGroupMemberData) {
  if (!confirm(`确定将 ${m.user?.name || m.user_id} 移出工作组？`)) return;
  try {
    await removeWorkGroupMember(groupId, m.user_id);
    await reloadGroup();
  } catch {
    /* ignore */
  }
}
</script>

<template>
  <div class="h-full flex flex-col bg-white dark:bg-slate-900">
    <div class="shrink-0 px-6 py-4 border-b border-slate-200 dark:border-slate-700">
      <div class="flex items-center gap-3">
        <button
          class="text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 transition-smooth flex items-center gap-1 text-sm"
          @click="router.push('/workbench')"
        >
          <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path
              stroke-linecap="round"
              stroke-linejoin="round"
              stroke-width="2"
              d="M15 19l-7-7 7-7"
            />
          </svg>
          工作台
        </button>
        <span class="text-slate-300 dark:text-slate-600">/</span>
        <h1 class="text-lg font-semibold text-slate-900 dark:text-slate-100 truncate">
          {{ group?.name || '加载中...' }}
        </h1>
      </div>
    </div>

    <div v-if="loading" class="flex-1 flex items-center justify-center">
      <div
        class="animate-spin rounded-full h-8 w-8 border-2 border-purple-500 border-t-transparent"
      ></div>
    </div>
    <div v-else-if="error" class="flex-1 flex items-center justify-center">
      <p class="text-slate-400">{{ error }}</p>
    </div>

    <template v-else-if="group">
      <!-- Info & Member area -->
      <div
        class="shrink-0 px-6 py-4 bg-purple-50/30 dark:bg-purple-900/5 border-b border-purple-100 dark:border-purple-800"
      >
        <div class="flex flex-wrap items-center gap-3 mb-3">
          <span
            :class="[
              'text-[10px] px-2 py-0.5 rounded-full font-medium',
              group.status === 'active'
                ? 'bg-green-100 dark:bg-green-900/50 text-green-600 dark:text-green-400'
                : 'bg-slate-100 dark:bg-slate-700 text-slate-500',
            ]"
            >{{ statusLabel(group.status) }}</span
          >
          <span
            class="text-[10px] px-2 py-0.5 rounded-full bg-purple-100 dark:bg-purple-900/50 text-purple-600 dark:text-purple-400 font-medium"
            >{{ templateLabel(group.template_type) }}</span
          >
          <span class="text-xs text-slate-400 dark:text-slate-500"
            >👤 {{ group.initiator?.name || '未知' }}
            <span v-if="isCreator" class="text-amber-500 font-medium">（创建人）</span></span
          >
          <span class="text-xs text-slate-400 dark:text-slate-500"
            >👥 {{ group.members?.length || 0 }} 人</span
          >
          <span v-if="group.due_time" class="text-xs text-red-400"
            >📅 截止 {{ group.due_time.slice(0, 10) }}</span
          >
          <span class="text-xs text-slate-400 dark:text-slate-500">{{
            formatTime(group.created_at)
          }}</span>
          <div class="ml-auto flex items-center gap-2">
            <button
              class="px-3 py-1.5 text-xs font-medium text-cyan-600 dark:text-cyan-400 bg-cyan-50 dark:bg-cyan-900/20 hover:bg-cyan-100 dark:hover:bg-cyan-900/40 rounded-lg transition-smooth flex items-center gap-1"
              @click="router.push(`/workbench/groups/${groupId}/dashboard`)"
            >
              <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path
                  stroke-linecap="round"
                  stroke-linejoin="round"
                  stroke-width="2"
                  d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z"
                />
              </svg>
              协作大屏
            </button>
            <button
              class="px-3 py-1.5 text-xs font-medium text-emerald-600 dark:text-emerald-400 bg-emerald-50 dark:bg-emerald-900/20 hover:bg-emerald-100 dark:hover:bg-emerald-900/40 rounded-lg transition-smooth flex items-center gap-1"
              :disabled="generatingReport"
              @click="handleGenerateReport()"
            >
              <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path
                  stroke-linecap="round"
                  stroke-linejoin="round"
                  stroke-width="2"
                  d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"
                />
              </svg>
              {{ generatingReport ? '生成中...' : '生成报告' }}
            </button>
            <button
              class="px-3 py-1.5 text-xs font-medium text-orange-600 dark:text-orange-400 bg-orange-50 dark:bg-orange-900/20 hover:bg-orange-100 dark:hover:bg-orange-900/40 rounded-lg transition-smooth flex items-center gap-1"
              @click="router.push(`/workbench/groups/${groupId}/reports`)"
            >
              <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path
                  stroke-linecap="round"
                  stroke-linejoin="round"
                  stroke-width="2"
                  d="M9 17v-2m3 2v-4m3 4v-6m2 10H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"
                />
              </svg>
              查看报告
            </button>
            <button
              v-if="isCreator"
              class="px-3 py-1.5 text-xs font-medium text-purple-600 dark:text-purple-400 bg-purple-50 dark:bg-purple-900/30 hover:bg-purple-100 dark:hover:bg-purple-900/50 rounded-lg transition-smooth flex items-center gap-1"
              @click="openMemberManager()"
            >
              <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path
                  stroke-linecap="round"
                  stroke-linejoin="round"
                  stroke-width="2"
                  d="M12 4v16m8-8H4"
                />
              </svg>
              管理成员
            </button>
            <button
              v-if="isCreator"
              class="px-3 py-1 text-xs text-red-500 hover:bg-red-50 dark:hover:bg-red-900/30 rounded-lg transition-smooth"
              @click="handleDeleteGroup()"
            >
              🗑 删除
            </button>
          </div>
        </div>
        <p v-if="group.description" class="text-sm text-slate-600 dark:text-slate-300 mb-3">
          {{ group.description }}
        </p>

        <!-- Member groups -->
        <div
          v-if="Object.keys(membersBySubGroup).length > 0"
          class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-2"
        >
          <div
            v-for="(members, sgName) in membersBySubGroup"
            :key="sgName"
            class="p-3 rounded-lg border border-purple-100 dark:border-purple-800 bg-white dark:bg-slate-800"
          >
            <div class="flex items-center justify-between mb-2">
              <p class="text-[11px] font-semibold text-purple-500 dark:text-purple-400">
                {{ sgName }}
              </p>
              <span class="text-[10px] text-slate-400">{{ members.length }}人</span>
            </div>
            <div class="flex flex-wrap gap-1.5">
              <div v-for="m in members" :key="m.user_id" class="relative group/member">
                <span
                  :class="[
                    'inline-flex items-center gap-1 text-[10px] px-2 py-1 rounded-full cursor-default transition-smooth',
                    m.role === 'leader'
                      ? 'bg-amber-50 dark:bg-amber-900/20 text-amber-600 dark:text-amber-400 ring-1 ring-amber-200 dark:ring-amber-800'
                      : 'bg-slate-50 dark:bg-slate-700 text-slate-500 dark:text-slate-400',
                  ]"
                >
                  {{ m.user?.name || m.user_id }}
                  <span class="text-[8px] opacity-70">{{ roleLabel(m.role) }}</span>
                </span>
                <div
                  v-if="isCreator"
                  class="absolute -top-1 -right-1 opacity-0 group-hover/member:opacity-100 transition-smooth flex gap-0.5"
                >
                  <button
                    class="w-4 h-4 rounded-full bg-white dark:bg-slate-600 shadow-sm flex items-center justify-center text-[8px] text-slate-500 hover:text-purple-500 dark:hover:text-purple-400 transition-smooth"
                    title="编辑"
                    @click="startEditMember(m)"
                  >
                    ✎
                  </button>
                  <button
                    class="w-4 h-4 rounded-full bg-white dark:bg-slate-600 shadow-sm flex items-center justify-center text-[8px] text-slate-500 hover:text-red-500 transition-smooth"
                    title="移除"
                    @click="handleRemoveMember(m)"
                  >
                    ✕
                  </button>
                </div>

                <!-- Inline edit popup -->
                <div
                  v-if="editingMemberId === m.user_id"
                  class="absolute top-full left-0 mt-1 w-48 p-3 rounded-xl border border-slate-200 dark:border-slate-600 bg-white dark:bg-slate-800 shadow-lg z-20 animate-fade-in"
                >
                  <label class="text-[10px] text-slate-400 mb-0.5 block">角色</label>
                  <select
                    v-model="editMemberRole"
                    class="w-full text-[11px] px-2 py-1 border border-slate-200 dark:border-slate-600 rounded bg-white dark:bg-slate-700 text-slate-900 dark:text-slate-100 mb-2"
                  >
                    <option value="leader">组长</option>
                    <option value="sub_leader">副组长</option>
                    <option value="member">组员</option>
                  </select>
                  <label class="text-[10px] text-slate-400 mb-0.5 block">子组名称</label>
                  <input
                    v-model="editMemberSubGroup"
                    class="w-full text-[11px] px-2 py-1 border border-slate-200 dark:border-slate-600 rounded bg-white dark:bg-slate-700 text-slate-900 dark:text-slate-100 mb-2"
                    placeholder="如：技术组"
                  />
                  <div class="flex gap-1.5">
                    <button
                      class="flex-1 text-[10px] px-2 py-1 bg-purple-500 text-white rounded hover:bg-purple-600 transition-smooth"
                      @click="saveEditMember(m)"
                    >
                      保存
                    </button>
                    <button
                      class="flex-1 text-[10px] px-2 py-1 bg-slate-100 dark:bg-slate-700 text-slate-500 rounded hover:bg-slate-200 dark:hover:bg-slate-600 transition-smooth"
                      @click="cancelEditMember()"
                    >
                      取消
                    </button>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div class="flex-1 overflow-auto p-6">
        <div class="flex items-center justify-between mb-4">
          <div>
            <h2 class="text-sm font-semibold text-slate-700 dark:text-slate-300">
              📋 专项工作任务
            </h2>
            <p class="text-[11px] text-slate-400 dark:text-slate-500 mt-0.5">
              仅属于此专项行动的任务任务，独立于日常工作任务
            </p>
          </div>
          <button
            class="px-4 py-2 text-sm font-medium text-white bg-gradient-to-r from-purple-500 to-blue-500 hover:from-purple-600 hover:to-blue-600 rounded-lg transition-smooth flex items-center gap-1.5"
            @click="openNoteModal()"
          >
            <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path
                stroke-linecap="round"
                stroke-linejoin="round"
                stroke-width="2"
                d="M12 4v16m8-8H4"
              />
            </svg>
            新建任务
          </button>
        </div>

        <div v-if="notesLoading" class="flex items-center justify-center py-12">
          <div
            class="animate-spin rounded-full h-8 w-8 border-2 border-purple-500 border-t-transparent"
          ></div>
        </div>

        <div
          v-else-if="notes.length === 0"
          class="text-center py-16 text-slate-400 dark:text-slate-500"
        >
          <p class="text-2xl mb-2">📝</p>
          <p class="text-sm">暂无专项任务</p>
          <p class="text-xs mt-1">点击「新建任务」添加此专项行动的任务</p>
        </div>

        <div v-else class="grid grid-cols-[repeat(auto-fill,minmax(280px,1fr))] gap-5">
          <StickyNoteCard
            v-for="note in notes"
            :key="note.id"
            :note="note"
            mode="web"
            :archived="false"
            :editing-by="editingNotes.get(note.id)?.name || null"
            class="animate-spring-enter"
            @click="openDetail(note)"
            @complete="handleComplete"
            @remind="handleRemind"
          />
        </div>

        <div v-if="notesTotal > notesPageSize" class="flex items-center justify-between mt-6">
          <span class="text-xs text-slate-400">共 {{ notesTotal }} 条</span>
          <div class="flex items-center gap-2">
            <button
              class="px-3 py-1 text-xs font-medium text-slate-600 bg-slate-100 hover:bg-slate-200 rounded transition-smooth disabled:opacity-40"
              :disabled="notesPage <= 1"
              @click="
                notesPage--;
                loadNotes();
              "
            >
              上一页
            </button>
            <button
              class="px-3 py-1 text-xs font-medium text-slate-600 bg-slate-100 hover:bg-slate-200 rounded transition-smooth disabled:opacity-40"
              :disabled="notesPage * notesPageSize >= notesTotal"
              @click="
                notesPage++;
                loadNotes();
              "
            >
              下一页
            </button>
          </div>
        </div>
      </div>
    </template>

    <!-- Create note modal -->
    <Teleport to="body">
      <div
        v-if="showNoteModal"
        class="fixed inset-0 z-50 flex items-start justify-center pt-[10vh]"
      >
        <div class="overlay-backdrop" @click="showNoteModal = false" />
        <div
          class="relative z-50 bg-white dark:bg-slate-800 rounded-card shadow-modal w-full max-w-lg mx-4 animate-fade-in"
        >
          <div class="p-6">
            <div class="flex items-center justify-between mb-6">
              <h2 class="text-lg font-semibold text-slate-900 dark:text-slate-100">
                📝 新建专项任务
              </h2>
              <button
                class="p-1 rounded-lg hover:bg-slate-100 dark:hover:bg-slate-700 transition-smooth"
                @click="showNoteModal = false"
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
            <form class="space-y-4" @submit.prevent="handleCreateNote" @keydown.enter.prevent>
              <input v-model="noteTitle" name="title" class="input-field" placeholder="任务标题" autofocus />
              <input v-model="noteSubTag" name="sub_tag" class="input-field" placeholder="二级标题 / 子标签（可选）" />
              <textarea
                v-model="noteContent"
                class="input-field h-24 resize-none"
                placeholder="任务内容..."
              />
              <div class="grid grid-cols-2 gap-3">
                <div>
                  <label class="text-xs text-slate-500 mb-1 block">负责人（可选）</label
                  ><select v-model="noteOwnerId" class="input-field text-sm">
                    <option value="">自己</option>
                    <option v-for="m in group?.members || []" :key="m.user_id" :value="m.user_id">
                      {{ m.user?.name || m.user_id }} ({{ roleLabel(m.role) }})
                    </option>
                  </select>
                </div>
                <div>
                  <label class="text-xs text-slate-500 mb-1 block">截止日期</label
                  ><input v-model="noteDueDate" type="date" class="input-field" />
                </div>
              </div>
              <div>
                <label class="text-xs text-slate-500 mb-1 block">标签</label>
                <TagSelector v-model="selectedTagIds" :max="5" />
              </div>
              <p v-if="noteError" class="text-sm text-red-500 bg-red-50 px-3 py-2 rounded-btn">
                {{ noteError }}
              </p>
              <div
                class="flex justify-end gap-3 pt-4 border-t border-slate-100 dark:border-slate-700"
              >
                <button
                  type="button"
                  class="px-5 py-2.5 text-sm text-slate-600 bg-slate-100 rounded-btn hover:bg-slate-200 transition-smooth"
                  @click="showNoteModal = false"
                  :disabled="noteCreating"
                >
                  取消
                </button>
                <button
                  type="submit"
                  class="px-5 py-2.5 text-sm text-white bg-gradient-to-r from-purple-500 to-blue-500 rounded-btn hover:from-purple-600 hover:to-blue-600 transition-smooth disabled:opacity-50"
                  :disabled="noteCreating"
                >
                  {{ noteCreating ? '创建中...' : '创建任务' }}
                </button>
              </div>
            </form>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- Detail slide panel -->
    <Teleport to="body">
      <div v-if="showDetailPanel && selectedDetailNote">
        <div class="overlay-backdrop" @click="closeDetail" />
        <div class="slide-panel">
          <div class="p-6 h-full flex flex-col">
            <div class="flex items-center justify-between mb-6">
              <div class="flex items-center gap-2">
                <h2 class="text-lg font-semibold text-slate-900 dark:text-slate-100">任务详情</h2>
                <span
                  v-if="selectedDetailNote.color_status === 'red'"
                  class="text-xs px-2 py-0.5 bg-red-100 text-red-700 rounded-tag"
                  >盯办中</span
                >
              </div>
              <button
                class="p-1 rounded-lg hover:bg-slate-100 dark:hover:bg-slate-700 transition-smooth"
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
                <span class="text-xs text-slate-400 mb-1 block">标题</span
                ><input v-model="editingTitle" name="title" class="input-field text-base font-semibold" />
              </div>
              <div>
                <span class="text-xs text-slate-400 mb-1 block">二级标题 / 子标签</span
                ><input
                  v-model="editingSubTag"
                  class="input-field"
                  placeholder="二级标题（可选）"
                />
              </div>
              <div>
                <span class="text-xs text-slate-400 mb-1 block">内容</span
                ><textarea
                  v-model="editingContent"
                  class="input-field min-h-[180px] resize-y text-sm"
                />
              </div>
              <div v-if="selectedDetailNote">
                <span class="text-xs text-slate-400 mb-1 flex items-center gap-2">
                  标签
                  <span v-if="tagSaving" class="text-[10px] text-blue-400">保存中...</span>
                  <span v-if="tagError" class="text-[10px] text-red-400">{{ tagError }}</span>
                </span>
                <TagSelector
                  v-model="selectedEditingTagIds"
                  :max="10"
                  scope="all"
                  @update:model-value="handleUpdateTags"
                />
              </div>
              <div class="bg-slate-50 dark:bg-slate-900 rounded-card p-4 space-y-2">
                <div class="flex justify-between text-xs">
                  <span class="text-slate-400">创建时间</span
                  ><span class="text-slate-700 dark:text-slate-300">{{
                    selectedDetailNote.created_at?.slice(0, 16).replace('T', ' ')
                  }}</span>
                </div>
                <div v-if="selectedDetailNote.due_time" class="flex justify-between text-xs">
                  <span class="text-slate-400">截止时间</span
                  ><span class="text-red-500">{{
                    selectedDetailNote.due_time.slice(0, 16).replace('T', ' ')
                  }}</span>
                </div>
                <div
                  v-if="selectedDetailNote.assignees?.length"
                  class="flex justify-between text-xs"
                >
                  <span class="text-slate-400">负责人</span
                  ><span class="text-slate-700 dark:text-slate-300">{{
                    selectedDetailNote.assignees.map((a) => a.name).join('、')
                  }}</span>
                </div>
                <div v-if="selectedDetailNote.serial_no" class="flex justify-between text-xs">
                  <span class="text-slate-400">流水号</span
                  ><span class="text-slate-700 dark:text-slate-300 font-mono">{{
                    selectedDetailNote.serial_no
                  }}</span>
                </div>
              </div>
            </div>
            <div class="pt-4 border-t border-slate-100 dark:border-slate-700 mt-4 space-y-3">
              <div class="flex gap-2">
                <button
                  class="flex-1 py-2.5 btn-primary text-sm disabled:opacity-50"
                  :disabled="saving"
                  @click="handleSaveDetail"
                >
                  {{ saving ? '保存中...' : '保存' }}
                </button>
                <button
                  class="flex-1 py-2.5 text-sm bg-green-500 text-white rounded-btn hover:bg-green-600 transition-smooth disabled:opacity-50"
                  :disabled="completing"
                  @click="
                    completing = true;
                    handleComplete(selectedDetailNote!);
                  "
                >
                  {{ completing ? '归档中...' : '完成并归档' }}
                </button>
                <button
                  v-if="selectedDetailNote.color_status !== 'red'"
                  class="flex-1 py-2.5 text-sm bg-red-50 text-red-600 rounded-btn hover:bg-red-100 transition-smooth"
                  @click="handleRemind(selectedDetailNote!)"
                >
                  盯办
                </button>
              </div>
              <button class="w-full py-2 btn-secondary text-sm" @click="closeDetail">关闭</button>
            </div>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- Member manager modal -->
    <Teleport to="body">
      <div
        v-if="showMemberManager"
        class="fixed inset-0 z-50 flex items-start justify-center pt-[8vh]"
      >
        <div class="overlay-backdrop" @click="showMemberManager = false" />
        <div
          class="relative z-50 bg-white dark:bg-slate-800 rounded-card shadow-modal w-full max-w-lg mx-4 animate-fade-in"
        >
          <div class="p-6">
            <div class="flex items-center justify-between mb-6">
              <h2 class="text-lg font-semibold text-slate-900 dark:text-slate-100">👥 管理成员</h2>
              <button
                class="p-1 rounded-lg hover:bg-slate-100 dark:hover:bg-slate-700 transition-smooth"
                @click="showMemberManager = false"
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

            <!-- Current members -->
            <div class="mb-5">
              <p class="text-xs text-slate-500 mb-2">
                当前成员（{{ group?.members?.length || 0 }}人）
              </p>
              <div class="space-y-1 max-h-40 overflow-y-auto">
                <div
                  v-for="m in group?.members || []"
                  :key="m.user_id"
                  class="flex items-center justify-between px-3 py-2 rounded-lg hover:bg-slate-50 dark:hover:bg-slate-700/50 transition-smooth"
                >
                  <div class="flex items-center gap-2 text-sm">
                    <span class="text-slate-700 dark:text-slate-200">{{
                      m.user?.name || m.user_id
                    }}</span>
                    <span
                      :class="[
                        'text-[10px] px-1.5 py-0.5 rounded-full',
                        m.role === 'leader'
                          ? 'bg-amber-100 dark:bg-amber-900/30 text-amber-600 dark:text-amber-400'
                          : 'bg-slate-100 dark:bg-slate-600 text-slate-500',
                      ]"
                      >{{ roleLabel(m.role) }}</span
                    >
                    <span v-if="m.sub_group_name" class="text-[10px] text-slate-400">{{
                      m.sub_group_name
                    }}</span>
                  </div>
                  <div class="flex items-center gap-1">
                    <select
                      :value="m.role"
                      class="text-[10px] px-1.5 py-0.5 border border-slate-200 dark:border-slate-600 rounded bg-white dark:bg-slate-700 text-slate-700 dark:text-slate-300"
                      @change="
                        updateWorkGroupMember(groupId, m.user_id, {
                          role: ($event.target as HTMLSelectElement).value,
                        }).then(() => reloadGroup())
                      "
                    >
                      <option value="leader">组长</option>
                      <option value="sub_leader">副组长</option>
                      <option value="member">组员</option>
                    </select>
                    <button
                      class="text-[10px] text-red-400 hover:text-red-600 px-1 transition-smooth"
                      title="移除"
                      @click="handleRemoveMember(m)"
                    >
                      ✕
                    </button>
                  </div>
                </div>
              </div>
            </div>

            <!-- Add members -->
            <div>
              <p class="text-xs text-slate-500 mb-2">添加新成员</p>
              <UserPicker v-model="addMemberUserIds" :multiple="true" :max="50" />
              <p
                v-if="memberError"
                class="text-sm text-red-500 bg-red-50 px-3 py-2 rounded-btn mt-3"
              >
                {{ memberError }}
              </p>
            </div>

            <div
              class="flex justify-end gap-3 pt-4 border-t border-slate-100 dark:border-slate-700 mt-4"
            >
              <button
                class="px-5 py-2.5 text-sm text-slate-600 bg-slate-100 rounded-btn hover:bg-slate-200 transition-smooth"
                @click="showMemberManager = false"
              >
                关闭
              </button>
              <button
                class="px-5 py-2.5 text-sm text-white bg-gradient-to-r from-purple-500 to-blue-500 rounded-btn hover:from-purple-600 hover:to-blue-600 transition-smooth disabled:opacity-50"
                :disabled="addingMembers"
                @click="handleAddMembers"
              >
                {{ addingMembers ? '添加中...' : '确认添加' }}
              </button>
            </div>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>
