"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import styles from "../dashboard.module.css";
import { authFetch } from "@/lib/auth";

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
  const [creating, setCreating] = useState(false);
  const [createdProject, setCreatedProject] = useState<CreatedProject | null>(
    null,
  );
  const [copied, setCopied] = useState(false);
  const [deleting, setDeleting] = useState<string | null>(null);

  useEffect(() => {
    const token = localStorage.getItem("token");
    if (!token) {
      router.push("/login");
      return;
    }

    authFetch(`${BASE_URL}/api/projects`, router)
      .then((res) => res.json())
      .then((data) => {
        setProjects(data);
        setLoading(false);
      })
      .catch(() => setLoading(false));
  }, [router]);

  async function createProject() {
    if (!newProjectName.trim() || creating) return;
    setCreating(true);
    try {
      const res = await authFetch(`${BASE_URL}/api/projects`, router, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ name: newProjectName }),
      });
      const created = await res.json();
      setProjects([...projects, created]);
      setNewProjectName("");
      setCreatedProject(created);
    } finally {
      setCreating(false);
    }
  }

  async function deleteProject(id: string) {
    if (deleting) return;
    if (!confirm("Удалить проект? Это действие необратимо")) return;
    setDeleting(id);
    try {
      const res = await authFetch(`${BASE_URL}/api/projects/${id}`, router, {
        method: "DELETE",
      });
      if (res.ok) {
        setProjects(projects.filter((p) => p.id !== id));
      } else {
        const text = await res.text();
        alert("Не удалось удалить проект: " + text);
      }
    } catch (e) {
      alert(`Не удалось удалить проект ${e}`);
    } finally {
      setDeleting(null);
    }
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

  if (loading)
    return (
      <div className={styles.page}>
        <div className={styles.header}>
          <h1 className={styles.title}>Мои проекты</h1>
        </div>
        <div className={styles.projectsGrid}>
          {[...Array(3)].map((_, i) => (
            <div key={i} className={styles.skeletonCard} />
          ))}
        </div>
      </div>
    );

  return (
    <div className={styles.page}>
      <div className={styles.header}>
        <h1 className={styles.title}>Мои проекты</h1>
        <button className={styles.buttonOutline} onClick={logout}>
          Выйти
        </button>
      </div>

      {projects.length === 0 && (
        <p className={styles.emptyState}>У вас пока нет проектов</p>
      )}

      <div className={styles.projectsGrid}>
        {projects.map((project) => (
          <div
            key={project.id}
            className={styles.projectCard}
            onClick={() => router.push(`/projects/${project.id}/dashboard`)}
            style={{ cursor: "pointer" }}
          >
            <span className={styles.projectName}>{project.name}</span>
            <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
              <button
                className={styles.deleteButton}
                onClick={(e) => {
                  e.stopPropagation();
                  deleteProject(project.id);
                }}
                disabled={deleting === project.id}
              >
                {deleting === project.id ? "Удаление..." : "Удалить"}
              </button>
              <span className={styles.projectArrow}>→</span>
            </div>
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
          <button
            onClick={createProject}
            disabled={creating}
            className={styles.button}
          >
            {creating ? "Создание..." : "Создать"}
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
