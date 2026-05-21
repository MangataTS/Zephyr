# 轻燕工作台 —— Web前端开发文档

> 版本：v2.0  
> 定位：面向公司系统的轻量化智能协同办公支撑解决方案 · Web管理端  
> 核心载体：桌面智能任务协同办公系统的Web管理端、专项工作组管理、数据大屏、工作成效分析

---

## 一、项目概述

Web前端作为系统的管理中枢与信息汇聚端，承载任务云端管理、专项工作组创建与任务分发、组织人员库维护（含人员岗位/技能标签/能力图谱）、标签模板配置、数据大屏实时展示、智能报告生成等功能。页面以纯白为底、极简为形，使用 `DaisyUI + Tailwind CSS` 构建高质感现代界面。

---

## 二、技术栈

| 层级        | 技术选型                     | 说明                                 |
| ----------- | ---------------------------- | ------------------------------------ |
| 框架        | Vue 3.4+ (Composition API)   | `<script setup lang="ts">` 标准写法  |
| 语言        | TypeScript                   | 全量类型定义                         |
| UI          | DaisyUI 4 + Tailwind CSS 3.4 | 原子化样式 + 组件库                  |
| 状态管理    | Pinia                        | 任务状态、用户权限、协同数据         |
| 路由        | Vue Router 4                 | 懒加载、路由守卫鉴权                 |
| HTTP 客户端 | Axios                        | 统一拦截器、自动注入 Bearer Token    |
| 实时通信    | Socket.io-client             | Web端协同编辑（可选）                |
| 构建工具    | Vite 8                       | 极速 HMR                             |
| 图表        | 内联 SVG                     | 数据大屏趋势图（三次贝塞尔平滑曲线） |
| 测试        | Vitest + Vue Test Utils      | 单元测试                             |

---

## 三、项目目录结构

```
Web-Front/src/
├── App.vue                    # 根组件
├── main.ts                    # 入口：注册 Pinia / Router / 全局错误处理
├── style.css                  # Tailwind 指令 + 全局样式
├── composables/               # 组合式 API（暗色模式、离线检测、WebSocket、Toast 等）
├── directives/
│   └── permission.ts          # v-permission 权限指令
├── pages/
│   ├── LoginPage.vue          # 登录页
│   ├── WorkbenchPage.vue      # ★ 核心工作台（任务墙 + 专项工作组列表 + 一键创建）
│   ├── ArchivePage.vue        # 归档查询
│   ├── CollaborationPage.vue  # 协同编辑室
│   ├── AnalyticsPage.vue      # 工作成效分析（含智能报告+报告模板编辑）
│   ├── ScreenPage.vue         # 数据大屏（平滑趋势图 + 实时动态）
│   ├── GroupDetailPage.vue    # ★ 专项行动详情（成员分组 + 专属任务卡片）
│   ├── ProfilePage.vue        # 个人中心
│   ├── ForbiddenPage.vue      # 403
│   ├── NotFoundPage.vue       # 404
│   └── admin/
│       ├── DepartmentsPage.vue # 部门库管理
│       ├── UsersPage.vue       # 人员库管理
│       ├── TagsPage.vue        # 标签库管理
│       ├── TemplatesPage.vue   # 模板库管理
│       ├── SystemSettingsPage.vue  # 系统设置
│       └── OperationLogPage.vue    # 操作日志
├── components/
│   ├── common/
│   │   ├── TagSelector.vue     # 标签选择器（搜索+创建+多选）
│   │   └── UserPicker.vue      # 人员选择器（部门树+搜索+多选）
│   └── note/
│       └── StickyNoteCard.vue  # 任务卡片（长方体、三色状态、hover动效）
├── router/
│   └── index.ts               # 路由表 + 权限守卫
├── services/
│   ├── api.ts                  # Axios 实例（BaseURL/拦截器）
│   ├── admin.ts                # 认证 + 部门 + 人员 API
│   ├── analytics.ts            # 分析统计 + 报告模板 API
│   ├── groupNotes.ts           # 专项工作组专属任务 API
│   ├── notes.ts                # 任务 CRUD + 盯办/归档 API
│   ├── system.ts               # 系统配置 API
│   ├── tags.ts                 # 标签管理 API
│   └── workgroup.ts            # 专项工作组 CRUD + 搜索 API
├── stores/
│   ├── auth.ts                 # 用户状态、Token、权限
│   ├── collaboration.ts        # 协同状态
│   └── notes.ts                # 任务状态
├── types/
│   ├── api.ts                  # ApiResponse / PaginatedData 泛型
│   ├── collaboration.ts        # 协同类型
│   ├── index.ts                # 统一导出
│   ├── note.ts                 # Note / CreateNotePayload 等
│   ├── system.ts               # 系统配置类型
│   ├── tag.ts                  # Tag 类型
│   ├── user.ts                 # User / UserBrief / Department / TreeNode
│   └── workbench.ts            # Template / Group / SerialNumber
└── utils/
    └── errorHandler.ts         # 全局错误处理
```

