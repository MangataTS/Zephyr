<script setup lang="ts">
import { ref, onMounted, watch, computed } from 'vue';
import { useRouter } from 'vue-router';
import { useNoteStore } from '@/stores/notes';
import { useAuthStore } from '@/stores/auth';
import type { Note } from '@/types';
import TagSelector from '@/components/common/TagSelector.vue';
import StickyNoteCard from '@/components/note/StickyNoteCard.vue';
import UserPicker from '@/components/common/UserPicker.vue';
import { createWorkGroup, searchGroups, deleteWorkGroup, aiSuggestGroups } from '@/services/workgroup';
import type { WorkGroupData, AISuggestGroupsResult } from '@/services/workgroup';
import { recommendUsers, getWorkTypeOptions } from '@/services/admin';
import type { WorkTypeOption } from '@/types';
import { getPresets } from '@/services/presets';
import type { PresetGroup } from '@/types/preset';
import { fetchTemplates } from '@/services/templates';
import type { Template } from '@/types';

const router = useRouter();
const noteStore = useNoteStore();
const auth = useAuthStore();
const showCreateModal = ref(false);
const showDetailPanel = ref(false);
const selectedNote = ref<Note | null>(null);

const newTitle = ref('');
const newSubTag = ref('');
const newContent = ref('');
const selectedTagIds = ref<string[]>([]);
const sourceType = ref<'self' | 'assigned' | 'collaboration'>('self');
const selectedAssigneeIds = ref<string[]>([]);
const creating = ref(false);
const createError = ref('');

const editingTitle = ref('');
const editingSubTag = ref('');
const editingContent = ref('');
const selectedEditingTagIds = ref<string[]>([]);
const saving = ref(false);
const completing = ref(false);
const tagSaving = ref(false);
const tagError = ref('');

const activeTab = ref('all');

const showWorkGroupModal = ref(false);
const workGroups = ref<WorkGroupData[]>([]);
const wgLoading = ref(false);
const groupsTotal = ref(0);
const groupsPage = ref(1);
const groupsPageSize = 20;
const groupsFilter = ref({ keyword: '', date_from: '', date_to: '' });
const wgName = ref('');
const wgDescription = ref('');
const wgTemplate = ref('default');
const wgDueDate = ref('');
const wgSubGroups = ref<
  { name: string; members: { user_id: string; role: string; sub_group_name: string }[] }[]
>([{ name: '', members: [] }]);
const wgCreating = ref(false);
const wgError = ref('');

const selectedWorkType = ref('');
const workTypeOptions = ref<WorkTypeOption[]>([]);
const recommending = ref(false);
const recommendError = ref('');
const recommendResult = ref<
  Array<{
    id: string;
    name: string;
    dept_name: string;
    work_type_stats?: { work_type: string; group_count: number }[];
  }>
>([]);
const selectedWGUserIds = ref<string[][]>([[]]);

const availablePresets = ref<PresetGroup[]>([]);
const selectedPresetId = ref('');

const userTemplates = ref<Template[]>([]);
const selectedTemplateId = ref('');

const aiMode = ref(false);
const aiDescription = ref('');
const aiSuggesting = ref(false);
const aiResult = ref<AISuggestGroupsResult | null>(null);
const aiError = ref('');

const displayedNotes = computed(() => {
  if (activeTab.value === 'red')
    return noteStore.activeNotes.filter((n) => n.color_status === 'red');
  if (activeTab.value === 'blue')
    return noteStore.activeNotes.filter((n) => n.color_status === 'blue');
  return noteStore.activeNotes;
});

onMounted(() => {
  noteStore.fetchNotes();
  loadWorkGroups();
  loadWorkTypeOptions();
  loadPresets();
  loadUserTemplates();
});

function handleTabClick(tab: string) {
  activeTab.value = tab;
  if (tab === 'groups') {
    loadWorkGroups();
    return;
  }
  if (tab === 'all') noteStore.fetchNotes({ status: undefined });
  else if (tab === 'red')
    noteStore.fetchNotes({ status: undefined }).then(() => {
      noteStore.activeNotes = noteStore.activeNotes.filter((n) => n.color_status === 'red');
    });
  else if (tab === 'assigned')
    noteStore.fetchNotes({ status: undefined }).then(() => {
      noteStore.activeNotes = noteStore.activeNotes.filter(
        (n) => n.source_type === 'assigned' || n.source_type === 'collaboration'
      );
    });
  else noteStore.fetchNotes({ status: tab });
}

watch(sourceType, (val) => {
  if (val === 'self') selectedAssigneeIds.value = [];
});

function openCreateModal() {
  newTitle.value = '';
  newContent.value = '';
  selectedTagIds.value = [];
  selectedAssigneeIds.value = [];
  sourceType.value = 'self';
  createError.value = '';
  showCreateModal.value = true;
}
function openDetail(note: Note) {
  selectedNote.value = note;
  editingTitle.value = note.title || '';
  editingSubTag.value = note.sub_tag || '';
  editingContent.value = note.content || '';
  selectedEditingTagIds.value = (note.tags || []).map((t) => t.id);
  tagError.value = '';
  showDetailPanel.value = true;
}
function closeDetail() {
  showDetailPanel.value = false;
  selectedNote.value = null;
  completing.value = false;
}

async function handleSubmit() {
  if (creating.value) return;
  if (!newTitle.value.trim()) {
    createError.value = '请输入任务标题';
    return;
  }
  if (sourceType.value !== 'self' && selectedAssigneeIds.value.length === 0) {
    createError.value = '请选择指派人员';
    return;
  }
  creating.value = true;
  createError.value = '';
  try {
    const payload: any = {
      title: newTitle.value.trim(),
      sub_tag: newSubTag.value.trim(),
      content: newContent.value,
      tags: selectedTagIds.value,
      source_type: sourceType.value,
    };
    if (sourceType.value !== 'self')
      payload.assignees = selectedAssigneeIds.value.map((id) => ({ user_id: id }));
    if (sourceType.value === 'assigned' && selectedAssigneeIds.value.length > 0)
      payload.owner_id = selectedAssigneeIds.value[0];
    const created = await noteStore.createNote(payload);
    if (sourceType.value !== 'self' && created) {
      try {
        await noteStore.remindNote(
          created.id,
          created.owner_id,
          `【任务指派】${auth.user?.name || '管理员'} 指派您处理：${newTitle.value.trim()}`
        );
      } catch {
        /* ignore */
      }
    }
    showCreateModal.value = false;
  } catch (e: unknown) {
    createError.value =
      (e as { response?: { data?: { message?: string } } })?.response?.data?.message ||
      '创建任务失败';
  } finally {
    creating.value = false;
  }
}

