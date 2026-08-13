# FarmKart Architecture

## Solution Overview

FarmKart is split into a standalone Angular client and a layered ASP.NET Core backend. Phase 2 adds the initial domain model and SQL Server schema design without implementing APIs, authentication flows, or business workflows.

## Backend Architecture

### `FarmKart.API`

- Presentation layer and HTTP entry point
- Composition root for dependency injection
- Controllers, middleware, API configuration, and health endpoint

### `FarmKart.Application`

- Application services and orchestration
- DTOs, validators, interfaces, and use-case logic
- Abstractions consumed by the API and implemented in infrastructure

### `FarmKart.Domain`

- Core entities, value objects, enums, and domain rules
- No framework-specific dependencies

### `FarmKart.Infrastructure`

- Entity Framework Core integration
- SQL Server configuration
- Persistence implementations and future external service adapters

### `FarmKart.Tests`

- Unit tests for important business logic
- Future integration test coverage

## Entity Overview

### Profiles

- `FarmerProfile`
- `WorkerProfile`
- `CustomerProfile`

These are application profiles only. They store business-facing profile data and a stable `UserId` that will later connect to ASP.NET Core Identity. They do not store passwords, tokens, or authentication secrets.

### Worker and Job Domain

- `Skill`
- `WorkerSkill`
- `Job`
- `JobApplication`
- `WorkerAssignment`
- `Attendance`
- `WorkerPayment`

### Machinery Rental Domain

- `MachineryCategory`
- `Machinery`
- `MachineryImage`
- `MachineryRentalRequest`
- `MachineryRental`
- `MachineryDamageReport`
- `MachineryDamageReportImage`

### Crop and Marketplace Domain

- `Crop`
- `CropImage`
- `CropListing`
- `Order`
- `OrderItem`
- `Payment`
- `Delivery`

### Auction Domain

- `Auction`
- `Bid`
- `AuctionWinner`

### Communication and Trust Domain

- `Conversation`
- `ConversationParticipant`
- `Message`
- `Notification`
- `Review`

## Main Relationships

- Farmer profiles own jobs, machinery, crops, crop listings, auctions, and worker payments.
- Worker profiles connect to skills, job applications, assignments, attendance, and worker payments.
- Customer profiles connect to bids, orders, and auction wins.
- Jobs accept many applications and can create many worker assignments.
- Machinery supports farmer-to-farmer rental requests, approved rentals, and damage reports.
- Crops can have many images and many sale listings.
- Crop listings can participate in orders or, when configured for auction, own one auction.
- Auctions collect many bids and can finalize one auction winner.
- Conversations hold many participants and many messages.

## Database Architecture

- SQL Server is used because the platform needs strong relational integrity, predictable transactional behavior, mature tooling, and compatibility with EF Core and future ASP.NET Core Identity integration.
- Entity Framework Core is configured in `FarmKart.Infrastructure`.
- `FarmKartDbContext` exposes DbSets for all Phase 2 entities and applies Fluent API configurations from the infrastructure assembly.
- Address/location fields are modeled with a reusable owned value object so profiles and deliveries can share the same practical address shape without over-normalizing.
- Decimal precision is explicitly configured for money, quantities, and coordinates.
- Check constraints are used for core invariants such as non-negative monetary values, valid date ranges, and rating bounds.
- Historical and transactional relationships default to `Restrict` delete behavior to avoid accidental loss of orders, payments, rentals, bids, or worker payment history.
- Phase 2 migration: `InitialFarmKartDomain`

## Why SQL Server

- Strong fit for transactional marketplace and auction data
- Mature support for relational constraints and indexing
- First-class EF Core provider support
- Good path for future Identity integration, reporting, and operational tooling

## Authentication Relationship

