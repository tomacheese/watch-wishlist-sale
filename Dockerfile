FROM zenika/alpine-chrome:with-puppeteer-xvfb AS runner

ENV PNPM_HOME="/pnpm"
ENV PATH="$PNPM_HOME/bin:$PATH"

# hadolint ignore=DL3002
USER root

# hadolint ignore=DL3018,DL3016
RUN apk upgrade --no-cache --available && \
  apk update && \
  apk add --update --no-cache tzdata x11vnc && \
  cp /usr/share/zoneinfo/Asia/Tokyo /etc/localtime && \
  echo "Asia/Tokyo" > /etc/timezone && \
  apk del tzdata && \
  npm install -g corepack@latest && \
  pnpm approve-builds && \
  corepack enable

WORKDIR /app

COPY pnpm-lock.yaml ./

RUN --mount=type=cache,id=pnpm,target=/pnpm/store pnpm fetch

COPY package.json tsconfig.json ./
COPY src src

RUN --mount=type=cache,id=pnpm,target=/pnpm/store pnpm install --frozen-lockfile --offline && \
  pnpm approve-builds

COPY entrypoint.sh ./
RUN chmod +x ./entrypoint.sh

ENV TZ=Asia/Tokyo
ENV NODE_ENV=production
ENV CONFIG_PATH=/data/config.json
ENV CHROMIUM_PATH=/usr/bin/chromium-browser
ENV LOG_DIR=/data/logs/
ENV APP_IDS_PATH=/data/appIds.json
ENV NOTIFIED_PATH=/data/notified.json

ENTRYPOINT ["tini", "--"]
CMD ["/app/entrypoint.sh"]
