FROM node:24-bookworm-slim AS build
WORKDIR /src

COPY package.json package-lock.json ./
COPY Frontend/radish.client/package.json Frontend/radish.client/package.json
COPY Frontend/radish.console/package.json Frontend/radish.console/package.json
COPY Frontend/radish.http/package.json Frontend/radish.http/package.json
COPY Frontend/radish.ui/package.json Frontend/radish.ui/package.json

RUN npm ci

COPY Frontend ./Frontend

RUN npm run build --workspace=radish.client
RUN npm run build --workspace=radish.console

FROM node:24-alpine AS final
WORKDIR /app
ENV NODE_ENV=production

RUN rm -rf \
    /opt/yarn* \
    /usr/local/bin/corepack \
    /usr/local/bin/npm \
    /usr/local/bin/npx \
    /usr/local/lib/node_modules/corepack \
    /usr/local/lib/node_modules/npm

COPY --from=build /src/Frontend/radish.client/dist ./client
COPY --from=build /src/Frontend/radish.console/dist ./console
COPY --from=build /src/Frontend/scripts/serve-static.mjs ./serve-static.mjs

EXPOSE 80
CMD ["node", "/app/serve-static.mjs"]
