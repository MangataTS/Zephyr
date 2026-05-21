import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import type { WorkGroupData, AISuggestGroupsResult } from '@/services/workgroup'

const mockUser = {
  id: 'user-me',
  username: 'testuser',
  name: '测试用户',
  avatar: '',
  email: 'test@police.gov.cn',
  phone: '13800000000',
  rank: '',
  dept_id: 'dept-1',
  dept_name: '刑警支队',
  role: 'group_leader' as const,
  permissions: ['create_note_self', 'create_note_assigned', 'edit_others_note', 'delete_note', 'remind', 'view_dept_archive', 'view_group_archive', 'manage_tags', 'access_screen'] as const,
  is_active: true,
}

vi.mock('@/services/admin', () => ({
  getDepartments: vi.fn(() =>
    Promise.resolve({
      data: [
        { id: 'dept-1', name: '刑警支队', parent_id: null, member_count: 3, children: [] },
        { id: 'dept-2', name: '交警支队', parent_id: null, member_count: 2, children: [] },
      ],
    })
  ),
  getUsers: vi.fn(() =>
    Promise.resolve({
      data: {
        data: [
          { id: 'user-1', name: '张三', avatar: '', dept_id: 'dept-1', department: { id: 'dept-1', name: '刑警支队' }, role: 'group_leader' },
          { id: 'user-2', name: '李四', avatar: '', dept_id: 'dept-1', department: { id: 'dept-1', name: '刑警支队' }, role: 'user' },
          { id: 'user-3', name: '王五', avatar: '', dept_id: 'dept-2', department: { id: 'dept-2', name: '交警支队' }, role: 'user' },
        ],
        total: 3,
        page: 1,
        page_size: 100,
      },
    })
  ),
}))

const mockWorkGroup: WorkGroupData = {
  id: 'wg-test-1',
  name: '雷霆2026专项行动',
  description: '测试描述',
  initiator_id: 'user-me',
  initiator: { id: 'user-me', name: '测试用户', username: 'testuser' },
  template_type: 'default',
  status: 'active',
  due_time: undefined,
  members: [],
  created_at: new Date().toISOString(),
  updated_at: new Date().toISOString(),
}

const mockCreateWorkGroup = vi.fn(() =>
  Promise.resolve({ data: mockWorkGroup })
)

const mockSearchGroups = vi.fn(() =>
  Promise.resolve({ data: { data: [], total: 0 } })
)

const mockDeleteWorkGroup = vi.fn(() =>
  Promise.resolve({ data: null })
)

const mockAiResult: AISuggestGroupsResult = {
  analysis: '该专项工作需要数据分析能力和现场执行能力，建议分为数据研判组和行动执行组两个小组。',
  suggested_name: '雷霆2026专项行动',
  suggested_template: 'special_project',
  sub_groups: [
    {
      name: '数据研判组',
      responsibility: '负责涉案数据分析与情报研判',
      members: [
        { user_id: 'user-1', name: '张三', role: 'leader', reason: '数据分析经验丰富' },
        { user_id: 'user-2', name: '李四', role: 'member', reason: '情报研判能力强' },
      ],
    },
    {
      name: '行动执行组',
      responsibility: '负责现场核查与抓捕执行',
      members: [
        { user_id: 'user-3', name: '王五', role: 'leader', reason: '现场指挥经验丰富' },
      ],
    },
  ],
  source: 'ai',
}

const mockAiSuggestGroups = vi.fn(() =>
  Promise.resolve({ data: mockAiResult })
)

const mockFetchNotes = vi.fn(() => Promise.resolve())

vi.mock('@/services/workgroup', () => ({
  createWorkGroup: (payload: unknown) => mockCreateWorkGroup(payload),
  searchGroups: (query: unknown) => mockSearchGroups(query),
  deleteWorkGroup: (id: string) => mockDeleteWorkGroup(id),
  aiSuggestGroups: (description: string, groupName?: string) => mockAiSuggestGroups(description, groupName),
}))

vi.mock('@/services/groupNotes', () => ({
  getGroupNotes: vi.fn(() => Promise.resolve({ data: [] })),
  getGroupDashboard: vi.fn(() => Promise.resolve({ data: {} })),
}))

vi.mock('@/stores/notes', () => {
  const actual = vi.importActual('@/stores/notes')
  return {
    useNoteStore: () => ({
      activeNotes: { value: [] },
      loading: { value: false },
      error: { value: '' },
      fetchNotes: mockFetchNotes,
      createNote: vi.fn(() => Promise.resolve({ id: 'note-1' })),
      updateNoteLocally: vi.fn(),
      completeNote: vi.fn(),
      remindNote: vi.fn(),
    }),
  }
})

