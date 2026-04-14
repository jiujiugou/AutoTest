<template>
  <div class="settings">

    <el-card>
      <h3>个人设置</h3>

      <el-tabs v-model="activeTab">

        <!-- 👤 基本信息 -->
        <el-tab-pane label="基本信息" name="profile">
          <el-form :model="profile" label-width="100px">

            <el-form-item label="用户名">
              <el-input v-model="profile.username" />
            </el-form-item>

            <el-form-item label="邮箱">
              <el-input v-model="profile.email" />
            </el-form-item>

            <el-form-item label="头像">
              <el-input v-model="profile.avatar" placeholder="头像URL" />
            </el-form-item>

            <el-form-item>
              <el-button type="primary" @click="saveProfile">
                保存
              </el-button>
            </el-form-item>

          </el-form>
        </el-tab-pane>

        <!-- 🔐 修改密码 -->
        <el-tab-pane label="修改密码" name="password">
          <el-form :model="passwordForm" label-width="100px">

            <el-form-item label="旧密码">
              <el-input type="password" v-model="passwordForm.oldPassword" />
            </el-form-item>

            <el-form-item label="新密码">
              <el-input type="password" v-model="passwordForm.newPassword" />
            </el-form-item>

            <el-form-item label="确认密码">
              <el-input type="password" v-model="passwordForm.confirmPassword" />
            </el-form-item>

            <el-form-item>
              <el-button type="primary" @click="changePassword">
                修改密码
              </el-button>
            </el-form-item>

          </el-form>
        </el-tab-pane>

        <!-- ⚙️ 偏好设置 -->
        <el-tab-pane label="偏好设置" name="prefs">
          <el-form :model="prefs" label-width="120px">

            <el-form-item label="主题">
              <el-select v-model="prefs.theme">
                <el-option label="浅色" value="light" />
                <el-option label="深色" value="dark" />
              </el-select>
            </el-form-item>

            <el-form-item label="邮件通知">
              <el-switch v-model="prefs.emailNotify" />
            </el-form-item>

            <el-form-item label="系统通知">
              <el-switch v-model="prefs.systemNotify" />
            </el-form-item>

            <el-form-item>
              <el-button type="primary" @click="savePrefs">
                保存设置
              </el-button>
            </el-form-item>

          </el-form>
        </el-tab-pane>

        <!-- 🔑 Token -->
        <el-tab-pane label="API Token" name="token">

          <div class="token-box">
            <p>你的 API Token：</p>

            <el-input
              v-model="token"
              readonly
              style="width: 400px"
            />

            <div class="btns">
              <el-button @click="copyToken">复制</el-button>
              <el-button type="danger" @click="resetToken">
                重置 Token
              </el-button>
            </div>
          </div>

        </el-tab-pane>

      </el-tabs>
    </el-card>

  </div>
</template>

<script setup>
import { ref } from 'vue'
import { ElMessage } from 'element-plus'

const activeTab = ref('profile')

/* 👤 基本信息 */
const profile = ref({
  username: 'admin',
  email: 'admin@test.com',
  avatar: ''
})

function saveProfile() {
  console.log('保存用户信息', profile.value)
  ElMessage.success('已保存')
}

/* 🔐 密码 */
const passwordForm = ref({
  oldPassword: '',
  newPassword: '',
  confirmPassword: ''
})

function changePassword() {
  if (passwordForm.value.newPassword !== passwordForm.value.confirmPassword) {
    ElMessage.error('两次密码不一致')
    return
  }

  console.log('修改密码', passwordForm.value)
  ElMessage.success('密码已修改')
}

/* ⚙️ 偏好 */
const prefs = ref({
  theme: 'light',
  emailNotify: true,
  systemNotify: true
})

function savePrefs() {
  console.log('保存偏好', prefs.value)
  ElMessage.success('设置已保存')
}

/* 🔑 Token */
const token = ref('abc123456789xyz')

function copyToken() {
  navigator.clipboard.writeText(token.value)
  ElMessage.success('已复制')
}

function resetToken() {
  token.value = Math.random().toString(36).slice(2)
  ElMessage.success('Token 已重置')
}
</script>

<style scoped>
.settings {
  padding: 16px;
}

h3 {
  margin-bottom: 16px;
}

.token-box {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.btns {
  display: flex;
  gap: 10px;
}
</style>