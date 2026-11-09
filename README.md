# 🏁 RaceDay

A run, walk and cycle event platform. **Organisers** create events and age/distance categories and capture results; **Participants** enter events by choosing a category and view their results. Built as a three-part Portfolio of Evidence (POE).

| Part | Deliverable | Where |
|---|---|---|
| **Part 1** | ERD, API endpoint plan, SQL script | [`/docs`](docs) |
| **Part 2** | RESTful ASP.NET Core Web API, EF Core (code-first), session auth, Swagger, unit tests, GitHub Actions | [`/Part2_RaceDayApi`](Part2_RaceDayApi) |
| **Part 3** | ASP.NET Core MVC front end, Azure Blob Storage, Docker | [`/Part3_RaceDayMvc`](Part3_RaceDayMvc) |

## Roles

- **Organiser** – creates, updates and deletes their own events; defines age or distance categories (e.g. *Under 20*, *Senior*, *10km*, *21km*); views all enrolments for their events; captures finish times and finishing positions after the event.
- **Participant** – registers, manages their own profile, views events and categories, enters an event by selecting a category (age categories are checked against their age on event day), withdraws, and views their own results.

Both roles can view events, categories and event results. Everything else is restricted by role, and Organisers can only manage **their own** events.

## Part 1 – System planning (`/docs`)

- `raceday_erd.png` – ERD: `User`, `Event`, `Category`, `EventEnrolment`, `Result`.
- `raceday_api_endpoint_plan.pdf` – the 23-endpoint plan the API is built to.
- `raceday_schema.sql` – SQL Server script with constraints and seed data (2 organisers, 2 participants, 3 events, categories, enrolments, results). All seed users have the password `Password123!`.

## Part 2 – RESTful API

### Run it

Prerequisites: .NET 8 SDK and SQL Server (LocalDB, Express or full).

1. Check the connection string in `Part2_RaceDayApi/RaceDay.Api/appsettings.json` (defaults to `(localdb)\MSSQLLocalDB`, database `RaceDayDb`).
2. From the repository root:

```bash
cd Part2_RaceDayApi
dotnet run --project RaceDay.Api
```

3. Open **https://localhost:7100/swagger**. The database is created automatically from the EF Core model on first run (code-first). Optionally run `docs/raceday_schema.sql` in SSMS first to load the seed data – the schema matches the EF model.

### Test it

```bash
cd Part2_RaceDayApi
dotnet test
```

### Authentication and roles

- `POST /api/auth/register` – choose a role. **Organiser** registration needs the invite code from `Registration:OrganiserCode` in `appsettings.json` (default `RACEDAY-ORG-2026`), so users cannot simply promote themselves. Participants must supply a date of birth.
- `POST /api/auth/login` – verifies the password and starts a **session**: the server issues an HttpOnly cookie holding the user id and role, which is checked on every later request. `POST /api/auth/logout` ends it.
- Passwords are stored as salted PBKDF2-SHA256 hashes, never in plain text.
- No session → `401 Unauthorized`. Wrong role, or an Organiser acting on someone else's event → `403 Forbidden`.
- In Swagger UI, call **login** first; the browser then sends the session cookie automatically for the other endpoints.

### Endpoints

| Area | Endpoints |
|---|---|
| Auth | `POST /api/auth/register`, `POST /api/auth/login`, `POST /api/auth/logout` |
| Profile | `GET/PUT /api/users/me` |
| Events | `GET /api/events`, `GET /api/events/mine`, `GET /api/events/{id}`, `POST /api/events`, `PUT/DELETE /api/events/{id}` |
| Categories | `GET/POST /api/events/{eventId}/categories`, `PUT/DELETE /api/categories/{id}` |
| Enrolments | `POST/GET /api/events/{eventId}/enrolments`, `GET /api/enrolments/mine`, `DELETE /api/enrolments/{id}` |
| Results | `POST /api/enrolments/{enrolmentId}/result`, `PUT /api/results/{id}`, `GET /api/events/{eventId}/results`, `GET /api/results/mine` |

Each endpoint shows its purpose, expected data and responses in Swagger.

### Unit tests

xUnit tests boot the real API in memory (EF Core in-memory database) and cover success **and** failure paths for registration and login, event management by Organisers, role rejection (Participant ↔ Organiser), authenticated vs unauthenticated requests, categories, enrolments (including age eligibility and duplicates) and results.

## Part 3 – MVC front end, Azure Blob Storage and Docker

The MVC app (`Part3_RaceDayMvc/RaceDay.Web`) is a thin, role-aware UI. It has **no database access and no business logic**: every read and write is a call to the Part 2 API.

### Run everything with one command (Docker)

Prerequisite: Docker Desktop. From the repository root:

```bash
docker compose up --build
```

| What | URL |
|---|---|
| **RaceDay web app (MVC)** | http://localhost:8081 |
| API + Swagger | http://localhost:8080/swagger |

