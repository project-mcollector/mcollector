const DEFAULT_INTERNAL_INGESTION_URL =
  process.env.INGESTION_INTERNAL_URL ||
  "http://localhost:5001";

export function getInternalIngestionUrl(pathname: string): string {
  return new URL(pathname, DEFAULT_INTERNAL_INGESTION_URL).toString();
}
