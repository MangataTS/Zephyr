import axios from 'axios';
import type { AxiosRequestConfig } from 'axios';
import type { ApiResponse } from '@/types';

const BASE_URL = (import.meta.env.VITE_API_BASE_URL || '').replace(/\/$/, '');

const api = axios.create({
  baseURL: BASE_URL,
  timeout: 15000,
  headers: {
    'Content-Type': 'application/json',
  },
});

api.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('auth_token');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

api.interceptors.response.use(
  (response) => {
    return response;
  },
  (error) => {
    const { response } = error;
    const status = response?.status;

    if (status === 401) {
      localStorage.removeItem('auth_token');
      localStorage.removeItem('auth_user');
      if (window.location.pathname !== '/login') {
        window.location.href = '/login';
      }
    }

    let errorMsg = '网络请求异常，请检查网络连接';
    if (response?.data?.message) {
      errorMsg = response.data.message;
    } else if (status) {
      const statusMessages: Record<number, string> = {
        400: '请求参数有误，请检查输入内容',
        401: '登录已过期，请重新登录',
        403: '您没有权限执行此操作',
        404: '请求的资源不存在',
        409: '数据冲突，请刷新后重试',
        429: '操作过于频繁，请稍后再试',
        500: '服务器内部错误，请稍后重试',
        502: '网关错误，请稍后重试',
        503: '服务暂时不可用，请稍后重试',
      };
      errorMsg = statusMessages[status] || `服务器返回错误（状态码：${status}）`;
    } else if (error.code === 'ECONNABORTED') {
      errorMsg = '请求超时，请检查网络后重试';
    } else if (error.code === 'ERR_NETWORK') {
      errorMsg = '无法连接到服务器，请检查网络或服务器是否启动';
    }

    error.friendlyMessage = errorMsg;
    console.warn('[API Error]', errorMsg, status);
    return Promise.reject(error);
  }
);

export async function get<T>(
  url: string,
  params?: Record<string, unknown>,
  config?: AxiosRequestConfig
): Promise<ApiResponse<T>> {
  const response = await api.get<ApiResponse<T>>(url, { params, ...config });
  return response.data;
}

export async function post<T>(
  url: string,
  data?: unknown,
  config?: AxiosRequestConfig
): Promise<ApiResponse<T>> {
  const response = await api.post<ApiResponse<T>>(url, data, config);
  return response.data;
}

export async function put<T>(
  url: string,
  data?: unknown,
  config?: AxiosRequestConfig
): Promise<ApiResponse<T>> {
  const response = await api.put<ApiResponse<T>>(url, data, config);
  return response.data;
}

export async function del<T>(
  url: string,
  config?: AxiosRequestConfig
): Promise<ApiResponse<T>> {
  const response = await api.delete<ApiResponse<T>>(url, config);
  return response.data;
}

export default api;
