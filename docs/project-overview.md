# HandyTool — how the project works

A working reference for the whole system as it stands: what the database holds, what the backend
does, and how the frontend uses it.

Companion documents go deeper on two areas:
[tracking-and-auth.md](tracking-and-auth.md) · [localization.md](localization.md) ·
[dynamic-objects.md](dynamic-objects.md)

---

## 1. The core idea

Users design their own data structures at runtime. "Property Inspection", "Customer Survey",
"Equipment Log" — each is a set of **metadata rows**, not a table and not a C# class.

```
ObjectDefinition        "Property Inspection"        ← the schema the user designed
   └── FieldDefinition  "Damage Type" (key: damageType, type: Dropdown)
          └── FieldOption  "Water Damage" (value: water)

ObjectRecord            one filled-in inspection      ← the data
   └── Values  {"damageType": "water", "conditionScore": 8}   (jsonb)
```

**Every record of every user-defined type lives in one table**, `ObjectRecords`, with its variable
part in a single `jsonb` column. Adding a new object type creates rows, never DDL.

The consequence that matters most: `FieldDefinition.Key` and `FieldOption.Value` are stable machine
identifiers, kept separate from the human-readable `Name` and `Label`. A record stores `"water"`, not
`"Water Damage"`. That is what makes renaming and translating labels free.

---

## 2. Deployment shape

```
                    Internet
                       │
              reverse proxy / CDN
                       │
            https://handytool.org
             ┌─────────┴─────────┐
       /  ───┤                   ├─── /api/*
     Next.js │                   │   ASP.NET Core
             └───────────────────┘
```

One origin from the browser's point of view. That single fact decides a great deal:

- the browser calls `/api/track` with a **relative URL**,
- `visitor_id` and `session_token` are ordinary first-party `SameSite=Lax` cookies,
- **there is no CORS configuration anywhere**, and none is needed,
- `SameSite=None` is never used.

In development there is no proxy, so `next.config.ts` rewrites `/api/*` to `http://localhost:5292`.
The browser still sees one origin, so cookies behave identically and no application code differs
between environments.

Future iOS and Android apps will call the same `https://handytool.org/api/*` directly. That is why
authentication, tracking and identity all live in ASP.NET Core and none of it lives in a Next.js route
handler.

### Repositories

| Path | What it is |
|---|---|
| `D:\pra\handytool-api` | ASP.NET Core 10 minimal API, EF Core 10, PostgreSQL 18, Serilog |
| `D:\pra\handytool` | Next.js 16 / React 19 / TypeScript 5 / Tailwind 4 |

---

## 3. Running it

```bash
dotnet ef database update --project handytool-api.csproj
```

```bash
dotnet run --project handytool-api.csproj
```

```bash
npm run dev
```

API on `http://localhost:5292`, Swagger UI at `/swagger`, website on `http://localhost:3000`.
`handytool-api.http` exercises every endpoint.

> `dotnet ef` does **not** work in Visual Studio's Package Manager Console — that console lacks
> `%USERPROFILE%\.dotnet\tools` on its PATH, and `Update-Database` needs the
> `Microsoft.EntityFrameworkCore.Tools` package, which this project does not reference. Use a normal
> terminal.

---

## 4. Database

PostgreSQL 18, UTF8. Twelve tables in three groups. `varchar(n)` counts **characters, not bytes**, so
every length limit below is 200 Chinese characters as readily as 200 Latin ones.

### 4.1 User-defined data

**`ObjectDefinitions`** — one user-designed object type.

| Column | Type | Notes |
|---|---|---|
| `Id` | bigint identity | |
| `UserId` | bigint | → `UserAccounts`, `RESTRICT` |
| `Name` | varchar(200) | canonical, default language |
| `NameTranslations` | jsonb | `{"zh-Hans": "房产检查"}` |
| `Description` | varchar(2000) | |
| `DescriptionTranslations` | jsonb | |
| `IsActive` | bool | inactive blocks new records |
| `CreatedDate` / `ModifiedDate` | timestamptz | |

Indexes: `UserId` · **unique** `(UserId, Name)` — one account cannot have two definitions of the same
name.

**`FieldDefinitions`** — one field on a definition.