---

## 四、页面结构与路由

### 4.1 路由表

| 路由                           | 名称            | 组件               | 权限               | 说明                        |
| ------------------------------ | --------------- | ------------------ | ------------------ | --------------------------- |
| `/login`                       | Login           | LoginPage          | 公开               | 登录页                      |
| `/workbench`                   | Workbench       | WorkbenchPage      | 已登录             | ★ 核心页：任务墙 + 专项行动 |
| `/workbench/archive`           | Archive         | ArchivePage        | 已登录             | 归档查询                    |
| `/workbench/collaboration/:id` | Collaboration   | CollaborationPage  | 已登录             | 协同编辑室                  |
| `/workbench/groups/:id`        | WorkGroupDetail | GroupDetailPage    | 已登录             | ★ 专项行动详情              |
| `/analytics`                   | Analytics       | AnalyticsPage      | 已登录             | 工作成效分析                |
| `/screen/:id`                  | Screen          | ScreenPage         | 已登录             | 数据大屏                    |
| `/admin/departments`           | Departments     | DepartmentsPage    | manage_departments | 部门管理                    |
| `/admin/users`                 | Users           | UsersPage          | manage_users       | 人员管理                    |
| `/admin/tags`                  | Tags            | TagsPage           | manage_tags        | 标签管理                    |
| `/admin/templates`             | Templates       | TemplatesPage      | manage_templates   | 模板管理                    |
| `/admin/system`                | SystemSettings  | SystemSettingsPage | manage_system      | 系统设置                    |
| `/admin/operation-log`         | OperationLog    | OperationLogPage   | manage_system      | 操作日志                    |
| `/profile`                     | Profile         | ProfilePage        | 已登录             | 个人中心                    |
| `/403`                         | Forbidden       | ForbiddenPage      | -                  | 无权访问                    |
| `/:pathMatch(.*)*`             | NotFound        | NotFoundPage       | -                  | 404                         |

### 4.2 路由守卫

- 检查 `localStorage` 中 `auth_token` 是否存在
- `super_admin` 角色跳过权限检查
- 其他角色需检查路由 `meta.permissions` 与用户权限交集

---

## 五、核心页面设计

### 5.1 WorkbenchPage — 工作台（核心入口）

**Tab 栏**：`全部 | 待办 | 指派 | 盯办 | 已完成 | 🏢专项工作组`

**专项工作组 Tab**：

- 搜索栏：关键词（ILIKE）、日期范围（date_from / date_to）组合筛选，Enter 触发搜索
- 专项行动列表：卡片式，展示标题/状态/模板类型/发起人/成员/截止时间，点击进入详情页
- 「一键创建」按钮：弹出模态框 → 填写名称/模板/描述/截止日期 → 多小组设置（每小组独立 UserPicker）→ 一键创建工作组并自动为每位成员创建任务任务
- **「🤖 AI智能成组」按钮**（紫蓝渐变样式）：
  - 点击后展开 AI 交互面板，包含工作要求描述输入区（≥10字符），带示例提示
  - 「开始AI智能分析」按钮触发后端 AI 建议请求，展示加载动画
  - AI 返回后展示分析说明、建议组名/模板类型、多小组卡片（每组含组长/组员标签及推荐理由）
  - 「✅ 采纳此方案」一键将 AI 推荐结果回填至创建表单，自动填充名称/模板/描述/小组配置
  - 支持随时关闭 AI 面板回到手动创建模式

**任务 Tab（全部/待办/指派/盯办/已完成）**：

- 任务墙：`grid-cols-[repeat(auto-fill,minmax(280px,1fr))]` 自适应网格
- 点击任务 → 右侧滑出详情面板（编辑标题/内容/查看标签/完成归档/盯办）
- 悬浮 FAB 按钮 → 弹窗创建任务（标题/二级标题/内容/标签/类型/指派人员）
  - 「指派他人」模式下支持选择工作类型，「一键推荐」基于历史参与数据智能推荐合适人员
