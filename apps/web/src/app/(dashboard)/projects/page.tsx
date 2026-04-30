"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import styles from "../dashboard.module.css";

type Project = {
  id: string;
  name: string;
};

type CreatedProject = {
  id: string;
  name: string;
  apiKey: string;
};

const BASE_URL =
  process.env.NEXT_PUBLIC_IDENTITY_URL || "http://localhost:5003";

export default function ProjectsPage() {
  const router = useRouter();
  const [projects, setProjects] = useState<Project[]>([]);
  const [newProjectName, setNewProjectName] = useState("");
  const [loading, setLoading] = useState(true);
  const [createdProject, setCreatedProject] = useState<CreatedProject | null>(
    null,
  );
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    const token = localStorage.getItem("token");
    if (!token) {
      router.push("/login");
      return;
    }

    fetch(`${BASE_URL}/api/projects`, {
      headers: { Authorization: `Bearer ${token}` },
    })
      .then((res) => res.json())
      .then((data) => {
        setProjects(data);
        setLoading(false);
      });
  }, [router]);

  async function createProject() {
    if (!newProjectName.trim()) return;
    const token = localStorage.getItem("token");

    const res = await fetch(`${BASE_URL}/api/projects`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${token}`,
      },
      body: JSON.stringify({ name: newProjectName }),
    });

    const created = await res.json();
    setProjects([...projects, created]);
    setNewProjectName("");
    setCreatedProject(created);
  }

  function closeModal() {
    setCreatedProject(null);
    setCopied(false);
  }

  function copyApiKey(key: string) {
    navigator.clipboard.writeText(key);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }

  function logout() {
    localStorage.removeItem("token");
    router.push("/login");
  }

  if (loading) return <p>Загрузка...</p>;

  return (
    <div className={styles.page}>
      <div className={styles.header}>
        <h1 className={styles.title}>Мои проекты</h1>
        <button className={styles.buttonOutline} onClick={logout}>Выйти</button>
      </div>

      {projects.length === 0 && (
        <p className={styles.emptyState}>У вас пока нет проектов</p>
      )}

      <div className={styles.projectsGrid}>
        {projects.map((project) => (
          <div key={project.id} className={styles.projectCard}>
            <span className={styles.projectName}>{project.name}</span>
            <button
              className={styles.buttonSmall}
              onClick={() => router.push(`/projects/${project.id}/dashboard`)}
            >
              Открыть
            </button>
          </div>
        ))}
      </div>

      <div className={styles.createSection}>
        <p className={styles.createTitle}>Создать новый проект</p>
        <div className={styles.createRow}>
          <input
            type="text"
            placeholder="Название проекта"
            value={newProjectName}
            onChange={(e) => setNewProjectName(e.target.value)}
            onKeyDown={(e) => e.key === "Enter" && createProject()}
            className={styles.input}
          />
          <button onClick={createProject} className={styles.button}>
            Создать
          </button>

        </div>
      </div>

      {createdProject && (
        <div className={styles.modalOverlay}>
          <div className={styles.modal}>
            <h2 className={styles.modalTitle}>Проект создан!</h2>
            <p className={styles.modalSubtitle}>
              Установите SDK на ваш сайт чтобы начать собирать данные
            </p>

            <label className={styles.label}>Ваш API-ключ</label>
            <div className={styles.apiKeyBox}>
              <span className={styles.apiKeyText}>{createdProject.apiKey}</span>
              <button
                className={styles.buttonSmall}
                onClick={() => copyApiKey(createdProject.apiKey)}
              >
                {copied ? "Скопировано ✓" : "Скопировать"}
              </button>
            </div>

            <label className={styles.label}>Добавьте на ваш сайт</label>
            <div className={styles.codeBlock}>
              {`<script>\n  analytics.init('${createdProject.apiKey}')\n</script>`}
            </div>

            <div className={styles.modalButtons}>
              <button
                className={styles.button}
                onClick={() =>
                  router.push(`/projects/${createdProject.id}/dashboard`)
                }
              >
                Перейти в дашборд
              </button>
              <button className={styles.buttonOutline} onClick={closeModal}>
                Закрыть
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
