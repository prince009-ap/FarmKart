# FarmKart Progress

## Major Phases

- [x] Phase 1: Repository foundation and solution setup
- [x] Phase 2: Database design and initial domain modeling
- [x] Phase 3: Authentication and role-based authorization foundation
- [ ] Phase 4: Farmer module implementation
  - [x] Phase 4.1: Farmer Profile Management
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

## Phase 3.10 Deliverables — Angular HTTP Interceptor

- [x] Create functional `authInterceptor` (`HttpInterceptorFn`) under `src/app/core/interceptors/`
- [x] Configure interceptor to append `withCredentials: true` only to requests targeting the FarmKart backend API (absolute or relative)
- [x] Clone the request objects preserving all headers, bodies, parameters, and HTTP properties (immutability rule)
- [x] Register the interceptor in `app.config.ts` using `provideHttpClient(withInterceptors([authInterceptor]))`
- [x] Add focused Vitest unit tests verifying credential appending, external requests bypass, request cloning, and token access isolation audits (spying on storage and cookies)
- [x] Confirm frontend build compiles and all unit tests pass successfully

## Phase 3.11 Deliverables — Angular Authentication and Role Guards

- [x] Create backend `AuthUserResponse` DTO and update `IAuthService` contract
- [x] Implement backend `GetCurrentUserAsync` resolving the profile's full name based on claims and role
- [x] Expose `[Authorize] GET /api/auth/current-user` endpoint in `AuthController`
- [x] Implement Angular `checkAuthSession()` caching session states using `shareReplay` to avoid duplicate concurrent checks
- [x] Create functional `authGuard` redirecting unauthenticated requests to `/login` preserving target in query params (`returnUrl`)
- [x] Create functional `roleGuard` validating user roles against route data criteria, redirecting unauthorized requests to `/unauthorized`
- [x] Create standalone placeholder components (`LoginComponent`, `RegisterComponent`, `UnauthorizedComponent`, `FarmerDashboardComponent`, `WorkerDashboardComponent`, `CustomerDashboardComponent`)
- [x] Wire up routes and attach guards in `app.routes.ts`
- [x] Add focused Vitest unit tests in `auth.guard.spec.ts`, `role.guard.spec.ts`, and `app.routes.spec.ts` verifying access, redirections, and token isolation properties
- [x] Confirm frontend/backend builds compile and all tests pass successfully

## Phase 3.12 Deliverables — Angular Login and Registration UI

- [x] Create visually-polished, responsive `LoginComponent` with Reactive Forms validation, visibility toggles, loading indicators, and redirect configurations
- [x] Create `RegisterFarmerComponent` matching simplified address contracts and farmer specific attributes (Farm Name, Farm Size, Farm Location)
- [x] Create `RegisterWorkerComponent` matching experience and expected wage constraint validators
- [x] Create `RegisterCustomerComponent` utilizing common registration fields
- [x] Configure password pattern matching in registration forms compatible with backend ASP.NET Identity rules
- [x] Configure password/confirmPassword mismatch validation checks
- [x] Set up automatic dashboard role redirects (Farmer $\rightarrow$ `/farmer`, Worker $\rightarrow$ `/worker`, Customer $\rightarrow$ `/customer`) on login completion
- [x] Set up auto-dashboard redirection for already authenticated users hitting `/auth/login`
- [x] Wire up routing paths `/auth/login` and `/auth/register/...` under `app.routes.ts`
- [x] Add focused Vitest unit tests in `login.component.spec.ts`, `register-farmer.component.spec.ts`, `register-worker.component.spec.ts`, and `register-customer.component.spec.ts` verifying inputs, validation flows, loading states, redirects, and token isolation parameters
- [x] Confirm frontend build compiles and all unit tests pass successfully

## Phase 3.13 Deliverables — Full Authentication Integration and End-to-End Validation

- [x] Run full audit of backend ASP.NET Core Identity and JWT configuration parameters
- [x] Run E2E local database queries validating correct seed roles, active user profiles mapping, and constraints
- [x] Audit CORS policies ensuring explicit allowed origins with credentials and no wildcard usage
- [x] Scan frontend files to guarantee zero manual JWT caching or cookie manipulations (document.cookie, localStorage, sessionStorage)
- [x] Run complete backend test suite (33 tests) and frontend test suite (53 tests) ensuring all pass successfully
- [x] Compile production-ready bundles for both client and server projects with zero errors

## Farmer Farm Size Unit Change

- [x] Add `FarmSizeUnit` enum with `Vigha` as the current farmer farm-size unit
- [x] Add nullable `FarmSizeUnit` column to `FarmerProfile` without reinterpreting legacy numeric values
- [x] Update `FarmerRegisterRequest` to require `FarmSize` + `FarmSizeUnit` and make `FarmName` optional
- [x] Update Angular farmer registration UI to show `[ number ] [ Vigha ]` and send `farmSizeUnit: "Vigha"`
- [x] Remove Acres from the active farmer registration flow
- [x] Add migration `AddFarmerFarmSizeUnit`
- [x] Update backend and frontend tests for Vigha registration, validation, and legacy null-unit handling

## Phase 4.1 Deliverables — Farmer Profile Management

- [x] Create `FarmerProfileResponse` and `FarmerProfileUpdateRequest` DTOs
- [x] Implement backend `IFarmerProfileService` and `FarmerProfileService` executing profile fetches and edits safely
- [x] Create `FarmerController` with authenticated GET & PUT `/api/farmer/profile` endpoints enforcing `Roles.Farmer`
- [x] Extract ownership exclusively from claims (`ClaimTypes.NameIdentifier`) to prevent identity spoofing
- [x] Create TS interfaces/models, `FarmerProfileService` (Angular), and standalone `FarmerProfileComponent`
- [x] Create visually-polished, responsive profile UI with loading, saving, error states, and edit/view mode toggles
- [x] Protect Angular `/farmer/profile` route using functional `authGuard` and `roleGuard`
- [x] Implement comprehensive backend integration tests (10 new tests) verifying role checks, 401/403/404 handling, validation, and profile field isolation (never returning PasswordHash or auth tokens)
- [x] Implement frontend unit tests (12 new tests) verifying routing, form controls, component states, loading/error flags, saving, and API behaviors
- [x] Confirm all 50 backend and 70 frontend tests pass successfully

## Phase 4.2 Deliverables - Farmer Dashboard and Navigation Shell

- [x] Create the protected `/farmer` dashboard route under a shared Farmer navigation shell
- [x] Preserve `/farmer/profile` as the existing Farmer Profile page inside the same shell
- [x] Add responsive desktop sidebar and mobile drawer navigation for Farmer modules
- [x] Add safe Coming Soon placeholder routes for Jobs, My Crops, Machinery, Marketplace, and Notifications
- [x] Display the authenticated Farmer name through the existing `AuthService` state with a safe fallback
- [x] Add focused Angular tests for dashboard rendering, profile navigation, placeholders, responsive navigation state, and route protection

## Phase 4.3 Deliverables - Farmer Job Posting

- [x] Add Farmer-owned job list, detail, create, update, and cancellation APIs
- [x] Enforce ownership from authenticated claims and return 404 for other farmers' jobs
- [x] Use `Open` for new jobs and `Cancelled` for soft cancellation; only Draft/Open jobs are editable
- [x] Add Farmer Jobs list, detail, create, and edit Angular pages under protected Farmer routes
- [x] Reuse the existing Job table and database constraints; no new migration required
