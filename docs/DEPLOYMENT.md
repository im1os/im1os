# Deployment

The platform expects PostgreSQL and optionally Redis. API, web, and worker hosts are separate deployable processes.

## Standard Procedures

Use `Deploy Dev` when validating local changes on the Dev server before committing them to Git. Use `Deploy Platform` when the change is ready to become the shared platform revision: it commits and pushes the Git change first, then deploys that committed revision to the Dev servers.

### Deploy Dev

Purpose: push the current working changes to the Dev server for validation without creating a Git commit.

Preconditions:

1. Confirm the local application builds and the intended tests pass.
2. Confirm the Dev server is available.
3. Confirm the local working tree contains only changes intended for this Dev deployment.
4. Record any database migration, configuration, or secret changes needed by the Dev server.

Procedure:

1. Review the pending file changes.
2. Build the solution locally.
3. Run the relevant automated tests.
4. Package or publish the deployable projects for the Dev environment.
5. Push the published API, web, and worker outputs to the Dev server.
6. Apply required Dev database migrations.
7. Restart the affected Dev services.
8. Verify API and web health checks.
9. Smoke test the changed workflow on the Dev server.
10. Record the deployment result, including the local source state, migration status, and any follow-up fixes.

Rollback:

1. Restore the previous Dev server package or redeploy the last known good build.
2. Revert any Dev-only configuration changes.
3. If a migration must be reversed, use the approved database rollback plan for that migration.
4. Restart affected services and verify health checks.

### Deploy Platform

Purpose: commit and push platform changes to Git, then deploy the committed revision to the Dev servers.

Preconditions:

1. Confirm the local application builds and the full relevant test suite passes.
2. Confirm the working tree contains only changes intended for the platform commit.
3. Confirm the commit message describes the platform change clearly.
4. Confirm database migrations, configuration changes, and operational notes are documented.
5. Confirm the Dev servers are available for deployment.

Procedure:

1. Review the complete Git diff.
2. Build the solution locally.
3. Run the relevant automated tests.
4. Stage only the intended files.
5. Commit the staged changes to Git.
6. Push the commit to the remote repository.
7. Confirm the pushed branch and commit SHA.
8. Package or publish the API, web, and worker projects from the committed revision.
9. Push the published outputs to the Dev servers.
10. Apply required Dev database migrations.
11. Restart the affected Dev services.
12. Verify API and web health checks.
13. Smoke test the changed platform workflow on the Dev servers.
14. Record the deployment result, including the commit SHA, migration status, and any follow-up fixes.

Rollback:

1. Revert or supersede the Git commit according to the repository's branch policy.
2. Redeploy the last known good committed revision to the Dev servers.
3. Revert configuration changes if required.
4. If a migration must be reversed, use the approved database rollback plan for that migration.
5. Restart affected services and verify health checks.

## Baseline Steps

Baseline deployment steps:

1. Configure production connection strings and JWT settings through environment variables or a secret manager.
2. Run EF Core migrations against PostgreSQL.
3. Start `iM1os.Api`, `iM1os.Web`, and `iM1os.Workers`.
4. Verify `/health` for API and web hosts.
5. Confirm logs are flowing to the configured sink.

Automatic migrations are disabled by default and controlled by `Database:AutoMigrate`.
