# pnpm v11 は Node.js 22+ が必要なため、node:22-alpine から Node.js バイナリをコピーする
FROM node:22-alpine AS node

FROM zenika/alpine-chrome:with-puppeteer-xvfb AS runner

ENV PNPM_HOME="/pnpm"
ENV PATH="$PNPM_HOME/bin:$PATH"

# hadolint ignore=DL3002
USER root

# Node.js 22 のバイナリおよびモジュールをコピーして既存の Node.js 20 を置き換える
# npm/npx/corepack はシンボリックリンクのため、node_modules ごとコピーしてリンクを再作成する
COPY --from=node /usr/local/bin/node /usr/local/bin/node
COPY --from=node /usr/local/lib/node_modules /usr/local/lib/node_modules

# /usr/bin/node および /usr/local/bin のシンボリックリンクを再作成する
RUN ln -sf /usr/local/bin/node /usr/bin/node && \
  ln -sf /usr/local/lib/node_modules/npm/bin/npm-cli.js /usr/local/bin/npm && \
  ln -sf /usr/local/lib/node_modules/npm/bin/npx-cli.js /usr/local/bin/npx && \
  ln -sf /usr/local/lib/node_modules/corepack/dist/corepack.js /usr/local/bin/corepack && \
  ln -sf /usr/local/bin/npm /usr/bin/npm && \
  ln -sf /usr/local/bin/npx /usr/bin/npx

# hadolint ignore=DL3018
RUN apk upgrade --no-cache --available && \
  apk update && \
  apk add --update --no-cache tzdata x11vnc && \
  cp /usr/share/zoneinfo/Asia/Tokyo /etc/localtime && \
  echo "Asia/Tokyo" > /etc/timezone && \
  apk del tzdata && \
  corepack enable

WORKDIR /app

COPY pnpm-lock.yaml package.json pnpm-workspace.yaml ./

RUN --mount=type=cache,id=pnpm,target=/pnpm/store pnpm fetch

COPY tsconfig.json ./
COPY src src

RUN --mount=type=cache,id=pnpm,target=/pnpm/store pnpm install --frozen-lockfile --offline

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