| Column | Type | Notes |
|---|---|---|
| `ObjectDefinitionId` | bigint | → `ObjectDefinitions`, `CASCADE` |
| `Key` | varchar(100) | **machine identifier**, the jsonb property name. Never translated. |
| `Name` / `NameTranslations` | varchar(200) / jsonb | the human label |
| `Description` / `DescriptionTranslations` | varchar(2000) / jsonb | |
| `FieldType` | varchar(32) | stored as a string |
| `IsRequired`, `IsActive`, `DisplayOrder` | | |
| `Settings` | jsonb | type-specific: min, max, step, placeholder |

Indexes: `ObjectDefinitionId` · **unique** `(ObjectDefinitionId, Key)`.

`FieldType` is one of: `Text`, `LongText`, `Integer`, `Decimal`, `Boolean`, `Date`, `DateTime`,
`Range`, `Dropdown`, `MultiSelect`.

**`FieldOptions`** — one dropdown/multi-select choice.

| Column | Type | Notes |
|---|---|---|
| `FieldDefinitionId` | bigint | → `FieldDefinitions`, `CASCADE` |
| `Value` | varchar(100) | **machine identifier**, what lands in record jsonb. Never translated. |
| `Label` / `LabelTranslations` | varchar(200) / jsonb | the human label |
| `DisplayOrder`, `IsActive` | | |

Indexes: `FieldDefinitionId` · **unique** `(FieldDefinitionId, Value)`.

**`ObjectRecords`** — the actual data. One table for every object type in the system.

| Column | Type | Notes |
|---|---|---|
| `ObjectDefinitionId` | bigint | → `ObjectDefinitions`, **`RESTRICT`** |
| `UserId` | bigint | → `UserAccounts`, `RESTRICT` |
| `Title` | varchar(300) | |
| `Description` | varchar(4000) | |
| `Values` | jsonb | keyed by `FieldDefinition.Key`; values only, never labels or metadata |

Indexes: `ObjectDefinitionId` · `UserId` · `CreatedDate`.
Check: `jsonb_typeof("Values") = 'object'`.

> `RESTRICT` on the definition is deliberate: **user data must never disappear because metadata was
> deleted.**

### 4.2 Authentication

**`UserAccounts`**

| Column | Type | Notes |
|---|---|---|
| `Email` | varchar(320) | stored lowercase, **unique** |
| `PasswordHash` | varchar(200) | base64 PBKDF2 derived key |
| `PasswordSalt` | varchar(100) | base64, **per account** |
| `DisplayName` | varchar(200) | |
| `PreferredLanguage` | varchar(10) null | null = follow the browser |
| `IsActive` | bool | false blocks sign-in and kills existing sessions |

**`UserSessions`** — one signed-in device.

| Column | Type | Notes |
|---|---|---|
| `Id` | uuid | app-assigned |
| `UserId` | bigint | → `UserAccounts`, `CASCADE` |
| `TokenHash` | varchar(100) | SHA-256 of the token. **The raw token is never stored.** |
| `ClientType` | varchar(20) | `Web` / `Ios` / `Android` |
| `DeviceName`, `UserAgent` | | informational, for "your devices" |
| `CreatedDate`, `LastUsedDate`, `ExpiresDate` | timestamptz | |
| `RevokedDate` | timestamptz null | set by logout; irreversible |

Indexes: **unique `TokenHash`** — every authenticated request is one lookup here, so this index
carries the load of the entire auth system · `(UserId, RevokedDate)` · `ExpiresDate`.

**`AuthThrottles`** — consecutive sign-in failures.

| Column | Type | Notes |
|---|---|---|
| `Scope` | varchar(20) | `Ip` or `Account` |
| `KeyHash` | varchar(100) | **SHA-256 of the address or email** |
| `FailedCount` | int | |
| `FirstFailedAt`, `LastFailedAt` | timestamptz | |
| `LockedUntil` | timestamptz null | always bounded, never permanent |

Indexes: **unique `(Scope, KeyHash)`** · `LastFailedAt`.

Deliberately **no foreign key** to `UserAccounts`: the account scope must count attempts against
emails that were never registered, or the table itself would answer "does this account exist". Keys
are hashed for the same reason — and because IP addresses are personal data.

### 4.3 Analytics

**`TrackingClients`** — one browser or device.