- ASP.NET Core Identity foundation is configured in Phase 3.1.
- Application roles setup is configured in Phase 3.2.
- Farmer registration is configured in Phase 3.3.
  - Farmer registration request is processed via `FarmerRegisterRequest` DTO and validated on the backend.
  - Farmer farm size is stored as `FarmSize` with an explicit `FarmSizeUnit`. The current supported registration unit is `Vigha`.
  - `FarmName` is optional during registration.
  - Farmer registration intentionally uses a single free-text `Address` field. Latitude, longitude, city, state, and pincode are not collected during registration; map-based location selection is deferred to a later phase.
  - Registration runs under an EF Core transaction to consistently create `ApplicationUser` (hashing password via Identity), assign the `Farmer` role, and insert the corresponding `FarmerProfile`.
  - Email duplicates return a 409 Conflict response.
  - Successful registration returns a safe `FarmerRegistrationResponse` (includes `UserId`, `Role`, `FullName`, `Email`, and a success message; excludes JWT or password details).
- Worker registration is configured in Phase 3.4.
  - Worker registration request is processed via `WorkerRegisterRequest` DTO and validated on the backend.
  - Registration runs under an EF Core transaction to consistently create `ApplicationUser` (hashing password via Identity), assign the `Worker` role, and insert the corresponding `WorkerProfile`.
  - Email duplicates return a 409 Conflict response.
  - Successful registration returns a safe `WorkerRegistrationResponse` (includes `UserId`, `Role`, `FullName`, `Email`, and a success message; excludes JWT or password details).
- Customer registration is configured in Phase 3.5.
  - Customer registration request is processed via `CustomerRegisterRequest` DTO and validated on the backend.
  - Registration runs under an EF Core transaction to consistently create `ApplicationUser` (hashing password via Identity), assign the `Customer` role, and insert the corresponding `CustomerProfile`.
  - Email duplicates return a 409 Conflict response.
  - Successful registration returns a safe `CustomerRegistrationResponse` (includes `UserId`, `Role`, `FullName`, `Email`, and a success message; excludes JWT or password details).
- Login API is configured in Phase 3.6.
  - Login request is processed via `LoginRequest` DTO containing `Email` and `Password`.
  - Validates credentials against Identity database via `UserManager<ApplicationUser>`.
  - Resolves role and queries profile (`FarmerProfile`, `WorkerProfile`, `CustomerProfile`) for the `FullName`.
  - Invalid credentials return a generic `401 Unauthorized` response to avoid email enumeration.
  - Returns safe user details (`LoginResponse`) excluding JWT/cookies/passwords.
- The `ApplicationUser` class inherits from `IdentityUser<Guid>` and acts as the central user model.
- Each profile (`FarmerProfile`, `WorkerProfile`, `CustomerProfile`) defines a `UserId` of type `Guid` which maps one-to-one with `ApplicationUser`.
- Delete behavior is set to `Restrict` on profile-user relationships to avoid accidental deletion of profile and historical data.
- The three application roles are centralized as constants in `Roles.cs`:
  - `Farmer`
  - `Worker`
  - `Customer`
- Roles are automatically seeded at startup in an idempotent manner via `IdentityRoleSeeder` in `Program.cs`. Running the application multiple times will not duplicate roles.
- The authentication flow (JWT generation, validation, and HttpOnly Secure Cookie storage (`FarmKartAuth`)) is implemented on the backend. Angular client integration is NOT implemented yet and will be added in later phases.

## Auction Data Model

- `CropListing` optionally owns one `Auction`
- `Auction` belongs to a farmer and tracks starting price, current highest bid, increment, time window, and status
- `Bid` belongs to an auction and a customer
- `AuctionWinner` finalizes the winning bid and customer after auction completion
- The schema is ready for future real-time bidding without implementing SignalR yet

## Machinery Rental Data Model

- `Machinery` belongs to an owner farmer and category
- `MachineryRentalRequest` models renter-to-owner requests between farmers
- `MachineryRental` models the approved rental lifecycle
- `MachineryDamageReport` and image children preserve post-rental issue history
- Delete behaviors are restrictive on rental history to protect completed records

