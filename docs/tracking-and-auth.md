# Authentication and website tracking

Two systems, deliberately kept apart. Authentication says who someone is. Tracking says what happened.
They meet at exactly one point: a tracked event may carry the user id that authentication resolved.

## Deployment shape

```
https://handytool.org/           →  Next.js
https://handytool.org/api/*      →  ASP.NET Core   (reverse proxy)
```

One origin from the browser's point of view. That single fact decides most of the design:

- the browser calls `/api/track` with a relative URL,
- the `visitor_id` and `session_token` cookies are ordinary first-party `SameSite=Lax` cookies,
- there is no CORS configuration anywhere, and none is needed,
- `SameSite=None` is never used.

In development there is no proxy, so `next.config.ts` rewrites `/api/*` to `http://localhost:5292`.
The browser still sees one origin, so the cookies behave identically and no code differs between
environments.

Future iOS and Android apps call the same `https://handytool.org/api/*`. That is why none of this
logic lives in a Next.js route handler.

Pages are locale-prefixed (`/en/...`, `/zh-Hans/...`). The prefix is lifted out of the analytics path
so per-page reports do not split per language - see [localization.md](localization.md).

## The four identities

| | What it names | Lifetime | Where it comes from |
|---|---|---|---|
| `VisitorId` | A browser | ~1 year | HttpOnly `visitor_id` cookie |
| `SessionId` | One period of browsing | 30 min idle | Derived from the visitor's recent activity |
| `UserId` | An account | Forever | `UserSession.UserId`, via the bearer token |
| Session token | A signed-in device | 90 days, revocable | Minted at login; a credential, never an identity |

The token is the odd one out and the distinction matters: it can be rotated, revoked, or replaced by
signing in on another device, and none of that changes the `UserId` it resolves to. It is never used
as `VisitorId`, `SessionId`, `ClientId` or `UserId`.

## Authentication

No JWT. A session token is 32 bytes from `RandomNumberGenerator`, base64url-encoded. It is opaque:
nothing is encoded inside it, so the API is the only thing that can say what it means.

Only `SHA256(token)` reaches the database. The raw token exists in two places — the login response,
and the client that stored it. A leaked `UserSessions` table therefore yields no usable credentials.

Every authenticated request:

```
Authorization: Bearer <token>      (or the session_token cookie, for browsers)
        ↓  SHA-256
   UserSessions.TokenHash          unique index, one lookup
        ↓
   revoked? expired? account inactive?
        ↓
   UserSessions.UserId             the stable identity
```

`SessionTokenAuthenticationHandler` returns `NoResult` — not `Fail` — when no credential is present,
which is what keeps `/api/track` usable by anonymous visitors.

### Two transports, one mechanism

Native apps send `Authorization: Bearer`. Browsers use an HttpOnly `session_token` cookie, because a
token held in JavaScript is one XSS bug away from being stolen, and because server-rendered pages need
the credential on the very first request. Same token, same hash, same row — only the envelope differs.
The header wins when both are present.

`SameSite=Lax` is the CSRF defence: it withholds the cookie on cross-site POST, so another site cannot
drive a state-changing endpoint with the user's credential attached.

### Multiple devices

One account, many sessions. Signing in on a phone does not disturb a laptop.

```
User 456
├── UserSession AUTH001  Chrome    → UserId 456
├── UserSession AUTH002  iPhone    → UserId 456
└── UserSession AUTH003  Android   → UserId 456
```

`GET /api/auth/sessions` lists them, `DELETE /api/auth/sessions/{id}` ends one,
`POST /api/auth/logout-all` ends every other one. Revocation is immediate — that is the whole reason
for database-backed tokens rather than a signed blob that stays valid until it expires.

Passwords are PBKDF2-HMAC-SHA256, 210,000 iterations, with a **per-account** random salt.

## Tracking

### Visitor

First request without a `visitor_id` cookie gets one: a fresh GUID, `HttpOnly`, `Secure` outside
Development, `SameSite=Lax`, `Path=/`, one year. It is opaque and holds nothing else — no name, no
email, no account id, no token. The frontend never reads it and cannot: it is HttpOnly, and the API
never echoes it back in a response body.

It identifies a browser, not a person. Incognito, a second laptop and a phone are three visitors.

The cookie is re-issued on `page_view` events, so a returning visitor keeps their identity rather than
ageing out of a fixed window. Heartbeats skip that, which keeps `Set-Cookie` off most responses.