describe('专项工作组创建流程 - 集成测试', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.setItem('auth_token', 'test-token')
    localStorage.setItem('auth_user', JSON.stringify(mockUser))
    mockCreateWorkGroup.mockClear()
    mockSearchGroups.mockClear()
  })

  describe('表单验证', () => {
    it('工作组名称为空时应显示错误提示', async () => {
      const { default: WorkbenchPage } = await import('@/pages/WorkbenchPage.vue')
      const wrapper = mount(WorkbenchPage, {
        global: {
          stubs: {
            'router-link': { template: '<a><slot /></a>' },
            StickyNoteCard: { template: '<div class="note-card" />' },
          },
        },
      })

      wrapper.vm.showWorkGroupModal = true
      await wrapper.vm.$nextTick()

      wrapper.vm.wgName = ''
      await wrapper.vm.handleCreateWorkGroup()
      await wrapper.vm.$nextTick()

      expect(wrapper.vm.wgError).toBe('请输入工作组名称')
      expect(mockCreateWorkGroup).not.toHaveBeenCalled()
    })

    it('未选择成员时应显示错误提示', async () => {
      const { default: WorkbenchPage } = await import('@/pages/WorkbenchPage.vue')
      const wrapper = mount(WorkbenchPage, {
        global: {
          stubs: {
            'router-link': { template: '<a><slot /></a>' },
            StickyNoteCard: { template: '<div class="note-card" />' },
          },
        },
      })

      wrapper.vm.showWorkGroupModal = true
      await wrapper.vm.$nextTick()

      wrapper.vm.wgName = '测试工作组'
      wrapper.vm.selectedWGUserIds = [[]]
      await wrapper.vm.handleCreateWorkGroup()
      await wrapper.vm.$nextTick()

      expect(wrapper.vm.wgError).toBe('请至少选择一个成员')
      expect(mockCreateWorkGroup).not.toHaveBeenCalled()
    })

    it('填写完整信息后应成功提交', async () => {
      const { default: WorkbenchPage } = await import('@/pages/WorkbenchPage.vue')
      const wrapper = mount(WorkbenchPage, {
        global: {
          stubs: {
            'router-link': { template: '<a><slot /></a>' },
            StickyNoteCard: { template: '<div class="note-card" />' },
          },
        },
      })

      wrapper.vm.showWorkGroupModal = true
      await wrapper.vm.$nextTick()

      wrapper.vm.wgName = '雷霆2026专项行动'
      wrapper.vm.wgDescription = '专项行动测试'
      wrapper.vm.wgTemplate = 'default'
      wrapper.vm.wgDueDate = '2026-12-31'
      wrapper.vm.selectedWGUserIds = [['user-1', 'user-2']]
      wrapper.vm.wgSubGroups = [
        { name: '组长组', members: [] },
      ]
      wrapper.vm.onWGUserSelect(0, ['user-1', 'user-2'])

      await wrapper.vm.handleCreateWorkGroup()
      await wrapper.vm.$nextTick()

      expect(wrapper.vm.wgError).toBe('')
      expect(mockCreateWorkGroup).toHaveBeenCalledTimes(1)
      expect(mockCreateWorkGroup).toHaveBeenCalledWith(
        expect.objectContaining({
          name: '雷霆2026专项行动',
          description: '专项行动测试',
          template_type: 'default',
          due_time: expect.stringContaining('2026-12-31'),
          members: expect.arrayContaining([
            expect.objectContaining({ user_id: 'user-1', role: 'leader' }),
            expect.objectContaining({ user_id: 'user-2', role: 'leader' }),
          ]),
        })
      )
    })
  })

  describe('双重提交防护', () => {
    it('wgCreating=true 时应阻止重复提交', async () => {
      const { default: WorkbenchPage } = await import('@/pages/WorkbenchPage.vue')
      const wrapper = mount(WorkbenchPage, {
        global: {
          stubs: {
            'router-link': { template: '<a><slot /></a>' },
            StickyNoteCard: { template: '<div class="note-card" />' },
          },
        },
      })

      wrapper.vm.showWorkGroupModal = true
      wrapper.vm.wgName = '测试工作组'
      wrapper.vm.wgCreating = true
      await wrapper.vm.$nextTick()

      await wrapper.vm.handleCreateWorkGroup()
      await wrapper.vm.$nextTick()

      expect(mockCreateWorkGroup).not.toHaveBeenCalled()
    })
  })

  describe('打开/关闭模态框', () => {
    it('openWGModal 应重置表单状态', async () => {
      const { default: WorkbenchPage } = await import('@/pages/WorkbenchPage.vue')
      const wrapper = mount(WorkbenchPage, {
        global: {
          stubs: {
            'router-link': { template: '<a><slot /></a>' },
            StickyNoteCard: { template: '<div class="note-card" />' },
          },
        },
      })

      wrapper.vm.wgName = '旧名称'
      wrapper.vm.wgError = '旧错误'
      wrapper.vm.selectedWGUserIds = [['user-1']]

      wrapper.vm.openWGModal()
      await wrapper.vm.$nextTick()

      expect(wrapper.vm.wgName).toBe('')
      expect(wrapper.vm.wgError).toBe('')
      expect(wrapper.vm.selectedWGUserIds).toEqual([[]])
    })
  })

  describe('人员选择不回传提交', () => {
    it('onWGUserSelect 仅更新成员数据,不应调用 API', async () => {
      const { default: WorkbenchPage } = await import('@/pages/WorkbenchPage.vue')
      const wrapper = mount(WorkbenchPage, {
        global: {
          stubs: {
            'router-link': { template: '<a><slot /></a>' },
            StickyNoteCard: { template: '<div class="note-card" />' },
          },
        },
      })

      wrapper.vm.showWorkGroupModal = true
      await wrapper.vm.$nextTick()

      wrapper.vm.onWGUserSelect(0, ['user-1'])
      await wrapper.vm.$nextTick()

      expect(mockCreateWorkGroup).not.toHaveBeenCalled()
      expect(wrapper.vm.selectedWGUserIds[0]).toEqual(['user-1'])
    })

    it('添加/删除小组不应触发提交', async () => {
      const { default: WorkbenchPage } = await import('@/pages/WorkbenchPage.vue')
      const wrapper = mount(WorkbenchPage, {
        global: {
          stubs: {
            'router-link': { template: '<a><slot /></a>' },
            StickyNoteCard: { template: '<div class="note-card" />' },
          },
        },
      })

      wrapper.vm.showWorkGroupModal = true
      await wrapper.vm.$nextTick()

      wrapper.vm.addSubGroup()
      await wrapper.vm.$nextTick()
      expect(wrapper.vm.wgSubGroups.length).toBe(2)

      wrapper.vm.removeSubGroup(1)
      await wrapper.vm.$nextTick()
      expect(wrapper.vm.wgSubGroups.length).toBe(1)

      expect(mockCreateWorkGroup).not.toHaveBeenCalled()
    })
  })

  describe('成员角色分配', () => {
    it('第一组(组长组)成员角色应为 leader', async () => {
      const { default: WorkbenchPage } = await import('@/pages/WorkbenchPage.vue')
      const wrapper = mount(WorkbenchPage, {
        global: {
          stubs: {
            'router-link': { template: '<a><slot /></a>' },
            StickyNoteCard: { template: '<div class="note-card" />' },
          },
        },
      })

      wrapper.vm.showWorkGroupModal = true
      wrapper.vm.wgName = '测试工作组'
      await wrapper.vm.$nextTick()

      wrapper.vm.addSubGroup()
      wrapper.vm.onWGUserSelect(0, ['user-1'])
      wrapper.vm.onWGUserSelect(1, ['user-2'])
      await wrapper.vm.$nextTick()

      await wrapper.vm.handleCreateWorkGroup()
      await wrapper.vm.$nextTick()

      expect(mockCreateWorkGroup).toHaveBeenCalledWith(
        expect.objectContaining({
          members: expect.arrayContaining([
            expect.objectContaining({ user_id: 'user-1', role: 'leader' }),
            expect.objectContaining({ user_id: 'user-2', role: 'member' }),
          ]),
        })
      )
    })
  })

  describe('AI智能成组', () => {
    beforeEach(() => {
      mockAiSuggestGroups.mockClear()
    })

    it('openAIMode 应激活AI模式并保留已有描述', async () => {
      const { default: WorkbenchPage } = await import('@/pages/WorkbenchPage.vue')
      const wrapper = mount(WorkbenchPage, {
        global: {
          stubs: {
            'router-link': { template: '<a><slot /></a>' },
            StickyNoteCard: { template: '<div class="note-card" />' },
          },
        },
      })

      wrapper.vm.showWorkGroupModal = true
      wrapper.vm.wgDescription = '现有工作描述'
      await wrapper.vm.$nextTick()

      wrapper.vm.openAIMode()
      await wrapper.vm.$nextTick()

      expect(wrapper.vm.aiMode).toBe(true)
      expect(wrapper.vm.aiDescription).toBe('现有工作描述')
      expect(wrapper.vm.aiResult).toBeNull()
      expect(wrapper.vm.aiError).toBe('')
    })

    it('closeAIMode 应退出AI模式并清空结果', async () => {
      const { default: WorkbenchPage } = await import('@/pages/WorkbenchPage.vue')
      const wrapper = mount(WorkbenchPage, {
        global: {
          stubs: {
            'router-link': { template: '<a><slot /></a>' },
            StickyNoteCard: { template: '<div class="note-card" />' },
          },
        },
      })

      wrapper.vm.showWorkGroupModal = true
      wrapper.vm.openAIMode()
      wrapper.vm.aiResult = mockAiResult as AISuggestGroupsResult
      await wrapper.vm.$nextTick()

      wrapper.vm.closeAIMode()
      await wrapper.vm.$nextTick()

      expect(wrapper.vm.aiMode).toBe(false)
      expect(wrapper.vm.aiResult).toBeNull()
      expect(wrapper.vm.aiError).toBe('')
    })

    it('handleAISuggest 描述不足10字应显示错误', async () => {
      const { default: WorkbenchPage } = await import('@/pages/WorkbenchPage.vue')
      const wrapper = mount(WorkbenchPage, {
        global: {
          stubs: {
            'router-link': { template: '<a><slot /></a>' },
            StickyNoteCard: { template: '<div class="note-card" />' },
          },
        },
      })

      wrapper.vm.showWorkGroupModal = true
      wrapper.vm.aiMode = true
      wrapper.vm.aiDescription = '短描述'
      await wrapper.vm.$nextTick()

      await wrapper.vm.handleAISuggest()
      await wrapper.vm.$nextTick()

      expect(wrapper.vm.aiError).toBe('请至少输入10个字的工作要求描述')
      expect(mockAiSuggestGroups).not.toHaveBeenCalled()
    })

    it('handleAISuggest 应成功调用并填充aiResult', async () => {
      const { default: WorkbenchPage } = await import('@/pages/WorkbenchPage.vue')
      const wrapper = mount(WorkbenchPage, {
        global: {
          stubs: {
            'router-link': { template: '<a><slot /></a>' },
            StickyNoteCard: { template: '<div class="note-card" />' },
          },
        },
      })

      wrapper.vm.showWorkGroupModal = true
      wrapper.vm.aiMode = true
      wrapper.vm.aiDescription = '针对近期辖区内电信网络诈骗案件高发态势，需成立专项研判打击工作组'
      await wrapper.vm.$nextTick()

      await wrapper.vm.handleAISuggest()
      await wrapper.vm.$nextTick()

      expect(mockAiSuggestGroups).toHaveBeenCalledTimes(1)
      expect(mockAiSuggestGroups).toHaveBeenCalledWith(
        expect.stringContaining('电信网络诈骗'),
        ''
      )
      expect(wrapper.vm.aiResult).not.toBeNull()
      expect(wrapper.vm.aiError).toBe('')
      expect(wrapper.vm.aiSuggesting).toBe(false)
    })

    it('applyAISuggestion 应将AI结果填充到表单', async () => {
      const { default: WorkbenchPage } = await import('@/pages/WorkbenchPage.vue')
      const wrapper = mount(WorkbenchPage, {
        global: {
          stubs: {
            'router-link': { template: '<a><slot /></a>' },
            StickyNoteCard: { template: '<div class="note-card" />' },
          },
        },
      })

      wrapper.vm.showWorkGroupModal = true
      wrapper.vm.aiResult = mockAiResult as AISuggestGroupsResult
      wrapper.vm.aiDescription = '针对近期辖区内电信网络诈骗案件高发态势'
      await wrapper.vm.$nextTick()

      wrapper.vm.applyAISuggestion()
      await wrapper.vm.$nextTick()

      expect(wrapper.vm.wgName).toBe('雷霆2026专项行动')
      expect(wrapper.vm.wgTemplate).toBe('special_project')
      expect(wrapper.vm.wgDescription).toBe('针对近期辖区内电信网络诈骗案件高发态势')
      expect(wrapper.vm.wgSubGroups.length).toBe(2)
      expect(wrapper.vm.wgSubGroups[0].name).toBe('数据研判组')
      expect(wrapper.vm.wgSubGroups[1].name).toBe('行动执行组')
      expect(wrapper.vm.aiMode).toBe(false)
      expect(wrapper.vm.aiResult).toBeNull()
    })

    it('openWGModal 应重置AI状态', async () => {
      const { default: WorkbenchPage } = await import('@/pages/WorkbenchPage.vue')
      const wrapper = mount(WorkbenchPage, {
        global: {
          stubs: {
            'router-link': { template: '<a><slot /></a>' },
            StickyNoteCard: { template: '<div class="note-card" />' },
          },
        },
      })

      wrapper.vm.aiMode = true
      wrapper.vm.aiDescription = '旧描述'
      wrapper.vm.aiResult = mockAiResult as AISuggestGroupsResult
      wrapper.vm.aiError = '旧错误'

      wrapper.vm.openWGModal()
      await wrapper.vm.$nextTick()

      expect(wrapper.vm.aiMode).toBe(false)
      expect(wrapper.vm.aiDescription).toBe('')
      expect(wrapper.vm.aiResult).toBeNull()
      expect(wrapper.vm.aiError).toBe('')
    })
  })
})
