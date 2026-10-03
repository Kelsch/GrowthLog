# GrowthLog

GrowthLog is a self-hosted web app for tracking children's growth over time.
Record height and weight measurements, compare people at the same age, and
view growth charts — all on your own server, with your own data.

## Features

- **People & families** — organize people into families, with roles and
  sharing between families.
- **Measurements** — record height and weight; storage is always metric
  (cm / kg), display follows each user's preferred units (Imperial or Metric).
- **Dashboard** — a by-age bar chart comparing people in a family at a
  selected age, with Height / Weight / Both modes (Both = height bars plus a
  weight line).
- **Compare** — cross-family comparison with multi-select family chips,
  clickable person toggles, and the same by-age / Both chart.
- **Due reminders** — see who is due or overdue for their next measurement.
- **Auth** — ASP.NET Core Identity with per-family authorization.

## Stack

- **Blazor Server** (.NET 10)
- **Dapper** for application data access
- **SQLite** for storage
- **ASP.NET Core Identity** (EF Core) for authentication

## Quick start with Docker Compose

1. Copy `docker-compose.yml` to your server and set the image to your
   published image (see below).
2. Start the stack:

   ```bash
   docker compose up -d
   ```

3. Open `http://<server>:8080` and register the first account.

The SQLite database is stored in the `growthlog-data` named volume at
`/data/growthlog.db`, so it survives container restarts and upgrades.

## Environment variables

| Variable | Default | Purpose |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` | ASP.NET Core environment. |
| `ConnectionStrings__GrowthLog` | `Data Source=/data/growthlog.db` | SQLite connection string. |
| `ASPNETCORE_URLS` | `http://+:8080` | Listen address inside the container. |

## Volume

| Path | Purpose |
| --- | --- |
| `/data` | SQLite database file (`growthlog.db`). Back this up. |

## Updates

Images are built and pushed to GitHub Container Registry (GHCR) by the
workflow in `.github/workflows/docker-publish.yml` on every push to `main`.

On the server:

```bash
docker compose pull
docker compose up -d
```

The database volume is untouched, so data is preserved across updates.

## Local development

```bash
dotnet run --project src/GrowthLog.Web/GrowthLog.Web.csproj
```

The app listens on the URL from `Properties/launchSettings.json` and uses
`growthlog.db` in the project directory by default.

## First-run migrations / Identity

On startup the app:

1. Runs `EnsureCreatedAsync()` for the Identity (EF Core) schema.
2. Runs the Dapper SQL migrations in `src/GrowthLog.Web/db/migrations`.

Both are idempotent, so the first run creates the schema and later runs are
no-ops. Register the first account after the app starts.

## Building the image locally

```bash
docker build -t growthlog:local .
```

Then point `docker-compose.yml` at `growthlog:local` (or uncomment the
`build:` block) and run `docker compose up -d`.

## Secrets

Do not commit secrets. The publish workflow uses the built-in
`GITHUB_TOKEN`; no extra tokens are required. Use placeholders for any
registry credentials you add.