### Session

The visitor's most recent `TrackingSession` is reused if its `LastActivityAt` is within 30 minutes.
Otherwise a new `SessionId` starts — with the **same** `ClientId`.

```
Day 1   Visitor A123   Session S001
Day 3   Visitor A123   Session S002      ← same browser, new visit
```

### Login: link, never replace

```
10:00  A123  null  /
10:03  A123  null  /tools
10:07  A123  null  /calculator
10:10  A123  U456  login          ← TrackingClientUser row written here
10:11  A123  U456  /account
```

The visitor id does not change. The three anonymous rows keep their null `UserId` **forever** — they
are found later through the `TrackingClientUsers` link, not by rewriting them. The link is written
once per `(ClientId, UserId)` pair and is a no-op on every subsequent request.

The relationship is many-to-many in both directions. One user has many browsers; one browser may have
had several users.

### Active time

Only the browser can see whether its tab is visible, so the browser measures active time and sends it:

```json
{ "eventType": "heartbeat", "path": "/properties/123", "activeSeconds": 30 }
```

`ActiveTimer` runs only while `document.visibilityState === "visible"`. A page left open in a
background tab accrues nothing. The server clamps what arrives to `MaximumActiveSecondsPerEvent` —
the browser is a client, and a heartbeat claiming an hour is either a bug or an attempt to skew the
numbers.

`page_leave` goes out via `navigator.sendBeacon`, because `fetch` is cancelled when a document is torn
down. Nothing depends on it arriving: if it is lost, at most one heartbeat interval is unaccounted for.

### Next.js navigation

`AnalyticsProvider` keys its effect on `usePathname()`. A client-side route change therefore runs the
cleanup — flush the old page's active time, clear the interval, remove the listeners — and then starts
the new page with a fresh `page_view`. No two pages ever beat at once.

### What is not tracked

Static assets, `/_next/*`, `/api/*`, favicons, `robots.txt`, `sitemap.xml`, and anything ending in a
known asset extension. Filtered in the browser and again in `TrackingRules`, so the events table stays
meaningful whatever a client decides to send.

Query strings are stripped before storage. Search terms, email addresses and password-reset tokens all
live in query strings, and none of them belong in an analytics table.

## Tables

```
UserAccounts ──< UserSessions            authentication
     │
     ├──< TrackingClientUsers >── TrackingClients ──< TrackingSessions
     │                                   │                  │
     └────────────< AnalyticsEvents >────┴──────────────────┘
```

| Table | Key | Notes |
|---|---|---|
| `UserAccounts` | `long` identity | unique index on lowercased `Email` |
| `UserSessions` | `Guid` | **unique** index on `TokenHash` — the hot path; `(UserId, RevokedDate)`; `ExpiresDate` |
| `TrackingClients` | `Guid`, app-assigned | for web this **is** the VisitorId; carries `ClientType` |
| `TrackingClientUsers` | `long` identity | unique `(ClientId, UserId)`; index on `UserId` |
| `TrackingSessions` | `Guid`, app-assigned | index `(ClientId, LastActivityAt)` — the session-roll lookup |
| `AnalyticsEvents` | `long` identity | `(UserId, Timestamp)`, `(ClientId, Timestamp)`, `(SessionId, Timestamp)`, `EventType`, `Timestamp` |

`AnalyticsEvents` has `Restrict` delete behaviour toward clients and sessions: history outlives the
identities it references.

`EventType` is a `varchar`, not an enum. Adding `property_viewed` or `contact_form_submitted` is one
constant in `AnalyticsEventTypes` and no migration.

### Future native clients

`TrackingClients.ClientType` is already `Web | Ios | Android`. A native app would send
`X-Client-Type: Ios` and `X-Client-Id: <installation id>` instead of relying on a cookie; everything
downstream is unchanged.

```
User 456
├── Web     A123
├── Web     B789
├── iOS     M111
└── Android M222
```

## Reading history back

`GET /api/analytics/users/{userId}/activity`

1. Find the clients linked to the user.
2. Select events where `UserId = {userId}` **or** `ClientId` is one of those clients.
3. Order by timestamp.

Which returns the anonymous rows and the authenticated rows in one timeline, each exactly as recorded.

`GET /api/analytics/users/{userId}/page-time` sums `ActiveSeconds` per path over the same set.

