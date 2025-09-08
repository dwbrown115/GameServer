# FUTURE PLANS — Third‑Party Login (Google Play + Apple)

## Goals
- Replace/augment username+password with platform logins: Google Play Games (Android) and Apple (Game Center and/or Sign in with Apple).
- Keep our server JWT model unchanged for HTTP/WebSocket; third‑party tokens are only used to authenticate and then mint our JWT.
- Support account linking (existing users can attach a provider) and new user auto‑provisioning.
- Minimize attack surface (proper token validation, key rotation, replay protection).

## High‑Level Architecture
1. Client obtains a platform credential:
   - Google (Android): OIDC ID token from Google Sign‑In or Play Games Services.
   - Apple (iOS/tvOS/macOS):
     - Game Center identity verification (server‑side signature validation), and/or
     - Sign in with Apple (OIDC ID token).
2. Client POSTs the credential to our server: `POST /authentication/external/{provider}`.
3. Server validates provider token/signature with provider public keys/APIs and strong claim checks (iss/aud/exp/nonce, etc.).
4. Server locates or creates a local `User` mapped to the provider subject, then issues our JWT (+ refresh) exactly like password login.
5. Downstream behavior (authorization, WebSocket handshake) remains the same.

## Minimal Contract (server endpoint)
- Input: `{ provider: "google|apple_game_center|apple_signin", idToken? , signatureBundle? , deviceId }`
  - google: `idToken` (JWT)
  - apple_signin: `idToken` (JWT)
  - apple_game_center: `signatureBundle` with fields from `GKLocalPlayer.generateIdentityVerificationSignature`:
    - `publicKeyUrl`, `signature` (base64), `salt` (base64), `timestamp`, `playerId`, `teamPlayerId`, `bundleId`
- Output (on success): `{ userId, token, refreshToken, expiresAt }`
- Errors: 400 (malformed), 401 (invalid/expired credential), 409 (link conflict), 429 (rate limited).

## Data Model Changes
- New table: `users.ExternalIdentities`
  - `Id` (PK), `UserId` (FK), `Provider` (enum/text), `Subject` (string), `DisplayName?`, `Email?`, `LinkedAt` (datetime)
  - Unique index on (`Provider`, `Subject`)
- Optionally: `Users.LastLoginProvider` for analytics.

## Provider Validation Details

### Google Play / Google Sign‑In (OIDC)
- Accept an ID token issued by Google.
- Validate:
  - Signature using Google JWKS (cache JWKS, respect `kid` rotation; refresh on miss).
  - `iss` in [`https://accounts.google.com`, `accounts.google.com`].
  - `aud` matches our configured client ID(s) (allowlist for debug vs prod).
  - `exp`/`iat` acceptable with small clock skew.
  - Extract `sub` (stable user identifier), `email` (optional), `name` (optional).
- Map `sub` to `ExternalIdentities(Provider=google, Subject=sub)`.
- If not linked, auto‑provision a `User` + `UserData` (same provisioning used today) and insert ExternalIdentity.

Notes:
- On Android, Play Games Services can also provide a server auth code; exchanging it for ID token is optional if the client hands us the ID token directly. Keep server logic OIDC‑centric.

### Apple — Option A: Game Center Identity Verification
- Client calls `GKLocalPlayer.generateIdentityVerificationSignature(...)` and sends the payload to server.
- Server verifies by:
  1) Validating URL is Apple’s `publicKeyUrl` host.
  2) Downloading/caching the ECDSA public key from `publicKeyUrl`.
  3) Constructing the message as documented: `playerId + bundleId + teamPlayerId + timestamp + salt` (exact concatenation per Apple docs).
  4) Verifying ECDSA signature against the message.
  5) Checking `timestamp` freshness (e.g., 5 minutes) to prevent replay.
  6) Ensuring `bundleId` matches our app and `teamPlayerId`/`playerId` are present.
- Subject key: `playerId` (or `teamPlayerId` if you prefer team‑scoped).
- Map to `ExternalIdentities(Provider=apple_game_center, Subject=playerId)`.