## Marketplace Data Model

- Farmers create `Crop` records and `CropListing` sale offers
- Customers place `Order` records with one or more `OrderItem` rows
- `Payment` and `Delivery` remain separate so payment and delivery state can evolve independently
- The model supports direct sale and auction sale flows without implementing payment gateway code yet

## Frontend Architecture

The Angular app follows a feature-based structure:

- `core/` for singleton services, guards, and interceptors
- `shared/` for reusable UI and presentation primitives
- `features/` for business domains such as farmer, worker, customer, jobs, crops, marketplace, auction, chat, notifications, and AI
- `layouts/` for application shells and top-level page structure
- `app.routes.ts` for route composition

## Frontend and Backend Communication

- The Angular client communicates with the ASP.NET Core API over HTTP using the registered `provideHttpClient()` mechanism.
- Authentication is handled using a centralized Angular `AuthService` which manages a reactive `currentUser$` state based on a `BehaviorSubject`.
- On application bootstrap or page refreshes, `checkAuthSession()` queries the backend `/api/auth/current-user` endpoint to re-establish the user's session safely. The request is cached using RxJS `shareReplay` to prevent redundant concurrent API requests.
- A functional HTTP interceptor (`authInterceptor`) is registered globally via `provideHttpClient(withInterceptors([authInterceptor]))` to automatically configure `withCredentials: true` for all backend requests.
- The JWT remains completely hidden from client-side Angular code; browser storage and cookie APIs are never used to access or manage token payloads.
- Angular route guards (`authGuard` and `roleGuard`) protect features routes (such as `/farmer`, `/worker`, and `/customer`) using metadata configured in `app.routes.ts`. Unauthorized attempts redirect unauthenticated requests to `/auth/login` and unauthorized roles to `/unauthorized`.
- Authentication forms (`LoginComponent`, `RegisterFarmerComponent`, `RegisterWorkerComponent`, `RegisterCustomerComponent`) are designed with Angular Reactive Forms, enforcing strict format constraints matching backend requirements (such as password complexities, non-negative labor attributes, and address lines). On successful login, the application redirects the user automatically to their dashboard route. Already authenticated users visiting `/auth/login` are immediately redirected to their dashboards.
- Backend business authorization will be applied as feature APIs are implemented.
- Logout, password reset, email verification, and refresh token strategies are intentionally deferred to future iterations.
- SignalR will be introduced later for auctions, chat, and notifications.

## Farmer Profile Management

- **Endpoint Security**: GET & PUT `/api/farmer/profile` are secured on the backend using `[Authorize(Roles = Roles.Farmer)]`.
- **Identity Isolation**: The client never passes the `UserId` to the profile endpoints. Instead, the backend controller resolves the profile owner exclusively from the token's authenticated claim (`ClaimTypes.NameIdentifier`).
- **Read-Only Email**: Email addresses are considered read-only in this flow. The DTO `FarmerProfileUpdateRequest` does not accept email, and the service does not alter the underlying ASP.NET Identity email record.
- **Owned Address Info**: Address updates are saved directly into the owned value object `AddressInfo` associated with the farmer's profile, keeping the simple one-line address representation.
- **Frontend Flow**: The standalone `FarmerProfileComponent` interacts with the `FarmerProfileService` via HttpClient. It uses local Angular signals for loading, saving, view/edit modes, and error state tracking. Form validations match backend DB constraints (e.g., non-negative farm size).
- **Unit/Integration Tests**: Covered by 10 xUnit integration tests in `FarmerProfileTests.cs` (checking endpoints under different authentication and error states) and 12 Vitest specs in `farmer-profile.component.spec.ts` (mocking the profile service to assert UI bindings and state transitions).

## Farmer Dashboard and Navigation

