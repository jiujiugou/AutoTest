import { createRouter, createWebHistory } from 'vue-router'

import Layout from '../layout/Layout.vue'
import Dashboard from '../views/Dashboard.vue'
import Monitor from '../views/Monitor.vue'
import Task from '../views/Task.vue'
import TestPlan from '../views/TestPlan.vue'
import Log from '../views/Log.vue'
import Login from '../views/Login.vue'
import Person from '../views/Person.vue'
import RbacAdmin from '../views/RbacAdmin.vue'
import Users from '../views/Users.vue'
const routes = [
  { path: '/login', component: Login },

  {
    path: '/',
    component: Layout,
    redirect: '/dashboard',
    children: [
      { path: '/dashboard', component: Dashboard },
      { path: '/monitor', component: Monitor },
      { path: '/task', component: Task },
      { path: '/testplan', component: TestPlan },
      { path: '/log', component: Log },
      { path: '/person', component: Person },

      { path: '/RbacAdmin', component: RbacAdmin },
      { path: '/users', component: Users }
    ]
  },

  { path: '/:pathMatch(.*)*', redirect: '/login' }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

router.beforeEach((to, from, next) => {
  const token = localStorage.getItem('accessToken')

  if (!token && to.path !== '/login') {
    next('/login')
    return
  }

  if (token && to.path === '/login') {
    next('/dashboard')
    return
  }

  next()
})

export default router
