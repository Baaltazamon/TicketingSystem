const apiBaseUrl = process.env.SUPPORTPILOT_SMOKE_API_URL ?? "http://localhost:8080/api";
const webBaseUrl = process.env.SUPPORTPILOT_SMOKE_WEB_URL ?? "http://localhost:5173";
const adminEmail = process.env.SUPPORTPILOT_SMOKE_ADMIN_EMAIL
  ?? process.env.SUPPORTPILOT_ADMIN_EMAIL
  ?? "admin@supportpilot.local";
const adminPassword = process.env.SUPPORTPILOT_SMOKE_ADMIN_PASSWORD
  ?? process.env.SUPPORTPILOT_ADMIN_PASSWORD
  ?? "Admin123!";
const timeoutMs = Number(process.env.SUPPORTPILOT_SMOKE_TIMEOUT_MS ?? 120_000);

const startedAt = Date.now();

function log(message) {
  console.log(`[smoke] ${message}`);
}

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

async function delay(ms) {
  await new Promise((resolve) => setTimeout(resolve, ms));
}

async function waitFor(name, action) {
  let lastError;

  while (Date.now() - startedAt < timeoutMs) {
    try {
      const result = await action();
      if (result) {
        log(`${name} is ready`);
        return result;
      }
    } catch (error) {
      lastError = error;
    }

    await delay(2_000);
  }

  throw new Error(`${name} did not become ready in ${timeoutMs} ms. ${lastError?.message ?? ""}`.trim());
}

async function request(path, options = {}) {
  const headers = new Headers(options.headers);
  headers.set("Accept", options.accept ?? "application/json");

  if (options.body && typeof options.body === "string" && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  if (options.token) {
    headers.set("Authorization", `Bearer ${options.token}`);
  }

  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...options,
    headers
  });

  if (!response.ok) {
    const body = await response.text().catch(() => "");
    throw new Error(`${options.method ?? "GET"} ${path} failed with ${response.status}. ${body}`);
  }

  if (response.status === 204) {
    return undefined;
  }

  const contentType = response.headers.get("content-type") ?? "";
  if (contentType.includes("application/json")) {
    return await response.json();
  }

  return await response.text();
}

await waitFor("API readiness", async () => {
  const response = await fetch(`${apiBaseUrl}/health/ready`).catch(() => null);
  return response?.ok;
});

await waitFor("Frontend", async () => {
  const response = await fetch(webBaseUrl).catch(() => null);
  return response?.ok;
});

log("logging in as seeded development admin");
const auth = await request("/auth/login", {
  method: "POST",
  body: JSON.stringify({ email: adminEmail, password: adminPassword })
});

const token = auth.token;
assert(token, "Login did not return a JWT token.");

const currentUser = await request("/auth/me", { token });
assert(currentUser.email === adminEmail, "Current user does not match the seeded admin.");

const categories = await request("/tickets/categories", { token });
assert(Array.isArray(categories) && categories.length > 0, "No active ticket categories were seeded.");

const unique = new Date().toISOString();
const createdTicket = await request("/tickets", {
  method: "POST",
  token,
  body: JSON.stringify({
    title: `Docker smoke ticket ${unique}`,
    description: "Created by Docker Compose smoke automation.",
    categoryId: categories[0].id,
    priority: 1
  })
});
assert(createdTicket.id, "Ticket creation did not return an id.");
log(`created ticket ${createdTicket.number}`);

await request(`/tickets/${createdTicket.id}/assignee`, {
  method: "PATCH",
  token,
  body: JSON.stringify({ assignedToId: currentUser.id })
});

await request(`/tickets/${createdTicket.id}/comments`, {
  method: "POST",
  token,
  body: JSON.stringify({
    body: "Docker smoke public comment.",
    isInternal: false
  })
});

const attachmentText = `supportpilot smoke attachment ${unique}`;
const form = new FormData();
form.append("file", new Blob([attachmentText], { type: "text/plain" }), "smoke.txt");

const attachment = await request(`/tickets/${createdTicket.id}/attachments`, {
  method: "POST",
  token,
  body: form
});
assert(attachment.id, "Attachment upload did not return an id.");

const download = await request(`/tickets/${createdTicket.id}/attachments/${attachment.id}`, {
  token,
  accept: "text/plain"
});
assert(download === attachmentText, "Downloaded attachment content does not match uploaded content.");

await request(`/tickets/${createdTicket.id}/attachments/${attachment.id}`, {
  method: "DELETE",
  token
});

await request(`/tickets/${createdTicket.id}/status`, {
  method: "PATCH",
  token,
  body: JSON.stringify({
    status: 1,
    reason: "Docker smoke workflow transition."
  })
});

const detail = await request(`/tickets/${createdTicket.id}`, { token });
assert(detail.status === 1 || detail.status === "InProgress", "Ticket status was not changed to InProgress.");
assert(detail.comments?.some((comment) => comment.body === "Docker smoke public comment."), "Comment was not persisted.");
assert(!detail.attachments?.some((item) => item.id === attachment.id), "Deleted attachment still appears on ticket.");

await waitFor("RabbitMQ notification worker", async () => {
  const notifications = await request("/notifications", { token });
  return notifications.some((notification) => notification.ticketId === createdTicket.id);
});

log("Docker Compose smoke completed successfully");