- The protected `/farmer` route now hosts `FarmerShellComponent`, a responsive Farmer-only workspace shell with a desktop sidebar and mobile navigation drawer.
- Its child routes render the dashboard at `/farmer` and the existing profile page at `/farmer/profile`; the parent route applies the existing `authGuard` and `roleGuard` with the `Farmer` role.
- The shell reads only the existing `AuthService.currentUser$` state to show the farmer's name, and uses `Farmer` as a safe fallback when the name is unavailable. It does not inspect cookies, tokens, or browser storage.
- Jobs, My Crops, Machinery, Marketplace, and Notifications use protected `ComingSoonComponent` routes. They provide stable navigation targets without adding APIs, domain workflows, or placeholder data for future modules.

## Farmer Job Posting

- Farmer job APIs are scoped to `/api/farmer/jobs` and use the authenticated claim to resolve the owning `FarmerProfile`; request DTOs never accept a Farmer or user identifier.
- New jobs begin in `Open`. Updates are permitted only while a job is `Draft` or `Open`; cancellation is a soft transition to `Cancelled`, preserving job history rather than deleting records.
- The existing `Job` schema supplies work category, workers required, wage, schedule, working hours, and farm location. It has no job-to-skill relationship, so this phase does not introduce an unsupported duplicate skill model or a migration.
- Angular routes `/farmer/jobs`, `/farmer/jobs/create`, `/farmer/jobs/:id`, and `/farmer/jobs/:id/edit` remain inside the existing guarded Farmer shell.

## Worker Job Browsing and Job Application

- **Worker APIs**: Worker endpoints are exposed under `/api/worker` (`GET /api/worker/jobs`, `GET /api/worker/jobs/{id}`, `POST /api/worker/jobs/{id}/apply`, `GET /api/worker/applications`) and strictly protected with `[Authorize(Roles = Roles.Worker)]`.
- **Identity Isolation & Ownership**: The backend resolves `WorkerProfile` exclusively from the authenticated JWT user claim (`ClaimTypes.NameIdentifier`). The client cannot pass or alter `WorkerId` or `UserId`.
- **Job Visibility**: Workers view only jobs with `JobStatus.Open`. Each job response includes a `HasApplied` boolean flag computed specifically for the authenticated worker.
- **Application Flow & Status**: Applications are created with `ApplicationStatus.Pending` and recorded in `JobApplication`. Workers cannot select or alter application status.
- **Duplicate Prevention**: Re-applying to the same job returns `HTTP 409 Conflict`. Database uniqueness is enforced by the index `(JobId, WorkerProfileId)` on `JobApplication`.
- **Frontend Architecture**:
  - `WorkerJobsComponent` (`/worker/jobs`) provides search by title/crop/description, category and location filters, responsive job cards, and empty states.
  - `WorkerJobDetailComponent` (`/worker/jobs/:id`) displays work terms, amenities, location, an optional application note field, and disables the Apply button when already applied.
  - `WorkerApplicationsComponent` (`/worker/applications`) lists submitted applications with color-coded status badges.
- **Route Security**: Worker child routes are protected by `authGuard` and `roleGuard` with `roles: ['Worker']`. Non-worker roles and unauthenticated users are denied access.

## Farmer Application Management and Hiring Decision

- **Farmer Application APIs**: Farmer application endpoints are exposed under `/api/farmer` (`GET /api/farmer/jobs/{jobId}/applications`, `GET /api/farmer/applications/{applicationId}`, `POST /api/farmer/applications/{applicationId}/accept`, `POST /api/farmer/applications/{applicationId}/reject`) and guarded by `[Authorize(Roles = Roles.Farmer)]`.
- **Ownership Verification**: Requests verify that the targeted job or application belongs to a job owned by the authenticated `FarmerProfile`. Unowned resource requests return `404 Not Found` to prevent resource probing.
- **Status Lifecycle & Rules**: Only `Pending` applications can be accepted or rejected. Finalized states (`Accepted` / `Rejected`) cannot be altered or re-accepted/re-rejected (returns `409 Conflict`).
- **Worker Capacity Enforcement**: Accepting an application calculates `Accepted` count vs `Job.WorkersRequired`. If capacity is reached, backend rejects additional accepts with `409 Conflict`.
- **Frontend Architecture**:
  - `FarmerJobApplicationsComponent` (`/farmer/jobs/:jobId/applications`) displays applicant profile details (Name, Phone, Experience, Skills, Note, Status Badge, Applied Date) and confirmable `Accept`/`Reject` actions.
  - Job cards in `FarmerJobsComponent` include an "Applications" action button.