| Column | Type | Notes |
|---|---|---|
| `Id` | uuid, app-assigned | **for web, this is the VisitorId** from the cookie |
| `ClientType` | varchar(20) | `Web` / `Ios` / `Android` |
| `FirstSeenDate`, `LastSeenDate`, `CreatedDate` | | |

**`TrackingSessions`** — one period of browsing.

| Column | Type | Notes |
|---|---|---|
| `Id` | uuid | the SessionId on every event |
| `ClientId` | uuid | → `TrackingClients`, `CASCADE` |
| `StartedAt`, `LastActivityAt`, `EndedAt` | | |

Index: `(ClientId, LastActivityAt)` — the "is there a live session?" lookup on every tracked request.

**`TrackingClientUsers`** — "this browser turned out to belong to this account."

| Column | Type | Notes |
|---|---|---|
| `ClientId` | uuid | → `TrackingClients`, `CASCADE` |
| `UserId` | bigint | → `UserAccounts`, `CASCADE` |
| `FirstIdentifiedAt`, `LastIdentifiedAt` | | |

Indexes: **unique `(ClientId, UserId)`** · `UserId`.

Many-to-many in both directions. One user has many browsers — a laptop, a phone, incognito. One
browser may have had several users — a shared machine.

**`AnalyticsEvents`** — what happened. Rows here are **immutable**.

| Column | Type | Notes |
|---|---|---|
| `ClientId` | uuid | → `TrackingClients`, **`RESTRICT`** |
| `ClientType` | varchar(20) | denormalised, so web/native splits need no join |
| `SessionId` | uuid | → `TrackingSessions`, **`RESTRICT`** |
| `UserId` | bigint null | **the account at the time — null stays null forever** |
| `EventType` | varchar(50) | a string, not an enum |
| `Path` | varchar(500) | locale prefix removed, query string stripped |
| `Timestamp` | timestamptz | |
| `ActiveSeconds` | int null | browser-measured, server-clamped |
| `Language`, `Referrer`, `UserAgent` | | |

Indexes: `(UserId, Timestamp)` · `(ClientId, Timestamp)` · `(SessionId, Timestamp)` · `EventType` ·
`Timestamp` · `Language`.
Check: `ActiveSeconds IS NULL OR ActiveSeconds >= 0`.

`EventType` is a varchar with constants in `AnalyticsEventTypes`, not an enum — adding
`property_viewed` or `contact_form_submitted` is one line and **no migration**.

### 4.4 Relationships

```
UserAccounts ──< UserSessions                     (authentication)
     │
     ├──< ObjectDefinitions ──< FieldDefinitions ──< FieldOptions
     │            │
     │            └──< ObjectRecords
     │
     ├──< TrackingClientUsers >── TrackingClients ──< TrackingSessions
     │                                    │                  │
     └───────────────< AnalyticsEvents >──┴──────────────────┘

AuthThrottles     (no FK, by design)
```

### 4.5 Migrations

| Migration | What it did |
|---|---|
| `InitialDynamicObjectModel` | definitions, fields, options, records |
| `AddAuthenticationAndWebsiteTracking` | accounts, sessions, tracking tables; `OwnerId` → `UserId` |
| `AddAuthThrottling` | `AuthThrottles` |
| `AddLanguagePreferences` | `AnalyticsEvents.Language`, `UserAccounts.PreferredLanguage` |
| `AddLabelTranslations` | the five jsonb translation columns |

---

## 5. Backend

### 5.1 Layout

```
Configuration/   strongly-typed options bound from appsettings.json
Contracts/       request and response records (the wire format)
Data/            DbContext, per-entity EF configurations, migrations
Endpoints/       minimal-API route groups
Localization/    translation resolution
Logging/         exception handler, security log
Models/          EF entities
OpenApi/         Swagger document and schema transformers
Security/        auth, cookies, hashing, rate limiting, identity resolution
Services/        business logic (auth, tracking, throttling)
Validation/      dynamic record-value validation
```

### 5.2 Request pipeline

Order in `Program.cs`, and why:

1. **`UseForwardedHeaders`** — first, because everything downstream that cares who is calling reads
   `Connection.RemoteIpAddress`, and it must already be the real client rather than the proxy.
2. **`UseExceptionHandler`** — `GlobalExceptionHandler` logs once with context, returns
   `ProblemDetails`; in Development it declines to handle so the developer page still renders.
