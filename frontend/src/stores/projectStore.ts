import { defineStore } from 'pinia'
import { ref } from 'vue'
import { projectsApi, type Project } from '@/api/projects'

const CURRENT_PROJECT_KEY = 'codingagent-current-project'

export const useProjectStore = defineStore('project', () => {
  const projects = ref<Project[]>([])
  const current = ref<Project | null>(null)

  async function loadAll() {
    projects.value = await projectsApi.getAll()
    // 恢复当前项目（localStorage 持久化）
    const savedId = Number(localStorage.getItem(CURRENT_PROJECT_KEY))
    const saved = projects.value.find((p) => p.id === savedId)
    if (saved) current.value = saved
    else if (projects.value.length > 0) current.value = projects.value[0]
  }

  function setCurrent(p: Project | null) {
    current.value = p
    if (p) localStorage.setItem(CURRENT_PROJECT_KEY, String(p.id))
    else localStorage.removeItem(CURRENT_PROJECT_KEY)
  }

  async function create(name: string, path?: string) {
    const p = await projectsApi.create({ name, path })
    await loadAll()
    setCurrent(p)
    return p
  }

  async function remove(id: number) {
    await projectsApi.remove(id)
    await loadAll()
    if (current.value?.id === id) setCurrent(projects.value[0] ?? null)
  }

  return { projects, current, loadAll, setCurrent, create, remove }
})