### Apple — Option B: Sign in with Apple (OIDC)
- Accept Apple ID `id_token` from ASAuthorization.
- Validate:
  - Signature using Apple JWKS (`https://appleid.apple.com/auth/keys`), cache + rotate by `kid`.
  - `iss = https://appleid.apple.com`.
  - `aud` equals our Service ID / Bundle ID (web vs native).
  - `exp`/`iat` valid; optional `nonce` support if you supply nonce from client.
  - Extract `sub` (stable Apple identifier) and optional `email` (only on first consent grant).
- Map to `ExternalIdentities(Provider=apple_signin, Subject=sub)`.

## Account Linking / Migration
- New endpoints:
  - `POST /authentication/external/link` (JWT required): attach a provider to the current account after validating provider credential.
  - `POST /authentication/external/unlink` (JWT required): detach provider (guard against removing last login option without password).
- One‑time migration path:
  - When a password user logs in, UI prompts to link Google/Apple; server rejects linking if another account already owns that exact provider `Subject`.
  - Optional: allow passwordless creation via provider directly; legacy password endpoint can be deprecated later.

## Security Considerations
- JWKS caching with TTL and retry on `kid` miss; circuit breaker to avoid provider outages cascading.
- Strict `aud` allowlist (separate dev/prod client IDs) and `iss` checks.
- Replay protection for Game Center via timestamp window and unique salt tracking (store last few salts per `playerId` if necessary).
- Rate limiting on external auth endpoints.
- Minimal PII retention; don’t rely on provider email for identity (use `sub`/`playerId`).
- Telemetry: log provider, subject hash (not raw), and validation outcomes.

## API Surface (proposed)
- `POST /authentication/external/{provider}`
  - Body depends on provider (see contract). Returns our `{ userId, token, refreshToken, expiresAt }`.
- `POST /authentication/external/link`
- `POST /authentication/external/unlink`

Server JWT contents: keep `sub = userId` as today. No changes required to existing protected endpoints.

## Implementation Roadmap
- Phase 0: Config plumbing (client IDs, bundle IDs, team ID), provider toggles, JWKS cache service.
- Phase 1: Google OIDC path (usually simplest), end‑to‑end in dev.
- Phase 2: Apple Game Center verification; add Sign in with Apple optionally.
- Phase 3: Account linking endpoints + UI prompts; migrate existing users progressively.
- Phase 4: Monitoring, rate limits, and staged rollout; deprecate password login if desired.

## Testing
- Unit tests for token validators (happy path + expired + wrong aud/iss + wrong signature).
- Integration tests with recorded JWKS and deterministic clocks.
- Device tests with Google Play Games (internal testing track) and Apple TestFlight / Sandbox.

## Edge Cases
- Provider rotation of keys (ensure JWKS refresh on `kid` miss).
- User reinstalls app (new deviceId) → unaffected; mapping is by provider subject.
- Provider account change/merge → subject stability guaranteed by providers; handle support flow if mismatch ever occurs.
- Attempted takeover: linking endpoint must require active session and validate provider token freshly.

---

## Extension Points & Future Ideas
- Add structured logging (ILogger) already partly used → centralize & add correlation IDs.
- Replace plaintext refresh tokens with hashed values (store SHA-256 hash) for at-rest protection.
- Add rate limiting (per IP / device) on auth and spawn endpoints.
- Add SignalR for higher-level realtime abstractions (optional; current raw WebSockets fine for custom protocol).
- Implement soft-ending & pruning of old `PlayerSessionLog` entries (scheduled background service).
- Introduce leaderboard update service triggered on score changes.
- Pagination & caching for large skins catalogs / leaderboard.
- Enforce optimistic concurrency / row versioning on points & leaderboard updates.
- Trim / archive `HighScoreLog` & `PointsLog` (size management strategy).
- Add unit/integration tests (services & WebSocket message flows) + test factory for DbContext (InMemory/SQLite).
- Add OpenAPI/Swagger for REST endpoints.
- Encrypt sensitive user fields (e.g., future email) and implement password complexity rules.
- Implement position/speed anomaly detection (server-side physics or rate thresholds).
