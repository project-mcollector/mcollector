"use client";

import styles from "./ConfirmModal.module.css";

type Props = {
  title: string;
  message: string;
  confirmLabel?: string;
  onConfirm: () => void;
  onCancel: () => void;
  danger?: boolean;
};

export default function ConfirmModal({
  title,
  message,
  confirmLabel = "Подтвердить",
  onConfirm,
  onCancel,
  danger = false,
}: Props) {
  return (
    <div className={styles.overlay} onClick={onCancel}>
      <div className={styles.modal} onClick={(e) => e.stopPropagation()}>
        <h2 className={styles.title}>{title}</h2>
        <p className={styles.message}>{message}</p>
        <div className={styles.buttons}>
          <button className={styles.cancel} onClick={onCancel}>
            Отмена
          </button>
          <button
            className={danger ? styles.danger : styles.confirm}
            onClick={onConfirm}
          >
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
