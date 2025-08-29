# GameServer Project Catalog

## Overview
GameServer is an ASP.NET Core Web API + WebSocket server backed by Entity Framework Core (SQL Server) and a shared class library (`SharedLibrary`) that provides request/response DTOs, domain models, and common primitives. It supports:
- User registration/login with JWT + refresh token rotation (per device)
- Authenticated REST endpoints for player profile & data mutations
- Authenticated WebSocket session establishment for realtime gameplay messaging (pings, spawn requests, object claims)
- Server‑authoritative scoring, object lifecycle & player position logging for anti‑cheat / auditing
- Extensible gameplay data model (leaderboards, user data, skins)
- Economy & inventory (skins shop, points accumulation, purchase flow)
- Historical logging of high scores & point progression
- Administrative debug UI for ad‑hoc table CRUD & JSON field editing

## Solution Structure
```
GameServer.sln
├── GameServer/              # ASP.NET Core host project
│   ├── Program.cs           # Host & service registration
│   ├── GameDbContext.cs     # EF Core DbContext & model config
│   ├── Controllers/         # HTTP API layer
│   ├── Services/            # Domain/application services
│   ├── Handlers/            # Low-level WebSocket handling pipeline
│   ├── Models/              # Host-only models (e.g. Settings)
│   ├── Migrations/          # EF Core migrations (code-first)
│   ├── wwwroot/             # Static assets
│   └── appsettings*.json    # Configuration (connection string, JWT secret, spawn tuning)
└── SharedLibrary/           # Cross-project shared contracts & models
    ├── Requests/            # Client → Server DTOs
    ├── Responses/           # Server → Client DTOs
    ├── Results/             # Internal result objects (auth etc.)
    ├── Models/              # EF entities & gameplay record types
    ├── Pings/               # Player ping payloads
    └── Common/              # Primitive/value objects (e.g. Position)
```

## Runtime Architecture
1. Client authenticates via `/authentication/register` or `/authentication/login` obtaining a JWT + refresh token (device bound).
2. Client may call `/authentication/validate` to refresh when nearly expired (JwtService auto-rotates refresh token when required).
3. Client requests a WebSocket session: POST `/ws/auth` with current tokens. On success a `SessionId` is returned.
4. Client opens WebSocket at `/ws?sessionId={SessionId}`.
5. Realtime messages (`player_ping`, `spawn_item_request`, `object_claimed_request`) flow over the socket; server persists authoritative state & responds with structured responses.
6. Server logs object lifecycle, positions, scoring events for audit & anti-cheat signals. Refresh tokens can be revoked via logout.

## Key Components
### Program & Hosting (`Program.cs`)
- Configures Kestrel HTTPS (port 7123) using `server.pfx` cert.
- Registers EF Core SQL Server context.
- Registers services: Player, Authentication, JWT, WebSocket, connection manager, WebSocket handler.
- Enables JWT Bearer auth with symmetric signing key from `Settings.JwtSecret`.
- Maps WebSocket middleware and controllers.

### Configuration (`Settings` & appsettings)
`Settings` exposes:
- `JwtSecret` (required)
- `SpawnCooldownSeconds` – minimum delay between successful spawns
- `NoSpawnRadius` – inner exclusion radius inside the requested spawn circle

### Data Access (`GameDbContext`)
DbSets:
- `PlayerSessionLogs` (gameplay session telemetry + anti-cheat fields)
- `Users` (user accounts)
- `RefreshTokens` (device-scoped refresh tokens, plain stored currently)
- `ObjectLifecycleLogs` (standalone table; also embedded JSON within sessions)
- `Leaderboards`, `UserDatas`, `Skins`
Model configuration: owns `LastKnownPosition` (session) & `Coordinates` (object lifecycle). `RefreshTokenRecord` mapped to `auth` schema.

### Entities (SharedLibrary/Models)
- `User` (users schema) – Username, salted PBKDF2 hash, timestamps, external UUID.
- `RefreshTokenRecord` (auth schema) – Device-bound refresh token with expiry & revocation flag.
- `PlayerSessionLog` (gameplay schema) – Extensive audit fields: object sync, scores, object/position logs (JSON strings), cooldown tracking, flags.
- `ObjectLifecycleLog` – Spawn/claim timing & coordinates (also serialized into session JSON log).
- `Leaderboard` – High score tracking per user plus `HighScoreLog` (JSON array of `{ HighScoreAtTime, HighScoreAtTimestamp }`).
- `UserData` – Points balance plus `OwnedSkins` (JSON array of `{ SkinId }`) and `PointsLog` (JSON array of `{ PointsAtTime, PointsAtTimestamp }`). Auto‑provisioned at registration & lazily when missing.
- `Skins` – Cosmetic skin definitions (hex color values & price).
- Log entry helper models: `HighScoreLogEntry`, `PointsLogEntry`.

