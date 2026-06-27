# Cloudflare DNS Import Notes

Zone: `im1os.com`

Import file:

```text
docs/cloudflare-dns-import-im1os.zone
```

Records created:

| Hostname | Type | Target | Purpose |
| --- | --- | --- | --- |
| `saas.im1os.com` | A | `209.126.5.249` | IM1 OS app server |
| `im1-dev.im1os.com` | A | `209.126.5.249` | Direct app server alias |
| `im1-db.im1os.com` | A | `209.126.12.91` | Direct database server alias |

Cloudflare proxy guidance:

- `saas.im1os.com`: can be proxied after Nginx and TLS are configured.
- `im1-dev.im1os.com`: keep DNS-only unless there is a reason to expose it through Cloudflare.
- `im1-db.im1os.com`: keep DNS-only. Do not expose PostgreSQL through Cloudflare proxy.

Security note:

DNS does not open PostgreSQL to the internet. The database server firewall should continue allowing PostgreSQL port `5432` only from the app server IP `209.126.5.249`.
