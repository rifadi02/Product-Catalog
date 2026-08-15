# Build context is `frontend/`:
#   docker build -f frontend/Dockerfile --build-arg VITE_API_BASE_URL=... ./frontend

FROM node:22-alpine AS build
WORKDIR /app

# Manifest first, on its own layer. `npm ci` is the slow step and only needs to rerun when the
# lockfile changes — not on every source edit.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .

# Vite inlines import.meta.env.* at build time (src/api/client.ts), so this is baked into the
# bundle and CANNOT be changed when the container starts. It has to be an ARG, before the build.
ARG VITE_API_BASE_URL=http://localhost:8080/api/v1
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL

# `tsc -b && vite build` — the type-check runs first, so a type error fails the image build.
RUN npm run build

FROM nginx:1.27-alpine AS final

COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

# 127.0.0.1, not localhost: nginx.conf has a bare `listen 80`, which binds IPv4 only, while
# `localhost` inside the container resolves to ::1 first. BusyBox wget does not fall back to the
# IPv4 address, so the probe answered "Connection refused" against an nginx that was serving 200s
# perfectly well — and compose's `depends_on: service_healthy` then held the stack back.
HEALTHCHECK --interval=10s --timeout=3s --retries=5 --start-period=5s \
    CMD wget --quiet --tries=1 --spider http://127.0.0.1/ || exit 1

CMD ["nginx", "-g", "daemon off;"]
