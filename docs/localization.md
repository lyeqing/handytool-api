# Languages

English and Simplified Chinese, with a third language costing a settings change and translation data
rather than a migration.

## The one idea that makes this work

Machine identifiers and human labels are separate columns, and always were:

| Never translated | Translated |
|---|---|
| `FieldDefinitions.Key` | `FieldDefinitions.Name`, `.Description` |
| `FieldOptions.Value` | `FieldOptions.Label` |
| `FieldType` | `ObjectDefinitions.Name`, `.Description` |

A record stores `{"damageType": "water"}`, never `{"damageType": "Water Damage"}`. So a label can be
translated, corrected or rewritten and **not one record row changes**. Without that separation none of
the rest would be affordable.

## Storage

The canonical column stays, and a jsonb map sits beside it:

```
Name             = 'Property Inspection'
NameTranslations = {"zh-Hans": "房产检查"}
```

Chosen over replacing `Name` with a map because it keeps the `(UserId, Name)` unique index working,
keeps `ORDER BY Name` meaningful, needs no backfill — and because a **missing translation falls back
to the canonical text rather than rendering blank**.

`LocalizedText.Sanitise` cleans anything submitted: unsupported languages dropped, keys normalised so
`ZH-HANS` and `zh-Hans` cannot become two entries, blanks removed, and every value held to the same
length limit as the column it shadows. That last part matters — without it the 200-character cap on
`Label` would be bypassed by putting a megabyte in the translation instead.

Each column carries a `jsonb_typeof(...) = 'object'` check constraint, matching `Values` and
`Settings`.

## Which language a request gets

`Security/RequestLanguage.cs`, one chain, highest first:

1. **Explicit** — `?lang=zh-Hans`. Someone who just clicked a switcher means it now.
2. **Account** — `UserAccount.PreferredLanguage`, carried as a claim from the row the auth handler
   already loaded, so it costs no extra query. Follows the person to a borrowed laptop.
3. **Browser** — `Accept-Language`, in quality order. Each candidate is tried exactly and then by
   primary subtag, so `zh-CN` and plain `zh` both reach `zh-Hans`.
4. **Default** — `en`.

Anything unrecognised falls through rather than being honoured. An unsupported tag must never reach a
translations map and become a key nothing can read back.

> A subtlety worth keeping: matching is per candidate, not per pass. Doing all the exact matches first
> would resolve `zh-CN,zh;q=0.9,en;q=0.8` to English — an exact match on the *least* wanted language
> beating a prefix match on the most wanted one.

## Reading vs editing

`ObjectDefinitionResponse` has two shapes:

- **`WithFields` / `Summary`** — every label already resolved into one language, no maps. What the
  website renders; it never learns translations exist.
- **`ForEditing`** — the same plus every translation map. What `GET /api/object-definitions/{id}`
  returns, since that is the endpoint a schema editor loads.

## URLs

Locale-prefixed: `/en/tools`, `/zh-Hans/tools`. Both indexable, and a link carries its language.

`src/proxy.ts` redirects an unprefixed path, preferring a remembered cookie over `Accept-Language`.
(Named `proxy` rather than `middleware` — Next 16 deprecated that convention.)

### Why analytics does not double-count

A locale prefix would otherwise split every per-page report in half. `TrackingRules.SplitLanguage`
lifts it out:

| Sent | Stored `Path` | Stored `Language` |
|---|---|---|
| `/en/tools` | `/tools` | `en` |
| `/zh-Hans/tools` | `/tools` | `zh-Hans` |
| `/zh-Hans` | `/` | `zh-Hans` |
| `/entries` | `/entries` | *(resolved)* |

Reports aggregate by page and still break down by language. Only a **whole segment** matching a
supported tag counts — otherwise `/entries` would quietly become `/tries`.

Done server-side so the future native apps get the same treatment.

## Validation errors

The API's error prose is English and embeds the canonical field name. That is deliberate: every error
also carries a stable `errorCode` and the `fieldKey` it belongs to, and **those** are what the
frontend translates, using the field label it already received in the reader's language.

```ts
translateError(dictionary, "too_long", field.name, error.message)
```

The English `message` is only a fallback for a code the dictionary has not caught up with.

## Frontend

Hand-rolled: two JSON dictionaries and the built-in `Intl` APIs, no `next-intl`. They are statically
imported and typed as `typeof en`, so **a key missing from `zh-Hans.json` is a build error**, not a
blank string in production. If the site outgrows this, the dictionaries port to `next-intl` unchanged.

## Adding a third language

1. Add the tag to `Localization:SupportedLanguages` in `appsettings.json`.
2. Add the same tag to `src/i18n/config.ts` and a `localeNames` entry.
3. Add `src/i18n/dictionaries/<tag>.json` — TypeScript will name every missing key.
4. Translate content through the API. `LocalizedText.MissingLanguages` lists what is outstanding.

No migration, no schema change.

## Known and deferred

**Record data is not translated, by design.** `ObjectRecords.Title`, `.Description` and `.Values` are
the user's own content. If someone writes an inspection note in Chinese it stays in Chinese; it needs
a language tag at most, never parallel columns.

**Chinese sorting is codepoint order.** The database collation is `English_Australia.1252`, so
`ORDER BY Name` is not pinyin. ICU collations are installed (PostgreSQL 18.6), so this is a one-line
`EF.Functions.Collate(d.Name, "zh-Hans-CN-x-icu")` in `ListAsync` whenever it matters — no migration,
no dump and restore.

**Analytics events written before `Language` existed have `Language = null`.** Expected: history is
recorded as it happened and never backfilled.