async function handleSaveDetail() {
  if (!selectedNote.value) return;
  saving.value = true;
  try {
    await noteStore.updateNoteLocally(selectedNote.value.id, {
      title: editingTitle.value.trim(),
      sub_tag: editingSubTag.value.trim(),
      content: editingContent.value,
      tags: selectedEditingTagIds.value,
    } as any);
    closeDetail();
  } catch {
    /* ignore */
  } finally {
    saving.value = false;
  }
}
async function handleUpdateTags(tagIds: string[]) {
  if (!selectedNote.value) return;
  selectedEditingTagIds.value = tagIds;
  tagSaving.value = true;
  tagError.value = '';
  try {
    await noteStore.updateNoteTags(selectedNote.value.id, tagIds);
    selectedNote.value = {
      ...selectedNote.value,
      tags:
        noteStore.activeNotes.find((n) => n.id === selectedNote.value!.id)?.tags ||
        selectedNote.value.tags,
    };
  } catch {
    tagError.value = '标签更新失败，请重试';
    selectedEditingTagIds.value = (selectedNote.value.tags || []).map((t) => t.id);
  } finally {
    tagSaving.value = false;
  }
}
async function handleComplete(note: Note) {
  await noteStore.completeNote(note.id);
  if (showDetailPanel.value && selectedNote.value?.id === note.id) closeDetail();
}
async function handleRemind(note: Note) {
  await noteStore.remindNote(note.id, note.owner_id, '请尽快处理');
  if (showDetailPanel.value && selectedNote.value?.id === note.id)
    selectedNote.value = { ...selectedNote.value, color_status: 'red' as const };
}

async function loadWorkGroups() {
  wgLoading.value = true;
  try {
    const res = await searchGroups({
      page: groupsPage.value,
      page_size: groupsPageSize,
      keyword: groupsFilter.value.keyword || undefined,
      date_from: groupsFilter.value.date_from || undefined,
      date_to: groupsFilter.value.date_to || undefined,
    });
    const d = res.data as unknown as { data: WorkGroupData[]; total: number };
    workGroups.value = d.data || [];
    groupsTotal.value = d.total || 0;
  } catch {
    /* ignore */
  } finally {
    wgLoading.value = false;
  }
}
function applyGroupsFilter() {
  groupsPage.value = 1;
  loadWorkGroups();
}
function resetGroupsFilter() {
  groupsFilter.value = { keyword: '', date_from: '', date_to: '' };
  groupsPage.value = 1;
  loadWorkGroups();
}
function goToGroup(id: string) {
  router.push(`/workbench/groups/${id}`);
}

function openWGModal() {
  wgName.value = '';
  wgDescription.value = '';
  wgTemplate.value = 'default';
  wgDueDate.value = '';
  wgSubGroups.value = [{ name: '', members: [] }];
  selectedWGUserIds.value = [[]];
  wgError.value = '';
  aiMode.value = false;
  aiDescription.value = '';
  aiResult.value = null;
  aiError.value = '';
  showWorkGroupModal.value = true;
}
function addSubGroup() {
  wgSubGroups.value.push({ name: '', members: [] });
  selectedWGUserIds.value.push([]);
}
function removeSubGroup(idx: number) {
  wgSubGroups.value.splice(idx, 1);
  selectedWGUserIds.value.splice(idx, 1);
}
function onWGUserSelect(idx: number, userIds: string[]) {
  selectedWGUserIds.value[idx] = userIds;
  wgSubGroups.value[idx].members = userIds.map((uid) => ({
    user_id: uid,
    role: idx === 0 ? 'leader' : 'member',
    sub_group_name: wgSubGroups.value[idx].name || `小组${idx + 1}`,
  }));
}
async function handleCreateWorkGroup() {
  if (wgCreating.value) return;
  if (!wgName.value.trim()) {
    wgError.value = '请输入工作组名称';
    return;
  }
  const allMembers = wgSubGroups.value.flatMap(
    (sg, idx) =>
      selectedWGUserIds.value[idx]?.map((uid) => ({
        user_id: uid,
        role: idx === 0 ? 'leader' : 'member',
        sub_group_name: sg.name || `小组${idx + 1}`,
      })) || []
  );
  if (allMembers.length === 0) {
    wgError.value = '请至少选择一个成员';
    return;
  }
  wgCreating.value = true;
  wgError.value = '';
  try {
    await createWorkGroup({
      name: wgName.value.trim(),
      description: wgDescription.value,
      template_type: wgTemplate.value,
      due_time: wgDueDate.value ? new Date(wgDueDate.value).toISOString() : undefined,
      preset_id: selectedPresetId.value || undefined,
      members: allMembers,
    });
    showWorkGroupModal.value = false;
    await Promise.all([noteStore.fetchNotes(), loadWorkGroups()]);
  } catch (e: unknown) {
    wgError.value =
      (e as { response?: { data?: { message?: string } } })?.response?.data?.message ||
      '创建工作组失败';
  } finally {
    wgCreating.value = false;
  }
}
async function handleDeleteGroup(id: string) {
  try {
    await deleteWorkGroup(id);
    await loadWorkGroups();
  } catch {
    /* ignore */
  }
}

function openAIMode() {
  aiMode.value = true;
  aiDescription.value = wgDescription.value || '';
  aiResult.value = null;
  aiError.value = '';
}

function closeAIMode() {
  aiMode.value = false;
  aiResult.value = null;
  aiError.value = '';
}

async function handleAISuggest() {
  if (!aiDescription.value.trim() || aiDescription.value.trim().length < 10) {
    aiError.value = '请至少输入10个字的工作要求描述';
    return;
  }
  aiSuggesting.value = true;
  aiError.value = '';
  aiResult.value = null;
  try {
    const res = await aiSuggestGroups(aiDescription.value.trim(), wgName.value.trim());
    aiResult.value = (res.data as unknown as AISuggestGroupsResult) ?? null;
    if (!aiResult.value) {
      aiError.value = 'AI未能返回有效的分组建议';
    }
  } catch (e: unknown) {
    aiError.value =
      (e as { response?: { data?: { message?: string } } })?.response?.data?.message ||
      'AI分析失败，请稍后重试';
  } finally {
    aiSuggesting.value = false;
  }
}

