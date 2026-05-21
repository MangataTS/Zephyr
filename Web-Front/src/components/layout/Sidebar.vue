<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useAuthStore } from '@/stores/auth';

const auth = useAuthStore();
const route = useRoute();
const router = useRouter();

const collapsed = ref(localStorage.getItem('sidebar_collapsed') === 'true');

watch(collapsed, (val) => {
  localStorage.setItem('sidebar_collapsed', String(val));
});

interface MenuItem {
  icon: string;
  label: string;
  path: string;
  permission?: string;
  adminOnly?: boolean;
}

const menuItems: MenuItem[] = [
  { icon: 'clipboard', label: '工作台', path: '/workbench' },
  { icon: 'archive', label: '归档查询', path: '/workbench/archive' },
  { icon: 'chart', label: '工作成效分析', path: '/analytics' },
  {
    icon: 'users',
    label: '部门管理',
    path: '/admin/departments',
    permission: 'manage_departments',
  },
  { icon: 'user', label: '人员管理', path: '/admin/users', permission: 'manage_users' },
  { icon: 'tag', label: '标签管理', path: '/admin/tags', permission: 'manage_tags' },
  { icon: 'template', label: '模板管理', path: '/admin/templates', permission: 'manage_templates' },
  { icon: 'monitor', label: '数据大屏', path: '/screen/default', permission: 'access_screen' },
  {
    icon: 'settings',
    label: '系统管理',
    path: '/admin/system',
    permission: 'manage_system',
    adminOnly: true,
  },
  {
    icon: 'list',
    label: '操作日志',
    path: '/admin/operation-log',
    permission: 'manage_system',
    adminOnly: true,
  },
];

const bottomItems: MenuItem[] = [{ icon: 'settings', label: '个人中心', path: '/profile' }];

const visibleMenuItems = computed(() =>
  menuItems.filter((item) => {
    if (item.adminOnly && !auth.isAdmin) return false;
    if (item.adminOnly && auth.isAdmin) return true;
    if (!item.permission) return true;
    return auth.permissions.includes(item.permission as never);
  })
);

function isActive(path: string): boolean {
  return route.path === path || route.path.startsWith(path + '/');
}

function navigate(path: string) {
  router.push(path);
}
</script>

