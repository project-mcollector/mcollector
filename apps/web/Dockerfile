FROM node:22-alpine AS base
ENV PNPM_HOME="/pnpm"
ENV PATH="$PNPM_HOME:$PATH"
# Enable corepack for pnpm
RUN corepack enable

FROM base AS builder
RUN apk add --no-cache libc6-compat
# Set working directory
WORKDIR /app
# Install turbo globally
RUN npm install turbo --global

# Copy only necessary files for pruning
COPY . .
# Prune the workspace for the web app
RUN turbo prune --scope=web --docker

# Add lockfile and package.json's of isolated subproject
FROM base AS installer
RUN apk add --no-cache libc6-compat
WORKDIR /app

# First install the dependencies (as they change less often)
COPY .gitignore .gitignore
COPY --from=builder /app/out/json/ .
COPY --from=builder /app/out/pnpm-lock.yaml ./pnpm-lock.yaml
# Install dependencies
RUN pnpm install --frozen-lockfile

# Build the project
COPY --from=builder /app/out/full/ .
COPY turbo.json turbo.json
RUN pnpm turbo run build --filter=web...

FROM base AS runner
WORKDIR /app

# Don't run production as root
RUN addgroup --system --gid 1001 nodejs
RUN adduser --system --uid 1001 nextjs
USER nextjs

COPY --from=installer /app/apps/web/public ./apps/web/public

# Automatically leverage output traces to reduce image size
# https://nextjs.org/docs/advanced-features/output-file-tracing
COPY --from=installer --chown=nextjs:nodejs /app/apps/web/.next/standalone ./
COPY --from=installer --chown=nextjs:nodejs /app/apps/web/.next/static ./apps/web/.next/static

EXPOSE 3000

ENV PORT 3000
ENV HOSTNAME "0.0.0.0"

CMD ["node", "apps/web/server.js"]
