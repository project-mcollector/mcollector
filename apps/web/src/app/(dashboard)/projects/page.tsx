"use client";

import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import styles from "../dashboard.module.css";
import { authFetch } from "@/lib/auth";
import ConfirmModal from "@/components/ConfirmModal";

type Project = {
  id: string;
  name: string;
  apiKey: string;
};

type CreatedProject = {
  id: string;
  name: string;
  apiKey: string;
};

const BASE_URL =
  process.env.NEXT_PUBLIC_IDENTITY_URL || "http://localhost:5003";

const EyeIcon = () => (
  <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
    <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/>
    <circle cx="12" cy="12" r="3"/>
  </svg>
);

const EyeOffIcon = () => (
  <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
    <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"/>
    <line x1="1" y1="1" x2="23" y2="23"/>
  </svg>
);

export default function ProjectsPage() {
  const router = useRouter();
  const createInputRef = useRef<HTMLInputElement>(null);

  const [projects, setProjects] = useState<Project[]>([]);
  const [newProjectName, setNewProjectName] = useState("");
  const [loading, setLoading] = useState(true);
  const [creating, setCreating] = useState(false);
  const [createdProject, setCreatedProject] = useState<CreatedProject | null>(null);
  const [copied, setCopied] = useState(false);
  const [error, setError] = useState("");

  const [deleteConfirm, setDeleteConfirm] = useState<Project | null>(null);
  const [deleting, setDeleting] = useState<string | null>(null);

  const [renameTarget, setRenameTarget] = useState<Project | null>(null);
  const [renameName, setRenameName] = useState("");
  const [renaming, setRenaming] = useState(false);

  const [visibleKeys, setVisibleKeys] = useState<Set<string>>(new Set());
  const [copiedKey, setCopiedKey] = useState<string | null>(null);

  const [regenerateTarget, setRegenerateTarget] = useState<Project | null>(null);
  const [regenerating, setRegenerating] = useState(false);
  const [regeneratedKey, setRegeneratedKey] = useState<{ projectId: string; apiKey: string } | null>(null);

  const [search, setSearch] = useState("");

  useEffect(() => {
    document.title = "MCollector — Мои проекты";
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
    setError("");
    try {
      const res = await authFetch(`${BASE_URL}/api/projects`, router, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ name: newProjectName }),
      });
      const created = await res.json();
      setProjects((prev) => [...prev, created]);
      setNewProjectName("");
      setCreatedProject(created);
    } catch {
      setError("Не удалось создать проект");
    } finally {
      setCreating(false);
    }
  }

  async function confirmDelete() {
    if (!deleteConfirm || deleting) return;
    const id = deleteConfirm.id;
    setDeleteConfirm(null);
    setDeleting(id);
    setError("");
    try {
      const res = await authFetch(`${BASE_URL}/api/projects/${id}`, router, {
        method: "DELETE",
      });
      if (res.ok) {
        setProjects((prev) => prev.filter((p) => p.id !== id));
      } else {
        setError("Не удалось удалить проект");
      }
    } catch {
      setError("Не удалось удалить проект");
    } finally {
      setDeleting(null);
    }
  }

  async function confirmRename() {
    if (!renameTarget || !renameName.trim() || renaming) return;
    setRenaming(true);
    setError("");
    try {
      const res = await authFetch(`${BASE_URL}/api/projects/${renameTarget.id}`, router, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ name: renameName.trim(), description: "" }),
      });
      if (res.ok) {
        const updated = await res.json();
        setProjects((prev) => prev.map((p) => (p.id === updated.id ? updated : p)));
        setRenameTarget(null);
        setRenameName("");
      } else {
        setError("Не удалось переименовать проект");
      }
    } catch {
      setError("Не удалось переименовать проект");
    } finally {
      setRenaming(false);
    }
  }

  async function confirmRegenerate() {
    if (!regenerateTarget || regenerating) return;
    setRegenerating(true);
    setError("");
    const id = regenerateTarget.id;
    setRegenerateTarget(null);
    try {
      const res = await authFetch(`${BASE_URL}/api/projects/${id}/api-key/regenerate`, router, {
        method: "POST",
      });
      if (res.ok) {
        const updated = await res.json();
        setProjects((prev) => prev.map((p) => (p.id === id ? updated : p)));
        setRegeneratedKey({ projectId: id, apiKey: updated.apiKey });
        setVisibleKeys((prev) => new Set([...prev, id]));
      } else {
        setError("Не удалось перегенерировать ключ");
      }
    } catch {
      setError("Не удалось перегенерировать ключ");
    } finally {
      setRegenerating(false);
    }
  }

  function toggleKeyVisibility(id: string) {
    setVisibleKeys((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  function copyKey(id: string, key: string) {
    navigator.clipboard.writeText(key);
    setCopiedKey(id);
    setTimeout(() => setCopiedKey(null), 2000);
  }

  function copyApiKey(key: string) {
    navigator.clipboard.writeText(key);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }

  function closeModal() {
    setCreatedProject(null);
    setCopied(false);
    setRegeneratedKey(null);
  }

  function logout() {
    localStorage.removeItem("token");
    router.push("/login");
  }

  const filteredProjects = projects.filter((p) =>
    p.name.toLowerCase().includes(search.toLowerCase())
  );

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
        <div>
          <h1 className={styles.title}>Мои проекты</h1>
          {projects.length > 0 && (
            <p className={styles.subtitle}>{projects.length} {projects.length === 1 ? "проект" : projects.length < 5 ? "проекта" : "проектов"}</p>
          )}
        </div>
        <button className={styles.buttonOutline} onClick={logout}>
          Выйти
        </button>
      </div>

      {error && (
        <div className={styles.errorBanner}>{error}</div>
      )}

      {projects.length > 3 && (
        <div className={styles.searchRow}>
          <input
            type="text"
            placeholder="Поиск проектов..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className={styles.input}
          />
        </div>
      )}

      {projects.length === 0 ? (
        <div className={styles.emptyStateBlock}>
          <p className={styles.emptyStateText}>У вас пока нет проектов</p>
          <button
            className={styles.button}
            onClick={() => createInputRef.current?.focus()}
          >
            Создать первый проект
          </button>
        </div>
      ) : (
        <div className={styles.projectsGrid}>
          {filteredProjects.map((project) => (
            <div key={project.id} className={styles.projectCard}>
              <div
                className={styles.projectCardMain}
                onClick={() => router.push(`/projects/${project.id}/dashboard`)}
              >
                <span className={styles.projectName}>{project.name}</span>
                <span className={styles.projectArrow}>→</span>
              </div>

              <div className={styles.projectApiKeyRow}>
                <span className={styles.apiKeyDisplay}>
                  {visibleKeys.has(project.id)
                    ? (regeneratedKey?.projectId === project.id ? regeneratedKey.apiKey : project.apiKey)
                    : "proj_••••••••••••••••"}
                </span>
                <div className={styles.apiKeyActions}>
                  <button
                    className={styles.iconButton}
                    onClick={() => toggleKeyVisibility(project.id)}
                    title={visibleKeys.has(project.id) ? "Скрыть ключ" : "Показать ключ"}
                  >
                    {visibleKeys.has(project.id) ? <EyeOffIcon /> : <EyeIcon />}
                  </button>
                  <button
                    className={styles.iconButton}
                    onClick={() => copyKey(project.id, project.apiKey)}
                    title="Скопировать ключ"
                  >
                    {copiedKey === project.id ? (
                      <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="#16a34a" strokeWidth="2"><polyline points="20 6 9 17 4 12"/></svg>
                    ) : (
                      <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></svg>
                    )}
                  </button>
                </div>
              </div>

              <div className={styles.projectCardActions}>
                <button
                  className={styles.buttonSmall}
                  onClick={(e) => { e.stopPropagation(); setRenameTarget(project); setRenameName(project.name); }}
                >
                  Переименовать
                </button>
                <button
                  className={styles.buttonSmall}
                  onClick={(e) => { e.stopPropagation(); setRegenerateTarget(project); }}
                  disabled={regenerating}
                >
                  Перегенерировать ключ
                </button>
                <button
                  className={styles.deleteButton}
                  onClick={(e) => { e.stopPropagation(); setDeleteConfirm(project); }}
                  disabled={deleting === project.id}
                >
                  {deleting === project.id ? "Удаление..." : "Удалить"}
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      <div className={styles.createSection}>
        <p className={styles.createTitle}>Создать новый проект</p>
        <div className={styles.createRow}>
          <input
            ref={createInputRef}
            type="text"
            placeholder="Название проекта"
            value={newProjectName}
            onChange={(e) => setNewProjectName(e.target.value)}
            onKeyDown={(e) => e.key === "Enter" && createProject()}
            className={styles.input}
          />
          <button
            onClick={createProject}
            disabled={creating || !newProjectName.trim()}
            className={styles.button}
          >
            {creating ? "Создание..." : "Создать"}
          </button>
        </div>
      </div>

      {deleteConfirm && (
        <ConfirmModal
          title="Удалить проект?"
          message={`Проект "${deleteConfirm.name}" и все его данные будут удалены. Это действие необратимо.`}
          confirmLabel="Удалить"
          danger
          onConfirm={confirmDelete}
          onCancel={() => setDeleteConfirm(null)}
        />
      )}

      {renameTarget && (
        <div className={styles.modalOverlay}>
          <div className={styles.modal} onClick={(e) => e.stopPropagation()}>
            <h2 className={styles.modalTitle}>Переименовать проект</h2>
            <label className={styles.label}>Новое название</label>
            <input
              type="text"
              value={renameName}
              onChange={(e) => setRenameName(e.target.value)}
              onKeyDown={(e) => e.key === "Enter" && confirmRename()}
              className={styles.input}
              autoFocus
              style={{ marginBottom: 20 }}
            />
            <div className={styles.modalButtons}>
              <button className={styles.button} onClick={confirmRename} disabled={renaming || !renameName.trim()}>
                {renaming ? "Сохранение..." : "Сохранить"}
              </button>
              <button className={styles.buttonOutline} onClick={() => setRenameTarget(null)}>
                Отмена
              </button>
            </div>
          </div>
        </div>
      )}

      {regenerateTarget && (
        <ConfirmModal
          title="Перегенерировать API-ключ?"
          message={`Старый ключ проекта "${regenerateTarget.name}" перестанет работать немедленно. Все интеграции, использующие его, сломаются до обновления.`}
          confirmLabel="Перегенерировать"
          danger
          onConfirm={confirmRegenerate}
          onCancel={() => setRegenerateTarget(null)}
        />
      )}

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

            <label className={styles.label}>Установка</label>
            <div className={styles.codeBlock}>{`npm install @mcollector/sdk`}</div>

            <label className={styles.label}>Инициализация</label>
            <div className={styles.codeBlock}>{`import { analytics } from '@mcollector/sdk'\n\nanalytics.init('${createdProject.apiKey}')`}</div>

            <div className={styles.modalButtons}>
              <button
                className={styles.button}
                onClick={() => router.push(`/projects/${createdProject.id}/dashboard`)}
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

      {regeneratedKey && (
        <div className={styles.modalOverlay}>
          <div className={styles.modal}>
            <h2 className={styles.modalTitle}>Новый API-ключ</h2>
            <p className={styles.modalSubtitle}>
              Сохраните ключ — это единственный раз, когда он отображается в явном виде после перегенерации.
            </p>
            <div className={styles.apiKeyBox}>
              <span className={styles.apiKeyText}>{regeneratedKey.apiKey}</span>
              <button
                className={styles.buttonSmall}
                onClick={() => copyApiKey(regeneratedKey.apiKey)}
              >
                {copied ? "Скопировано ✓" : "Скопировать"}
              </button>
            </div>
            <div className={styles.modalButtons}>
              <button className={styles.button} onClick={closeModal}>
                Закрыть
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