### Requests (Selected)
- `AuthenticationRequest` – Username/password/device combos
- `TokenValidationRequest` – For refresh / validation
- `WebSocketAuthRequest` – Initiates session handshake
- `PlayerChangeRequest` – Structured update (username/password changes) with nested payload & validation
- `SpawnItemRequest`, `ObjectClaimedRequest`, `PlayerPing` (under Pings) – Gameplay events

### Responses
- Auth: `LoginResult`, `AuthenticationResponse`, `WebSocketAuthResponse`
- Player: `PlayerResponse`, `PlayerChangeResponse`, `PlayerPingResponse`
- Gameplay: `SpawnRequestResponse`, `ObjectClaimedResponse`
- Leaderboard / Economy: `LeaderboardDataResponse`, `SkinsDataResponse`, `UserSkinsAndPointsResponse`, `BuySkinResponse`

### Services
- `AuthenticationService` – Registration (ensures unique username), login (creates refresh record + JWT), logout (revokes refresh token).
- `JwtService` – JWT issuance (30 min), validation, refresh logic (rotates refresh tokens & revokes old), device scoping.
- `PlayerService` – Retrieves player profile, validates session via refresh token, applies whitelisted username/password mutations, processes claimed objects (score server-side & tamper detection via duplicate IDs).
- `WebSocketService` – Authenticates WebSocket handshake using `JwtService.ValidateOrRefreshAsync`, creates persistent `PlayerSessionLog` entry.
- `WebSocketConnectionManager` – In-memory mapping of SessionId → WebSocket.
- `LeaderboardService` – Provides ordered leaderboard data (HTTP GET endpoint).
- `Shop` (within `ShopController`) – Skins catalog + purchase flow; also surfaces owned skins & points.

### WebSocket Handling (`WebSocketHandler`)
Single entrypoint after socket upgrade performing:
- SessionId validation against active `PlayerSessionLog` (must be un-ended).
- Message dispatch based on `request_type` field (JSON):
  - `player_ping`: Updates position, attempted client score; appends position log; replies with authoritative server score & consistency status (Ok/Bad).
  - `spawn_item_request`: Enforces spawn cooldown; if granted, generates object ID + random spawn position inside circle excluding inner radius; logs lifecycle & position.
  - `object_claimed_request`: Validates claim, sets claim timestamp, increments server score & logs score event; responds with status.
- Flags anomalies (e.g., duplicate object claim leads to "Bad" status or review flag at service layer).
- On socket disconnect: stamps session end, upserts leaderboard entry (always overwriting latest score), appends to `HighScoreLog`, upserts `UserData`, converts session score to awarded points, and appends a snapshot entry to `PointsLog`.

### Security & Auth Flow
- Password hashing: PBKDF2 (Rfc2898DeriveBytes) with per-user salt (24 bytes, 10101 iterations, SHA256).
- JWT: Symmetric HMAC SHA256, subject claim = user UUID, 30 minute lifetime.
- Refresh Token: GUID (N format) stored plaintext (recommendation: hash for leakage resistance—see Improvements below).
- Device Binding: Refresh tokens unique per (UserId, DeviceId) pair; rotation invalidates prior token.

### Public vs Protected Endpoints
Public (no JWT required):
- GET /api/Leaderboard
- GET /api/Shop/skins

Protected (JWT required):
- GET /api/Shop/user-assets/{userId}
- POST /api/Shop/buy-skin
- PATCH /player/update
- POST /ws/auth (session establishment)
- Any future write / mutation endpoints

Row-Level Authorization:
- For endpoints that include a `{userId}` route parameter or body field (e.g., `GET /api/Shop/user-assets/{userId}`, `POST /api/Shop/buy-skin`), the server now enforces that the JWT subject (`sub` claim) matches the targeted `userId`. Requests where the token's subject differs return `403 Forbidden` to prevent horizontal privilege escalation.

