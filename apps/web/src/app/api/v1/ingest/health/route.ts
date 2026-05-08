import { NextResponse } from "next/server";
import { getInternalIngestionUrl } from "@/lib/ingestionProxy";

export const runtime = "nodejs";

export async function GET() {
  const response = await fetch(getInternalIngestionUrl("/api/v1/ingest/health"), {
    cache: "no-store",
  });

  const body = await response.text();
  return new NextResponse(body, {
    status: response.status,
    headers: {
      "content-type": response.headers.get("content-type") || "application/json",
    },
  });
}
