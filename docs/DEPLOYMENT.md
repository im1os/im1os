# Deployment

The platform expects PostgreSQL and optionally Redis. API, web, and worker hosts are separate deployable processes.

Baseline deployment steps:

1. Configure production connection strings and JWT settings through environment variables or a secret manager.
2. Run EF Core migrations against PostgreSQL.
3. Start `iM1os.Api`, `iM1os.Web`, and `iM1os.Workers`.
4. Verify `/health` for API and web hosts.
5. Confirm logs are flowing to the configured sink.

Automatic migrations are disabled by default and controlled by `Database:AutoMigrate`.
