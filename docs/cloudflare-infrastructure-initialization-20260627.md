# Cloudflare Infrastructure Initialization - 2026-06-27

Zone: `im1os.com`

Cloudflare zone id: `a41d1da3a5e8ed3d38c9d3bf55ad0216`

Audit export directory:

```text
artifacts/cloudflare-20260627174138
```

## Exports Created Before Changes

- `artifacts/cloudflare-20260627174138/zone.json`
- `artifacts/cloudflare-20260627174138/dns-records-before.json`
- `artifacts/cloudflare-20260627174138/dns-records-before.csv`
- `artifacts/cloudflare-20260627174138/zone-settings-audit.json`
- `artifacts/cloudflare-20260627174138/audit-summary.json`

Initial DNS records found:

| Record | Type | Target | Proxied |
| --- | --- | --- | --- |
| `saas.im1os.com` | A | `209.126.5.249` | No |
| `im1-dev.im1os.com` | A | `209.126.5.249` | No |
| `im1-db.im1os.com` | A | `209.126.12.91` | No |

## DNS Changes

Created proxied A records pointing to `209.126.5.249`:

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

Updated existing record:

- `saas.im1os.com` remains pointed at `209.126.5.249` and is now proxied through Cloudflare.

Preserved DNS-only records:

- `im1-dev.im1os.com` remains DNS-only for direct development server access.
- `im1-db.im1os.com` remains DNS-only because PostgreSQL must not be proxied through Cloudflare.

No records were removed.

No email records were modified.

No MX, SPF, DKIM, or DMARC records were modified.

## Post-Change Exports

- `artifacts/cloudflare-20260627174138/dns-records-after.json`
- `artifacts/cloudflare-20260627174138/dns-records-after.csv`
- `artifacts/cloudflare-20260627174138/dns-change-log.json`
- `artifacts/cloudflare-20260627174138/dns-change-log.csv`
- `artifacts/cloudflare-20260627174138/ssl-certificate-packs.json`

## SSL And Security Audit

Cloudflare SSL settings observed:

| Setting | Value |
| --- | --- |
| SSL mode | `full` |
| Universal SSL certificate status | `active` |
| Minimum TLS version | `1.0` |
| TLS 1.3 | `on` |
| Automatic HTTPS rewrites | `on` |
| Always Use HTTPS | `off` |
| Browser Integrity Check | `on` |
| Security level | `medium` |
| Challenge TTL | `1800` |

No SSL, security, WAF, firewall, or cache settings were changed.

## Resolution Verification

Cloudflare authoritative/API state confirms the created production records are proxied.

DNS resolution against `1.1.1.1` returned Cloudflare edge IPs for:

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

Existing direct records continue to resolve to their origin IPs:

- `im1-dev.im1os.com` -> `209.126.5.249`
- `im1-db.im1os.com` -> `209.126.12.91`

## Follow-Up

The API token used during this operation was pasted into chat. Rotate it in Cloudflare after this initialization work is complete, then update `.env.cloudflare` with the replacement token.