Compose starts the MVC container, the API, SQL Server and **Azurite** (the Azure Blob Storage emulator), so it works without an Azure account. Demo data is seeded on first start. Log in with the password `Password123!`:

| Role | Email |
|---|---|
| Organiser | `lerato.organiser@raceday.co.za` |
| Participant | `sipho.runner@raceday.co.za` (also `amy.runner@…`, `thandi.runner@…`) |

New Organiser registrations need the invite code `RACEDAY-ORG-2026`.

**Run just the MVC container** (when an API is already running on your machine on port 5100):

```bash
docker build -t raceday-web ./Part3_RaceDayMvc
docker run --rm -p 8081:8080 -e Api__BaseUrl=http://host.docker.internal:5100/ raceday-web
```

The containerised app behaves identically to running it locally.

### Run locally without Docker

1. Start the API (needs SQL Server LocalDB and [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite), or a real storage account):
   `dotnet run --project Part2_RaceDayApi/RaceDay.Api` (https://localhost:7100)
2. Start the web app: `dotnet run --project Part3_RaceDayMvc/RaceDay.Web` (https://localhost:7200)

> If you ran Part 2 before, drop the old `RaceDayDb` first (or re-run `docs/raceday_schema.sql`): Part 3 adds columns (`EventEnrolment.Status`, `Event.BannerBlob`, `User.ProfilePictureBlob`) and `EnsureCreated` does not alter existing tables.

### Using real Azure Blob Storage

Set `AzureBlob:ConnectionString` for the API (in `appsettings.json`, user-secrets, or the `AzureBlob__ConnectionString` environment variable in `docker-compose.yml`) to your storage account's connection string. Containers `event-banners` and `profile-pictures` are created automatically and are **private**.

### How the MVC app works

- **Session-based authentication.** On login the MVC app calls `POST /api/auth/login`, keeps the API's HttpOnly session cookie plus the user's id, name and role in a **server-side ASP.NET Core session**, and forwards that cookie on every API call. `[SessionAuthorize]` guards pages: not signed in → login page (with return URL); wrong role → access-denied page. Logout calls the API and clears the session.
- **Role-specific interfaces and navigation.** Organisers get *Dashboard* and *Manage Events*; Participants get *Events*, *My Enrolments* and *My Results*. Both have *Profile*.
- **Organiser:** dashboard (events, enrolments per event, upcoming dates); create/edit/delete events with banner upload; age/distance categories; enrolments per event with **Confirm / Set to pending**; results entry (finish time and position, published on save, only after the event date).
- **Participant:** upcoming events with filters (search, type, dates); event page with categories and results; enrol by choosing a category (age eligibility checked); personal enrolment list with colour-coded status (green *Confirmed*, amber *Pending*); *My Results* with event, date, category, time and position such as "47th out of 312 finishers in 10km"; profile page with picture upload.
- **Azure Blob Storage lives in the API.** Banner and profile-picture uploads go MVC → API (`POST /api/events/{id}/banner`, `POST /api/users/me/picture`) → Blob Storage. Images are validated by real file signature (JPG/PNG/GIF/WebP, max 5 MB). The browser fetches images through the MVC app (`/Media/...`), which streams them from the API, so **the MVC app never talks to Azure**.
- **Graceful errors.** Client- and server-side validation; API errors are turned into friendly messages; an unreachable API, expired session, 403 and 404 all show clean pages, never raw errors or stack traces.

### API changes made for Part 3

| Change | Why |
|---|---|
| `EventEnrolment.Status` (`Pending`/`Confirmed`) and `PUT /api/enrolments/{id}/status` | Colour-coded enrolment statuses; organiser confirms entries |
| `POST/GET /api/events/{id}/banner`, `POST/GET /api/users/me/picture` | Azure Blob Storage uploads |
| `EnrolmentCount` on events; `EventDate`/`Status`/location on enrolments; `EventDate`/`TotalFinishers` on results | Dashboard counts and "47th out of 312" |
| Optional demo data (`Seed:DemoData`), SQL start-up retry, `docker-compose.yml`, API Dockerfile | One-command start |
| New xUnit tests (blob upload/validation/permissions, enrolment status); Blob Storage replaced by an in-memory fake in tests | Keep the Part 2 test suite covering new endpoints |

## CI/CD

`.github/workflows/ci.yml` restores, builds and tests the API, **builds the MVC project**, and builds both Docker images on every push and pull request.

![Successful green GitHub Actions build](docs/ci-green-build.png)

## Video walkthrough

[Part 3 demonstration on YouTube (unlisted)](YOUR_YOUTUBE_LINK_HERE)

## Tech stack

ASP.NET Core 8 MVC and Web API · Bootstrap 5 · Entity Framework Core (SQL Server) · Azure Blob Storage (Azurite locally) · Docker and Docker Compose · xUnit · GitHub Actions