3. **`UseSerilogRequestLogging`** — one line per request, `Debug` normally, `Warning` past 2s,
   `Error` on 5xx. 4xx is deliberately silent: endpoints log their own reason, which says more.
4. Swagger (Development only) · HTTPS redirect (non-Development only).
5. **`UseAuthentication`** then **`UseAuthorization`**.
6. **`UseRateLimiter`** — *after* authentication on purpose, so a signed-in caller is limited on their
   stable user id rather than sharing a bucket with everyone behind the same office NAT.

### 5.3 Authentication

Opaque, database-backed session tokens. **No JWT.**

```
Authorization: Bearer <token>   (or the session_token cookie, for browsers)
        ↓  SHA-256
   UserSessions.TokenHash       unique index, one lookup
        ↓
   revoked? expired? account inactive?
        ↓
   UserSessions.UserId          the stable identity
```

| File | Responsibility |
|---|---|
| `Security/SessionToken.cs` | 32 CSPRNG bytes → base64url; SHA-256 for storage |
| `Security/PasswordHasher.cs` | PBKDF2-HMAC-SHA256, 210,000 iterations, per-account salt, fixed-time compare |
| `Security/SessionTokenAuthenticationHandler.cs` | the scheme; header first, cookie second |
| `Security/SessionCookie.cs` | the browser transport — HttpOnly, `SameSite=Lax`, `Path=/` |
| `Security/CurrentUser.cs` | the only place the user id is read (from claims) |
| `Services/AuthService.cs` | register, login, logout, revoke, list devices |

Two transports, **one mechanism**: native apps send the header, browsers use the HttpOnly cookie. Same
token, same hash, same row. `SameSite=Lax` is the CSRF defence — it withholds the cookie on cross-site
POST.

The handler returns `NoResult` (not `Fail`) when no credential is present, which is what keeps
`/api/track` usable by anonymous visitors.

**Multi-device**: one account, many sessions, each revocable independently. Signing in on a phone does
not disturb a laptop. Revocation is immediate — the whole reason for DB-backed tokens over a signed
blob that stays valid until it expires.

### 5.4 Brute-force protection

`Services/AuthThrottleService.cs` + `AuthThrottleRules.cs`. Counts outcomes, not requests, on two axes:

- **IP** — one machine working through a password list.
- **Account** — a botnet trying one email from a thousand addresses. IP limiting cannot see this.

| Consecutive failures | Lockout |
|---|---|
| 1–4 | none |
| 5 / 6 / 7 / 8 | 1 / 2 / 4 / 8 minutes |
| 9+ | 15 minutes (capped) |

Cleared by one successful sign-in; expires after 60 idle minutes. The cap is the answer to
account-lockout DoS: an attacker can throttle someone else's email, but only ever for 15 minutes, and
never permanently.

### 5.5 Rate limiting

In-box middleware, `Security/RateLimitPolicies.cs`. Rejections return **429 with `Retry-After`**.

| Policy | Applied to | Limit / window | Partition |
|---|---|---|---|
| `general` | definitions, records, most `/api/auth/*` | 200 / 60s sliding | user id, else IP |
| `analytics` | `/api/track*` | 120 / 60s sliding | user id, else IP |
| `expensive` | `/api/analytics/*` | 20 / 60s fixed | user id, else IP |
| `login` | `POST /api/auth/{login,register}` | 10 / 60s fixed | IP only |
| `password-reset` | *nothing yet* | 5 / 900s fixed | IP only |

### 5.6 Tracking

Three identities, kept strictly apart:

| | Names | Lifetime |
|---|---|---|
| `VisitorId` | a browser | ~1 year, HttpOnly cookie |
| `SessionId` | one period of browsing | 30 min idle |
| `UserId` | an account | forever |

| File | Responsibility |
|---|---|
| `Services/TrackingIdentity.cs` | resolves client → session → user; mints the visitor cookie |
| `Services/AnalyticsTracker.cs` | writes events; links visitor to user on first sighting |
| `Services/TrackingRules.cs` | pure decisions: path filtering, normalisation, clamping, locale split |
| `Security/VisitorCookie.cs` | the `visitor_id` cookie |

**Login links, never replaces.** The visitor id survives sign-in; historical anonymous events keep
`UserId = null` **forever** and are found later through `TrackingClientUsers`.

