<template>
  <el-container class="layout-container">
    <!-- 侧边栏 -->
    <el-aside :width="isCollapse ? '64px' : '240px'" class="aside">
      <div class="logo-container">
        <span class="logo-text" v-if="!isCollapse">AutoTest Pro</span>
        <span class="logo-text-mini" v-else>AT</span>
      </div>
      <el-menu
        :default-active="route.path"
        router
        class="aside-menu"
        background-color="#1e1e2d"
        text-color="#a1a5b7"
        active-text-color="#ffffff"
        :collapse="isCollapse"
        :collapse-transition="false"
      >
        <el-menu-item index="/dashboard">
          <el-icon><DataBoard /></el-icon>
          <template #title>仪表盘</template>
        </el-menu-item>
        <el-menu-item index="/monitor">
          <el-icon><Monitor /></el-icon>
          <template #title>监控观测</template>
        </el-menu-item>
        <el-menu-item index="/task">
          <el-icon><Operation /></el-icon>
          <template #title>任务调度</template>
        </el-menu-item>
        <el-menu-item index="/RbacAdmin">
          <el-icon><User /></el-icon>
          <template #title>权限管理</template>
        </el-menu-item>
        <el-menu-item index="/log">
          <el-icon><Document /></el-icon>
          <template #title>系统日志</template>
        </el-menu-item>
        <el-menu-item index="/setting">
          <el-icon><Setting /></el-icon>
          <template #title>系统设置</template>
        </el-menu-item>
        <el-menu-item index="/ai">
          <el-icon><ChatDotRound /></el-icon>
          <template #title>AI 助手</template>
        </el-menu-item>
      </el-menu>
    </el-aside>

    <el-container class="main-container">
      <!-- 顶部 Header -->
      <el-header class="header">
        <div class="header-left">
          <el-icon class="collapse-btn" @click="isCollapse = !isCollapse">
            <Fold v-if="!isCollapse" />
            <Expand v-else />
          </el-icon>
          <el-breadcrumb separator="/" class="breadcrumb">
            <el-breadcrumb-item :to="{ path: '/' }">首页</el-breadcrumb-item>
            <el-breadcrumb-item>{{ currentRouteName }}</el-breadcrumb-item>
          </el-breadcrumb>
        </div>
        <div class="header-right">
          <el-dropdown trigger="click" @command="handleCommand">
            <span class="user-profile">
              <el-avatar size="default" class="avatar">Admin</el-avatar>
              <span class="username">管理员</span>
              <el-icon><ArrowDown /></el-icon>
            </span>
            <template #dropdown>
              <el-dropdown-menu>
                <el-dropdown-item command="profile">个人中心</el-dropdown-item>
                <el-dropdown-item divided command="logout">退出登录</el-dropdown-item>
              </el-dropdown-menu>
            </template>
          </el-dropdown>
        </div>
      </el-header>

      <!-- 主体内容 -->
      <el-main class="main-content">
        <router-view v-slot="{ Component }">
          <transition name="fade-transform" mode="out-in">
            <component :is="Component" />
          </transition>
        </router-view>
      </el-main>
    </el-container>
  </el-container>
</template>

<script setup>
import { ref, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  DataBoard, Monitor, Operation, User,
  Document, Setting, ChatDotRound,
  Fold, Expand, ArrowDown
} from '@element-plus/icons-vue'

const route = useRoute()
const router = useRouter()
const isCollapse = ref(false)

const routeNameMap = {
  '/dashboard': '仪表盘',
  '/monitor': '监控观测',
  '/task': '任务调度',
  '/log': '系统日志',
  '/setting': '系统设置',
  '/person': '个人中心',
  '/ai': 'AI 助手',
  '/RbacAdmin': '权限管理'
}

const currentRouteName = computed(() => routeNameMap[route.path] || '页面')

function handleCommand(command) {
  if (command === 'logout') {
    localStorage.removeItem('accessToken')
    localStorage.removeItem('refreshToken')
    router.push('/login')
  } else if (command === 'profile') {
    router.push('/person')
  }
}
</script>

<style scoped>
.layout-container {
  height: 100vh;
  background-color: #f5f8fa;
}

.aside {
  background-color: #1e1e2d;
  transition: width 0.3s;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.logo-container {
  height: 60px;
  display: flex;
  align-items: center;
  justify-content: center;
  background-color: #151521;
  color: #ffffff;
}

.logo-text {
  font-size: 20px;
  font-weight: 700;
  letter-spacing: 1px;
}

.logo-text-mini {
  font-size: 20px;
  font-weight: 900;
}

.aside-menu {
  border-right: none;
  flex: 1;
}

.aside-menu .el-menu-item.is-active {
  background-color: #1b1b29;
  color: #3e97ff;
  border-left: 3px solid #3e97ff;
}

.main-container {
  display: flex;
  flex-direction: column;
}

.header {
  height: 60px;
  background-color: #ffffff;
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0 20px;
  box-shadow: 0 1px 4px rgba(0, 21, 41, 0.08);
  z-index: 10;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 16px;
}

.collapse-btn {
  font-size: 20px;
  cursor: pointer;
  color: #666;
  transition: color 0.3s;
}

.collapse-btn:hover {
  color: #3e97ff;
}

.breadcrumb {
  margin-top: 2px;
}

.header-right {
  display: flex;
  align-items: center;
}

.user-profile {
  display: flex;
  align-items: center;
  cursor: pointer;
  gap: 8px;
  color: #333;
}

.avatar {
  background-color: #3e97ff;
  color: #fff;
  font-size: 12px;
}

.username {
  font-size: 14px;
  font-weight: 500;
}

.main-content {
  padding: 24px;
  overflow-y: auto;
}

/* 路由切换动画 */
.fade-transform-leave-active,
.fade-transform-enter-active {
  transition: all 0.3s;
}
.fade-transform-enter-from {
  opacity: 0;
  transform: translateX(-10px);
}
.fade-transform-leave-to {
  opacity: 0;
  transform: translateX(10px);
}
</style>
