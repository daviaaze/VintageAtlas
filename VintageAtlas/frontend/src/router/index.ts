import { createRouter, createWebHistory } from 'vue-router';

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      name: 'home',
      component: () => import('@/pages/MapView.vue'),
      meta: {
        title: 'Map - VintageAtlas'
      }
    },
    {
      path: '/historical',
      name: 'historical',
      component: () => import('@/pages/HistoricalView.vue'),
      meta: {
        title: 'Historical Data - VintageAtlas'
      }
    },
    {
      path: '/settings',
      name: 'settings',
      component: () => import('@/pages/SettingsView.vue'),
      meta: {
        title: 'Settings - VintageAtlas'
      }
    },
    {
      path: '/admin',
      name: 'admin',
      component: () => import('@/pages/AdminDashboard.vue'),
      meta: {
        title: 'Admin Dashboard - VintageAtlas',
        requiresAdmin: true
      }
    },
    {
      path: '/:pathMatch(.*)*',
      name: 'not-found',
      component: () => import('@/pages/NotFound.vue'),
      meta: {
        title: '404 Not Found - VintageAtlas'
      }
    }
  ]
});

// Update document title based on route meta
router.afterEach((to) => {
  document.title = to.meta.title as string || 'VintageAtlas';
});

export default router;
