# Cloudflare DNS Import Notes

Zone: `im1os.com`

Import file:

```text
docs/cloudflare-dns-import-im1os.zone
```

Initial bootstrap records:

| Hostname | Type | Target | Purpose |
| --- | --- | --- | --- |
| `saas.im1os.com` | A | `209.126.5.249` | IM1 OS app server |
| `im1-dev.im1os.com` | A | `209.126.5.249` | Direct app server alias |
| `im1-db.im1os.com` | A | `209.126.12.91` | Direct database server alias |

Production initialization was completed on 2026-06-27. See:

```text
docs/cloudflare-infrastructure-initialization-20260627.md
```

Production records now include:

- `im1os.com`
- `www.im1os.com`
- `platform.im1os.com`
- `app.im1os.com`
- `api.im1os.com`
- `identity.im1os.com`
- `portal.im1os.com`
- `pay.im1os.com`
- `market.im1os.com`
- `docs.im1os.com`
- `status.im1os.com`
- `dev.im1os.com`
- `staging.im1os.com`
- `sandbox.im1os.com`
- `cdn.im1os.com`
- `files.im1os.com`
- `hooks.im1os.com`
- `ai.im1os.com`
- `internal.im1os.com`

Cloudflare proxy guidance:

- Production application records should be proxied through Cloudflare.
- `saas.im1os.com`: proxied app server alias.
- `im1-dev.im1os.com`: keep DNS-only unless there is a reason to expose it through Cloudflare.
- `im1-db.im1os.com`: keep DNS-only. Do not expose PostgreSQL through Cloudflare proxy.

Security note:

DNS does not open PostgreSQL to the internet. The database server firewall should continue allowing PostgreSQL port `5432` only from the app server IP `209.126.5.249`.