- **Security & Privacy**: Applicant passwords, hashes, and identity internal fields are never exposed. Access is strictly guarded on both backend controllers and frontend Angular route guards.

## Worker Assignment

- **Atomic Acceptance & Assignment**: Accepting an application (`POST /api/farmer/applications/{applicationId}/accept`) automatically creates a `WorkerAssignment` (`Status = AssignmentStatus.Active`, `JobApplicationId = applicationId`) within the same database transaction.
- **Assignment APIs**: Exposed under `/api/farmer` (`GET /api/farmer/jobs/{jobId}/assignments`) and `/api/worker` (`GET /api/worker/assignments`, `GET /api/worker/assignments/{id}`).
- **Ownership & Security**: Server derives farmer/worker identities strictly from authenticated JWT user claims (`ClaimTypes.NameIdentifier`). Unowned job or assignment queries return `404 Not Found`.
- **Capacity & Duplicate Guardrails**: Creating an assignment verifies active assignments count < `Job.WorkersRequired` and checks that the worker is not already assigned (`409 Conflict`).
- **Frontend Architecture**:
  - `FarmerJobAssignmentsComponent` (`/farmer/jobs/:jobId/assignments`) displays assigned workers, experience, phone, skills, schedule, and status badge.
  - `WorkerAssignmentsComponent` (`/worker/assignments`) & `WorkerAssignmentDetailComponent` (`/worker/assignments/:id`) display job assignment terms for workers.

## Attendance Management

- **Attendance APIs**:
  - Farmer: `GET /api/farmer/jobs/{jobId}/attendance`, `GET /api/farmer/jobs/{jobId}/attendance/{date}`, `POST /api/farmer/jobs/{jobId}/attendance`, `PUT /api/farmer/attendance/{attendanceId}` under `[Authorize(Roles = Roles.Farmer)]`.
  - Worker: `GET /api/worker/attendance` and `GET /api/worker/assignments/{assignmentId}/attendance` under `[Authorize(Roles = Roles.Worker)]`.
- **Assignment Relationship & Validation**: Attendance can only be recorded for a valid `WorkerAssignment`. Attempts to mark attendance for unassigned workers or unowned jobs return `400 Bad Request` or `404 Not Found`.
- **Date-Based Upserts & Idempotency**: Batch marking accepts a `DateOnly` date. The backend atomically updates existing records or inserts new records for each `(WorkerAssignmentId, Date)`, respecting the Entity Framework Core unique index `(WorkerAssignmentId, Date)`.
- **Derived Summary Statistics**: Worker attendance views calculate summary statistics (Total Days, Present Days, Absent Days, Half Days, Leave Days, Attendance Rate %) on the fly from attendance records without mutating schema.
- **Ownership & Security**: Server derives identities strictly from authenticated JWT user claims (`ClaimTypes.NameIdentifier`). Farmers cannot mark attendance for other farmers' jobs; Workers cannot view or modify other workers' attendance records.
- **Frontend Architecture**:
  - `FarmerAttendanceComponent` (`/farmer/jobs/:jobId/attendance`): Date selection, quick "Mark All Present/Absent" actions, worker attendance status selection (`Present`, `Absent`, `HalfDay`, `Leave`), optional notes, and attendance history log.
  - `WorkerAttendanceComponent` (`/worker/attendance` & `/worker/assignments/:assignmentId/attendance`): Metric summary cards (Total Days, Present, Absent, Half Day/Leave, Attendance Rate %) and detailed attendance history log table.

