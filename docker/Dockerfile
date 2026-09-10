# syntax=docker/dockerfile:1.7
FROM --platform=$BUILDPLATFORM node:20-alpine AS web
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY web/ ./
RUN npm run build

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
ARG TARGETARCH
ARG VERSION=0.0.0
WORKDIR /src
COPY global.json Directory.Build.props Spacearr.sln NuGet.config ./
COPY src/Spacearr/Spacearr.csproj src/Spacearr/
COPY src/Spacearr.Tests/Spacearr.Tests.csproj src/Spacearr.Tests/
RUN dotnet restore src/Spacearr/Spacearr.csproj -a $TARGETARCH
COPY src/ src/
COPY --from=web /src/Spacearr/wwwroot src/Spacearr/wwwroot
RUN dotnet publish src/Spacearr/Spacearr.csproj -c Release -a $TARGETARCH -o /app -p:SkipWeb=true -p:UseAppHost=false -p:Version=$VERSION

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
LABEL org.opencontainers.image.title="Spacearr" \
      org.opencontainers.image.description="The cooler looking WinDirStat for your arr stack" \
      org.opencontainers.image.source="https://github.com/ZOMBiEZ4GIT/Spacearr" \
      org.opencontainers.image.licenses="GPL-3.0"
RUN apk add --no-cache ffmpeg mediainfo su-exec tzdata \
    && addgroup -g 1000 spacearr && adduser -D -u 1000 -G spacearr spacearr
ENV SPACEARR_CONFIG_DIR=/config SPACEARR_PORT=8787 PUID=1000 PGID=1000 UMASK=002 TZ=Etc/UTC \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 ASPNETCORE_ENVIRONMENT=Production
WORKDIR /app
COPY --from=build /app ./
COPY docker/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh && mkdir -p /config && chown spacearr:spacearr /config
VOLUME /config
EXPOSE 8787
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s CMD wget -qO- http://127.0.0.1:8787/api/v1/system/status >/dev/null || exit 1
ENTRYPOINT ["/entrypoint.sh"]
