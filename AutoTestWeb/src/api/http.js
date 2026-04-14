import axios from 'axios'

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

    // ⚠️ 其他错误
    const msg =
      err.response?.data?.message ||
      err.response?.data?.text ||
      err.message ||
      '请求失败'

    return Promise.reject(new Error(msg))
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
