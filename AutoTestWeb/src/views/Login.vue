<template>
  <div class="login-container">
    <div class="login-box">
      <div class="login-header">
        <img src="../assets/vue.svg" alt="logo" class="logo" />
        <h2 class="title">AutoTest Pro</h2>
        <p class="subtitle">自动化监控与测试平台</p>
      </div>

      <el-form :model="form" @keyup.enter="login" class="login-form">
        <el-form-item>
          <el-input
            v-model="form.username"
            placeholder="请输入账号"
            size="large"
            :prefix-icon="'User'"
          />
        </el-form-item>
        <el-form-item>
          <el-input
            v-model="form.password"
            type="password"
            placeholder="请输入密码"
            size="large"
            :prefix-icon="'Lock'"
            show-password
          />
        </el-form-item>

        <el-button
          type="primary"
          class="login-btn"
          size="large"
          :loading="loading"
          @click="login"
        >
          登 录
        </el-button>
      </el-form>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import api from '../api/http'

const router = useRouter()
const loading = ref(false)

const form = reactive({
  username: '',
  password: ''
})

async function login() {
  if (!form.username || !form.password) {
    ElMessage.warning('请输入账号和密码')
    return
  }

  loading.value = true

  try {
    localStorage.removeItem('accessToken')
    localStorage.removeItem('refreshToken')

    const res = await api.post('/api/auth/login', {
      username: form.username,
      password: form.password
    })

    const { accessToken, refreshToken } = res

    if (!accessToken) {
      throw new Error('登录失败：未返回 accessToken')
    }

    localStorage.setItem('accessToken', accessToken)
    localStorage.setItem('refreshToken', refreshToken)

    ElMessage.success('登录成功')
    router.push('/dashboard')

  } catch (err) {
    ElMessage.error(err?.message || '登录失败')

  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.login-container {
  height: 100vh;
  display: flex;
  justify-content: center;
  align-items: center;
  background: url('https://gw.alipayobjects.com/zos/rmsportal/TVYTbAXqsXIfRkcg1234.svg') no-repeat center center;
  background-size: cover;
  background-color: #f0f2f5;
}

.login-box {
  width: 380px;
  padding: 40px;
  background: #ffffff;
  border-radius: 8px;
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.1);
}

.login-header {
  text-align: center;
  margin-bottom: 30px;
}

.logo {
  width: 48px;
  height: 48px;
  margin-bottom: 10px;
}

.title {
  font-size: 24px;
  font-weight: 600;
  color: #1f2f3d;
  margin: 0 0 8px;
}

.subtitle {
  font-size: 14px;
  color: #909399;
  margin: 0;
}

.login-form {
  margin-top: 20px;
}

.login-btn {
  width: 100%;
  margin-top: 10px;
  font-size: 16px;
  border-radius: 4px;
}
</style>
