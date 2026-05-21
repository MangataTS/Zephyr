<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { getUsers, getUsersWithStats, createUser, updateUser, deleteUser, getDepartments } from '@/services/admin'
import type { User, Department, WorkTypeStat } from '@/types'
import { WORK_TYPE_LABELS } from '@/types'
import { getPresets, createPreset, deletePreset } from '@/services/presets'
import type { PresetGroup } from '@/types/preset'

interface UserRow {
  id: string
  username: string
  name: string
  dept_name: string
  dept_id: string
  role: string
  rank: string
  position: string
  skills: string
  phone: string
  email: string
  is_active: boolean
  work_type_stats: WorkTypeStat[]
}

const users = ref<UserRow[]>([])
const loading = ref(false)
const loadError = ref('')

const departments = ref<Department[]>([])
const allDepts = ref<{ id: string; name: string }[]>([])

const showModal = ref(false)
const showProfile = ref(false)
const profileUser = ref<UserRow | null>(null)
const editingUserId = ref<string | null>(null)
const formUsername = ref('')
const formName = ref('')
const formPassword = ref('')
const formRole = ref('user')
const formRank = ref('')
const formPosition = ref('')
const formSkills = ref('')
const formPhone = ref('')
const formEmail = ref('')
const formDeptId = ref('')
const formIsActive = ref(true)
const submitting = ref(false)
const formError = ref('')

const roleMap: Record<string, string> = {
  super_admin: '系统管理员',
  dept_admin: '部门管理员',
  group_leader: '组长',
  user: '普通员工',
  screen_role: '大屏角色',
}

function flattenDepts(depts: Department[], prefix = ''): void {
  for (const d of depts) {
    allDepts.value.push({ id: d.id, name: prefix + d.name })
    if (d.children) flattenDepts(d.children, prefix + '  ')
  }
}

function parseSkills(skills: string): string[] {
  if (!skills) return []
  try {
    const parsed = JSON.parse(skills)
    return Array.isArray(parsed) ? parsed : [skills]
  } catch {
    return skills.split(/[,，;；]/).map((s) => s.trim()).filter(Boolean)
  }
}

async function loadData() {
  loading.value = true
  loadError.value = ''
  try {
    const [deptRes, userRes] = await Promise.all([
      getDepartments(false),
      getUsersWithStats({ page: 1, page_size: 100 }),
    ])
    departments.value = deptRes.data as unknown as Department[]
    allDepts.value = []
    flattenDepts(departments.value)
    const rawData = (userRes.data as unknown as { data: any[] }).data || []
    users.value = rawData.map((u: any) => ({
      ...u,
      dept_name: u.department?.name || u.dept_name || '未分配',
      dept_id: u.dept_id || u.department?.id || '',
      role: u.role || 'user',
      rank: u.rank || '',
      position: u.position || '',
      skills: u.skills || '',
      phone: u.phone || '',
      email: u.email || '',
      work_type_stats: u.work_type_stats || [],
    }))
  } catch {
    loadError.value = '加载人员列表失败'
  } finally {
    loading.value = false
  }
}

function openCreate() {
  editingUserId.value = null
  formUsername.value = ''
  formName.value = ''
  formPassword.value = ''
  formRole.value = 'user'
  formRank.value = ''
  formPosition.value = ''
  formSkills.value = ''
  formPhone.value = ''
  formEmail.value = ''
  formDeptId.value = ''
  formIsActive.value = true
  formError.value = ''
  showModal.value = true
}

function openEdit(user: UserRow) {
  editingUserId.value = user.id
  formUsername.value = user.username
  formName.value = user.name
  formPassword.value = ''
  formRole.value = user.role
  formRank.value = user.rank || ''
  formPosition.value = user.position || ''
  formSkills.value = user.skills || ''
  formPhone.value = user.phone || ''
  formEmail.value = user.email || ''
  formDeptId.value = (user.dept_id || '').trim()
  formIsActive.value = user.is_active !== false
  formError.value = ''
  showModal.value = true
}

