<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { getDepartments, createDepartment, updateDepartment, deleteDepartment } from '@/services/admin'
import type { Department } from '@/types'

const departments = ref<Department[]>([])
const loading = ref(false)
const loadError = ref('')
const expanded = ref<Set<string>>(new Set())
const selectedDept = ref<Department | null>(null)

const editName = ref('')
const editParentId = ref('')
const saving = ref(false)
const saveError = ref('')
const deleting = ref(false)

const showNewModal = ref(false)
const newName = ref('')
const newParentId = ref('')
const newLevel = ref(1)
const creating = ref(false)
const createError = ref('')

async function loadData() {
  loading.value = true
  loadError.value = ''
  try {
    const res = await getDepartments(false)
    const data = res.data
    departments.value = Array.isArray(data) ? data : []
    if (departments.value.length > 0 && expanded.value.size === 0) {
      expanded.value.add(departments.value[0].id)
    }
  } catch {
    loadError.value = '加载部门架构失败'
  } finally {
    loading.value = false
  }
}

onMounted(loadData)

function toggleExpand(id: string) {
  const s = new Set(expanded.value)
  if (s.has(id)) s.delete(id)
  else s.add(id)
  expanded.value = s
}

function selectDept(dept: Department) {
  selectedDept.value = dept
  editName.value = dept.name
  editParentId.value = dept.parent_id || ''
  saveError.value = ''
}

function openNewModal(parentId?: string) {
  newName.value = ''
  newParentId.value = parentId || ''
  newLevel.value = parentId ? 2 : 1
  createError.value = ''
  showNewModal.value = true
}

async function handleCreate() {
  if (!newName.value.trim()) {
    createError.value = '请输入部门名称'
    return
  }
  creating.value = true
  createError.value = ''
  try {
    await createDepartment({
      name: newName.value.trim(),
      parent_id: newParentId.value || undefined,
      level: newLevel.value,
    })
    showNewModal.value = false
    await loadData()
  } catch (e: unknown) {
    const err = e as { response?: { data?: { message?: string } } }
    createError.value = err?.response?.data?.message || '创建部门失败'
  } finally {
    creating.value = false
  }
}

async function handleSave() {
  if (!selectedDept.value) return
  if (!editName.value.trim()) {
    saveError.value = '部门名称不能为空'
    return
  }
  saving.value = true
  saveError.value = ''
  try {
    await updateDepartment(selectedDept.value.id, {
      name: editName.value.trim(),
      parent_id: editParentId.value || undefined,
    })
    await loadData()
    selectedDept.value = null
  } catch (e: unknown) {
    const err = e as { response?: { data?: { message?: string } } }
    saveError.value = err?.response?.data?.message || '保存失败'
  } finally {
    saving.value = false
  }
}

async function handleDelete() {
  if (!selectedDept.value) return
  if (!confirm(`确定要删除部门"${selectedDept.value.name}"吗？此操作不可恢复。`)) return
  deleting.value = true
  try {
    await deleteDepartment(selectedDept.value.id)
    await loadData()
    selectedDept.value = null
  } catch (e: unknown) {
    const err = e as { response?: { data?: { message?: string } } }
    alert(err?.response?.data?.message || '删除失败')
  } finally {
    deleting.value = false
  }
}

function getParentName(id: string): string {
  const dept = departments.value.find(d => d.id === id)
  return dept?.name || '—'
}

function getDeptLevelLabel(level?: number): string {
  if (!level) return ''
  if (level === 1) return '一级'
  return '二级'
}
</script>