Both are scoped to the caller's own account until there are roles.

## Write volume

One `SaveChanges` per tracked request. A new client, a new session, the event and the link all commit
together. That is one write per 30 seconds per visible tab.

`UserSessions.LastUsedDate` is touched at most once per `Auth:SessionTouchMinutes` (default 15), so
authenticated requests do not each cost a write.

## Configuration

```jsonc
"Auth": {
  "SessionDays": 90,
  "SessionTouchMinutes": 15,
  "MinimumPasswordLength": 8,
  "MaximumSessionsPerUser": 20
},
"Tracking": {
  "HeartbeatSeconds": 30,
  "SessionIdleMinutes": 30,
  "VisitorCookieDays": 365,
  "MaximumActiveSecondsPerEvent": 300,
  "MaximumPathLength": 500,
  "IgnoredPathPrefixes": ["/api/", "/_next/", "/static/", "/assets/", "/favicon", "/robots.txt", "/sitemap.xml"]
}
```

`GET /api/track/config` serves `HeartbeatSeconds` to the browser, so the interval changes without
redeploying the website.

## Rate limiting and brute-force protection

Built on the in-box `Microsoft.AspNetCore.RateLimiting` middleware. No third-party package.

### Policies

| Policy | Applied to | Limit | Window | Partition |
|---|---|---|---|---|
| `general` | object-definitions, records, `/api/auth/{logout,logout-all,me,sessions}` | 200 | 60s sliding, 6 segments | user id, else IP |
| `analytics` | `/api/track`, `/api/track/config` | 120 | 60s sliding, 6 segments | user id, else IP |
| `expensive` | `/api/analytics/*` | 20 | 60s fixed | user id, else IP |
| `login` | `POST /api/auth/login`, `POST /api/auth/register` | 10 | 60s fixed | IP only |
| `password-reset` | *nothing yet* | 5 | 900s fixed | IP only |

`analytics` is 120 because a visible tab beats twice a minute and someone may have a dozen open.
`expensive` is 20 because reading a full history is an unbounded scan over the events table.
`login` partitions on IP alone because there is no authenticated identity yet — working out whose
account is being claimed is the very thing being protected.

A signed-in caller is keyed on their **stable user id**, so their allowance follows them between
networks rather than being shared with everyone behind one office NAT. The session token is never the
key: it rotates on every sign-in, and a rotating key is one an attacker can reset at will.

Rejections return **429** with `Retry-After` in seconds, and are written to the security log.

### Login brute-force protection

Separate from the middleware, because it counts **authentication outcomes**, not requests. Signing in
correctly a hundred times never touches it; only wrong passwords do.

Counters live in `AuthThrottles` — in the database rather than in memory, so they survive a restart
and are shared if this ever runs on more than one instance. Two axes, because neither works alone:

- **IP** — catches one machine working through a password list.
- **Account** — catches a botnet trying one email from a thousand addresses, a few guesses each. IP
  limiting cannot see that at all.

Whichever trips first blocks the attempt.

Keys are stored **SHA-256 hashed**. The account axis has to count attempts against emails that were
never registered — only counting real ones would make the table itself an answer to "does this account
exist" — so it would otherwise fill with whatever addresses an attacker submitted, in the clear.
Hashing covers IP addresses too, which are personal data in their own right.

### Backoff schedule

| Consecutive failures | Lockout |
|---|---|
| 1–4 | none |
| 5 | 1 minute |
| 6 | 2 minutes |
| 7 | 4 minutes |
| 8 | 8 minutes |
| 9+ | 15 minutes (capped) |

Counters reset on a successful sign-in, and expire after 60 minutes with no failures.

**On account-lockout denial of service:** an attacker who knows an email address *can* deliberately
fail against it and throttle that account. That cannot be prevented outright without deciding whose
attempts are genuine, which is not knowable. It is bounded instead — the worst anyone can inflict is
15 minutes, it can never become permanent, and one correct password clears it immediately. The IP axis
trips first for a single-source attacker. `AccountScopeEnabled: false` turns the account axis off
entirely if it ever proves more trouble than it prevents; the IP axis keeps working.

### User enumeration

Sign-in returns one message — "The email address or password is incorrect." — for an unknown account,
a wrong password and a deactivated account alike. `AuthService` also verifies against a dummy hash
when no account matches, so both paths take the same time and cannot be told apart by timing.

