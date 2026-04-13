'use client'

import { useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'

type Project = {
  id: string
  name: string
}

type CreatedProject = {
  id: string
  name: string
  apiKey: string
}

export default function ProjectsPage() {
  const router = useRouter()
  const [projects, setProjects] = useState<Project[]>([])
  const [newProjectName, setNewProjectName] = useState('')
  const [loading, setLoading] = useState(true)
  const [createdProject, setCreatedProject] = useState<CreatedProject | null>(null)

  useEffect(() => {
    const token = localStorage.getItem('token')
    if (!token) {
      router.push('/login')
      return
    }

    fetch('http://localhost:PORT/api/projects', {
      headers: { Authorization: `Bearer ${token}` }
    })
      .then(res => res.json())
      .then(data => {
        setProjects(data)
        setLoading(false)
      })
  }, [router])


  async function createProject() {
    if (!newProjectName.trim()) return
    const token = localStorage.getItem('token')

    const res = await fetch('http://localhost:PORT/api/projects', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${token}`
      },
      body: JSON.stringify({ name: newProjectName })
    })

    const created = await res.json()
    setProjects([...projects, created])
    setNewProjectName('')
    setCreatedProject(created)
  }

  function closeModal() {
    setCreatedProject(null)
  }

  if (loading) return <p>Загрузка...</p>

  return (
    <div>
      <h1>Мои проекты</h1>

      {projects.length === 0 && <p>У вас пока нет проектов</p>}

      {projects.map(project => (
        <div key={project.id}>
          <span>{project.name}</span>
          <button onClick={() => router.push(`/projects/${project.id}/dashboard`)}>
            Открыть
          </button>
        </div>
      ))}

      <h2>Создать новый проект</h2>
      <input
        type="text"
        placeholder="Название проекта"
        value={newProjectName}
        onChange={e => setNewProjectName(e.target.value)}
      />
      <button onClick={createProject}>Создать</button>

      {createdProject && (
        <div>
          <div>
            <h2>Проект создан!</h2>

            <p>Ваш API-ключ:</p>
            <code>{createdProject.apiKey}</code>
            <button onClick={() => navigator.clipboard.writeText(createdProject.apiKey)}>
              Скопировать
            </button>

            <p>Добавьте этот код на ваш сайт:</p>
            <pre>
              {`<script>
  analytics.init('${createdProject.apiKey}')
</script>`}
            </pre>

            <button onClick={() => router.push(`/projects/${createdProject.id}/dashboard`)}>
              Перейти в дашборд
            </button>
            <button onClick={closeModal}>
              Закрыть
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