<template>
  <div>
    <div class="flex items-center justify-between mb-6">
      <h2 class="text-lg font-semibold text-slate-900">部门库管理</h2>
      <button class="btn-primary text-sm" @click="openNewModal()">新建部门</button>
    </div>

    <div class="grid grid-cols-2 gap-6">
      <!-- 左侧：部门树 -->
      <div class="bg-white dark:bg-slate-800 rounded-card border border-slate-100 dark:border-slate-700 p-4 transition-colors duration-300">
        <h4 class="text-sm font-semibold text-slate-700 mb-3">组织架构</h4>
        <div v-if="loading" class="skeleton h-40 rounded-lg" />
        <div v-else-if="loadError" class="text-sm text-red-400 py-8 text-center">
          {{ loadError }}
          <button class="block mx-auto mt-2 text-xs text-blue-500 hover:underline" @click="loadData">重试</button>
        </div>
        <div v-else class="space-y-1">
          <template v-for="dept in departments" :key="dept.id">
            <div class="flex items-center gap-1">
              <button
                class="flex-1 flex items-center gap-2 px-3 py-2 rounded-btn text-sm text-left transition-smooth hover:bg-slate-50"
                :class="{ 'bg-blue-50 text-blue-700 font-medium': selectedDept?.id === dept.id }"
                @click="toggleExpand(dept.id); selectDept(dept)"
              >
                <svg :class="['w-3.5 h-3.5 text-slate-400 transition-transform shrink-0', expanded.has(dept.id) ? 'rotate-90' : '']" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
                </svg>
                <span class="flex-1 truncate">{{ dept.name }}</span>
                <span class="text-xs text-slate-400 shrink-0">{{ dept.member_count }}人</span>
              </button>
              <button
                class="shrink-0 w-6 h-6 flex items-center justify-center rounded text-xs text-slate-300 hover:text-blue-500 hover:bg-blue-50 transition-smooth"
                title="添加子部门"
                @click.stop="openNewModal(dept.id)"
              >+</button>
            </div>
            <div v-if="expanded.has(dept.id) && dept.children" class="ml-6 space-y-1 border-l border-slate-100 dark:border-slate-700 pl-4">
              <template v-for="child in dept.children" :key="child.id">
                <div class="flex items-center gap-1">
                  <button
                    class="flex-1 flex items-center gap-2 px-3 py-2 rounded-btn text-sm text-left transition-smooth hover:bg-slate-50"
                    :class="{ 'bg-blue-50 text-blue-700 font-medium': selectedDept?.id === child.id }"
                    @click="selectDept(child)"
                  >
                    <span class="flex-1 truncate">{{ child.name }}</span>
                    <span class="text-xs text-slate-400 shrink-0">{{ child.member_count }}人</span>
                  </button>
                  <button
                    class="shrink-0 w-6 h-6 flex items-center justify-center rounded text-xs text-slate-300 hover:text-blue-500 hover:bg-blue-50 transition-smooth"
                    title="添加子部门"
                    @click.stop="openNewModal(child.id)"
                  >+</button>
                </div>
              </template>
            </div>
          </template>
        </div>
      </div>

      <!-- 右侧：部门详情 / 编辑 -->
      <div class="bg-white rounded-card border border-slate-100 p-4">
        <h4 class="text-sm font-semibold text-slate-700 mb-3">部门详情</h4>
        <div v-if="!selectedDept" class="text-sm text-slate-400 py-8 text-center">
          请在左侧选择一个部门
        </div>
        <div v-else class="space-y-3">
          <div>
            <span class="text-xs text-slate-400 mb-1 block">部门名称</span>
            <input v-model="editName" name="dept_name" class="input-field !py-1.5 !text-sm" placeholder="部门名称" />
          </div>
          <div>
            <span class="text-xs text-slate-400 mb-1 block">上级部门</span>
            <p class="text-sm text-slate-700">{{ getParentName(selectedDept.parent_id || '') }}</p>
          </div>
          <div class="flex items-center gap-2">
            <span class="text-xs text-slate-400 w-16">级别</span>
            <span class="text-sm text-slate-700">{{ getDeptLevelLabel(selectedDept.level) || '一级' }}</span>
          </div>
          <div class="flex items-center gap-2">
            <span class="text-xs text-slate-400 w-16">人数</span>
            <span class="text-sm text-slate-700">{{ selectedDept.member_count }} 人</span>
          </div>

          <p v-if="saveError" class="text-xs text-red-500">{{ saveError }}</p>

          <div class="flex gap-2 pt-3 border-t border-slate-100 dark:border-slate-700">
            <button class="btn-primary text-xs !py-1.5 !px-3" :disabled="saving" @click="handleSave">
              {{ saving ? '保存中...' : '保存' }}
            </button>
            <button class="px-3 py-1.5 text-xs bg-red-50 text-red-600 rounded-btn hover:bg-red-100 transition-smooth disabled:opacity-50" :disabled="deleting" @click="handleDelete">
              {{ deleting ? '删除中...' : '删除部门' }}
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- 新建部门模态框 -->
    <Teleport to="body">
      <div v-if="showNewModal" class="fixed inset-0 z-50 flex items-center justify-center">
        <div class="overlay-backdrop" @click="showNewModal = false" />
        <div class="relative z-50 bg-white dark:bg-slate-800 rounded-card shadow-modal w-full max-w-sm mx-4 p-6 animate-fade-in">
          <h3 class="text-base font-semibold text-slate-900 mb-4">新建部门</h3>
          <form @submit.prevent="handleCreate" class="space-y-4">
            <div>
              <span class="text-xs text-slate-500 mb-1 block">部门名称</span>
              <input v-model="newName" name="dept_name" class="input-field" placeholder="请输入部门名称" autofocus />
            </div>
            <div>
              <span class="text-xs text-slate-500 mb-1 block">上级部门</span>
              <select v-model="newParentId" class="input-field">
                <option value="">无（顶级部门）</option>
                <option v-for="dept in departments" :key="dept.id" :value="dept.id">{{ dept.name }}</option>
              </select>
            </div>
            <p v-if="createError" class="text-xs text-red-500">{{ createError }}</p>
            <div class="flex justify-end gap-3 pt-2">
              <button type="button" class="btn-secondary text-xs !py-1.5 !px-4" @click="showNewModal = false">取消</button>
              <button type="submit" class="btn-primary text-xs !py-1.5 !px-4" :disabled="creating">{{ creating ? '创建中...' : '创建' }}</button>
            </div>
          </form>
        </div>
      </div>
    </Teleport>
  </div>
</template>