**Active time** is measured by the browser (only it can see tab visibility), sent as `activeSeconds`,
and clamped server-side. **Not tracked**: static assets, `/_next/*`, `/api/*`, favicons. Query strings
are stripped before storage — search terms and reset tokens live there.

**Write cost**: one `SaveChanges` per tracked request. New client, new session, the event and the link
all commit together.

### 5.7 Localization

`Security/RequestLanguage.cs` resolves, highest first: explicit `?lang=` → account preference (as a
claim, no extra query) → `Accept-Language` in quality order → default `en`. Unsupported tags fall
through rather than being honoured.

`Localization/LocalizedText.cs` resolves `translations[lang] ?? canonical`, so a **missing translation
falls back rather than rendering blank**, and sanitises on write: unsupported languages dropped, keys
normalised, values held to the same length limit as the column they shadow.

### 5.8 Validation

`Validation/RecordValueValidator.cs` checks record values against the definition's **active** fields
before any write. Types are not coerced — `"8"` is rejected for a numeric field. Dropdown values must
be active `FieldOption` values.

Every failure carries a **stable `errorCode`** (`required`, `too_long`, `unknown_option`, …) and the
`fieldKey`, which is what the frontend translates. The English `message` is a fallback.

---

## 6. API reference

All routes are under `/api`. `429` is possible on every rate-limited route.

### Authentication — `/api/auth`

| Method | Route | Auth | Policy | Purpose |
|---|---|---|---|---|
| POST | `/register` | — | `login` | create an account, sign in this device |
| POST | `/login` | — | `login` | returns the opaque token + sets the cookie |
| POST | `/logout` | ✔ | `general` | revoke this device |
| POST | `/logout-all` | ✔ | `general` | revoke every *other* device |
| GET | `/me` | ✔ | `general` | the signed-in account |
| PATCH | `/me` | ✔ | `general` | update display name / preferred language |
| GET | `/sessions` | ✔ | `general` | list active devices |
| DELETE | `/sessions/{id}` | ✔ | `general` | sign one device out |

### Object definitions — `/api/object-definitions`

| Method | Route | Purpose |
|---|---|---|
| GET | `/` | list (summaries, labels resolved) |
| GET | `/{id}` | one definition with fields and options, **plus translation maps** (editing view) |
| POST | `/` | design a new object type |

### Records

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/object-definitions/{definitionId}/records` | list, newest first, `take` clamped 1–200 |
| POST | `/api/object-definitions/{definitionId}/records` | add a record (validated) |
| GET | `/api/records/{id}` | read one |
| PUT | `/api/records/{id}` | replace one |
| DELETE | `/api/records/{id}` | delete one |

### Tracking — `/api/track`

| Method | Route | Auth | Purpose |
|---|---|---|---|
| POST | `/` | anonymous OK | record one event |
| GET | `/config` | anonymous OK | heartbeat interval, so it is configured server-side |

Request body carries **only what happened** — `eventType`, `path`, `activeSeconds`, `referrer`,
`language`. Never who it happened to.

### Analytics — `/api/analytics` (authenticated, own account only)

| Method | Route | Purpose |
|---|---|---|
| GET | `/users/{userId}/activity` | full chronological history, anonymous events included |
| GET | `/users/{userId}/page-time` | active seconds per page |

---

## 7. Frontend

Next.js 16 App Router, React 19, Tailwind 4, TypeScript 5. Locale-prefixed routes.

```
src/
  proxy.ts                          locale detection + redirect (Next 16 renamed "middleware")
  app/[lang]/
    layout.tsx                      html lang, language switcher, analytics provider
    page.tsx                        schema list
    schemas/new/                    page + schema-builder + server action
    schemas/[id]/records/new/       page + record-form + server action
  components/
    i18n/language-switcher.tsx      client; rewrites the locale segment, sets a cookie
    tracking/analytics-provider.tsx client; page views, heartbeats, active time
  i18n/
    config.ts                       locales, matchLocale (mirrors the API)
    get-dictionary.ts               server-only loader + translateError
    dictionaries/en.json, zh-Hans.json
  lib/
    handytool-api.ts                server-side API client
    handytool-types.ts              shared types
    schema-draft.ts                 the schema builder's draft model
    tracking.ts                     browser transport + ActiveTimer