<template>
  <aside
    :class="[
      'bg-white dark:bg-slate-900 border-r border-slate-200 dark:border-slate-700 flex flex-col transition-colors duration-300 overflow-hidden',
      collapsed ? 'w-16' : 'w-60',
    ]"
  >
    <!-- 顶部 Logo -->
    <div
      class="h-14 flex items-center border-b border-slate-200 dark:border-slate-700 px-4 shrink-0 transition-colors duration-300"
    >
      <button
        class="p-1.5 rounded-lg hover:bg-slate-100 dark:hover:bg-slate-800 transition-smooth shrink-0"
        @click="collapsed = !collapsed"
      >
        <svg
          class="w-5 h-5 text-slate-500 dark:text-slate-400"
          fill="none"
          viewBox="0 0 24 24"
          stroke="currentColor"
        >
          <path
            stroke-linecap="round"
            stroke-linejoin="round"
            stroke-width="2"
            d="M4 6h16M4 12h16M4 18h16"
          />
        </svg>
      </button>
      <transition name="fade">
        <div v-if="!collapsed" class="ml-3 flex items-center gap-2">
          <img
            src="/logo.jpg"
            alt="轻燕"
            class="w-7 h-7 rounded-lg object-cover shrink-0"
          />
          <span class="text-sm font-semibold text-slate-900 dark:text-slate-100 whitespace-nowrap truncate">轻燕</span>
        </div>
      </transition>
      <transition name="fade">
        <img
          v-if="collapsed"
          src="/logo.jpg"
          alt="轻燕"
          class="w-7 h-7 rounded-lg object-cover shrink-0"
        />
      </transition>
    </div>

    <!-- 菜单项 -->
    <nav class="flex-1 overflow-y-auto scrollbar-thin py-2">
      <ul class="space-y-0.5 px-2">
        <li v-for="item in visibleMenuItems" :key="item.path">
          <button
            :class="[
              'w-full flex items-center gap-3 px-3 py-2.5 rounded-btn text-sm transition-smooth',
              isActive(item.path)
                ? 'bg-blue-50 dark:bg-blue-900/40 text-blue-600 dark:text-blue-400 font-medium border-l-[3px] border-blue-500 dark:border-blue-400'
                : 'text-slate-600 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 border-l-[3px] border-transparent',
            ]"
            :title="collapsed ? item.label : ''"
            @click="navigate(item.path)"
          >
            <!-- 图标占位 -->
            <span class="w-5 h-5 shrink-0 flex items-center justify-center text-lg leading-none">
              <template v-if="item.icon === 'clipboard'">📋</template>
              <template v-else-if="item.icon === 'archive'">📁</template>
              <template v-else-if="item.icon === 'chart'">📈</template>
              <template v-else-if="item.icon === 'users'">🏢</template>
              <template v-else-if="item.icon === 'user'">👤</template>
              <template v-else-if="item.icon === 'tag'">🏷️</template>
              <template v-else-if="item.icon === 'template'">📄</template>
              <template v-else-if="item.icon === 'monitor'">📊</template>
              <template v-else-if="item.icon === 'settings'">⚙️</template>
              <template v-else-if="item.icon === 'list'">📋</template>
              <template v-else>📌</template>
            </span>
            <span v-if="!collapsed" class="truncate">{{ item.label }}</span>
          </button>
        </li>
      </ul>
    </nav>

    <!-- 底部区域 -->
    <div
      class="border-t border-slate-200 dark:border-slate-700 p-2 shrink-0 transition-colors duration-300"
    >
      <ul class="space-y-0.5">
        <li v-for="item in bottomItems" :key="item.path">
          <button
            :class="[
              'w-full flex items-center gap-3 px-3 py-2.5 rounded-btn text-sm transition-smooth',
              isActive(item.path)
                ? 'bg-blue-50 dark:bg-blue-900/40 text-blue-600 dark:text-blue-400 font-medium border-l-[3px] border-blue-500 dark:border-blue-400'
                : 'text-slate-600 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 border-l-[3px] border-transparent',
            ]"
            :title="collapsed ? item.label : ''"
            @click="navigate(item.path)"
          >
            <span class="w-5 h-5 shrink-0 flex items-center justify-center text-lg leading-none"
              >⚙️</span
            >
            <span v-if="!collapsed" class="truncate">{{ item.label }}</span>
          </button>
        </li>
      </ul>

      <!-- 用户信息 -->
      <div v-if="auth.user" class="flex items-center gap-3 px-3 py-2.5 mt-1">
        <div
          class="w-8 h-8 rounded-full bg-blue-500 flex items-center justify-center text-white text-xs font-medium shrink-0"
        >
          {{ auth.user.name.charAt(0) }}
        </div>
        <div v-if="!collapsed" class="flex-1 min-w-0">
          <div class="text-xs font-medium text-slate-900 dark:text-slate-100 truncate">
            {{ auth.user.name }}
          </div>
          <div class="text-[10px] text-slate-400 dark:text-slate-500 truncate">
            {{ auth.user.dept_name }}
          </div>
        </div>
        <button
          v-if="!collapsed"
          class="shrink-0 p-1 rounded hover:bg-slate-100 dark:hover:bg-slate-800 transition-smooth"
          title="退出登录"
          @click="
            auth.logout();
            router.push('/login');
          "
        >
          <svg
            class="w-4 h-4 text-slate-400 dark:text-slate-500"
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
          >
            <path
              stroke-linecap="round"
              stroke-linejoin="round"
              stroke-width="2"
              d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1"
            />
          </svg>
        </button>
      </div>
    </div>
  </aside>
</template>