async function handleSubmit() {
  if (!formName.value.trim()) {
    formError.value = '请输入姓名'
    return
  }
  if (!editingUserId.value && !formUsername.value.trim()) {
    formError.value = '请输入用户名'
    return
  }
  if (!editingUserId.value && !formPassword.value.trim()) {
    formError.value = '请输入密码'
    return
  }

  const deptId = (formDeptId.value || '').trim()
  const rawSkills = formSkills.value.trim()

  submitting.value = true
  formError.value = ''
  try {
    if (editingUserId.value) {
      await updateUser(editingUserId.value, {
        name: formName.value.trim(),
        role: formRole.value,
        rank: (formRank.value || '').trim(),
        position: (formPosition.value || '').trim(),
        skills: rawSkills,
        phone: (formPhone.value || '').trim(),
        email: (formEmail.value || '').trim(),
        dept_id: deptId,
        is_active: formIsActive.value,
      })
    } else {
      await createUser({
        username: formUsername.value.trim(),
        name: formName.value.trim(),
        password: formPassword.value,
        role: formRole.value,
        rank: (formRank.value || '').trim(),
        position: (formPosition.value || '').trim(),
        skills: rawSkills,
        phone: (formPhone.value || '').trim(),
        email: (formEmail.value || '').trim(),
        dept_id: deptId,
      })
    }
    showModal.value = false
    await loadData()
  } catch (e: unknown) {
    const err = e as { response?: { data?: { message?: string } } }
    formError.value = err?.response?.data?.message || '操作失败'
  } finally {
    submitting.value = false
  }
}

async function handleDelete(user: UserRow) {
  if (!confirm(`确定要删除人员"${user.name}"吗？`)) return
  try {
    await deleteUser(user.id)
    await loadData()
  } catch (e: unknown) {
    const err = e as { response?: { data?: { message?: string } } }
    alert(err?.response?.data?.message || '删除失败')
  }
}

function openProfile(user: UserRow) {
  profileUser.value = user
  showProfile.value = true
}

function getRoleClass(role: string) {
  const map: Record<string, string> = {
    super_admin: 'bg-amber-100 text-amber-700',
    dept_admin: 'bg-blue-100 text-blue-700',
    group_leader: 'bg-green-100 text-green-700',
    user: 'bg-slate-100 text-slate-600',
  }
  return map[role] || 'bg-slate-100 text-slate-600'
}

function getStatusClass(active: boolean) {
  return active ? 'bg-green-100 text-green-700' : 'bg-red-100 text-red-700'
}

const presets = ref<PresetGroup[]>([])
const presetLoading = ref(false)
const showPresetModal = ref(false)
const presetName = ref('')
const presetDesc = ref('')
const presetType = ref('default')
const presetMemberIds = ref<string[]>([])
const presetSubmitting = ref(false)
const presetError = ref('')

const presetTypeLabels: Record<string, string> = {
  default: '日常任务',
  data_analysis: '数据分析',
  special_project: '专项行动',
  emergency_canvas: '紧急协查',
  collaborative_writing: '协同作战',
}

async function loadPresets() {
  presetLoading.value = true
  try {
    const res = await getPresets()
    presets.value = (res.data as unknown as PresetGroup[]) || []
  } catch {
    /* ignore */
  } finally {
    presetLoading.value = false
  }
}

function openPresetCreate() {
  presetName.value = ''
  presetDesc.value = ''
  presetType.value = 'default'
  presetMemberIds.value = []
  presetError.value = ''
  showPresetModal.value = true
}

async function handleCreatePreset() {
  if (!presetName.value.trim()) {
    presetError.value = '请输入预设组名称'
    return
  }
  if (presetMemberIds.value.length === 0) {
    presetError.value = '请选择至少一个成员'
    return
  }
  presetSubmitting.value = true
  presetError.value = ''
  try {
    await createPreset({
      name: presetName.value.trim(),
      description: presetDesc.value.trim(),
      template_type: presetType.value,
      members: presetMemberIds.value.map((uid) => ({ user_id: uid, role: 'member' })),
    })
    showPresetModal.value = false
    await loadPresets()
  } catch {
    presetError.value = '创建预设组失败'
  } finally {
    presetSubmitting.value = false
  }
}