```

### How data reaches a page

**Server-side only.** Pages and server actions call `src/lib/handytool-api.ts`, which uses an absolute
URL (server code has no origin to be relative to) and **forwards the request's `session_token` and
`visitor_id` cookies**. Without that forwarding a server-rendered page would reach the API anonymous
and get 401 even though the person is signed in.

The browser never calls the API for data — only for tracking, which it does with a relative URL.

### Tracking in the browser

`AnalyticsProvider` mounts once in the layout and keys its effect on `usePathname()`, so:

1. a client-side route change runs the cleanup — flush the old page's active time, clear the interval,
   remove listeners;
2. then a fresh `page_view` starts the new page.

`ActiveTimer` accrues time only while `document.visibilityState === "visible"`. `page_leave` goes via
`navigator.sendBeacon` because `fetch` is cancelled during teardown — and nothing depends on it
arriving, since heartbeats have already accounted for all but the last interval.

### Localization

Two JSON dictionaries, statically imported and typed as `typeof en`, so **a key missing from
`zh-Hans.json` is a build error**, not a blank string in production. No `next-intl` — the built-in
`Intl` APIs cover dates and numbers.

`proxy.ts` redirects unprefixed paths, preferring a remembered cookie over `Accept-Language`. The API
strips the locale prefix back out of analytics paths, so `/en/tools` and `/zh-Hans/tools` report as
one page.

---

## 8. Configuration

`appsettings.json`:

| Section | Controls |
|---|---|
| `ConnectionStrings:HandyTool` | PostgreSQL |
| `Serilog` | levels and sinks |
| `Auth` | session lifetime, touch interval, password length, max devices |
| `Auth:BruteForce` | thresholds, backoff, the lockout cap |
| `Tracking` | heartbeat interval, session idle, cookie lifetime, ignored paths |
| `Localization` | supported languages, default |
| `RateLimiting` | per-policy limits and windows |
| `ForwardedHeaders` | **trusted proxies — must be set before production** |

> `ForwardedHeaders` is empty by default and that is the safe default: with nothing trusted,
> `X-Forwarded-For` is ignored. **Left empty in production, every visitor shares one rate-limit
> bucket.** The API logs a startup warning outside Development.

---

## 9. Tests

131 xUnit tests in `tests/handytool-api.Tests`, all pure — no database, no host.

| File | Covers |
|---|---|
| `RecordValueValidatorTests` | every field type, required, bounds, options |
| `PasswordHasherTests` | round-trip, per-account salts, fail-closed on unusable hashes |
| `SessionTokenTests` | uniqueness, URL safety, hash ≠ token, expiry, revocation |
| `AuthThrottleRulesTests` | the backoff schedule and its cap |
| `TrackingRulesTests` | path filtering, query stripping, clamping, locale splitting |
| `RequestLanguageTests` | the resolution chain and `Accept-Language` quality order |
| `LocalizedTextTests` | resolution, fallback, sanitising, length limits |

```bash
dotnet test tests/handytool-api.Tests/handytool-api.Tests.csproj
```

---

## 10. Known gaps

Things deliberately not built, in rough priority order.

**No login UI.** The API is complete and `handytool-api.http` exercises all of it, but nothing in the
Next app signs in — so the site shows "Sign in to see this" until a login page exists. This is the
biggest gap.

**No password reset.** There is no email delivery, and a reset flow that cannot send anything is worse
than none. The rate-limit policy and the token requirements are documented ready for it.

**`POST /api/auth/register` returns 409 for a taken email**, which reveals that the account exists.
Making it generic needs email confirmation to stay usable. Capped at 10/min per IP meanwhile.

**No email verification, no roles.** Analytics endpoints are self-service only — a caller reads their
own history and nobody else's.

**Chinese sorting is codepoint order.** ICU collations are installed, so this is a one-line
`EF.Functions.Collate(d.Name, "zh-Hans-CN-x-icu")` whenever it matters — no migration.

**Rate limiting partitions in-process.** Single instance, limits are exactly as documented; across
several, each enforces its own. Brute-force counters are in the database and shared.

**Application-level protection only.** This stops abuse and crude flooding. A real DDoS must be shed
at the edge — Cloudflare, Azure Front Door + WAF, or equivalent in front of `handytool.org`.

**The Chinese translations are not from a translator.** They read correctly but want a native
speaker's pass before launch, particularly the technical prose in `schemaNew.intro`.
