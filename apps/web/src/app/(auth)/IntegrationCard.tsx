import styles from "./login.module.css";

export default function IntegrationCard() {
  return (
    <div className={styles.integrationCard}>
      <div>
        <p className={styles.integrationTitle}>Подключение SDK</p>
        <p className={styles.integrationSubtitle} style={{ marginTop: 8 }}>
          Собирайте аналитику с вашего сайта в несколько строк кода
        </p>
      </div>

      <div className={styles.integrationStep}>
        <span className={styles.integrationStepLabel}>1. Установка</span>
        <div className={styles.integrationCode}>{`npm install @mcollector/sdk`}</div>
      </div>

      <div className={styles.integrationStep}>
        <span className={styles.integrationStepLabel}>2. Инициализация</span>
        <div className={styles.integrationCode}>{`import { analytics } from '@mcollector/sdk'

analytics.init('YOUR_API_KEY')`}</div>
      </div>

      <div className={styles.integrationStep}>
        <span className={styles.integrationStepLabel}>3. Отправка событий</span>
        <div className={styles.integrationCode}>{`analytics.track('page_view', {
  url: window.location.href
})`}</div>
      </div>

      <a
        href="https://www.npmjs.com/package/@mcollector/sdk"
        target="_blank"
        rel="noopener noreferrer"
        className={styles.integrationLink}
      >
        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"/>
        </svg>
        Документация на npm
        <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" style={{ opacity: 0.5 }}>
          <path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"/><polyline points="15 3 21 3 21 9"/><line x1="10" y1="14" x2="21" y2="3"/>
        </svg>
      </a>
    </div>
  );
}