async function handleDeletePreset(id: string) {
  if (!confirm('确定要删除这个预设组吗？')) return
  try {
    await deletePreset(id)
    await loadPresets()
  } catch {
    /* ignore */
  }
}

const visibleUsers = computed(() => users.value)

onMounted(async () => {
  await loadData()
  loadPresets()
})
</script>

<template>
  <div>
    <div class="flex items-center justify-between mb-6">
      <h2 class="text-lg font-semibold text-slate-900">人员库管理</h2>
      <button class="btn-primary text-sm" @click="openCreate">新建人员</button>
    </div>

    <div class="bg-white dark:bg-slate-800 rounded-card border border-slate-100 dark:border-slate-700 overflow-hidden transition-colors duration-300">
      <div v-if="loading" class="p-8 text-center text-sm text-slate-400">加载中...</div>
      <div v-else-if="loadError" class="p-8 text-center text-sm text-red-400">
        {{ loadError }}
        <button class="block mx-auto mt-2 text-xs text-blue-500 hover:underline" @click="loadData">重试</button>
      </div>
      <table v-else class="w-full">
        <thead>
          <tr class="border-b border-slate-100 dark:border-slate-700 bg-slate-50 dark:bg-slate-900">
            <th class="text-left px-4 py-3 text-xs font-medium text-slate-500 uppercase tracking-wider">姓名</th>
            <th class="text-left px-4 py-3 text-xs font-medium text-slate-500 uppercase tracking-wider">用户名</th>
            <th class="text-left px-4 py-3 text-xs font-medium text-slate-500 uppercase tracking-wider">部门</th>
            <th class="text-left px-4 py-3 text-xs font-medium text-slate-500 uppercase tracking-wider">岗位/技能标签</th>
            <th class="text-left px-4 py-3 text-xs font-medium text-slate-500 uppercase tracking-wider">角色</th>
            <th class="text-left px-4 py-3 text-xs font-medium text-slate-500 uppercase tracking-wider">参与类型</th>
            <th class="text-left px-4 py-3 text-xs font-medium text-slate-500 uppercase tracking-wider">状态</th>
            <th class="text-right px-4 py-3 text-xs font-medium text-slate-500 uppercase tracking-wider">操作</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-slate-50">
          <tr v-for="user in users" :key="user.id" class="hover:bg-slate-50/50 transition-smooth">
            <td class="px-4 py-3">
              <div class="flex items-center gap-3">
                <div class="w-8 h-8 rounded-full bg-blue-100 flex items-center justify-center text-xs font-medium text-blue-600">
                  {{ user.name.charAt(0) }}
                </div>
                <div>
                  <div class="text-sm font-medium text-slate-900">{{ user.name }}</div>
                  <div class="text-xs text-slate-400" v-if="user.rank">{{ user.rank }}</div>
                </div>
              </div>
            </td>
            <td class="px-4 py-3 text-sm text-slate-600 font-mono">{{ user.username }}</td>
            <td class="px-4 py-3 text-sm text-slate-600">{{ user.dept_name }}</td>
            <td class="px-4 py-3">
              <div class="flex flex-wrap gap-1">
                <span v-if="user.position" class="text-[10px] px-1.5 py-0.5 rounded-full bg-purple-50 dark:bg-purple-900/40 text-purple-600 dark:text-purple-400 font-medium">
                  {{ user.position }}
                </span>
                <span
                  v-for="skill in parseSkills(user.skills)"
                  :key="skill"
                  class="text-[10px] px-1.5 py-0.5 rounded-full bg-blue-50 dark:bg-blue-900/40 text-blue-600 dark:text-blue-400 font-medium"
                >
                  {{ skill }}
                </span>
                <span v-if="!user.position && !user.skills" class="text-xs text-slate-300">-</span>
              </div>
            </td>
            <td class="px-4 py-3">
              <span :class="['text-xs px-2 py-0.5 rounded-tag font-medium', getRoleClass(user.role)]">
                {{ roleMap[user.role] || user.role }}
              </span>
            </td>
            <td class="px-4 py-3">
              <div class="flex flex-wrap gap-1">
                <span
                  v-for="stat in (user.work_type_stats || [])"
                  :key="stat.work_type"
                  class="text-[10px] px-1.5 py-0.5 rounded-full bg-green-50 dark:bg-green-900/40 text-green-600 dark:text-green-400 font-medium"
                  :title="`${WORK_TYPE_LABELS[stat.work_type] || stat.work_type}：${stat.group_count}次`"
                >
                  {{ WORK_TYPE_LABELS[stat.work_type] || stat.work_type }} {{ stat.group_count }}次
                </span>
                <span v-if="!user.work_type_stats?.length" class="text-xs text-slate-300">暂无</span>
              </div>
            </td>
            <td class="px-4 py-3">
              <span :class="['text-xs px-2 py-0.5 rounded-tag font-medium', getStatusClass(user.is_active !== false)]">
                {{ user.is_active !== false ? '正常' : '禁用' }}
              </span>
            </td>
            <td class="px-4 py-3 text-right">
              <div class="flex items-center justify-end gap-1">
                <button class="text-xs px-2 py-1 bg-purple-50 text-purple-600 rounded hover:bg-purple-100 transition-smooth" @click="openProfile(user)">档案</button>
                <button class="text-xs px-2 py-1 bg-blue-50 text-blue-600 rounded hover:bg-blue-100 transition-smooth" @click="openEdit(user)">编辑</button>
                <button class="text-xs px-2 py-1 bg-red-50 text-red-600 rounded hover:bg-red-100 transition-smooth" @click="handleDelete(user)">删除</button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- 组预设管理 -->
    <div class="mt-6">
      <div class="flex items-center justify-between mb-3">
        <h3 class="text-base font-semibold text-slate-900">📋 组预设管理</h3>
        <button class="btn-primary text-xs !py-1.5 !px-3" @click="openPresetCreate">新建预设组</button>
      </div>
      <div v-if="presetLoading" class="text-xs text-slate-400 py-2">加载中...</div>
      <div v-else-if="presets.length === 0" class="text-xs text-slate-400 py-4 bg-slate-50 rounded-card text-center">
        暂无预设组，点击「新建预设组」创建常用人员组合，新建专项工作组时可直接选用
      </div>
      <div v-else class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
        <div
          v-for="preset in presets"
          :key="preset.id"
          class="bg-white border border-slate-100 rounded-card p-4 hover:border-blue-200 transition-smooth"
        >
          <div class="flex items-start justify-between mb-2">
            <div>
              <h4 class="text-sm font-medium text-slate-900">{{ preset.name }}</h4>
              <span class="text-[10px] px-1.5 py-0.5 rounded-full bg-purple-50 text-purple-600 font-medium mt-0.5 inline-block">
                {{ presetTypeLabels[preset.template_type] || preset.template_type }}
              </span>
            </div>
            <button
              class="text-[10px] px-1.5 py-0.5 bg-red-50 text-red-500 rounded hover:bg-red-100"
              @click="handleDeletePreset(preset.id)"
            >
              删除
            </button>
          </div>
          <p v-if="preset.description" class="text-xs text-slate-400 mb-2 line-clamp-1">{{ preset.description }}</p>
          <div class="flex flex-wrap gap-1">
            <span
              v-for="m in (preset.members || [])"
              :key="m.user_id"
              class="text-[10px] px-1.5 py-0.5 rounded-full bg-slate-100 text-slate-600"
            >
              {{ m.user?.name || m.user_id }}
            </span>
          </div>
        </div>
      </div>
    </div>

    <!-- 人员档案弹窗 -->
    <Teleport to="body">
      <div v-if="showProfile && profileUser" class="fixed inset-0 z-50 flex items-start justify-center pt-[8vh]">
        <div class="overlay-backdrop" @click="showProfile = false" />
        <div class="relative z-50 bg-white dark:bg-slate-800 rounded-card shadow-modal w-full max-w-lg mx-4 p-6 animate-fade-in">
          <div class="flex items-center justify-between mb-5">
            <div class="flex items-center gap-3">
              <div class="w-10 h-10 rounded-full bg-blue-100 flex items-center justify-center text-sm font-medium text-blue-600">
                {{ profileUser.name.charAt(0) }}
              </div>
              <div>
                <h3 class="text-base font-semibold text-slate-900">{{ profileUser.name }}</h3>
                <p class="text-xs text-slate-400">{{ profileUser.username }} · {{ profileUser.dept_name }}</p>
              </div>
            </div>
            <button class="p-1 rounded-lg hover:bg-slate-100" @click="showProfile = false">
              <svg class="w-5 h-5 text-slate-400" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/></svg>
            </button>
          </div>

          <div class="space-y-4 max-h-[65vh] overflow-y-auto pr-1 scrollbar-thin">
            <div class="grid grid-cols-2 gap-3">
              <div class="bg-slate-50 dark:bg-slate-900 rounded-lg p-3">
                <span class="text-[10px] text-slate-400 uppercase">角色</span>
                <p class="text-sm font-medium text-slate-700 dark:text-slate-300 mt-0.5">{{ roleMap[profileUser.role] || profileUser.role }}</p>
              </div>
              <div class="bg-slate-50 dark:bg-slate-900 rounded-lg p-3">
                <span class="text-[10px] text-slate-400 uppercase">警衔/职级</span>
                <p class="text-sm font-medium text-slate-700 dark:text-slate-300 mt-0.5">{{ profileUser.rank || '-' }}</p>
              </div>
            </div>

            <div class="bg-slate-50 dark:bg-slate-900 rounded-lg p-3">
              <span class="text-[10px] text-slate-400 uppercase">岗位</span>
              <p class="text-sm font-medium text-slate-700 dark:text-slate-300 mt-0.5">{{ profileUser.position || '未设置' }}</p>
            </div>

            <div class="bg-slate-50 dark:bg-slate-900 rounded-lg p-3">
              <span class="text-[10px] text-slate-400 uppercase">技能特长</span>
              <div class="flex flex-wrap gap-1 mt-1">
                <template v-if="parseSkills(profileUser.skills).length">
                  <span
                    v-for="skill in parseSkills(profileUser.skills)"
                    :key="skill"
                    class="text-xs px-2 py-0.5 rounded-full bg-blue-50 dark:bg-blue-900/40 text-blue-600 dark:text-blue-400 font-medium"
                  >
                    {{ skill }}
                  </span>
                </template>
                <span v-else class="text-sm text-slate-400">未设置</span>
              </div>
            </div>

            <div class="bg-slate-50 dark:bg-slate-900 rounded-lg p-3">
              <span class="text-[10px] text-slate-400 uppercase">参与过的工作类型</span>
              <div class="flex flex-wrap gap-1 mt-1">
                <template v-if="profileUser.work_type_stats?.length">
                  <span
                    v-for="stat in profileUser.work_type_stats"
                    :key="stat.work_type"
                    class="text-xs px-2 py-0.5 rounded-full bg-green-50 dark:bg-green-900/40 text-green-600 dark:text-green-400 font-medium"
                  >
                    {{ WORK_TYPE_LABELS[stat.work_type] || stat.work_type }} × {{ stat.group_count }}
                  </span>
                </template>
                <span v-else class="text-sm text-slate-400">暂无参与记录</span>
              </div>
            </div>

            <div class="grid grid-cols-2 gap-3">
              <div class="bg-slate-50 dark:bg-slate-900 rounded-lg p-3">
                <span class="text-[10px] text-slate-400 uppercase">手机号</span>
                <p class="text-sm font-medium text-slate-700 dark:text-slate-300 mt-0.5">{{ profileUser.phone || '-' }}</p>
              </div>
              <div class="bg-slate-50 dark:bg-slate-900 rounded-lg p-3">
                <span class="text-[10px] text-slate-400 uppercase">邮箱</span>
                <p class="text-sm font-medium text-slate-700 dark:text-slate-300 mt-0.5">{{ profileUser.email || '-' }}</p>
              </div>
            </div>
          </div>

          <div class="flex justify-end gap-3 pt-4 border-t border-slate-100 dark:border-slate-700 mt-4">
            <button class="btn-secondary text-xs !py-1.5 !px-4" @click="showProfile = false">关闭</button>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- 新建/编辑人员模态框 -->
    <Teleport to="body">
      <div v-if="showModal" class="fixed inset-0 z-50 flex items-start justify-center pt-[8vh]">
        <div class="overlay-backdrop" @click="showModal = false" />
        <div class="relative z-50 bg-white dark:bg-slate-800 rounded-card shadow-modal w-full max-w-lg mx-4 p-6 animate-fade-in">
          <h3 class="text-base font-semibold text-slate-900 mb-4">{{ editingUserId ? '编辑人员' : '新建人员' }}</h3>

          <form @submit.prevent="handleSubmit" class="space-y-3 max-h-[70vh] overflow-y-auto pr-1 scrollbar-thin">
            <div class="grid grid-cols-2 gap-3">
              <div>
                <span class="text-xs text-slate-500 mb-1 block">用户名</span>
                <input v-model="formUsername" name="username" class="input-field !py-1.5 !text-sm" placeholder="登录用户名" :disabled="!!editingUserId" />
              </div>
              <div>
                <span class="text-xs text-slate-500 mb-1 block">密码</span>
                <input v-model="formPassword" name="password" type="password" class="input-field !py-1.5 !text-sm" :placeholder="editingUserId ? '留空不修改' : '设置密码'" />
              </div>
            </div>

            <div class="grid grid-cols-2 gap-3">
              <div>
                <span class="text-xs text-slate-500 mb-1 block">姓名</span>
                <input v-model="formName" name="name" class="input-field !py-1.5 !text-sm" placeholder="真实姓名" />
              </div>
              <div>
                <span class="text-xs text-slate-500 mb-1 block">警衔/职级</span>
                <input v-model="formRank" name="rank" class="input-field !py-1.5 !text-sm" placeholder="如: 二级警督" />
              </div>
            </div>

            <div class="grid grid-cols-2 gap-3">
              <div>
                <span class="text-xs text-slate-500 mb-1 block">岗位</span>
                <input v-model="formPosition" name="position" class="input-field !py-1.5 !text-sm" placeholder="如: 刑侦民警" />
              </div>
              <div>
                <span class="text-xs text-slate-500 mb-1 block">技能特长</span>
                <input v-model="formSkills" name="skills" class="input-field !py-1.5 !text-sm" placeholder="逗号分隔，如: Python,数据分析" />
              </div>
            </div>

            <div class="grid grid-cols-2 gap-3">
              <div>
                <span class="text-xs text-slate-500 mb-1 block">角色</span>
                <select v-model="formRole" class="input-field !py-1.5 !text-sm">
                  <option value="super_admin">系统管理员</option>
                  <option value="dept_admin">部门管理员</option>
                  <option value="group_leader">组长</option>
                  <option value="user">普通员工</option>
                  <option value="screen_role">大屏角色</option>
                </select>
              </div>
              <div>
                <span class="text-xs text-slate-500 mb-1 block">所属部门</span>
                <select v-model="formDeptId" class="input-field !py-1.5 !text-sm">
                  <option value="">未分配</option>
                  <option v-for="d in allDepts" :key="d.id" :value="d.id">{{ d.name }}</option>
                </select>
              </div>
            </div>

            <div class="grid grid-cols-2 gap-3">
              <div>
                <span class="text-xs text-slate-500 mb-1 block">手机号</span>
                <input v-model="formPhone" name="phone" class="input-field !py-1.5 !text-sm" placeholder="手机号" />
              </div>
              <div>
                <span class="text-xs text-slate-500 mb-1 block">邮箱</span>
                <input v-model="formEmail" name="email" class="input-field !py-1.5 !text-sm" placeholder="邮箱地址" />
              </div>
            </div>

            <div v-if="editingUserId" class="flex items-center gap-2">
              <span class="text-xs text-slate-500">账号状态</span>
              <label class="flex items-center gap-1.5 cursor-pointer">
                <input v-model="formIsActive" type="checkbox" class="w-4 h-4 text-blue-500 rounded" />
                <span class="text-xs text-slate-700">{{ formIsActive ? '正常' : '禁用' }}</span>
              </label>
            </div>

            <p v-if="formError" class="text-xs text-red-500 bg-red-50 px-3 py-2 rounded-btn">{{ formError }}</p>

            <div class="flex justify-end gap-3 pt-4 border-t border-slate-100 dark:border-slate-700">
              <button type="button" class="btn-secondary text-xs !py-1.5 !px-4" @click="showModal = false">取消</button>
              <button type="submit" class="btn-primary text-xs !py-1.5 !px-4" :disabled="submitting">
                {{ submitting ? '提交中...' : editingUserId ? '保存修改' : '创建人员' }}
              </button>
            </div>
          </form>
        </div>
      </div>
    </Teleport>

    <!-- 新建预设组弹窗 -->
    <Teleport to="body">
      <div v-if="showPresetModal" class="fixed inset-0 z-50 flex items-start justify-center pt-[8vh]">
        <div class="overlay-backdrop" @click="showPresetModal = false" />
        <div class="relative z-50 bg-white dark:bg-slate-800 rounded-card shadow-modal w-full max-w-md mx-4 p-6 animate-fade-in">
          <h3 class="text-base font-semibold text-slate-900 mb-4">📋 新建预设组</h3>
          <form @submit.prevent="handleCreatePreset" class="space-y-3">
            <div>
              <span class="text-xs text-slate-500 mb-1 block">预设组名称</span>
              <input v-model="presetName" name="name" class="input-field !py-1.5 !text-sm" placeholder="如：研判专班A组" autofocus />
            </div>
            <div>
              <span class="text-xs text-slate-500 mb-1 block">描述（可选）</span>
              <input v-model="presetDesc" name="description" class="input-field !py-1.5 !text-sm" placeholder="简要说明此预设组的用途" />
            </div>
            <div>
              <span class="text-xs text-slate-500 mb-1 block">适用工作类型</span>
              <select v-model="presetType" class="input-field !py-1.5 !text-sm">
                <option value="default">日常任务</option>
                <option value="data_analysis">数据分析</option>
                <option value="special_project">专项行动</option>
                <option value="emergency_canvas">紧急协查</option>
                <option value="collaborative_writing">协同作战</option>
              </select>
            </div>
            <div>
              <span class="text-xs text-slate-500 mb-1 block">选择成员（{{ presetMemberIds.length }}人选）</span>
              <div class="max-h-40 overflow-y-auto border border-slate-200 rounded-btn p-2 space-y-0.5">
                <label
                  v-for="user in visibleUsers"
                  :key="user.id"
                  class="flex items-center gap-2 px-2 py-1 rounded hover:bg-slate-50 cursor-pointer"
                >
                  <input
                    v-model="presetMemberIds"
                    type="checkbox"
                    :value="user.id"
                    class="w-4 h-4 text-blue-500 rounded"
                  />
                  <span class="text-sm text-slate-700">{{ user.name }}</span>
                  <span class="text-xs text-slate-400">{{ user.dept_name }}</span>
                </label>
              </div>
            </div>
            <p v-if="presetError" class="text-xs text-red-500 bg-red-50 px-3 py-2 rounded-btn">{{ presetError }}</p>
            <div class="flex justify-end gap-3 pt-4 border-t border-slate-100 dark:border-slate-700">
              <button type="button" class="btn-secondary text-xs !py-1.5 !px-4" @click="showPresetModal = false">取消</button>
              <button type="submit" class="btn-primary text-xs !py-1.5 !px-4" :disabled="presetSubmitting">
                {{ presetSubmitting ? '创建中...' : '创建预设组' }}
              </button>
            </div>
          </form>
        </div>
      </div>
    </Teleport>
  </div>
</template>