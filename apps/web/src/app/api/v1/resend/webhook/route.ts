import { NextRequest, NextResponse } from "next/server";
import { Resend } from "resend";

export const runtime = "nodejs";

export async function POST(request: NextRequest) {
  const resend = new Resend(process.env.RESEND_APITOKEN);
  const payload = await request.text();

  const headers = {
    id: request.headers.get("svix-id") ?? "",
    timestamp: request.headers.get("svix-timestamp") ?? "",
    signature: request.headers.get("svix-signature") ?? "",
  };

  const webhookSecret = process.env.RESEND_WEBHOOK_SECRET;
  if (!webhookSecret) {
    return NextResponse.json({ error: "Webhook secret not configured" }, { status: 500 });
  }

  try {
    resend.webhooks.verify({ webhookSecret, payload, headers });
  } catch {
    return NextResponse.json({ error: "Invalid signature" }, { status: 401 });
  }

  const event = JSON.parse(payload);

  if (event.type !== "email.received") {
    return NextResponse.json({ received: true });
  }

  const forwardTo = process.env.RESEND_FORWARD_TO;
  const forwardFrom = process.env.RESEND_FORWARD_FROM;

  if (!forwardTo || !forwardFrom) {
    return NextResponse.json({ error: "Forward addresses not configured" }, { status: 500 });
  }

  const { error } = await resend.emails.receiving.forward({
    emailId: event.data.email_id,
    to: forwardTo,
    from: forwardFrom,
  });

  if (error) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }

  return NextResponse.json({ received: true });
}