### Database Schemas
Schemas used: `users`, `auth`, `gameplay`.
Notable tables & columns (see migrations for full):
- `users.Users`: Id (PK), UUID, Username, PasswordHash, Salt, CreatedAt, UpdatedAt
- `auth.RefreshTokenRecord`: Id, UserId, DeviceId, EncryptedRefreshToken, ExpiresAt, IsRevoked
- `gameplay.PlayerSessionLog`: Rich telemetry (scores, spawn attempts, hashed sync fields, JSON logs, cooldown timestamps)
- `gameplay.Leaderboard`: High score & timestamps + `HighScoreLog` JSON history
- `gameplay.UserData`: Points + `OwnedSkins` JSON + `PointsLog` JSON snapshot history
- `gameplay.Skins`: Cosmetic inventory (UUID, HexValue, Price)

### Anti-Cheat / Integrity Measures
- Server-authoritative `ScoreServer` vs client attempts → status feedback in ping responses.
- Duplicate object claim detection marks session for review.
- Spawn cooldown enforcement + tracking of spawn attempts vs validated spawns.
- Full lifecycle JSON log of each spawned object & positional history for forensic review.

## Request / Response Examples (Conceptual)
Authentication (Login):
POST /authentication/login
{ "username": "alice", "password": "p@ss", "deviceId": "ios-13" }
→ 200 { userId, token, refreshToken, expiresAt }

WebSocket Auth:
POST /ws/auth
{ deviceId, userId, jwtToken, refreshToken }
→ 200 { authenticated: true, sessionId, token?, refreshToken? }

Open Socket:
wss://host:7123/ws?sessionId=... (HTTPS cert configured)

Spawn Request (WebSocket):
{ "request_type": "spawn_item_request", ... }
→ { "session_id": "...", granted: true, uniqueId: "...", spawnPosition: { x, y } }

Player Ping:
{ "request_type": "player_ping", "attemptedClientScore": 10, ... }
→ { "session_id": "...", "status": "Ok", "serverScore": 10 }

Leaderboard (HTTP, Public):
GET /api/Leaderboard
→ 200 { response_type: "leaderboard_data_response", payload: [ { Username, PlayerHighestScore }, ... ] }

Skins Catalog (Public):
GET /api/Shop/skins
→ 200 { response_type: "skins_data_response", payload: [ { SkinId, HexValue, Price }, ... ] }

Owned Skins & Points (Protected – JWT required):
GET /api/Shop/user-assets/{userId}
→ 200 { response_type: "user_skins_points_response", UserId, Points, OwnedSkinIds: [] }

Buy Skin (Protected – JWT required):
POST /api/Shop/buy-skin { userId, skinId }
→ 200 { Approved, Message, points_after_purchase?, owned_skin_ids? }

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

## Known Tradeoffs / Technical Debt
- Refresh tokens stored plaintext (security risk if DB leaked).
- No concurrency handling on session score updates (potential race if scaled horizontally without sticky sessions).
- WebSocket message parsing uses manual switch; could adopt strongly-typed message envelope & dispatch registry.
- JSON logs may grow large; consider offloading to append-only table or external telemetry store.
- Missing indexes (e.g., Users.Username UNIQUE, RefreshTokenRecord (UserId, DeviceId)).
- Leaderboard & user points updates currently not wrapped in explicit transactions beyond single SaveChanges; race conditions possible at scale.
- No authorization gating for shop GET endpoints (public enumeration) – evaluate if intended.

## Quick Start (Development)
1. Ensure local SQL Server accessible at configured `ConnectionStrings:Db`.
2. Apply migrations (dotnet ef database update) if not auto-applied.
3. Run the server (HTTPS on 7123). WebSocket path: `/ws`. REST base path: `/` (controllers by route attributes).

## High-Level Data Flows
Auth: Controller → AuthenticationService/JwtService → DbContext → tokens
Gameplay Session: WebSocketAuthController → WebSocketService → DbContext (creates session) → client opens raw socket → WebSocketHandler loops & dispatches → DbContext state updates → responses.
Player Data Update: PlayerController → PlayerService (validates refresh token) → DbContext.
Leaderboard Fetch: LeaderboardController → LeaderboardService → DbContext → response DTO.
Economy: ShopController (skins list / user assets / buy) → DbContext (Skins, UserData) → JSON arrays updated.
Purchase Flow: On successful skin purchase server validates points ≥ price, snapshots pre-deduction into PointsLog, deducts price, appends skin UUID to OwnedSkins, returns updated points & owned skin IDs.
Disconnect Awarding: WebSocketHandler disconnect path → DbContext (PlayerSessionLog, Leaderboard, UserData) → persistence of high score & point logs.

## Summary
This catalog documents the major constructs, flows, and considerations of the GameServer solution to speed onboarding, auditing, and future enhancement planning.
