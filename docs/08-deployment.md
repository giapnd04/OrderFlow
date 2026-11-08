# 08 - Deployment, Docker and CI (OF-11)

## What is here

The repository contains a multi-stage Dockerfile for the API, a docker-compose.yml that runs the API together with SQL Server, and a GitHub Actions workflow that builds and tests every push and pull request. This document explains how they fit together and what is still unverified.

## Dockerfile

The build stage uses the .NET 9 SDK image. It copies only the four project files first and runs restore, so the restore layer is cached until a package reference changes. Then it copies the source and publishes in Release mode. The final stage uses the much smaller ASP.NET runtime image, copies only the published output, listens on the unprivileged port 8080 and runs as the non-root user the base image provides. The SDK, the source code and the intermediate build files never reach the final image.

The .dockerignore keeps bin, obj, .git, tests, docs and any .env file out of the build context.

## docker-compose

There are two services. The sqlserver service runs SQL Server 2022 with its data directory on the named volume sqldata, and has a healthcheck that runs sqlcmd SELECT 1. The api service is built from the Dockerfile and waits for the sqlserver healthcheck to pass before it starts (depends_on with service_healthy). It has its own healthcheck that calls /health/ready. The aspnet image has no curl, so the check talks HTTP through bash /dev/tcp.

Configuration comes from environment variables, using the double underscore form that ASP.NET Core maps to nested settings (ConnectionStrings__OrderFlow, Jwt__SigningKey, Database__MigrateOnStartup, Bootstrap__AdminEmail). The values come from a local .env file. The repository only contains .env.example with placeholders, and .env is git-ignored, so no real secret is committed. Compose refuses to start if MSSQL_SA_PASSWORD or JWT_SIGNING_KEY is missing.

To run it: copy .env.example to .env, set real values, run docker compose up --build, then call GET http://localhost:8080/health/ready. It should return 200 once migrations have been applied.

## Data and containers

An image is the read-only template built from the Dockerfile. A container is a running (or stopped) instance of an image with a thin writable layer on top. If a container is removed, anything written only in that writable layer is lost. The database files live on the sqldata volume, which is managed outside the container, so removing and recreating the containers keeps the data. Only docker compose down -v (or deleting the volume) removes it.

## Migrations: decision and trade-off

The API does not migrate by default. The setting Database:MigrateOnStartup is false in appsettings.json and compose sets it to true. When it is true, the API applies pending EF Core migrations before it seeds the optional bootstrap administrator.

Options considered. First, migrate at API startup behind a flag: one command to get a working stack, no extra image, but the API connection must then be allowed to change the schema, and if several API instances start together they race to migrate. Second, a separate migration job (an EF migrations bundle run as its own compose service or deployment step): the API can keep a least-privilege login and migrations run once, but there is one more image or step to maintain.

Decision: the flag, for local development and the demo stack. Consequence: in compose the API connects as sa, which is the opposite of the least-privilege login described in OF-07 and db/scripts/create-app-user.sql. A real deployment should run migrations as a separate job under an owner account and keep the API on the restricted login.

## CI

The workflow .github/workflows/ci.yml runs on every push to main or master and on every pull request. Steps: checkout, install .NET 9, restore, build in Release, unit tests, integration tests. The integration tests start SQL Server themselves through Testcontainers, which uses the Docker daemon already present on the GitHub ubuntu runner, so no service container is declared in the workflow. A second job builds the Docker image after the tests pass, so a broken Dockerfile fails the pipeline. The badge at the top of the README shows the state of this workflow.

## Not verified yet

The development machine has no Docker, so the image build, docker compose up from a clean clone, the container healthchecks and the Testcontainers path were not run locally. They are checked by CI and by running compose on a machine with Docker. Update this section with the real results after the first run.

## Known gaps

No image is pushed to a registry and there is no CD step. HTTPS is not terminated inside the container (HTTP on 8080 only), so a real deployment needs a reverse proxy or load balancer in front. The compose password and JWT key are for local use only.
