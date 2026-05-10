import axios from 'axios'

function tryParseJson(v) {
  if (typeof v !== 'string') return v
  try { return JSON.parse(v) } catch { return v }
}

const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '',
  timeout: 60000
})

/* =========================
   request interceptor
========================= */
api.interceptors.request.use(config => {
  const token = localStorage.getItem('accessToken')

  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }

  return config
})

/* =========================
   response interceptor
========================= */
api.interceptors.response.use(
  res => {
    return res.data
  },

  async err => {
    const status = err.response?.status

    if (status === 401) {
      const original = err.config || {}
      const url = String(original.url || '')
      const isAuthEndpoint =
        url.includes('/api/auth/login') ||
        url.includes('/api/auth/bootstrap') ||
        url.includes('/api/auth/refresh') ||
        url.includes('/api/auth/logout')

      if (isAuthEndpoint) {
        const msg = err.response?.data?.message || err.message || '未授权'
        return Promise.reject(new Error(msg))
      }

      if (original.__retry401) {
        localStorage.removeItem('accessToken')
        localStorage.removeItem('refreshToken')
        window.location.href = '/login'
        return Promise.reject(new Error('未登录或登录过期'))
      }

      const refreshToken = localStorage.getItem('refreshToken')
      if (!refreshToken) {
        localStorage.removeItem('accessToken')
        window.location.href = '/login'
        return Promise.reject(new Error('未登录或登录过期'))
      }

      try {
        original.__retry401 = true
        const refreshed = await api.post('/api/auth/refresh', { refreshToken }, { __retry401: true })
        const accessToken = refreshed?.accessToken
        const newRefreshToken = refreshed?.refreshToken
        if (accessToken) localStorage.setItem('accessToken', accessToken)
        if (newRefreshToken) localStorage.setItem('refreshToken', newRefreshToken)
        if (refreshed?.user) {
          localStorage.setItem('userInfo', JSON.stringify(refreshed.user))
          if (refreshed.user.permissions) {
            localStorage.setItem('userPermissions', JSON.stringify(refreshed.user.permissions))
          }
        }
        original.headers = original.headers || {}
        original.headers.Authorization = `Bearer ${accessToken}`
        return api(original)
      } catch {
        localStorage.removeItem('accessToken')
        localStorage.removeItem('refreshToken')
        window.location.href = '/login'
        return Promise.reject(new Error('未登录或登录过期'))
      }
    }

    // 🌐 网络错误
    if (!err.response) {
      return Promise.reject(new Error('网络错误，请检查后端'))
    }

    // ⚠️ 其他错误 —— 保留完整响应体便于调试
    const data = err.response?.data
    const detail = new Error(
      data?.message ||
      data?.title ||
      data?.detail ||
      data?.text ||
      err.message ||
      '请求失败'
    )
    detail.status = err.response?.status
    detail.data = data
    detail.headers = err.response?.headers
    detail.config = { url: err.config?.url, method: err.config?.method, data: tryParseJson(err.config?.data) }
    return Promise.reject(detail)
  }
)

/* =========================
   可选：封装 request
   👉 让调用更干净
========================= */
export function request(config) {
  return api(config)
}

export default api
