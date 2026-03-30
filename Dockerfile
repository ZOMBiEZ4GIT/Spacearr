# Stage 1: Build frontend
FROM node:20-alpine AS frontend
WORKDIR /build
COPY package.json yarn.lock* ./
RUN npm install --legacy-peer-deps
COPY frontend/ frontend/
COPY tsconfig.json ./
RUN npm run build

# Stage 2: Build backend
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend
WORKDIR /build
COPY src/ src/
COPY global.json ./
RUN dotnet publish src/NzbDrone.Console/Spacearr.Console.csproj \
    -c Release \
    -o /app \
    --no-self-contained \
    -f net8.0

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
      libicu-dev \
      sqlite3 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=backend /app .
COPY --from=frontend /build/_output/UI /app/UI

EXPOSE 8787
VOLUME ["/config", "/media"]

ENV SPACEARR__PORT=8787

ENTRYPOINT ["dotnet", "Spacearr.Console.dll", "--nobrowser", "--data=/config"]