function applyAISuggestion() {
  if (!aiResult.value) return;

  if (aiResult.value.suggested_name) {
    wgName.value = aiResult.value.suggested_name;
  }
  if (aiResult.value.suggested_template) {
    wgTemplate.value = aiResult.value.suggested_template;
  }
  wgDescription.value = aiDescription.value;

  wgSubGroups.value = aiResult.value.sub_groups.map((sg) => ({
    name: sg.name,
    members: sg.members.map((m) => ({
      user_id: m.user_id,
      role: m.role,
      sub_group_name: sg.name,
    })),
  }));
  selectedWGUserIds.value = aiResult.value.sub_groups.map((sg) =>
    sg.members.map((m) => m.user_id)
  );

  aiMode.value = false;
  aiResult.value = null;
}

async function loadWorkTypeOptions() {
  try {
    const res = await getWorkTypeOptions();
    workTypeOptions.value = (res.data as unknown as WorkTypeOption[]) || [];
  } catch {
    workTypeOptions.value = [];
  }
}

async function loadPresets() {
  try {
    const res = await getPresets();
    availablePresets.value = (res.data as unknown as PresetGroup[]) || [];
  } catch {
    availablePresets.value = [];
  }
}

async function loadUserTemplates() {
  try {
    const res = await fetchTemplates();
    userTemplates.value = res.data || [];
  } catch {
    userTemplates.value = [];
  }
}

function onTemplateSelect() {
  if (!selectedTemplateId.value) return;
  const tpl = userTemplates.value.find((t) => t.id === selectedTemplateId.value);
  if (!tpl) return;
  let fields: any[] = [];
  try {
    fields = JSON.parse(
      typeof tpl.fields === 'string' ? tpl.fields : JSON.stringify(tpl.fields ?? [])
    );
  } catch {}
  if (fields.length > 0) {
    const lines = fields.map((f: any) => `【${f.name}】`);
    newContent.value = `📋 模板：${tpl.name}\n${lines.join('\n')}\n\n${newContent.value || ''}`;
  }
}

function handlePresetSelect() {
  if (!selectedPresetId.value) return;
  const preset = availablePresets.value.find((p) => p.id === selectedPresetId.value);
  if (!preset || !preset.members) return;
  selectedAssigneeIds.value = preset.members.map((m) => m.user_id);
}

async function handleRecommend() {
  if (!selectedWorkType.value) return;
  recommending.value = true;
  recommendError.value = '';
  recommendResult.value = [];
  try {
    const res = await recommendUsers({ work_type: selectedWorkType.value, limit: 10 });
    const users = (res.data as unknown as any[]) || [];
    recommendResult.value = users.map((u: any) => ({
      id: u.id,
      name: u.name,
      dept_name: u.department?.name || u.dept_name || '',
      work_type_stats: [],
    }));
    if (users.length === 0) {
      recommendError.value = '暂无匹配的推荐人员';
    }
  } catch {
    recommendError.value = '推荐失败，请重试';
  } finally {
    recommending.value = false;
  }
}

function selectRecommendUser(userId: string) {
  if (!selectedAssigneeIds.value.includes(userId)) {
    if (sourceType.value === 'assigned') {
      selectedAssigneeIds.value = [userId];
    } else {
      selectedAssigneeIds.value.push(userId);
    }
  }
}

function getMemberCount(g: WorkGroupData): number {
  return g.members?.length || 0;
}
function getMemberNames(g: WorkGroupData): string {
  return (
    g.members
      ?.slice(0, 4)
      .map((m) => m.user?.name || m.user_id)
      .join('、') || ''
  );
}
function formatTime(d: string) {
  return d ? new Date(d).toLocaleString('zh-CN') : '-';
}
function statusLabel(s: string) {
  const m: Record<string, string> = { active: '进行中', completed: '已完成', archived: '已归档' };
  return m[s] || s;
}
const templateLabels: Record<string, string> = {
  default: '日常任务',
  data_analysis: '数据分析',
  special_project: '专项行动',
  emergency_canvas: '紧急协查',
  collaborative_writing: '协同作战',
};
</script>

