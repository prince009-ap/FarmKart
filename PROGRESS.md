# FarmKart Progress

## Major Phases

- [x] Phase 1: Repository foundation and solution setup
- [x] Phase 2: Database design and initial domain modeling
- [/] Phase 3: Authentication and role-based authorization foundation
- [ ] Phase 4: Farmer module implementation
- [ ] Phase 5: Worker module implementation
- [ ] Phase 6: Customer module implementation
- [ ] Phase 7: Marketplace and crop selling flows
- [ ] Phase 8: Auction and bidding flows
- [ ] Phase 9: Real-time chat and notifications with SignalR
- [ ] Phase 10: AI service integration and intelligent assistant features
- [ ] Phase 11: Test expansion, hardening, and deployment readiness

## Phase 1 Deliverables

- [x] Create Angular frontend shell
- [x] Create ASP.NET Core backend solution and projects
- [x] Add layered project references
- [x] Prepare Entity Framework Core and SQL Server wiring
- [x] Add root documentation files
- [x] Confirm backend build
- [x] Confirm frontend build

## Phase 2 Deliverables

- [x] Inspect existing backend architecture before changes
- [x] Create initial domain entities and enums
- [x] Configure EF Core relationships, indexes, precision, and check constraints
- [x] Update `FarmKartDbContext` with domain DbSets
- [x] Generate initial migration `InitialFarmKartDomain`
- [x] Confirm backend build
- [x] Confirm backend tests
- [x] Update architecture and setup documentation

## Phase 3.1 Deliverables — ASP.NET Core Identity Foundation

- [x] Install ASP.NET Core Identity Entity Framework package
- [x] Create ApplicationUser inheriting from IdentityUser<Guid>
- [x] Centralize Farmer, Worker, and Customer role names
- [x] Make FarmKartDbContext inherit from IdentityDbContext
- [x] Configure profile-user 1-to-1 relationships and restrict delete behavior
- [x] Add Identity configuration to dependency injection container
- [x] Scaffold AddIdentityFoundation migration
- [x] Implement backend verification tests
- [x] Confirm backend build and tests pass

## Phase 3.2 Deliverables — Application Roles Setup

- [x] Centralize Farmer, Worker, and Customer role names (from Phase 3.1)
- [x] Create static role seeder helper `IdentityRoleSeeder` in Infrastructure
- [x] Integrate role seeding in Program.cs startup pipeline
- [x] Add tests verifying centralized role constants and seeder idempotency
- [x] Confirm build and integration tests pass

## Phase 3.3 Deliverables — Farmer Registration

- [x] Create FarmerRegisterRequest and FarmerRegistrationResponse DTOs with validators
- [x] Define custom DuplicateEmailException and RegistrationFailedException types
- [x] Define IAuthService contract in Application layer
- [x] Implement concrete AuthService in Infrastructure layer executing in a transaction
- [x] Register IAuthService in dependency injection container
- [x] Create AuthController containing POST /api/auth/register/farmer endpoint
- [x] Add integration tests covering successful registration, database rollback, duplicates, and weak passwords
- [x] Confirm build and integration tests pass

## Phase 3.4 Deliverables — Worker Registration

- [x] Create WorkerRegisterRequest and WorkerRegistrationResponse DTOs with validators
- [x] Add RegisterWorkerAsync to IAuthService contract
- [x] Implement RegisterWorkerAsync concrete service execution inside AuthService
- [x] Add POST /api/auth/register/worker endpoint in AuthController
- [x] Add integration tests verifying Worker registration, duplicate rejection, and rollback consistency
- [x] Confirm build and integration tests pass

## Phase 3.5 Deliverables — Customer Registration

- [x] Create CustomerRegisterRequest and CustomerRegistrationResponse DTOs with validators
- [x] Add RegisterCustomerAsync to IAuthService contract
- [x] Implement RegisterCustomerAsync concrete service execution inside AuthService
- [x] Add POST /api/auth/register/customer endpoint in AuthController
- [x] Add integration tests verifying Customer registration, duplicate rejection, and rollback consistency
- [x] Confirm build and integration tests pass

## Database Connection and Migration Verification

- [x] Local SQL Server database configured (`LAPTOP-K5SJ7HFJ\SQLEXPRESS`)
- [x] FarmKartDb database created
- [x] Existing EF Core migrations applied successfully (`InitialFarmKartDomain` and `AddIdentityFoundation`)

## Registration Simplification (Requirement Change)

- [x] Remove Latitude, Longitude, City, State, and Pincode from DTOs and validations
- [x] Update AuthService to map Address and default other address/coordinate fields
- [x] Update Farmer, Worker, and Customer registration integration tests
- [x] Confirm build and integration tests pass

## Phase 3.6 Deliverables — Login API

- [x] Create LoginRequest and LoginResponse DTOs with validations
- [x] Define custom generic InvalidCredentialsException type returning 401 Unauthorized
- [x] Add LoginAsync to IAuthService contract
- [x] Implement LoginAsync concrete service execution inside AuthService resolving roles and profile names
- [x] Add POST /api/auth/login endpoint in AuthController
- [x] Add integration tests verifying Farmer, Worker, and Customer login successes, invalid passwords, unknown emails, and DTO security constraints
- [x] Confirm build and integration tests pass

## Phase 3.7 Deliverables — JWT Token Generation

- [x] Define JwtOptions configuration model mapping to JwtSettings
- [x] Define IJwtTokenService abstraction in Application layer
- [x] Implement concrete JwtTokenService in Infrastructure layer signing tokens with HmacSha256
- [x] Configure framework token validation parameters inside ServiceCollectionExtensions
- [x] Configure local development secrets inside API project's User Secrets store
- [x] Inject IJwtTokenService into AuthService and include generated JWT inside direct LoginResponse
- [x] Add unit and integration tests verifying claims, signature validation, and token lifetimes

## Phase 3.8 Deliverables — JWT Storage using HttpOnly Cookie

- [x] Extend JwtOptions to support CookieName, CookieSecure, and CookieSameSite configurations
- [x] Define LoginResult in Application to hold token between service and presentation layers
- [x] Update LoginResponse DTO to remove direct Token parameter (prevent JS exposure)
- [x] Implement HTTP-level cookie-writing inside API's AuthController.Login
- [x] Configure JwtBearer authentication handler to extract token from HttpOnly cookie
- [x] Configure CORS to allow Angular dev server origins (http/https localhost:4200) with credentials
- [x] Add test-only authenticated endpoint `/api/auth/test-auth` for integration testing
- [x] Add integration tests verifying cookie structure (HttpOnly, Path, Expiration, Secure), claims authentication, and unauthorized request rejections
- [x] Confirm backend build and tests pass successfully

## Phase 3.9 Deliverables — Angular Authentication Service

- [x] Configure Angular environments for API URL matching `http://localhost:5055/api`
- [x] Register `provideHttpClient()` in `app.config.ts`
- [x] Create TS interfaces/models matching backend DTO schemas (camelCase)
- [x] Create centralized `AuthService` handling farmer/worker/customer registration and cookie logins
- [x] Configure `withCredentials: true` on login and auth requests to allow automatic HttpOnly cookie transmission
- [x] Implement simple reactive `currentUser$` authentication state using RxJS `BehaviorSubject`
- [x] Add focused Vitest unit tests verifying login calls, registration endpoints, state updates, and JWT isolation properties
- [x] Confirm frontend build compiles and all unit tests pass successfully


