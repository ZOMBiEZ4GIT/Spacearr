#!/bin/sh
set -e
PUID="${PUID:-1000}"; PGID="${PGID:-1000}"; UMASK="${UMASK:-002}"
if [ "$(id -u)" = "0" ]; then
  if [ "$(id -g spacearr)" != "$PGID" ]; then groupmod -o -g "$PGID" spacearr 2>/dev/null || sed -i "s/^spacearr:x:[0-9]*:/spacearr:x:$PGID:/" /etc/group; fi
  if [ "$(id -u spacearr)" != "$PUID" ]; then usermod -o -u "$PUID" spacearr 2>/dev/null || sed -i "s/^spacearr:x:[0-9]*:[0-9]*:/spacearr:x:$PUID:$PGID:/" /etc/passwd; fi
  chown -R "$PUID:$PGID" /config 2>/dev/null || true
  umask "$UMASK"
  echo "Spacearr starting as uid=$PUID gid=$PGID umask=$UMASK tz=${TZ}"
  exec su-exec "$PUID:$PGID" dotnet /app/Spacearr.dll
fi
umask "$UMASK"
exec dotnet /app/Spacearr.dll
