// Production build target (5.11) — empty apiBase means every service call is a relative
// `/api/...` path, so it rides through Caddy's same-origin reverse proxy to the API
// (see deploy/Caddyfile) with no CORS configuration needed on the API side.
export const environment = {
  production: true,
  apiBase: '',
};
