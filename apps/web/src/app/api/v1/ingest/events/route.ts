import { NextRequest, NextResponse } from "next/server";
import { getInternalIngestionUrl } from "@/lib/ingestionProxy";

export const runtime = "nodejs";

export async function POST(request: NextRequest) {
  const response = await fetch(getInternalIngestionUrl("/api/v1/ingest/events"), {
    method: "POST",
    headers: {
      "content-type": request.headers.get("content-type") || "application/json",
    },
    body: await request.text(),
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
