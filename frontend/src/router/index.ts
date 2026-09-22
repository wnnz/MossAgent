import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/authStore'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/setup',
      name: 'setup',
      component: () => import('@/views/SetupView.vue'),
      meta: { public: true },
    },
    {
      path: '/login',
      name: 'login',
      component: () => import('@/views/LoginView.vue'),
      meta: { public: true },
    },
    {
      path: '/',
      name: 'chat',
      component: () => import('@/views/ChatView.vue'),
    },
    {
      path: '/settings',
      name: 'settings',
      component: () => import('@/views/SettingsView.vue'),
    },
  ],
})

// 路由守卫：needsSetup → /setup；未认证 → /login
router.beforeEach(async (to) => {
  const auth = useAuthStore()
  await auth.refreshStatus()
  if (auth.needsSetup) {
    return to.name === 'setup' ? true : { name: 'setup' }
  }
  if (to.meta.public) {
    // 已设置密码：setup 不再可达；未认证进 login
    if (to.name === 'setup') return { name: 'login' }
    return auth.authenticated && to.name === 'login' ? { name: 'chat' } : true
  }
  if (!auth.authenticated) {
    return { name: 'login' }
  }
  return true
})

export default router
