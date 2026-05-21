<script setup lang="ts">
import { ref } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import { useAuthStore } from '@/stores/auth';

const auth = useAuthStore();
const router = useRouter();
const route = useRoute();

const username = ref('');
const password = ref('');
const loading = ref(false);
const errorMsg = ref('');
const showPassword = ref(false);

async function handleLogin() {
  errorMsg.value = '';
  if (!username.value.trim()) {
    errorMsg.value = '请输入用户名';
    return;
  }
  if (!password.value.trim()) {
    errorMsg.value = '请输入密码';
    return;
  }

  loading.value = true;
  try {
    await auth.login({ username: username.value, password: password.value });
    const redirect = (route.query.redirect as string) || '/workbench';
    router.push(redirect);
  } catch (e: unknown) {
    const err = e as Error & { friendlyMessage?: string };
    errorMsg.value = err.friendlyMessage || err.message || '用户名或密码错误，请重试';
  } finally {
    loading.value = false;
  }
}
</script>

<template>
  <div class="min-h-screen bg-white flex">
    <div class="hidden lg:flex lg:w-[480px] bg-slate-50 flex-col items-center justify-center p-12">
      <img
        src="/logo.jpg"
        alt="轻燕工作台"
        class="w-24 h-24 rounded-2xl object-cover mb-8 shadow-lg"
      />
      <h1 class="text-2xl font-semibold text-slate-900 mb-2" style="letter-spacing: -0.02em">
        轻燕工作台
      </h1>
      <p class="text-slate-500 text-sm">轻量化情指行一体化支撑解决方案</p>
      <div class="mt-12 text-center">
        <div class="inline-flex items-center gap-2 px-4 py-2 bg-blue-50 rounded-lg">
          <svg class="w-4 h-4 text-blue-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path
              stroke-linecap="round"
              stroke-linejoin="round"
              stroke-width="2"
              d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z"
            />
          </svg>
          <span class="text-xs text-blue-600 font-medium">公司内部系统 · 安全连接</span>
        </div>
      </div>
    </div>

    <div class="flex-1 flex items-center justify-center p-8">
      <div class="w-full max-w-[400px]">
        <div class="lg:hidden mb-10 text-center">
          <img
            src="/logo.jpg"
            alt="轻燕工作台"
            class="w-16 h-16 rounded-xl object-cover mx-auto mb-4 shadow-lg"
          />
          <h1 class="text-xl font-semibold text-slate-900">轻燕工作台</h1>
        </div>

        <h2 class="text-2xl font-semibold text-slate-900 mb-2">欢迎登录</h2>
        <p class="text-slate-500 text-sm mb-8">请使用您的公司内部账号登录系统</p>

        <div class="flex bg-slate-100 rounded-btn p-1 mb-8">
          <button
            class="flex-1 py-2 text-sm font-medium rounded-md bg-white text-slate-900 shadow-sm transition-smooth"
          >
            账号登录
          </button>
          <button
            class="flex-1 py-2 text-sm font-medium rounded-md text-slate-500 transition-smooth"
            disabled
          >
            扫码登录（暂未开放）
          </button>
        </div>

        <form @submit.prevent="handleLogin" class="space-y-5">
          <div>
            <label class="block text-sm font-medium text-slate-700 mb-1.5">用户名</label>
            <input
              v-model="username"
              type="text"
              class="input-field"
              placeholder="请输入用户名"
              autocomplete="username"
              :disabled="loading"
            />
          </div>

          <div>
            <label class="block text-sm font-medium text-slate-700 mb-1.5">密码</label>
            <div class="relative">
              <input
                v-model="password"
                :type="showPassword ? 'text' : 'password'"
                class="input-field pr-10"
                placeholder="请输入密码"
                autocomplete="current-password"
                :disabled="loading"
              />
              <button
                type="button"
                class="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600"
                @click="showPassword = !showPassword"
              >
                <svg
                  v-if="!showPassword"
                  class="w-5 h-5"
                  fill="none"
                  viewBox="0 0 24 24"
                  stroke="currentColor"
                >
                  <path
                    stroke-linecap="round"
                    stroke-linejoin="round"
                    stroke-width="2"
                    d="M15 12a3 3 0 11-6 0 3 3 0 016 0z"
                  />
                  <path
                    stroke-linecap="round"
                    stroke-linejoin="round"
                    stroke-width="2"
                    d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z"
                  />
                </svg>
                <svg v-else class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path
                    stroke-linecap="round"
                    stroke-linejoin="round"
                    stroke-width="2"
                    d="M13.875 18.825A10.05 10.05 0 0112 19c-4.478 0-8.268-2.943-9.543-7a9.97 9.97 0 011.563-3.029m5.858.908a3 3 0 114.243 4.243M15 12a3 3 0 01-3 3m0 0a9.97 9.97 0 01-2.636-.374"
                  />
                  <path
                    stroke-linecap="round"
                    stroke-linejoin="round"
                    stroke-width="2"
                    d="M3 3l18 18"
                  />
                </svg>
              </button>
            </div>
          </div>

          <div v-if="errorMsg" class="text-sm text-red-500 bg-red-50 px-3 py-2 rounded-btn">
            {{ errorMsg }}
          </div>

          <button
            type="submit"
            :disabled="loading"
            class="w-full py-3 bg-[#3B82F6] text-white font-medium rounded-btn transition-smooth hover:bg-blue-600 active:scale-[0.98] disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2"
          >
            <svg v-if="loading" class="animate-spin w-4 h-4" fill="none" viewBox="0 0 24 24">
              <circle
                class="opacity-25"
                cx="12"
                cy="12"
                r="10"
                stroke="currentColor"
                stroke-width="4"
              />
              <path
                class="opacity-75"
                fill="currentColor"
                d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
              />
            </svg>
            {{ loading ? '登录中...' : '登录' }}
          </button>
        </form>

        <div class="mt-6 text-center">
          <span class="text-xs text-slate-400">忘记密码请联系系统管理员</span>
        </div>
      </div>
    </div>
  </div>
</template>