- **辅助模块**：为不同的业务场景提供了各自独立的管理页面：
  - **[WorkbenchPage]** 便签 + 任务云端管理，团队任务统筹分发
  - **[GroupDetailPage]** 专项工作组详情页，支持新建任务时从预设组快速加载成员
  - **[UsersPage]** 人员库管理 → 含人员标签（岗位/技能）和「组预设」管理，可创建命名预设组供新建工作组时选用

### 5.2 GroupDetailPage — 专项行动详情

- 顶部信息栏：状态/模板/发起人/成员数/截止时间 → 成员按 `sub_group_name` 分组卡片展示
- **专属任务区**：仅展示 `group_id = 当前工作组` 的任务，以 `StickyNoteCard` 卡片形式呈现在自适应网格中
- 新建任务弹窗：支持输入二级标题（子标签）、选择负责人（从工作组成员中选）、截止日期、标签（TagSelector）
- 点击任务卡片 → 右侧滑出详情面板（编辑/归档/盯办）
- 所有表单使用 `@keydown.enter.prevent` 防止误提交

### 5.3 UsersPage — 人员库管理（增强）

- **人员列表**：展示姓名/用户名/部门/岗位标签/技能标签/角色/工作类型参与统计/状态
- **人员档案弹窗**：点击「档案」查看完整人员档案，包括岗位、技能特长、参与过的工作类型及次数统计
- **编辑表单**：新增「岗位」和「技能特长」字段，支持通过逗号分隔输入多项技能
- **智能推荐集成**：人员标签数据为「指派他人」中的「一键推荐」提供数据支撑
- **组预设管理**：
  - 底部「组预设管理」区域展示所有预设组卡片（名称/成员/适用类型）
  - 「新建预设组」弹窗：输入名称、描述、选择适用工作类型、勾选成员列表
  - 预设组卡片可一键删除
  - 创建的预设组可在 WorkbenchPage 新建专项工作组时直接选用，自动填充成员列表

### 5.4 ScreenPage — 数据大屏

- 顶部时钟 + 日期 + "轻燕工作台 · 数据大屏" + 实时标识 + 「← 返回工作台」按钮
- 四指标卡片：任务总数 / 活跃任务 / 归档任务 / 人员总数
- 趋势图：内联 SVG，三次贝塞尔平滑曲线 + 渐变填充区域 + 数据点 + 坐标轴刻度
- 时间切换：近一周 / 近一月 / 近一季
- 最新动态：垂直时间轴，按操作类型着色

### 5.5 AnalyticsPage — 工作成效分析（★ AI 增强）

- 数据统计 Tab：个人统计面板（创建/完成/完成率/盯办/趋势/标签分布）
- **AI 报告生成按钮组**：顶部操作栏提供「今日日报」「本周周报」「本月月报」三个按钮，点击后调用后端 AI 报告生成接口，展示加载动画，生成成功后自动刷新报告列表
  - 黄色「今日日报」：统计今日工作数据，AI 分析后生成日报
  - 蓝色「本周周报」：统计本周工作数据，AI 分析后生成周报
  - 紫色「本月月报」：统计本月工作数据，AI 分析后生成月报
- 报告历史 Tab：AI 生成的报告列表，标注 `report_type`（`ai` 或 `template`）
- 「📝 编辑模板」按钮：Markdown 模板编辑器，支持变量占位符（如 `{{userName}}`、`{{completionRate}}`），变量高亮显示

### 5.6 TemplatesPage — 模板库管理（★ 用户自定义模板）

- **网格展示**：每个模板卡片展示名称、类型徽章（颜色编码）、字段数量、系统/用户标识
- **「+ 添加模版」按钮**：右上角主操作入口，打开创建/编辑模态框
- **模态表单**：
  - 模板名称（必填）
  - 模板类型下拉选择：通用任务 / 数据分析 / 专项行动 / 紧急协查 / 协同作战 / 自定义
  - 布局样式下拉选择：单栏 / 双栏 / 四宫格 / 六宫格
  - 字段定义 JSON 编辑区（textarea，带示例提示）
- **编辑功能**：所有模板均可编辑，点击「编辑」按钮打开相同模态框回填数据
- **删除保护**：系统内置模板（`is_system: true`）不显示删除按钮；用户自定义模板点击「删除」弹出二次确认
- **任务创建联动**：WorkbenchPage 创建任务时，可选择已有模板自动预填字段占位符到内容区

