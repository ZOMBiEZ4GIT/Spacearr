# Security

Spacearr stores API keys for your Radarr and Sonarr, so it is a credential store and is treated as one.

- Authentication is mandatory on every route except the version/status check and the login/setup endpoints (`GET /api/v1/system/status`, `POST /api/v1/setup`, `POST /api/v1/auth/login`), plus the SPA shell and its static assets. A test enforces this in CI.
- Arr API keys are encrypted at rest (AES-256-GCM, key in `/config/secret.key`, mode 0600) and are never returned by the API.
- Five failed sign-ins within a minute lock out that account, and separately that source address, for a minute each.
- The container runs as the PUID/PGID user, not root.

**Reporting.** Use GitHub's private vulnerability reporting on this repository (Security tab → Report a vulnerability). You will get a reply within 72 hours. Please do not open a public issue for a vulnerability.