Failed sign-ins are logged **without the email address**. A log of failed attempts naming the address
tried is a list of accounts worth attacking.

**Known gap:** `POST /api/auth/register` still returns 409 for an already-registered email, which does
reveal that the account exists. Making it generic requires email confirmation to stay usable, and
there is no email delivery in this project yet. It is capped at 10/min per IP in the meantime.

### Real client IP behind the proxy

`UseForwardedHeaders` runs first in the pipeline, before anything that reads the client address.

It trusts **only** the proxies listed in the `ForwardedHeaders` section, and that list is **empty by
default**. With nothing configured, `X-Forwarded-For` is ignored entirely and every limit sees the
socket address — wrong behind a proxy, but merely wrong. Trusting the header unconditionally would be
worse: any caller could invent an address per request and hand themselves an unlimited number of
rate-limit buckets.

```jsonc
"ForwardedHeaders": {
  "KnownProxies":  ["10.0.0.4"],     // individual addresses
  "KnownNetworks": ["10.0.0.0/8"],   // CIDR ranges
  "ForwardLimit": 1                  // hops to walk back; one proxy means one
}
```

**This must be filled in before production.** Left empty, every visitor behind the proxy shares one
rate-limit bucket and the site breaks for everyone at once. The API logs a startup warning outside
Development when the list is empty.

### Password storage

Unchanged, and deliberately: PBKDF2-HMAC-SHA256, 210,000 iterations, 16-byte per-account random salt,
via `Rfc2898DeriveBytes.Pbkdf2`. That is the framework implementation at OWASP's current work factor —
not a custom algorithm, and nothing like a bare `SHA256(password)`. ASP.NET Core Identity is not used
by this project, and Argon2 or bcrypt would mean a third-party package.

Session tokens are hashed with plain SHA-256, which is right for them and would be wrong for a
password: a token is 256 random bits with nothing to guess, and its hash must be recomputable on every
authenticated request.

### Password reset

**Not implemented.** There is no email delivery in this project, and a reset flow that cannot send
anything is worse than none. The `password-reset` policy is defined so that whoever adds the endpoint
cannot forget to attach it.

When it is built, the token must be cryptographically random, stored **hashed** at rest, single-use,
expiring, invalidated on use, and must contain nothing about the user. The response must read the same
whether or not the account exists — "If an account exists for this email address, password reset
instructions will be sent." — and requests must be limited by IP *and* by account, so nobody can
trigger thousands of emails at somebody else.

### Security logging

Category `handytool_api.Security`, at Information. Records failed sign-ins with the IP and both
failure counts, throttles applied and cleared, rate-limit rejections, and session revocations.

Never logged: passwords, raw session tokens, token hashes, password-reset tokens, or the email address
on a failed attempt.

### What this does not cover

Application-level rate limiting stops abuse and crude flooding. It does **not** stop a real
distributed denial-of-service attack: by the time a request reaches this code it has already cost a
connection, a thread and a socket. That has to be turned away at the edge.

Production should run `handytool.org` behind **Cloudflare**, **Azure Front Door + WAF**, or an
equivalent CDN/WAF, configured to shed obviously abusive traffic before it reaches the origin. Nothing
in application code substitutes for that.

Note also that the middleware partitions **in-process**. On a single instance the limits are exactly
as documented; across several, each instance enforces its own. The brute-force counters are in the
database and so are shared.

### What you can tune

| Setting | Default | What it does |
|---|---|---|
| `RateLimiting:Enabled` | `true` | Master switch. Logs a warning at startup when off. |
| `RateLimiting:*:PermitLimit` | see table | Requests per window |
| `RateLimiting:*:WindowSeconds` | see table | Window length |
| `RateLimiting:*:SegmentsPerWindow` | 1 or 6 | Above 1 makes the window sliding |
| `Auth:BruteForce:FailureThreshold` | 5 | Failures before throttling starts |
| `Auth:BruteForce:BaseLockoutSeconds` | 60 | The first lockout; doubles from there |
| `Auth:BruteForce:MaximumLockoutMinutes` | 15 | The cap — the account-DoS bound |
| `Auth:BruteForce:AttemptWindowMinutes` | 60 | How long a counter remembers |
| `Auth:BruteForce:AccountScopeEnabled` | `true` | Turns the account axis off |
| `ForwardedHeaders:KnownProxies` / `KnownNetworks` | empty | **Must be set for production** |