<template>
  <div class="relative min-h-full">
    <!-- Tab bar -->
    <div class="flex items-center gap-3 mb-6">
      <button
        v-for="tab in [
          { label: '全部', value: 'all' },
          { label: '待办', value: 'active' },
          { label: '指派', value: 'assigned' },
          { label: '盯办', value: 'red' },
          { label: '已完成', value: 'completed' },
        ]"
        :key="tab.value"
        :class="[
          'px-4 py-1.5 rounded-btn text-sm font-medium transition-smooth',
          activeTab === tab.value
            ? 'bg-[#3B82F6] text-white'
            : 'bg-white dark:bg-slate-800 text-slate-600 dark:text-slate-400 hover:bg-slate-50 dark:hover:bg-slate-700 border border-slate-200 dark:border-slate-600',
        ]"
        @click="handleTabClick(tab.value)"
      >
        {{ tab.label }}
      </button>
      <div class="w-px h-6 bg-slate-200 dark:bg-slate-700 mx-1" />
      <button
        :class="[
          'px-4 py-1.5 rounded-btn text-sm font-medium transition-smooth flex items-center gap-1.5',
          activeTab === 'groups'
            ? 'bg-gradient-to-r from-purple-500 to-blue-500 text-white'
            : 'bg-white dark:bg-slate-800 text-purple-600 dark:text-purple-400 hover:bg-purple-50 dark:hover:bg-slate-700 border border-purple-200 dark:border-purple-700',
        ]"
        @click="handleTabClick('groups')"
      >
        <span>🏢</span><span>专项工作组</span>
      </button>
      <button
        v-if="activeTab === 'groups'"
        class="ml-auto px-4 py-1.5 text-sm font-medium text-white bg-gradient-to-r from-purple-500 to-blue-500 hover:from-purple-600 hover:to-blue-600 rounded-lg transition-smooth shadow-sm flex items-center gap-1.5"
        @click="openWGModal()"
      >
        <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path
            stroke-linecap="round"
            stroke-linejoin="round"
            stroke-width="2"
            d="M12 4v16m8-8H4"
          />
        </svg>
        一键创建
      </button>
    </div>

    <!-- Error -->
    <div
      v-if="noteStore.error"
      class="mb-6 px-4 py-3 bg-red-50 border border-red-200 rounded-card text-sm text-red-600 flex items-center justify-between"
    >
      <span>{{ noteStore.error }}</span>
      <button
        class="text-xs text-red-500 underline hover:text-red-700 ml-4"
        @click="noteStore.fetchNotes()"
      >
        重试
      </button>
    </div>

    <!-- ====== 专项行动列表 ====== -->
    <template v-if="activeTab === 'groups'">
      <div
        class="mb-4 p-4 rounded-xl border border-purple-100 dark:border-purple-800 bg-purple-50/30 dark:bg-purple-900/5"
      >
        <div class="flex flex-wrap items-center gap-3">
          <input
            v-model="groupsFilter.keyword"
            type="text"
            class="px-3 py-1.5 text-sm border border-purple-200 dark:border-purple-700 rounded-lg bg-white dark:bg-slate-900 text-slate-900 dark:text-slate-100 focus:outline-none focus:ring-1 focus:ring-purple-400 placeholder-slate-400 w-48"
            placeholder="🔍 关键词搜索..."
            @keydown.enter.prevent="applyGroupsFilter()"
          />
          <input
            v-model="groupsFilter.date_from"
            type="date"
            class="px-3 py-1.5 text-sm border border-purple-200 dark:border-purple-700 rounded-lg bg-white dark:bg-slate-900 text-slate-900 dark:text-slate-100 focus:outline-none focus:ring-1 focus:ring-purple-400"
            @change="applyGroupsFilter()"
          />
          <span class="text-xs text-slate-400">至</span>
          <input
            v-model="groupsFilter.date_to"
            type="date"
            class="px-3 py-1.5 text-sm border border-purple-200 dark:border-purple-700 rounded-lg bg-white dark:bg-slate-900 text-slate-900 dark:text-slate-100 focus:outline-none focus:ring-1 focus:ring-purple-400"
            @change="applyGroupsFilter()"
          />
          <button
            class="px-3 py-1.5 text-xs text-slate-500 dark:text-slate-400 hover:text-slate-700 dark:hover:text-slate-300 bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 rounded-lg transition-smooth"
            @click="resetGroupsFilter()"
          >
            🔄 重置
          </button>
          <span class="ml-auto text-xs text-slate-400 dark:text-slate-500"
            >共 {{ groupsTotal }} 个专项行动</span
          >
        </div>
      </div>

      <div v-if="wgLoading" class="flex items-center justify-center py-16">
        <div
          class="animate-spin rounded-full h-8 w-8 border-2 border-purple-500 border-t-transparent"
        ></div>
      </div>

      <div
        v-else-if="workGroups.length === 0"
        class="text-center py-16 text-slate-400 dark:text-slate-500"
      >
        <p class="text-3xl mb-3">🏢</p>
        <p class="text-sm">暂无专项行动</p>
        <p class="text-xs mt-1">点击右上角「一键创建」发起跨部门专项协同工作</p>
      </div>

      <div v-else class="space-y-3">
        <div
          v-for="wg in workGroups"
          :key="wg.id"
          class="group p-4 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 hover:shadow-md hover:border-purple-200 dark:hover:border-purple-700 transition-smooth cursor-pointer"
          @click="goToGroup(wg.id)"
        >
          <div class="flex items-start justify-between mb-2">
            <div class="flex-1 min-w-0">
              <div class="flex items-center gap-2 mb-1">
                <span class="text-base font-semibold text-slate-800 dark:text-slate-200 truncate">{{
                  wg.name
                }}</span>
                <span
                  :class="[
                    'text-[10px] px-1.5 py-0.5 rounded-full font-medium shrink-0',
                    wg.status === 'active'
                      ? 'bg-green-100 dark:bg-green-900/50 text-green-600 dark:text-green-400'
                      : 'bg-slate-100 dark:bg-slate-700 text-slate-500',
                  ]"
                  >{{ statusLabel(wg.status) }}</span
                >
                <span
                  class="text-[10px] px-1.5 py-0.5 rounded-full bg-purple-50 dark:bg-purple-900/30 text-purple-500 dark:text-purple-400 font-medium shrink-0"
                  >{{ templateLabels[wg.template_type] || wg.template_type }}</span
                >
              </div>
              <p
                v-if="wg.description"
                class="text-xs text-slate-500 dark:text-slate-400 line-clamp-2 mb-1.5"
              >
                {{ wg.description }}
              </p>
              <div class="flex items-center gap-4 text-[11px] text-slate-400 dark:text-slate-500">
                <span>👤 {{ wg.initiator?.name || '未知' }}</span>
                <span>👥 {{ getMemberCount(wg) }} 人</span>
                <span class="truncate max-w-[200px]">{{ getMemberNames(wg) }}</span>
                <span v-if="wg.due_time" class="text-red-400"
                  >📅 截止 {{ wg.due_time.slice(0, 10) }}</span
                >
                <span class="text-slate-300 dark:text-slate-600">{{
                  formatTime(wg.created_at)
                }}</span>
              </div>
            </div>
            <button
              class="text-[11px] text-red-400 hover:text-red-600 dark:hover:text-red-300 transition-smooth opacity-0 group-hover:opacity-100 shrink-0 ml-3"
              @click.stop="handleDeleteGroup(wg.id)"
              title="删除"
            >
              🗑
            </button>
          </div>
        </div>
      </div>

      <div v-if="groupsTotal > groupsPageSize" class="flex items-center justify-between mt-6">
        <span class="text-xs text-slate-400">共 {{ groupsTotal }} 个专项行动</span>
        <div class="flex items-center gap-2">
          <button
            class="px-3 py-1 text-xs font-medium text-slate-600 dark:text-slate-400 bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 rounded transition-smooth disabled:opacity-40"
            :disabled="groupsPage <= 1"
            @click="
              groupsPage--;
              loadWorkGroups();
            "
          >
            上一页
          </button>
          <button
            class="px-3 py-1 text-xs font-medium text-slate-600 dark:text-slate-400 bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 rounded transition-smooth disabled:opacity-40"
            :disabled="groupsPage * groupsPageSize >= groupsTotal"
            @click="
              groupsPage++;
              loadWorkGroups();
            "
          >
            下一页
          </button>
        </div>
      </div>
    </template>

    <!-- ====== 任务内容区 ====== -->
    <template v-else>
      <div
        v-if="noteStore.loading && noteStore.activeNotes.length === 0"
        class="grid grid-cols-[repeat(auto-fill,minmax(280px,1fr))] gap-5"
      >
        <div v-for="n in 6" :key="n" class="skeleton h-44 rounded-card" />
      </div>
      <div
        v-else-if="!noteStore.loading && displayedNotes.length === 0 && !noteStore.error"
        class="flex flex-col items-center justify-center py-24"
      >
        <div
          class="w-24 h-24 bg-slate-100 dark:bg-slate-800 rounded-3xl flex items-center justify-center mb-6"
        >
          <svg
            class="w-12 h-12 text-slate-300 dark:text-slate-600"
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
          >
            <path
              stroke-linecap="round"
              stroke-linejoin="round"
              stroke-width="1.5"
              d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"
            />
          </svg>
        </div>
        <p class="text-slate-400 dark:text-slate-500 text-sm">
          {{ activeTab === 'completed' ? '暂无已完成任务' : '暂无活跃任务' }}
        </p>
        <p class="text-slate-300 dark:text-slate-600 text-xs mt-1">点击右下角 '+' 新建任务</p>
      </div>
      <div v-else class="grid grid-cols-[repeat(auto-fill,minmax(280px,1fr))] gap-5">
        <StickyNoteCard
          v-for="note in displayedNotes"
          :key="note.id"
          :note="note"
          mode="web"
          :archived="false"
          class="animate-spring-enter"
          @click="openDetail(note)"
          @complete="handleComplete"
          @remind="handleRemind"
        />
      </div>
    </template>

    <!-- FAB -->
    <button
      class="fixed right-8 bottom-8 w-14 h-14 rounded-full bg-[#3B82F6] text-white shadow-btn-float hover:bg-blue-600 active:scale-95 transition-smooth flex items-center justify-center z-30"
      @click="openCreateModal"
    >
      <svg class="w-7 h-7" fill="none" viewBox="0 0 24 24" stroke="currentColor">
        <path
          stroke-linecap="round"
          stroke-linejoin="round"
          stroke-width="2.5"
          d="M12 4v16m8-8H4"
        />
      </svg>
    </button>

    <!-- ====== 新建任务模态框 ====== -->
    <Teleport to="body">
      <div
        v-if="showCreateModal"
        class="fixed inset-0 z-50 flex items-start justify-center pt-[10vh]"
      >
        <div class="overlay-backdrop" @click="showCreateModal = false" />
        <div
          class="relative z-50 bg-white dark:bg-slate-800 rounded-card shadow-modal w-full max-w-xl mx-4 animate-fade-in"
        >
          <div class="p-6">
            <div class="flex items-center justify-between mb-6">
              <h2 class="text-lg font-semibold text-slate-900 dark:text-slate-100">新建任务</h2>
              <button
                class="p-1 rounded-lg hover:bg-slate-100 dark:hover:bg-slate-700 transition-smooth"
                @click="showCreateModal = false"
              >
                <svg
                  class="w-5 h-5 text-slate-400 dark:text-slate-500"
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
            <form class="space-y-4" @submit.prevent="handleSubmit" @keydown.enter.prevent>
              <input v-model="newTitle" name="title" class="input-field" placeholder="任务标题" autofocus />
              <input
                v-model="newSubTag"
                name="sub_tag"
                class="input-field"
                placeholder="二级标题 / 子标签（可选）"
              />
              <textarea
                v-model="newContent"
                name="content"
                class="input-field h-32 resize-none"
                placeholder="任务内容..."
              />
              <div>
                <label class="block text-xs font-medium text-slate-500 mb-1"
                  >使用模板（可选）</label
                >
                <select
                  v-model="selectedTemplateId"
                  class="w-full text-sm border rounded p-2"
                  @change="onTemplateSelect"
                >
                  <option value="">不使用模板</option>
                  <option v-for="t in userTemplates" :key="t.id" :value="t.id">
                    {{ t.name }}{{ t.is_system ? ' (系统)' : '' }}
                  </option>
                </select>
              </div>
              <div>
                <span class="text-xs text-slate-500 mb-1.5 block">标签</span
                ><TagSelector v-model="selectedTagIds" :max="5" />
              </div>
              <div>
                <span class="text-xs text-slate-500 mb-2 block">任务类型</span>
                <div class="flex gap-3">
                  <label
                    :class="[
                      'flex-1 flex items-center justify-center gap-2 px-4 py-3 rounded-btn border-2 cursor-pointer transition-smooth',
                      sourceType === 'self'
                        ? 'border-blue-500 bg-blue-50 dark:bg-blue-900/40 text-blue-700 dark:text-blue-400'
                        : 'border-slate-200 dark:border-slate-600 text-slate-500',
                    ]"
                    ><input v-model="sourceType" type="radio" value="self" class="sr-only" /><span
                      class="text-sm font-medium"
                      >仅自己</span
                    ></label
                  >
                  <label
                    :class="[
                      'flex-1 flex items-center justify-center gap-2 px-4 py-3 rounded-btn border-2 cursor-pointer transition-smooth',
                      sourceType === 'assigned'
                        ? 'border-blue-500 bg-blue-50 dark:bg-blue-900/40 text-blue-700 dark:text-blue-400'
                        : 'border-slate-200 dark:border-slate-600 text-slate-500',
                    ]"
                    ><input
                      v-model="sourceType"
                      type="radio"
                      value="assigned"
                      class="sr-only"
                    /><span class="text-sm font-medium">指派他人</span></label
                  >
                </div>
              </div>
              <div v-if="sourceType !== 'self'">
                <span class="text-xs text-slate-500 mb-1.5 block">{{
                  sourceType === 'assigned' ? '选择负责人' : '选择协作人员'
                }}</span
                ><UserPicker
                  v-model="selectedAssigneeIds"
                  :multiple="sourceType === 'collaboration'"
                  :max="sourceType === 'assigned' ? 1 : 20"
                  :drop-up="true"
                />
                <div
                  class="mt-3 p-3 rounded-lg border border-dashed border-blue-200 dark:border-blue-700 bg-blue-50/50 dark:bg-blue-900/10"
                >
                  <div class="flex items-center gap-2 mb-2">
                    <span class="text-xs text-slate-500">🔍 工作类型</span>
                    <select
                      v-model="selectedWorkType"
                      class="input-field !py-1 !text-xs !w-auto"
                      @change="handleRecommend()"
                    >
                      <option value="">选择类型</option>
                      <option v-for="opt in workTypeOptions" :key="opt.value" :value="opt.value">
                        {{ opt.label }}
                      </option>
                    </select>
                    <button
                      type="button"
                      class="text-xs px-2.5 py-1 bg-gradient-to-r from-blue-500 to-purple-500 text-white rounded-btn hover:from-blue-600 hover:to-purple-600 transition-smooth disabled:opacity-50 flex items-center gap-1"
                      :disabled="!selectedWorkType || recommending"
                      @click="handleRecommend()"
                    >
                      <span
                        v-if="recommending"
                        class="inline-block w-3 h-3 border-2 border-white border-t-transparent rounded-full animate-spin"
                      ></span>
                      🤖 一键推荐
                    </button>
                  </div>
                  <p v-if="recommendError" class="text-xs text-red-500 mb-2">
                    {{ recommendError }}
                  </p>
                  <div
                    v-if="recommendResult.length > 0"
                    class="flex flex-wrap gap-1 max-h-24 overflow-y-auto"
                  >
                    <button
                      v-for="u in recommendResult"
                      :key="u.id"
                      type="button"
                      :class="[
                        'text-xs px-2 py-1 rounded-full transition-smooth',
                        selectedAssigneeIds.includes(u.id)
                          ? 'bg-blue-500 text-white'
                          : 'bg-white dark:bg-slate-700 text-slate-700 dark:text-slate-300 hover:bg-blue-100 dark:hover:bg-blue-900/40 border border-slate-200 dark:border-slate-600',
                      ]"
                      @click="selectRecommendUser(u.id)"
                    >
                      {{ u.name }}
                      <span v-if="u.dept_name" class="text-[10px] opacity-60 ml-0.5">{{
                        u.dept_name
                      }}</span>
                      <span v-if="selectedAssigneeIds.includes(u.id)" class="ml-1">✓</span>
                    </button>
                  </div>
                </div>
              </div>
              <p v-if="createError" class="text-sm text-red-500 bg-red-50 px-3 py-2 rounded-btn">
                {{ createError }}
              </p>
              <div
                class="flex justify-end gap-3 pt-4 border-t border-slate-100 dark:border-slate-700"
              >
                <button
                  type="button"
                  class="px-5 py-2.5 text-sm text-slate-600 dark:text-slate-400 bg-slate-100 dark:bg-slate-700 rounded-btn hover:bg-slate-200 dark:hover:bg-slate-600 transition-smooth"
                  @click="showCreateModal = false"
                  :disabled="creating"
                >
                  取消
                </button>
                <button
                  type="submit"
                  class="px-5 py-2.5 text-sm text-white bg-[#3B82F6] rounded-btn hover:bg-blue-600 transition-smooth disabled:opacity-50"
                  :disabled="creating"
                >
                  {{ creating ? '创建中...' : '创建任务' }}
                </button>
              </div>
            </form>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- ====== 详情侧滑面板 ====== -->
    <Teleport to="body">
      <div v-if="showDetailPanel && selectedNote">
        <div class="overlay-backdrop" @click="closeDetail" />
        <div class="slide-panel">
          <div class="p-6 h-full flex flex-col">
            <div class="flex items-center justify-between mb-6">
              <div class="flex items-center gap-2">
                <h2 class="text-lg font-semibold text-slate-900">任务详情</h2>
                <span
                  v-if="selectedNote.color_status === 'red'"
                  class="text-xs px-2 py-0.5 bg-red-100 text-red-700 rounded-tag"
                  >盯办中</span
                >
              </div>
              <button
                class="p-1 rounded-lg hover:bg-slate-100 dark:hover:bg-slate-700 transition-smooth"
                @click="closeDetail"
              >
                <svg
                  class="w-5 h-5 text-slate-400 dark:text-slate-500"
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
                ><input v-model="editingTitle" class="input-field text-base font-semibold" />
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
              <div>
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
                  <span class="text-slate-400">来源类型</span
                  ><span class="text-slate-700 dark:text-slate-300">{{
                    selectedNote.source_type === 'self'
                      ? '自己创建'
                      : selectedNote.source_type === 'assigned'
                        ? '上级指派'
                        : '协同任务'
                  }}</span>
                </div>
                <div class="flex justify-between text-xs" v-if="selectedNote.assignees?.length">
                  <span class="text-slate-400">负责人</span
                  ><span class="text-slate-700 dark:text-slate-300">{{
                    selectedNote.assignees.map((a) => a.name).join('、')
                  }}</span>
                </div>
                <div class="flex justify-between text-xs">
                  <span class="text-slate-400">创建时间</span
                  ><span class="text-slate-700 dark:text-slate-300">{{
                    selectedNote.created_at?.slice(0, 16).replace('T', ' ')
                  }}</span>
                </div>
                <div class="flex justify-between text-xs" v-if="selectedNote.due_time">
                  <span class="text-slate-400">截止时间</span
                  ><span class="text-red-500">{{
                    selectedNote.due_time.slice(0, 16).replace('T', ' ')
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
                    handleComplete(selectedNote!);
                  "
                >
                  {{ completing ? '归档中...' : '完成并归档' }}
                </button>
                <button
                  v-if="selectedNote.color_status !== 'red'"
                  class="flex-1 py-2.5 text-sm bg-red-50 text-red-600 rounded-btn hover:bg-red-100 transition-smooth"
                  @click="handleRemind(selectedNote!)"
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

    <!-- ====== 专项工作组创建模态框 ====== -->
    <Teleport to="body">
      <div
        v-if="showWorkGroupModal"
        class="fixed inset-0 z-50 flex items-start justify-center pt-[5vh]"
      >
        <div class="overlay-backdrop" @click="showWorkGroupModal = false" />
        <div
          class="relative z-50 bg-white dark:bg-slate-800 rounded-card shadow-modal w-full max-w-2xl mx-4 animate-fade-in max-h-[90vh] flex flex-col"
        >
          <div class="p-6 overflow-auto flex-1">
            <div class="flex items-center justify-between mb-6">
              <div>
                <h2 class="text-lg font-semibold text-slate-900 dark:text-slate-100">
                  🏢 一键创建专项工作组
                </h2>
                <p class="text-xs text-slate-400 dark:text-slate-500 mt-0.5">
                  创建工作组并自动将任务分发至每位成员
                </p>
              </div>
              <button
                class="p-1 rounded-lg hover:bg-slate-100 dark:hover:bg-slate-700 transition-smooth"
                @click="showWorkGroupModal = false"
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
            <form class="space-y-4" @submit.prevent="handleCreateWorkGroup" @keydown.enter.prevent>
              <div class="grid grid-cols-2 gap-4">
                <div>
                  <label class="text-xs text-slate-500 mb-1 block">工作组名称 *</label
                  ><input v-model="wgName" class="input-field" placeholder="如：雷霆2026专项行动" />
                </div>
                <div>
                  <label class="text-xs text-slate-500 mb-1 block">模板类型</label
                  ><select v-model="wgTemplate" class="input-field">
                    <option value="default">日常工作任务</option>
                    <option value="data_analysis">数据分析研判</option>
                    <option value="special_project">专项行动方案</option>
                    <option value="emergency_canvas">紧急协查通报</option>
                    <option value="collaborative_writing">协同作战方案</option>
                  </select>
                </div>
              </div>
              <div>
                <label class="text-xs text-slate-500 mb-1 block">工作要求描述</label
                ><textarea
                  v-model="wgDescription"
                  class="input-field h-20 resize-none"
                  :placeholder="aiMode ? '请详细描述专项工作内容、目标、预期成果、所需技能要求…' : '填写专项工作的具体要求、目标、时间节点及交付标准...'"
                />
              </div>
              <div v-if="!aiMode">
                <button
                  type="button"
                  class="w-full px-4 py-2.5 text-sm font-medium rounded-lg border-2 border-dashed border-purple-300 dark:border-purple-600 bg-gradient-to-r from-purple-50 to-blue-50 dark:from-purple-900/20 dark:to-blue-900/20 hover:from-purple-100 hover:to-blue-100 dark:hover:from-purple-900/30 dark:hover:to-blue-900/30 text-purple-600 dark:text-purple-400 transition-smooth flex items-center justify-center gap-2"
                  @click="openAIMode"
                >
                  <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17h14a2 2 0 002-2V5a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                  </svg>
                  🤖 AI智能成组 — 根据工作描述自动推荐人员分组
                </button>
              </div>
              <div
                v-if="aiMode"
                class="p-4 rounded-xl border-2 border-purple-300 dark:border-purple-600 bg-gradient-to-br from-purple-50/60 via-white to-blue-50/60 dark:from-purple-900/10 dark:via-slate-800 dark:to-blue-900/10 space-y-3"
              >
                <div class="flex items-center gap-2">
                  <span class="text-sm font-semibold text-purple-700 dark:text-purple-400">🤖 AI智能成组</span>
                  <span class="text-[10px] px-1.5 py-0.5 bg-purple-100 dark:bg-purple-800 text-purple-600 dark:text-purple-300 rounded-tag">Beta</span>
                  <button
                    type="button"
                    class="ml-auto text-xs text-slate-400 hover:text-slate-600 dark:hover:text-slate-300"
                    @click="closeAIMode"
                  >✕ 退出</button>
                </div>
                <textarea
                  v-model="aiDescription"
                  class="input-field h-24 resize-none"
                  placeholder="请详细描述专项工作内容：包括工作目标、主要任务、所需技能、时间要求等。例如：&#10;针对近期辖区内电信网络诈骗案件高发态势，需成立专项研判打击工作组，对涉案资金流、信息流进行深入分析，并与各片区派出所联动开展精准预警劝阻工作…"
                />
                <div class="flex items-center gap-3">
                  <button
                    type="button"
                    class="px-5 py-2 text-sm font-medium text-white bg-gradient-to-r from-purple-500 to-blue-500 hover:from-purple-600 hover:to-blue-600 rounded-lg transition-smooth disabled:opacity-50 flex items-center gap-2"
                    :disabled="aiSuggesting"
                    @click="handleAISuggest"
                  >
                    <span
                      v-if="aiSuggesting"
                      class="inline-block w-3.5 h-3.5 border-2 border-white border-t-transparent rounded-full animate-spin"
                    ></span>
                    <svg v-else class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z" />
                    </svg>
                    {{ aiSuggesting ? 'AI正在分析中…' : '开始AI智能分析' }}
                  </button>
                  <span class="text-[10px] text-slate-400">
                    AI将根据人员技能、职位、历史经验自动匹配最佳分组方案
                  </span>
                </div>
                <p v-if="aiError" class="text-sm text-red-500 bg-red-50 dark:bg-red-900/20 px-3 py-2 rounded-lg">
                  {{ aiError }}
                </p>
                <div
                  v-if="aiResult && aiResult.sub_groups.length > 0"
                  class="space-y-3"
                >
                  <div class="flex items-center gap-2 text-xs text-slate-500 dark:text-slate-400">
                    <span class="font-medium">{{ aiResult.source === 'ai' ? '✨ AI分析结果' : '📋 系统推荐方案' }}</span>
                    <span v-if="aiResult.source !== 'ai'" class="text-[10px] text-orange-500">（AI未可用，使用备用推荐）</span>
                  </div>
                  <p class="text-xs text-slate-600 dark:text-slate-300 leading-relaxed bg-white/50 dark:bg-slate-800/50 rounded-lg p-3">
                    {{ aiResult.analysis }}
                  </p>
                  <div class="flex items-center gap-2 text-xs">
                    <span class="text-slate-400">建议名称：</span>
                    <span class="font-medium text-slate-700 dark:text-slate-200">{{ aiResult.suggested_name }}</span>
                    <span class="text-slate-400 ml-2">模板：</span>
                    <span class="px-1.5 py-0.5 bg-blue-100 dark:bg-blue-800 text-blue-600 dark:text-blue-300 rounded-tag text-[10px]">
                      {{ templateLabels[aiResult.suggested_template] || aiResult.suggested_template }}
                    </span>
                  </div>
                  <div class="space-y-2">
                    <div
                      v-for="(sg, sgi) in aiResult.sub_groups"
                      :key="sgi"
                      class="p-3 rounded-xl border border-purple-100 dark:border-purple-800 bg-white/70 dark:bg-slate-800/70"
                    >
                      <div class="flex items-baseline gap-2 mb-1">
                        <span
                          class="text-[10px] px-1.5 py-0.5 rounded-tag font-medium"
                          :class="sgi === 0
                            ? 'bg-purple-100 dark:bg-purple-800 text-purple-600 dark:text-purple-400'
                            : 'bg-blue-100 dark:bg-blue-800 text-blue-600 dark:text-blue-400'"
                        >{{ sg.name }}</span>
                        <span class="text-[10px] text-slate-400">{{ sg.responsibility }}</span>
                      </div>
                      <div class="flex flex-wrap gap-1.5 mt-2">
                        <span
                          v-for="m in sg.members"
                          :key="m.user_id"
                          class="text-xs px-2 py-1 rounded-full border"
                          :class="m.role === 'leader'
                            ? 'border-amber-300 dark:border-amber-600 bg-amber-50 dark:bg-amber-900/20 text-amber-700 dark:text-amber-400'
                            : 'border-slate-200 dark:border-slate-600 bg-slate-50 dark:bg-slate-700 text-slate-600 dark:text-slate-300'"
                          :title="m.reason"
                        >
                          {{ m.role === 'leader' ? '👑 ' : '' }}{{ m.name }}
                          <span class="text-[10px] opacity-50 ml-0.5">{{ m.reason }}</span>
                        </span>
                      </div>
                    </div>
                  </div>
                  <button
                    type="button"
                    class="w-full px-4 py-2.5 text-sm font-medium text-white bg-gradient-to-r from-green-500 to-emerald-500 hover:from-green-600 hover:to-emerald-600 rounded-lg transition-smooth flex items-center justify-center gap-2"
                    @click="applyAISuggestion"
                  >
                    ✅ 采纳此方案，填充工作组信息
                  </button>
                </div>
              </div>
              <div>
                <label class="text-xs text-slate-500 mb-1 block">截止日期</label
                ><input v-model="wgDueDate" type="date" class="input-field" />
              </div>
              <div
                v-if="availablePresets.length > 0"
                class="p-3 rounded-lg border border-dashed border-orange-200 bg-orange-50/50"
              >
                <label class="text-xs text-slate-500 mb-1.5 flex items-center gap-1">
                  📋 选择人员预设组
                  <span class="text-[10px] text-orange-500">（自动填充成员）</span>
                </label>
                <select
                  v-model="selectedPresetId"
                  class="input-field !py-1.5 !text-sm"
                  @change="handlePresetSelect"
                >
                  <option value="">不使用预设</option>
                  <option v-for="preset in availablePresets" :key="preset.id" :value="preset.id">
                    {{ preset.name }} ({{ preset.members?.length || 0 }}人)
                  </option>
                </select>
              </div>
              <div>
                <div class="flex items-center justify-between mb-2">
                  <label class="text-xs text-slate-500">工作小组设置</label
                  ><button
                    type="button"
                    class="text-xs text-blue-500 hover:text-blue-600 font-medium"
                    @click="addSubGroup"
                  >
                    + 添加小组
                  </button>
                </div>
                <div class="space-y-3">
                  <div
                    v-for="(sg, idx) in wgSubGroups"
                    :key="idx"
                    class="p-3 rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800/40"
                  >
                    <div class="flex items-center gap-2 mb-2">
                      <span
                        class="text-[10px] px-1.5 py-0.5 rounded font-medium"
                        :class="
                          idx === 0
                            ? 'bg-purple-100 dark:bg-purple-900/50 text-purple-600 dark:text-purple-400'
                            : 'bg-blue-100 dark:bg-blue-900/50 text-blue-600 dark:text-blue-400'
                        "
                        >{{ idx === 0 ? '组长组' : `小组${idx + 1}` }}</span
                      >
                      <input
                        v-model="sg.name"
                        class="flex-1 text-xs px-2 py-1 border border-slate-200 dark:border-slate-600 rounded bg-white dark:bg-slate-700 text-slate-900 dark:text-slate-100"
                        placeholder="小组名称（可选）"
                      />
                      <button
                        v-if="wgSubGroups.length > 1"
                        type="button"
                        class="text-xs text-red-400 hover:text-red-600"
                        @click="removeSubGroup(idx)"
                      >
                        ✕
                      </button>
                    </div>
                    <UserPicker
                      :model-value="selectedWGUserIds[idx] || []"
                      :multiple="true"
                      :max="50"
                      @update:model-value="onWGUserSelect(idx, $event)"
                    />
                    <p class="text-[10px] text-slate-400 dark:text-slate-500 mt-1">
                      {{
                        idx === 0 ? '第一组为组长组，成员角色自动设为组长' : '成员角色自动设为组员'
                      }}
                    </p>
                  </div>
                </div>
              </div>
              <p v-if="wgError" class="text-sm text-red-500 bg-red-50 px-3 py-2 rounded-btn">
                {{ wgError }}
              </p>
              <div
                class="flex justify-end gap-3 pt-4 border-t border-slate-100 dark:border-slate-700"
              >
                <button
                  type="button"
                  class="px-5 py-2.5 text-sm text-slate-600 dark:text-slate-400 bg-slate-100 dark:bg-slate-700 rounded-btn hover:bg-slate-200 dark:hover:bg-slate-600 transition-smooth"
                  @click="showWorkGroupModal = false"
                  :disabled="wgCreating"
                >
                  取消
                </button>
                <button
                  type="submit"
                  class="px-5 py-2.5 text-sm text-white bg-gradient-to-r from-purple-500 to-blue-500 rounded-btn hover:from-purple-600 hover:to-blue-600 transition-smooth disabled:opacity-50"
                  :disabled="wgCreating"
                >
                  {{ wgCreating ? '创建中...' : '一键创建并分发任务' }}
                </button>
              </div>
            </form>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>
