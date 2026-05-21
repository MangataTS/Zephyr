<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { fetchTags, createTag } from '@/services/tags'
import type { Tag } from '@/types'

const tags = ref<Tag[]>([])
const loading = ref(false)
const loadError = ref('')
const showNewModal = ref(false)
const newTagName = ref('')
const newTagSubTag = ref('')
const newTagColor = ref('#3B82F6')
const creating = ref(false)
const createError = ref('')

const colorOptions = ['#EF4444', '#F97316', '#EAB308', '#22C55E', '#14B8A6', '#3B82F6', '#8B5CF6', '#EC4899', '#78716C', '#64748B', '#94A3B8', '#475569']

async function loadTags() {
  loading.value = true
  loadError.value = ''
  try {
    const res = await fetchTags()
    const data = res.data as unknown as Tag[]
    tags.value = Array.isArray(data) ? data : []
  } catch {
    loadError.value = '加载标签失败'
  } finally {
    loading.value = false
  }
}

onMounted(loadTags)

async function addTag() {
  if (!newTagName.value.trim()) return
  creating.value = true
  createError.value = ''
  try {
    await createTag({
      name: newTagName.value.trim(),
      sub_tag: newTagSubTag.value.trim() || undefined,
      color: newTagColor.value,
      scope: 'system',
    })
    showNewModal.value = false
    await loadTags()
  } catch (e: unknown) {
    const err = e as { friendlyMessage?: string }
    createError.value = err?.friendlyMessage || '创建标签失败，请重试'
  } finally {
    creating.value = false
  }
}
</script>

<template>
  <div>
    <div class="flex items-center justify-between mb-6">
      <h2 class="text-lg font-semibold text-slate-900">标签库管理</h2>
      <button class="btn-primary text-sm" :disabled="creating" @click="showNewModal = true">{{ creating ? '创建中...' : '新建标签' }}</button>
    </div>

    <div v-if="loading" class="grid grid-cols-4 gap-4">
      <div v-for="n in 8" :key="n" class="skeleton h-16 rounded-card" />
    </div>

    <div v-else-if="loadError" class="text-center py-8 text-sm text-red-400">{{ loadError }}</div>

    <div v-else class="grid grid-cols-4 gap-4">
      <div
        v-for="tag in tags"
        :key="tag.id"
        class="bg-white dark:bg-slate-800 rounded-card border border-slate-100 dark:border-slate-700 p-4 flex items-center gap-3 hover:shadow-note transition-smooth"
      >
        <span class="w-4 h-4 rounded-full shrink-0" :style="{ backgroundColor: tag.color }" />
        <div class="flex-1 min-w-0">
          <div class="text-sm font-medium text-slate-900 truncate">
            {{ tag.name }}
            <span v-if="tag.sub_tag" class="text-xs text-slate-400 ml-1">› {{ tag.sub_tag }}</span>
          </div>
          <div class="text-xs text-slate-400">{{ tag.category }} · {{ tag.scope === 'system' ? '系统' : '个人' }}</div>
        </div>
        <span :class="['text-xs shrink-0 font-medium', tag.usage_count > 5 ? 'text-blue-600' : 'text-slate-400']">
          {{ tag.usage_count }}次
        </span>
      </div>
    </div>

    <!-- 新建标签模态框 -->
    <Teleport to="body">
      <div v-if="showNewModal" class="fixed inset-0 z-50 flex items-center justify-center">
        <div class="overlay-backdrop" @click="showNewModal = false" />
        <div class="relative z-50 bg-white dark:bg-slate-800 rounded-card shadow-modal w-full max-w-sm mx-4 p-6 animate-fade-in">
          <h3 class="text-base font-semibold text-slate-900 mb-4">新建标签</h3>
          <form @submit.prevent="addTag" class="space-y-4">
            <input v-model="newTagName" name="tag_name" class="input-field" placeholder="一级标签名称" autofocus />
            <input v-model="newTagSubTag" name="tag_sub_tag" class="input-field" placeholder="二级标签（可选）" />
            <div>
              <span class="text-xs text-slate-500 mb-2 block">颜色</span>
              <div class="flex flex-wrap gap-2">
                <button
                  v-for="c in colorOptions"
                  :key="c"
                  type="button"
                  class="w-7 h-7 rounded-full transition-smooth"
                  :class="newTagColor === c ? 'ring-2 ring-offset-2 ring-blue-400 scale-110' : 'hover:scale-105'"
                  :style="{ backgroundColor: c }"
                  @click="newTagColor = c"
                />
              </div>
            </div>
            <p v-if="createError" class="text-xs text-red-500 mb-2">{{ createError }}</p>
            <div class="flex justify-end gap-3 pt-2">
              <button type="button" class="btn-secondary text-xs !py-1.5 !px-4" :disabled="creating" @click="showNewModal = false">取消</button>
              <button type="submit" class="btn-primary text-xs !py-1.5 !px-4" :disabled="creating">{{ creating ? '创建中...' : '创建' }}</button>
            </div>
          </form>
        </div>
      </div>
    </Teleport>
  </div>
</template>