---

## 六、核心组件设计

### 6.1 StickyNoteCard

- **Props**：`note: Note`、`mode: 'desktop' | 'web'`、`archived: boolean`
- **Events**：`click`、`complete`、`remind`、`restore`
- **状态视觉**：
  - 黄色（默认待办）：`#FEF3C7` 底色
  - 红色（盯办预警）：`#FEE2E2` 底色 + 脉冲动画
  - 绿色（已完成）：`#DCFCE7` 底色
- **内容展开**：超过 100 字显示"展开全文"
- **标签展示**：最多 2 个胶囊，超出显示 `+N`；`tags` 为 `undefined` 时安全降级
- **二级标题**：卡片标题下方显示 `sub_tag` 子标签分类标记，`text-xs text-slate-400` 单行截断

### 6.2 TagSelector

- 下拉浮层，支持多选/搜索/Enter 创建新标签
- 已选标签以彩色胶囊展示
- 所有 `<input>` 禁止 Enter 冒泡触发外层表单提交（`@keydown.enter.prevent`）

### 6.3 UserPicker

- 部门树浏览 + 搜索双模式
- 多选/单选，已选人员头像+姓名胶囊
- 搜索框加 `@keydown.enter.prevent` 防止触发外层表单提交

---

## 七、状态管理（Pinia Stores）

### 7.1 `useNoteStore`

- **State**：`activeNotes`、`selectedNote`、`loading`
- **Actions**：`fetchNotes`、`createNote`、`completeNote`、`remindNote`、`updateNoteLocally`

### 7.2 `useAuthStore`

- **State**：`user`、`token`
- 登录后存储 token 到 localStorage，自动注入请求头

---

## 八、服务层（Services）

| 服务文件        | 主要函数                                                                                                                     | 说明                   |
| --------------- | ---------------------------------------------------------------------------------------------------------------------------- | ---------------------- |
| `api.ts`        | `get / post / put / del`                                                                                                     | Axios 封装，统一拦截器 |
| `admin.ts`      | `login / getDepartments / getUsers / createUser / updateUser / deleteUser / getVisibleUsers / getUserProfile / getUsersWithStats / recommendUsers / getWorkTypeOptions` | 认证+组织+人员+推荐   |
| `notes.ts`      | `fetchNotes / createNote / updateNote / completeNote / remindNote / deleteNote / restoreNote / fetchNoteStats / exportNotes / fetchHeatmap` | 任务 CRUD + 统计 + 热力图 |
| `tags.ts`       | `fetchTags / createTag / updateTag / deleteTag`                                                                              | 标签管理               |
| `templates.ts`  | `fetchTemplates / fetchTemplateById / createTemplate / updateTemplate / deleteTemplate`                                      | ★ 模板管理 CRUD        |
| `workgroup.ts`  | `searchGroups / getMyGroups / getWorkGroupDetail / createWorkGroup / deleteWorkGroup / getWorkGroupMembers / aiSuggestGroups` | 工作组 CRUD + 搜索 + AI智能成组 |
| `groupNotes.ts` | `getGroupNotes / createGroupNote`                                                                                            | 专属任务               |
| `analytics.ts`  | `fetchPersonalStats / generateDailyReport / generateWeeklyReport / generateMonthlyReport / generateAIReport / fetchReports / fetchReportTemplate / saveReportTemplate` | 分析+AI报告生成+报告模板 |
| `system.ts`     | `fetchConfig / updateConfig / fetchOperations`                                                                               | 系统配置+日志          |

---

## 九、设计规范

### 9.1 色彩体系

- **主背景**：`#FFFFFF` / `#F8FAFC`
- **主文字**：`#0F172A`（标题）/ `#475569`（正文）
- **交互蓝**：`#3B82F6`
- **专项紫蓝渐变**：`from-purple-500 to-blue-500`
- **任务三色**：黄 `#FEF3C7` / 绿 `#DCFCE7` / 红 `#FEE2E2`

### 9.2 圆角与阴影

- 卡片：`rounded-xl`（12px），hover 时 `shadow-md`
- 按钮：`rounded-btn`（10px）
- 模态框：`rounded-card`（16px）+ `shadow-modal`

### 9.3 动效

- 全局过渡：`transition-smooth`（all 0.3s cubic-bezier）
- 任务插入：`animate-spring-enter`
- 盯办脉冲：2s infinite pulse

---

_文档结束_
