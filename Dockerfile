# syntax=docker/dockerfile:1

FROM node:22-alpine AS deps
WORKDIR /usr/src/app
COPY package.json package-lock.json ./
RUN npm ci --omit=dev

FROM node:22-alpine AS runtime

ARG VERSION=dev
ARG REVISION=unknown
LABEL org.opencontainers.image.title="prometheus-mssql-exporter" \
      org.opencontainers.image.description="Prometheus exporter for Microsoft SQL Server" \
      org.opencontainers.image.source="https://github.com/BoBoBaSs84/prometheus-mssql-exporter" \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.authors="Pierre Awaragi (pierre@awaragi.com), cobolbaby" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.revision="${REVISION}"

ENV NODE_ENV=production
WORKDIR /usr/src/app

COPY --from=deps /usr/src/app/node_modules ./node_modules
COPY package.json ./
COPY src ./src

USER node
EXPOSE 4000

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD node -e "fetch('http://localhost:'+(process.env.EXPOSE||4000)+'/healthz').then(r=>process.exit(r.ok?0:1)).catch(()=>process.exit(1))"

CMD ["node", "src/index.js"]
