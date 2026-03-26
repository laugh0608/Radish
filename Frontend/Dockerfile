FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

RUN apt-get update \
    && apt-get install -y curl ca-certificates gnupg \
    && curl -fsSL https://deb.nodesource.com/setup_24.x | bash - \
    && apt-get install -y nodejs \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

COPY package.json package-lock.json ./
COPY Frontend/radish.client/package.json Frontend/radish.client/package.json
COPY Frontend/radish.console/package.json Frontend/radish.console/package.json
COPY Frontend/radish.http/package.json Frontend/radish.http/package.json
COPY Frontend/radish.ui/package.json Frontend/radish.ui/package.json

RUN npm ci

COPY Frontend ./Frontend

RUN npm run build --workspace=radish.client
RUN npm run build --workspace=radish.console

FROM build AS final
WORKDIR /app
RUN mkdir -p /app/client /app/console \
    && cp -r /src/Frontend/radish.client/dist/. /app/client \
    && cp -r /src/Frontend/radish.console/dist/. /app/console \
    && cp /src/Frontend/scripts/serve-static.mjs /app/serve-static.mjs
EXPOSE 80
CMD ["node", "/app/serve-static.mjs"]
