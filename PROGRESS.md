# FarmKart Progress

## Major Phases

- [x] Phase 1: Repository foundation and solution setup
- [x] Phase 2: Database design and initial domain modeling
- [x] Phase 3: Authentication and role-based authorization foundation
- [x] Phase 4: Core Module Implementation
  - [x] Phase 4.1: Farmer Profile Management
  - [x] Phase 4.2: Farmer Dashboard and Navigation Shell
  - [x] Phase 4.3: Farmer Job Posting
  - [x] Phase 4.4: Worker Job Browsing and Job Application
  - [x] Phase 4.5: Farmer Application Management and Accept/Reject
  - [x] Phase 4.6: Worker Assignment
  - [x] Phase 4.7: Attendance Management
- [x] Phase 5: Worker module implementation
  - [x] Phase 5.1: Worker Profile Management
  - [x] Phase 5.2: Worker Skills & Experience Management
  - [x] Phase 5.3: Worker Availability Management
  - [x] Phase 5.4: Worker Job Preferences
  - [x] Phase 5.5: Worker In-App Notifications
  - [x] Phase 5.6: Worker Ratings & Reviews
  - [x] Phase 5.7: Worker Earnings Management
  - [x] Phase 5.8: Worker Work History
  - [x] Phase 5.9: Worker Profile Verification & Completion
- [x] Phase 6: Customer module implementation
  - [x] Phase 6.1: Farmer Crop Management
  - [x] Phase 6.2: Crop Inventory / Stock Management
  - [x] Phase 6.3: Farmer Auction Creation
  - [x] Phase 6.4: Customer Dashboard and Navigation Shell
  - [x] Phase 6.5: Customer Auction Marketplace
  - [x] Phase 6.6: Customer Bidding System
  - [x] Phase 6.7: Auction Ending and Winner Finalization
  - [x] Phase 6.8: Auction Winner Payment / Checkout
  - [x] Phase 6.9: Multi-Quantity Auction / Partial Allocation System
  - [x] Safe Pricing Model Change: ₹ per 20 Kg (₹ per Man) Across System
  - [x] Farmer Auction UI Fixes: Timezone Display & Auction Visibility
- [x] Phase 7: Marketplace and crop selling flows
  - [x] Phase 7.1: Order Creation After Successful Payment
  - [x] Phase 7.2: Customer My Orders Module (List, Filtering, Search, Sorting, Security)
  - [x] Phase 7.3: Customer Order Details & Order Timeline Module (Full Breakdown, Purchase Summary, Payment & Seller Info, Visual Timeline, Security Authorization)
  - [x] Phase 7.4: Farmer Order Management & Farmer Order Details Module (Summary Metrics Cards, Orders List, Search & Status Filters, Detailed Breakdown, Separate Order Items for Multiple Winners, Security Authorization)
  - [x] Phase 7.5: Order Fulfillment Status Workflow Module (Lifecycle: CONFIRMED -> READY_FOR_PICKUP -> DISPATCHED/PICKED_UP -> DELIVERED -> COMPLETED, Status History Logging, Role Authorization, Validation Guardrails)
  - [x] Phase 7.7: Farmer Handover / Dispatch Module (Action buttons: MARK AS PICKED UP / DISPATCH ORDER for READY_FOR_PICKUP orders, Confirmation Modal Dialogs with optional notes, Farmer-only status transition backend authorization)
  - [x] Phase 7.8: Customer Order Tracking Module (Route /customer/orders/:id/track, Visual progress timeline with real timestamps, Friendly status messages, Pickup/Delivery information card, No fake live tracking, 416 Backend Integration Tests & 235 Frontend Specs)
  - [x] Phase 7.9: Order Notifications System (Structured NotificationType enum, GET /api/notifications with claims user isolation, GET /api/notifications/unread-count, PUT/PATCH /api/notifications/{id}/read & read-all, Idempotency checks, Order creation & status transition notifications, Navbar notification bell badge with live unread counter)
  - [x] Phase 7.10: Final Stock & Order Settlement Module (OrderSettlement domain entity & EF Core mapping, IsSettled flag on AuctionOrder, SettleOrderAsync service method, Automatic settlement on paid order creation & order completion, Non-negative stock deduction guardrails, Idempotent settlement, SETTLED badges in Farmer Order Details UI, 424 Backend Integration Tests & 235 Frontend Specs)
  - [x] Phase 7.11: Invoice / Receipt Module (Invoice domain entity & EF Core mapping, Idempotent invoice creation with deterministic INV-YYYYMMDD-XXXX numbering, Rate per Man ₹/Man display, Quantity in Kg & Man, Tax ₹0.00, Snapshot stability, Security claims verification, Printable PDF/HTML view via window.print(), Dedicated Customer & Farmer invoice routes /customer/orders/:id/invoice and /farmer/orders/:id/invoice)
  - [x] Phase 7.12: Order History & Completed Orders Module (Active / Completed / All order status tab filtering for Customer and Farmer workspaces, Completed order immutability guardrails, Search & sorting, View Invoice buttons on completed/paid order cards, 431 Backend Integration Tests & 243 Frontend Specs)
- [x] Phase 8: Ratings, Reviews, Wishlist & Advanced Search
  - [x] Phase 8.1: Ratings & Reviews Module (OrderReview domain using existing Review entity, CreateOrderReviewAsync with ownership/payment/completion validation, duplicate rejection at service + unique DB index, 1–5 star enforcement, 5–1000 char optional comment, farmer average rating calculation (rounded to 1 decimal), farmer in-app notification on new review via INotificationService, GetCustomerReviewsAsync, GetFarmerRatingSummaryAsync, POST/GET/PUT /api/customer/orders/{id}/review, GET /api/customer/reviews, GET /api/farmer/orders/{id}/review, GET /api/farmer/reviews, OrderReviewDialogComponent (interactive star selector, comment textarea), CustomerReviewsComponent (/customer/reviews), FarmerReviewsComponent (/farmer/reviews) with avg rating summary, Rate & Review button on COMPLETED+PAID order detail page, Edit Review support, My Reviews nav items in both shells, 447 Backend Integration Tests & frontend build succeeded)
  - [x] Phase 8.2: Wishlist / Favorites Module (WishlistItem entity in Communication.cs, WishlistItemType enum for Crop/Auction/Machinery extensibility, unique index on UserId+ItemType+ItemId for DB-level duplicate prevention, Claims-authenticated user isolation, AddAsync idempotent add, RemoveAsync, GetWishlistAsync enriched with live crop/auction data, GetCountAsync, CustomerWishlistController /api/customer/wishlist, WishlistButtonComponent ♡/♥ toggle, CustomerWishlistComponent at /customer/wishlist with All/Crop/Auction tab filtering, My Wishlist nav item in customer shell)
  - [x] Phase 8.3: Advanced Search & Filters Module (Case-insensitive partial search across crop name, variety, crop type, and farmer name; Min & Max PricePerMan ₹/Man range filter; Min & Max QuantityKg range filter; Ending Soon 24h filter; Status & Category filters; Combined AND logic; Page-based backend pagination with PagedCustomerAuctionResponse; 300ms debounced search input; IsFavorited wishlist state injection into marketplace cards; Clear All Filters button; 16 integration tests passing in WishlistAndSearchTests & Angular build succeeded)
  - [x] Phase 8.4: Machinery Rental Marketplace (Complete production-ready bi-directional machinery marketplace allowing both Farmers and Customers to own, list, and rent machinery; Driver options with DriverAvailable toggle, DriverChargePerDay, and renter choice 'I will drive myself' vs 'I want a driver'; Server-side price breakdown & snapshot calculations for RentPerDay, DriverChargePerDay, MachineryAmount, DriverAmount, TotalAmount, and TotalPayableAmount; Full IQueryable database-side filtering across Name, Brand, Model, Category, Location, City, State, Owner Name, Min/Max Rent, Driver Available, Date Availability range, Sorting, and Pagination; Concurrency protection with database transactions; Role-based claim isolation & self-rental prevention; Full Angular UI with search/filters, driver selection radio controls, price breakdown summary, My Machinery management for both roles, My Rentals tracking, and navigation links in both shells; 30 backend integration tests passing in MachineryRentalMarketplaceTests & 25 frontend vitest specs passing)
  - [x] Phase 8.5: Farmer Public Profile + Machinery Ratings & Reviews (Public Farmer Profile page at /farmers/:farmerId showing Farmer Name, Farm Name, Location, Member Since date, Average Farmer Rating (calculated ONLY from crop order reviews), Verified Reviews, Active Auctions with status filter tabs, and Listed Machinery owned by this farmer; Machinery Ratings & Reviews reusing generic Review entity with ReviewEntityType.MachineryRental; Renter eligibility validation on completed rentals; Owner-authorized machinery review management endpoint GET /api/my-machinery/{id}/reviews verifying machinery.OwnerUserId == authenticatedUserId; Unified My Reviews endpoint GET /api/my-reviews with tabbed separation for CROP REVIEWS and MACHINERY REVIEWS with dynamic DB counts for both Farmers and Customers; Owner review dialog in My Machinery management; 501/501 total backend tests passing & Angular build succeeded)
  - [x] Phase 8.6: Farmer Analytics Dashboard (Backend GET /api/farmer/analytics guarded by [Authorize(Roles="Farmer")]; Real EF Core aggregations scoped to authenticated farmer ID; Date range filters Today, Last 7 Days, Last 30 Days, This Month, Last Month, This Year, Custom; KPI summary cards for Auctions, Quantity listed/sold/remaining in Kg & Man, Orders, Revenue, Farmer Rating & distribution, Machinery listed/rented/income/rating, Driver revenue, and bi-directional rented machinery spending; Time-series SVG charts for Revenue, Quantity Sold, and Orders over time; Top Selling Crops, Auction Performance Table, and Top Rented Machinery tables; 2 backend unit tests passing in FarmerAnalyticsTests)
  - [x] Phase 8.7: Customer Analytics Dashboard (Backend GET /api/customer/analytics guarded by [Authorize(Roles="Customer")]; Real EF Core aggregations scoped to authenticated customer ID; Date range filters; KPI summary cards for Auctions participated, Bids placed, Winning bids, Winning rate %, Quantity purchased in Kg & Man respecting partial allocation, Crop spending, AOV, Highest order value, Machinery rentals spending, Customer who owns machinery income, Driver spending, Reviews written, and Wishlist count; Time-series SVG charts for Spending and Bidding activity over time; Top Purchased Crops and Machinery Rental History tables; 2 backend unit tests passing in CustomerAnalyticsTests; 505/505 total backend integration tests passing & Angular build succeeded)
  - [x] Phase 8.8: Profile System + Profile Image Upload (Complete user profile experience for Farmer, Customer, and Worker roles; Customer My Profile implemented replacing "SOON"; Profile image upload supporting real file selector input <input type="file" accept="image/*"> with JPG/PNG/WEBP formats and 5 MB limit; Server-side file validation for content length, MIME types, extension whitelist, and magic header byte signatures; Safe unique filename generation in wwwroot/uploads/profile-images/; Physical file deletion on image replacement and image removal; Default initial/SVG avatar fallback; Immediate reactive update of top bar / sidebar user avatars across all three role shells without requiring re-login; 6 backend integration tests passing in ProfileImageTests, 518/518 total backend tests passing & Angular build succeeded)
  - [x] Phase 8.9: Advanced Notification Center (Paged GET /api/notifications with filtering by all/unread/read status, category filtering for Auction/Order/Payment/Rental/Review/Dispute/System, text search across title and message, unread count tracking, patch mark-as-read & mark-all-as-read, delete and clear-all operations, live Angular NotificationCenterComponent page at /notifications with interactive category icons, priority tags, and smart navigation using actionUrl/relatedOrderId/relatedAuctionId; live unread badge in customer, farmer, and worker navbar shells; 1 unit test passing in ReportsAndDisputesTests)
  - [x] Phase 8.10: Reports & Disputes Management (Distinct separation between REPORT = inappropriate content/misleading listing/spam and DISPUTE = transaction issue on Order/Payment/AuctionAllocation/MachineryRental; UserReport & UserDispute EF Core entities with status lifecycle Open, UnderReview, Resolved, Rejected, Closed; Duplicate report/dispute prevention; Strict participant authorization verification on raised disputes; Timeline tracking with DisputeTimelineItemDto; Close dispute action for participants; Reusable Angular ReportModalComponent and DisputeModalComponent dialogs; Dedicated My Reports (/my-reports), My Disputes (/my-disputes), and Dispute Detail (/disputes/:id) pages; 2 unit tests passing in ReportsAndDisputesTests; 518 total backend tests passing & Angular build succeeded)
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

## Phase 4.4 Deliverables — Worker Job Browsing and Job Application

- [x] Implemented `IWorkerJobService` & `WorkerJobService` for worker job catalog (`JobStatus.Open`), job details (`HasApplied`), application submission (`JobApplication`), and application history.
- [x] Created `WorkerController.cs` with `[Authorize(Roles = Roles.Worker)]` exposing `/api/worker/jobs`, `/api/worker/jobs/{id}`, `/api/worker/jobs/{id}/apply`, and `/api/worker/applications`.
- [x] Implemented server-side worker profile resolution from JWT claims; prevented `WorkerId` spoofing.
- [x] Enforced duplicate application prevention returning `409 Conflict` backed by database unique constraint `(JobId, WorkerProfileId)`.
- [x] Created Angular `WorkerJobService`, `WorkerJobsComponent`, `WorkerJobDetailComponent`, and `WorkerApplicationsComponent`.
- [x] Protected Worker child routes (`/worker/jobs`, `/worker/jobs/:id`, `/worker/applications`) with `authGuard` and `roleGuard`.
- [x] Added 14 backend integration tests in `WorkerJobTests.cs` and 10 Vitest specs for worker components.
- [x] Verified solution build (`dotnet build FarmKart.sln`), backend tests (71/71 passing), frontend build (`npm run build`), and frontend tests (93/93 passing).

## Phase 4.5 Deliverables — Farmer Application Management and Hiring Decision

- [x] Implemented `IFarmerApplicationService` & `FarmerApplicationService` for application listing by job, application details viewing, accept decision, and reject decision.
- [x] Added endpoints in `FarmerController.cs` under `[Authorize(Roles = Roles.Farmer)]`: `GET /api/farmer/jobs/{jobId}/applications`, `GET /api/farmer/applications/{applicationId}`, `POST /api/farmer/applications/{applicationId}/accept`, and `POST /api/farmer/applications/{applicationId}/reject`.
- [x] Enforced job ownership via authenticated JWT `FarmerProfile`; unowned job application requests return `404 Not Found`.
- [x] Enforced application lifecycle (`Pending` -> `Accepted` / `Rejected`) and job capacity limits (`Accepted` count < `WorkersRequired`, returning `409 Conflict` on overflow).
- [x] Created Angular `FarmerJobApplicationsComponent` (`/farmer/jobs/:jobId/applications`) with applicant experience, skills, note, status badges, and confirmable `Accept`/`Reject` actions.
- [x] Updated `FarmerJobsComponent` cards with an "Applications" action button and added `/farmer/jobs/:jobId/applications` route under `authGuard` & `roleGuard`.
- [x] Added 16 backend integration tests in `FarmerApplicationTests.cs` and 5 Vitest specs in `farmer-job-applications.component.spec.ts`.
- [x] Verified solution build (`dotnet build FarmKart.sln`), backend tests (87/87 passing), frontend build (`npm run build`), and frontend tests (98/98 passing).

## Phase 4.6 Deliverables — Worker Assignment

- [x] Implemented atomic application acceptance and assignment creation in `FarmerApplicationService.cs` (`ApplicationStatus.Accepted` + `WorkerAssignment` with `AssignmentStatus.Active`).
- [x] Implemented `IFarmerAssignmentService` / `FarmerAssignmentService` and `IWorkerAssignmentService` / `WorkerAssignmentService`.
- [x] Added `GET /api/farmer/jobs/{jobId}/assignments` in `FarmerController.cs` under `[Authorize(Roles = Roles.Farmer)]`.
- [x] Added `GET /api/worker/assignments` and `GET /api/worker/assignments/{id}` in `WorkerController.cs` under `[Authorize(Roles = Roles.Worker)]`.
- [x] Enforced server-side ownership verification (`FarmerProfile` and `WorkerProfile` derived strictly from JWT claims).
- [x] Enforced worker capacity and duplicate assignment guardrails returning `409 Conflict`.
- [x] Created Angular `FarmerJobAssignmentsComponent` (`/farmer/jobs/:jobId/assignments`), `WorkerAssignmentsComponent` (`/worker/assignments`), and `WorkerAssignmentDetailComponent` (`/worker/assignments/:id`).
- [x] Updated `FarmerJobsComponent` cards with an "Assigned Workers" button and `WorkerDashboardComponent` with a "My Assignments" card.
- [x] Added 16 backend integration tests in `WorkerAssignmentTests.cs` and 8 Vitest specs across new Angular assignment components.
- [x] Verified solution build (`dotnet build FarmKart.sln`), backend tests (103/103 passing), frontend build (`npm run build`), and frontend tests (106/106 passing).

## Phase 4.7 Deliverables — Attendance Management

- [x] Implemented `IFarmerAttendanceService` / `FarmerAttendanceService` for farmer attendance listing, date filtering, batch marking (atomic upserting), and record updating.
- [x] Implemented `IWorkerAttendanceService` / `WorkerAttendanceService` for worker attendance history retrieval and derived summary statistics (Total Days, Present, Absent, Half Days, Leave, Attendance Rate %).
- [x] Added farmer endpoints under `[Authorize(Roles = Roles.Farmer)]`: `GET /api/farmer/jobs/{jobId}/attendance`, `GET /api/farmer/jobs/{jobId}/attendance/{date}`, `POST /api/farmer/jobs/{jobId}/attendance`, and `PUT /api/farmer/attendance/{attendanceId}`.
- [x] Added worker endpoints under `[Authorize(Roles = Roles.Worker)]`: `GET /api/worker/attendance` and `GET /api/worker/assignments/{assignmentId}/attendance`.
- [x] Enforced strict server-side ownership verification (`FarmerProfile` and `WorkerProfile` resolved exclusively from authenticated JWT claims). Unowned requests return `404 Not Found`.
- [x] Respected the existing Entity Framework Core unique index `(WorkerAssignmentId, Date)` for atomic upserts; prevented duplicate attendance records.
- [x] Created Angular `FarmerAttendanceComponent` (`/farmer/jobs/:jobId/attendance`) for date selection, marking workers Present/Absent, and saving attendance.
- [x] Created Angular `WorkerAttendanceComponent` (`/worker/attendance` & `/worker/assignments/:assignmentId/attendance`) for viewing work history log and summary metrics.
- [x] Updated navigation headers and drawer links for Farmers and Workers.
- [x] Added 16 backend integration tests in `AttendanceTests.cs` and 6 Vitest specs across new Angular attendance components.
- [x] Verified solution build (`dotnet build FarmKart.sln`), backend tests (125/125 passing), frontend build (`npm run build`), and frontend tests (112/112 passing).

## Phase 5.3 Deliverables — Worker Availability Management

- [x] Reused existing `WorkerProfile` fields: `IsAvailable` (bool), `AvailableFrom` (DateOnly?), and `AvailabilityNotes` (string?, max 500 chars).
- [x] Confirmed EF Core configurations (`ProfileConfigurations.cs`) and database schema already match model parameters; no new migrations required.
- [x] Service layer (`WorkerProfileService`) and DTOs (`WorkerProfileResponse`, `WorkerProfileUpdateRequest`) support availability retrieval and updates.
- [x] Server-side identity enforcement resolves `WorkerProfile` exclusively from JWT claims; prevented unauthorized profile/availability modifications.
- [x] Created Angular Availability UI section on `/worker/profile` in both View Mode (Status badge, Available From date, Notes) and Edit Mode (Toggle switch, date input, notes textarea).
- [x] Added 10 backend integration tests in `WorkerProfileTests.cs` verifying availability reading, updating, toggle, persistence, 401/403 security, and worker isolation (151 total backend tests passing).
- [x] Added 8 frontend unit specs in `worker-profile.component.spec.ts` verifying availability loading, toggle, persistence, validation, and error states (120 total frontend tests passing).
- [x] Verified solution build (`dotnet build FarmKart.sln`), test suites (`dotnet test` & `npm test`), and production client bundle (`npm run build`).

## Phase 5.4 & 5.5 Deliverables — Worker Job Preferences and In-App Notifications

- [x] Extended `WorkerProfile` entity with preference properties (`PreferredWorkCategories`, `PreferredLocations`, `MinimumDailyWage`, `PreferredWorkingHours`, `FoodPreference`, `AccommodationPreference`).
- [x] Configured EF Core check constraint `CK_WorkerProfile_MinimumDailyWage_NonNegative` and generated EF migration `AddWorkerJobPreferences`.
- [x] Implemented `IWorkerProfileService` methods (`GetPreferencesAsync`, `UpdatePreferencesAsync`) supporting comma-separated category/location deduplication, trimming, and non-negative wage validation.
- [x] Implemented `INotificationService` / `NotificationService` supporting notification creation, retrieval, unread count resolution, marking as read, and marking all as read.
- [x] Integrated notification triggers as side-effects in `FarmerApplicationService` (Application Accepted/Rejected, Assignment Created), `FarmerAttendanceService` (Attendance Updated), and `FarmerJobService` (Job Cancelled).
- [x] Exposed REST endpoints in `WorkerController.cs` under `[Authorize(Roles = Roles.Worker)]`:
  - `GET /api/worker/preferences` & `PUT /api/worker/preferences`
  - `GET /api/worker/notifications` & `GET /api/worker/notifications/unread-count`
  - `PUT /api/worker/notifications/{id}/read` & `PUT /api/worker/notifications/read-all`
- [x] Created Angular `WorkerPreferencesComponent` (`/worker/preferences`) for managing work categories (chip tags), locations (chip tags), minimum daily wage, working hours, food preference, and accommodation preference.
- [x] Created Angular `WorkerNotificationsComponent` (`/worker/notifications`) with unread count badges, notification category icons, mark as read, mark all read, and target route navigation.
- [x] Updated `WorkerShellComponent` sidebar navigation and drawer with `Job Preferences`, `Notifications`, and dynamic unread badge counter.
- [x] Added 14 backend integration tests in `WorkerPreferencesTests.cs` and 12 in `WorkerNotificationTests.cs` (177/177 total backend tests passing).
- [x] Added 10 frontend unit specs in `worker-preferences.component.spec.ts` and 8 in `worker-notifications.component.spec.ts` (138/138 total frontend tests passing).
- [x] Verified solution build (`dotnet build FarmKart.sln`), backend tests (`dotnet test`), frontend tests (`npm test`), and production client bundle (`npm run build`).

## Phase 5.6 & 5.7 Deliverables — Worker Ratings & Reviews and Worker Earnings

- [x] Confirmed `Review` entity (`Communication.cs`) and `Reviews` table with `CK_Review_Rating_Range` (`1 <= Rating <= 5`) already exist in SQL Server schema; no database migration required.
- [x] Implemented `IWorkerReviewService` / `WorkerReviewService` for rating workers (1 to 5 stars, optional comment up to 2000 chars), fetching assignment reviews, and calculating worker rating summaries (average rating, total reviews count, star breakdown, recent reviews).
- [x] Enforced completion rules (ratings only allowed for completed or finished worker assignments) and security rules (only assigned farmers can rate workers, workers cannot rate themselves, customers cannot rate workers).
- [x] Integrated in-app notification trigger ("New Review Received", "{FarmerName} rated your work {Rating} stars.") when a review is created.
- [x] Implemented `IWorkerEarningsService` / `WorkerEarningsService` deriving earnings dynamically from `WorkerAssignment`, `Job`, and `Attendance` records (`Present` = 100% daily wage, `HalfDay` = 50% daily wage, `Absent`/`Leave` = ₹0).
- [x] Exposed REST endpoints in `FarmerController.cs` and `WorkerController.cs`:
  - `POST /api/farmer/assignments/{assignmentId}/review` & `GET /api/farmer/assignments/{assignmentId}/review`
  - `GET /api/worker/reviews` & `GET /api/worker/earnings`
- [x] Updated Angular `FarmerJobAssignmentsComponent` with a "Rate Worker" / "Edit Rating" button and interactive star rating modal for completed worker assignments.
- [x] Created Angular `WorkerEarningsComponent` (`/worker/earnings`) displaying Total Earnings banner, Completed Jobs counter, This Month earnings, and Earnings History table.
- [x] Updated Angular `WorkerProfileComponent` (`/worker/profile`) displaying Received Ratings & Reviews summary (Average Rating, Total Reviews, Star breakdown chart, Recent Reviews).
- [x] Updated `WorkerShellComponent` sidebar/drawer navigation and `app.routes.ts` with `/worker/earnings`.
- [x] Added 14 backend integration tests in `WorkerRatingTests.cs` and 12 in `WorkerEarningsTests.cs` (203/203 total backend tests passing).
## Phase 5.8 & 5.9 Deliverables — Worker Work History & Worker Profile Verification & Completion

- [x] Derived Worker Work History 100% dynamically from existing `WorkerAssignment`, `Job`, `Attendance`, `Review`, and `WorkerEarnings` records without creating duplicate WorkHistory tables.
- [x] Added `VerificationStatus` (`nvarchar(50)`, default `'Not Verified'`) property to `WorkerProfile` entity and `WorkerProfileConfiguration` with EF Core migration `AddWorkerVerificationStatus`.
- [x] Implemented `IWorkerWorkHistoryService` / `WorkerWorkHistoryService` returning completed job history cards with Job Title, Work Category, Farmer Name, Farm Location, Work Dates, Daily Wage, Attendance Summary breakdown (`5 Present, 1 Half Day`), Earned Amount, Farmer Star Rating & Comment, and Completion Status, as well as summary metrics (Total Completed Jobs, Total Work Days, Total Earnings).
- [x] Implemented `IWorkerProfileCompletionService` / `WorkerProfileCompletionService` calculating profile completion dynamically across 5 weighted sections (Basic Information: 20%, Skills & Experience: 25%, Availability: 20%, Job Preferences: 25%, Profile Photo: 10%) and exposing application verification status.
- [x] Enforced strict security: Server-side identity resolution from JWT claims prevents accessing or modifying another worker's history or completion status; workers cannot manually mark themselves `Verified`.
- [x] Exposed REST endpoints in `WorkerController.cs` under `[Authorize(Roles = Roles.Worker)]`:
  - `GET /api/worker/work-history`
  - `GET /api/worker/profile/completion`
- [x] Created Angular `WorkerWorkHistoryComponent` (`/worker/work-history`) with summary metric cards, completed job cards, search & rating filters, loading, error, and empty states.
- [x] Updated Angular `WorkerProfileComponent` (`/worker/profile`) displaying Profile Completion Progress Bar (`85%`), Verification Status Badge (`Not Verified`), section checklist with `[ Complete Section ]` action buttons navigating to incomplete sections.
- [x] Updated `WorkerShellComponent` sidebar/drawer navigation and `app.routes.ts` with `/worker/work-history`.
- [x] Added 12 backend integration tests in `WorkerWorkHistoryTests.cs` and 9 in `WorkerProfileCompletionTests.cs` (**224 / 224 total backend tests passing**).
- [x] Added 10 frontend unit specs in `worker-work-history.component.spec.ts` and updated `worker-profile.component.spec.ts` (**159 / 159 total frontend tests passing**).
- [x] Verified solution build (`dotnet build FarmKart.sln`), backend tests (`dotnet test`), frontend tests (`npm test`), and production client bundle (`npm run build`).

## Phase 6.1 Deliverables — Farmer Crop Management

- [x] Updated `Crop` domain entity (`Crops.cs`) with `CropType` (string, max length 120, required) and `AreaUnit` (`FarmSizeUnit` enum: `Vigha` / `Bigha` = 1, `Acre` = 2, `Hectare` = 3).
- [x] Configured EF Core properties in `CropConfigurations.cs` and generated migration `AddCropTypeAndAreaUnit`.
- [x] Created `FarmerCropDtos.cs` (`CropResponse`, `CreateCropRequest`, `UpdateCropRequest`).
- [x] Implemented `IFarmerCropService` / `FarmerCropService` handling farmer profile resolution, input validation, date range checking (`ExpectedHarvestDate >= SowingDate`, `ActualHarvestDate >= SowingDate`), area > 0 enforcement, lifecycle status parsing (`Planned`, `Growing`, `ReadyForHarvest`, `Harvested`), and ownership security.
- [x] Exposed REST endpoints in `FarmerCropController.cs` under `[Authorize(Roles = Roles.Farmer)]`:
  - `GET /api/farmer/crops`
  - `GET /api/farmer/crops/{id}`
  - `POST /api/farmer/crops`
  - `PUT /api/farmer/crops/{id}`
  - `DELETE /api/farmer/crops/{id}`
- [x] Enforced strict security: Server-side identity resolution from JWT claims prevents accessing, modifying, or deleting another farmer's crop. Workers, Customers, and unauthenticated users receive 401/403.
- [x] Created Angular `FarmerCropService`, `FarmerCropsComponent` (`/farmer/crops`), `FarmerCropFormComponent` (`/farmer/crops/new` and `/farmer/crops/:id/edit`), and `FarmerCropDetailComponent` (`/farmer/crops/:id`).
- [x] Activated "My Crops" navigation item in `farmer-shell.component.ts`, dashboard card in `farmer-dashboard.component.ts`, and protected routes in `app.routes.ts`.
- [x] Added 29 backend integration tests in `FarmerCropTests.cs` covering crop creation, updates, deletes, ownership isolation, 401/403/404 security, image uploads, multiple image uploads, file format validation, file size limits, primary image selection, and image deletion (**248 / 248 total backend tests passing**).
- [x] Added 20 frontend unit specs across crop components and service (**179 / 179 total frontend tests passing**).
- [x] Verified solution build (`dotnet build FarmKart.sln`), backend integration tests (`dotnet test`), frontend tests (`npm test`), production client bundle (`npm run build`), and backend API startup (`dotnet run --project backend/FarmKart.API`).

## Phase 6.2 Deliverables — Crop Inventory / Stock Management

- [x] Added `CropStockTransactionType` enum (`Harvest`, `Adjustment`, `Correction`) to `DomainEnums.cs`.
- [x] Added `CropStockTransaction` entity to `Crops.cs` with `CropId`, `Quantity`, `Unit`, `QuantityInBaseUnit` (Kg), `TransactionType`, and `Notes`.
- [x] Configured EF Core mapping in `CropConfigurations.cs` (precision, FK cascade, indexes) and generated migration `AddCropStockManagement`.
- [x] Implemented `IFarmerCropStockService` / `FarmerCropStockService` with unit normalization (Kg / Quintal / Ton → Kg), lifecycle validation (Planned/Growing crops cannot receive stock), negative-balance guard, and stock summary formatting.
- [x] **Fixed EF Core relationship-fixup double-counting bug**: `AddCropStockAsync` and `AdjustCropStockAsync` now use `crop.Quantity + quantityInBaseUnit` instead of re-summing `crop.StockTransactions` after `DbContext.Add()` — EF Core adds the unsaved entity to the navigation collection immediately, causing double-counting.
- [x] `Crop.Quantity` is the single authoritative running total in Kg, updated atomically on every stock transaction.
- [x] Added `AvailableQuantityKg` and `AvailableQuantityFormatted` fields to `CropResponse` DTO, populated from `crop.Quantity` in `FarmerCropService.MapToResponse()`.
- [x] Exposed REST endpoints in `FarmerCropStockController.cs` under `[Authorize(Roles = Roles.Farmer)]`:
  - `GET /api/farmer/crops/{cropId}/stock` — current stock summary
  - `POST /api/farmer/crops/{cropId}/stock` — add harvest record
  - `POST /api/farmer/crops/{cropId}/stock/adjust` — adjustment / correction
  - `GET /api/farmer/crops/{cropId}/stock/history` — full transaction log
- [x] Enforced strict security: farmer profile resolved server-side from JWT claims; 404 returned for other farmers' crops.
- [x] Updated Angular `FarmerCropService` with stock API methods (`getCropStock`, `addCropStock`, `getCropStockHistory`).
- [x] Updated Angular `FarmerCropDetailComponent` with stock summary section, Add Stock modal (quantity / unit / type / notes), and Stock History modal (transaction list, loading/empty states).
- [x] Added "Available Stock" row to the Crop Card metric banner in `FarmerCropsComponent` — displays formatted stock (e.g. "5 Quintals") for harvest-eligible crops, "No stock" for zero-stock crops, hidden for Planned/Growing.
- [x] Added 23 backend integration tests in `FarmerCropStockTests.cs` (Test01–Test23) covering: stock add, associations, summary retrieval, cumulative totals, unit conversions (Kg/Quintal/Ton), history, quantity validation, invalid units, negative balance guard, crop lifecycle enforcement, role security (Worker/Customer/Unauthenticated → 401/403/404), farmer isolation, crop list stock display sync, and formatted display (**271 / 271 total backend tests passing**).
- [x] Verified backend build (`dotnet build FarmKart.Infrastructure`), test suite (`dotnet test --filter FarmerCropStockTests`: 23/23 passing), and frontend build (`npx ng build`).

## Phase 6.4 Deliverables — Customer Dashboard and Navigation Shell

- [x] Created Angular `CustomerShellComponent` (`/customer`) with responsive desktop sidebar, mobile menu drawer, FarmKart branding, dynamic customer name/email, customer role badge, and logout action.
- [x] Created Angular `CustomerDashboardComponent` displaying customer hero greeting ("Welcome, {Customer Name}"), workspace subtitle, and 6 customer service cards (Browse Auctions - ACTIVE, My Bids - COMING SOON, My Orders - COMING SOON, Payments - COMING SOON, Notifications - COMING SOON, My Profile - COMING SOON).
- [x] Created Angular customer `ComingSoonComponent` displaying module title, "Coming Soon" badge, future phase explanation message, and "Back to Dashboard" navigation button.
- [x] Protected `/customer` route structure in `app.routes.ts` using existing `authGuard` and `roleGuard` (`roles: ['Customer']`), preserving existing Farmer (`/farmer`) and Worker (`/worker`) route guards intact.
- [x] Dynamically resolved customer name and email from `AuthService.currentUser$` observable without hardcoded values.
- [x] Added Angular unit tests for `CustomerShellComponent`, `CustomerDashboardComponent`, and customer `ComingSoonComponent` (**190 / 190 total frontend unit tests passing**).
- [x] Verified frontend build (`npx ng build`) and test suite (`npm test`).

## Phase 6.5 Deliverables — Customer Auction Marketplace

- [x] Enforced strictly **AUCTION-ONLY marketplace business rule**: Zero direct buy, cart, checkout, or payment functionality added.
- [x] Created `CustomerAuctionResponse` and `CustomerAuctionFilterRequest` DTOs in `CustomerAuctionDtos.cs`.
- [x] Implemented `ICustomerAuctionService` / `CustomerAuctionService` with dynamic status computation (`LIVE`, `UPCOMING`, `ENDED`), search (crop name, variety, farmer name, location), category filter, status filter, location filter, sorting (Ending soon, Newest, Price asc/desc), and primary image resolution.
- [x] Excluded `Draft` and `Cancelled` auctions from customer marketplace queries.
- [x] Exposed REST endpoints in `CustomerAuctionsController.cs` under `[Authorize(Roles = Roles.Customer)]`:
  - `GET /api/customer/auctions` — query marketplace with filters & sorting
  - `GET /api/customer/auctions/{id}` — retrieve single auction details
- [x] Created Angular `customer-auction.models.ts` and `CustomerAuctionService` API client.
- [x] Created Angular `CustomerAuctionsComponent` (`/customer/auctions`) with search bar, category select, status select, sort dropdown, clear filters action, responsive auction card grid (primary image, crop type badge, status overlay, quantity, starting price, current highest bid, min increment, farmer info, start/end dates, "View Auction" button), loading skeleton, error retry state, and empty result state.
- [x] Created Angular `CustomerAuctionDetailComponent` (`/customer/auctions/:id`) displaying large primary crop photo with gallery thumbnails, crop description, farmer profile, auction schedule, starting price & min increment, and a guarded "Live Bidding" section showing notice `"Bidding will be available in the next phase."`.
- [x] Added 9 backend integration tests in `CustomerAuctionTests.cs` verifying marketplace retrieval, cancelled/draft exclusion, search/category/status filters, single auction resolution, 404 for invalid IDs, 401 unauthenticated rejection, and 403 role security for Farmer/Worker (**280 / 280 total backend tests passing**).
- [x] Added Angular unit specs for `CustomerAuctionsComponent` and `CustomerAuctionDetailComponent` (**196 / 196 total frontend unit tests passing**).
- [x] Verified solution build (`dotnet build backend/FarmKart.API`), backend tests (`dotnet test`), frontend build (`npx ng build`), and frontend tests (`npm test`).

## Phase 6.5 Extension — Auction Durations & Real-Time Countdown Timer

- [x] **Predefined & Custom Auction Durations**:
  - Refactored `CreateFarmerAuctionRequest` and `UpdateFarmerAuctionRequest` DTOs to replace `EndTimeUtc` with `Duration` string parameter.
  - Implemented `FarmerAuctionService.ParseDurationToHours` supporting predefined options (`5 Hours`, `12 Hours`, `1 Day` (24h, default), `3 Days` (72h), `7 Days` (168h)) and custom manual hours entry (e.g. `"8 Hours"` or `"36"`).
  - Server automatically calculates `EndTimeUtc = StartTimeUtc + DurationHours`.
- [x] **Authoritative UTC & Server Time Offset Sync**:
  - Added `ServerTimeUtc` to `FarmerAuctionResponse` and `CustomerAuctionResponse` DTOs.
  - Clients compute server time offset (`serverTimeUtc - clientUtcNow`) to ensure all user clocks align seamlessly across devices regardless of local clock drift.
- [x] **Dynamic Auction Lifecycle & Reusable Countdown Timer Component**:
  - Built standalone `AuctionCountdownComponent` (`app-auction-countdown`) emitting real-time tick-by-tick countdowns (`DD:HH:MM:SS` or `HH:MM:SS`).
  - Automatically handles status transitions (`SCHEDULED` -> `LIVE` -> `ENDED` / `CANCELLED`) derived directly from server-authoritative UTC end times.
  - Guarantees countdowns stop at `00:00:00` without displaying negative numbers.
  - Cleans up `setInterval` handle on component destroy to eliminate memory leaks.
- [x] **Farmer & Customer Workspace UI Refactoring**:
  - Updated Farmer Create Auction Modal with Duration select dropdown, custom hours field, and live End Date & Time preview label (`Auction ends on: {date}`).
  - Fixed input field styling (`.app-input`) and card contrast in light and dark modes for high visibility.
  - Updated Farmer crop auction list and Customer marketplace auction cards to feature live countdown timers.
- [x] **Unit & Integration Tests**:
  - Added 26 backend tests in `FarmerAuctionDurationTests.cs` verifying duration parsing, end-time calculation, dynamic status checks, and API integration.
  - Added 12 frontend unit tests in `auction-countdown.component.spec.ts` covering status transitions, formatting, server offset sync, and lifecycle cleanup (**208 / 208 total frontend unit tests passing**).

## Phase 6.9 Deliverables — Farmer Auction Management & Live Bids Dashboard

- [x] **Farmer Sidebar & Routing**:
  - Added `My Auctions` (`/farmer/auctions`) link with `gavel` icon to `farmer-shell.component.ts`.
  - Registered `/farmer/auctions`, `/farmer/auctions/:id`, and `/farmer/auctions/:id/bids` in `app.routes.ts` protected by `authGuard` and `roleGuard`.
- [x] **My Auctions Page (`/farmer/auctions`)**:
  - Real-time summary header cards (Total Auctions, Upcoming, Live, Ended).
  - Client-side status filter tabs (All, Live, Upcoming, Ended, Cancelled) and search box (crop name & variety).
  - Auction cards displaying crop image, variety, quantity (Kg & Man), starting price (₹/Man), current highest bid (₹/Man), total bids count, total demand (Kg & %), status badge (🔴 LIVE, 🟡 UPCOMING, ⚫ ENDED, ⚪ CANCELLED), `app-auction-countdown`, and action buttons `[ VIEW AUCTION ]` & `[ VIEW BIDS ]`.
- [x] **View Auction Page (`/farmer/auctions/:id`)**:
  - Complete details sections: Auction Information, Crop Info, Quantity/Stock, Pricing, Timeline, Live Bidding Activity banner, Final Allocation Summary table (for Ended auctions), and Payment Summary.
- [x] **View Bids Page (`/farmer/auctions/:id/bids`)**:
  - Metric banner header displaying Total Demand Kg, Demand %, Total Bids.
  - Custom sort dropdown (Highest Bid, Lowest Bid, Highest Quantity, Latest Bid, Earliest Bid).
  - Responsive table and cards displaying Customer Name (Public full name ONLY), Requested Quantity (Kg & Man), Bid Price / Man, Bid Time (Local timezone), Bid Status (`VALID`), and Allocation Status (`WON`, `PARTIALLY_WON`, `LOST`).
- [x] **Security & Authorization**:
  - Authenticated user + Farmer role + Farmer ownership check enforced across all backend APIs. Returns 404/403 for unowned requests. Customer credentials (email, password, tokens, internal IDs) are strictly omitted.
- [x] **Real-Time Polling & Pricing Consistency**:
  - Safe 5-second polling on live views for active live auctions.
  - All pricing consistently rendered in **₹ / Man** ($1\text{ Man} = 20\text{ Kg}$).
- [x] **Automated Tests**:
  - Backend integration tests in `FarmerAuctionManagementTests.cs` (4/4 passing).
  - Frontend unit tests for `FarmerAuctionsComponent`, `FarmerAuctionDetailComponent`, and `FarmerAuctionBidsComponent` (**217 / 217 total frontend unit tests passing**).

## Phase 8.5 Deliverables — Farmer Public Profile + Machinery Ratings & Reviews

- [x] **Farmer Public Profile Backend**:
  - Implemented `IFarmerProfileService` / `FarmerProfileService` retrieving public farmer profile data: Farmer Name, Farm Name, Location (City, State), Member Since Date, Average Farmer Rating (calculated ONLY from Crop Order Reviews), Verified Reviews list, Active Auctions list, and Listed Machinery owned strictly by this farmer (`Machinery.OwnerUserId == FarmerUserId`).
  - Sensitive information (passwords, auth data, payment details, private emails) is strictly omitted.
  - Exposed `GET /api/farmers/{farmerId}/profile` in `FarmerProfileController.cs`.
- [x] **Machinery Ratings & Reviews Backend**:
  - Reused existing Phase 8.1 `Review` entity using `ReviewEntityType.MachineryRental` and `RelatedEntityId = rentalId`.
  - Implemented `IMachineryReviewService` / `MachineryReviewService` validating completed rental eligibility, renter-only authorization, 1-5 rating range enforcement, duplicate review prevention, owner-authorized machinery review management (`GET /api/my-machinery/{id}/reviews` verifying `machinery.OwnerUserId == authenticatedUserId`), and unified My Reviews (`GET /api/my-reviews`).
  - Exposed endpoints in `MachineryReviewController.cs`.
- [x] **Frontend UI & Components**:
  - Created `FarmerPublicProfileComponent` (`/customer/farmers/:farmerId` & `/farmer/farmers/:farmerId`) with header profile card, average rating badge, filter tabs for Active Auctions (`All`, `Live`, `Upcoming`, `Ended`), filter tabs for Listed Machinery (`All`, `Available`, `Rented`, `Unavailable`), and Customer Reviews grid.
  - Created `OwnerMachineryReviewsDialogComponent` allowing machinery owners (Farmers and Customers) to view received reviews directly from `My Machinery`.
  - Updated `CustomerReviewsComponent` and `FarmerReviewsComponent` with tabbed filter buttons (`All`, `Crop Reviews`, `Machinery Reviews`), metric summary cards, and dynamic empty states.
  - Updated `CustomerMachineryDetailComponent` displaying owner's Farmer Profile link and Machinery Reviews & Ratings section.
  - Updated `CustomerMyRentalsComponent` adding `[ RATE MACHINERY ]` button for completed rentals.
- [x] **Automated Integration Tests**:
  - 8 integration tests in `FarmerProfileAndMachineryReviewTests.cs` (100% passing). Total backend suite: 501 / 501 tests passing.
  - Verified backend build (`dotnet build`) and Angular frontend build (`npx ng build`).
- [x] Added 16 backend integration tests in `AttendanceTests.cs` and 6 Vitest specs across new Angular attendance components.
- [x] Verified solution build (`dotnet build FarmKart.sln`), backend tests (125/125 passing), frontend build (`npm run build`), and frontend tests (112/112 passing).

## Phase 5.3 Deliverables — Worker Availability Management

- [x] Reused existing `WorkerProfile` fields: `IsAvailable` (bool), `AvailableFrom` (DateOnly?), and `AvailabilityNotes` (string?, max 500 chars).
- [x] Confirmed EF Core configurations (`ProfileConfigurations.cs`) and database schema already match model parameters; no new migrations required.
- [x] Service layer (`WorkerProfileService`) and DTOs (`WorkerProfileResponse`, `WorkerProfileUpdateRequest`) support availability retrieval and updates.
- [x] Server-side identity enforcement resolves `WorkerProfile` exclusively from JWT claims; prevented unauthorized profile/availability modifications.
- [x] Created Angular Availability UI section on `/worker/profile` in both View Mode (Status badge, Available From date, Notes) and Edit Mode (Toggle switch, date input, notes textarea).
- [x] Added 10 backend integration tests in `WorkerProfileTests.cs` verifying availability reading, updating, toggle, persistence, 401/403 security, and worker isolation (151 total backend tests passing).
- [x] Added 8 frontend unit specs in `worker-profile.component.spec.ts` verifying availability loading, toggle, persistence, validation, and error states (120 total frontend tests passing).
- [x] Verified solution build (`dotnet build FarmKart.sln`), test suites (`dotnet test` & `npm test`), and production client bundle (`npm run build`).

## Phase 5.4 & 5.5 Deliverables — Worker Job Preferences and In-App Notifications

- [x] Extended `WorkerProfile` entity with preference properties (`PreferredWorkCategories`, `PreferredLocations`, `MinimumDailyWage`, `PreferredWorkingHours`, `FoodPreference`, `AccommodationPreference`).
- [x] Configured EF Core check constraint `CK_WorkerProfile_MinimumDailyWage_NonNegative` and generated EF migration `AddWorkerJobPreferences`.
- [x] Implemented `IWorkerProfileService` methods (`GetPreferencesAsync`, `UpdatePreferencesAsync`) supporting comma-separated category/location deduplication, trimming, and non-negative wage validation.
- [x] Implemented `INotificationService` / `NotificationService` supporting notification creation, retrieval, unread count resolution, marking as read, and marking all as read.
- [x] Integrated notification triggers as side-effects in `FarmerApplicationService` (Application Accepted/Rejected, Assignment Created), `FarmerAttendanceService` (Attendance Updated), and `FarmerJobService` (Job Cancelled).
- [x] Exposed REST endpoints in `WorkerController.cs` under `[Authorize(Roles = Roles.Worker)]`:
  - `GET /api/worker/preferences` & `PUT /api/worker/preferences`
  - `GET /api/worker/notifications` & `GET /api/worker/notifications/unread-count`
  - `PUT /api/worker/notifications/{id}/read` & `PUT /api/worker/notifications/read-all`
- [x] Created Angular `WorkerPreferencesComponent` (`/worker/preferences`) for managing work categories (chip tags), locations (chip tags), minimum daily wage, working hours, food preference, and accommodation preference.
- [x] Created Angular `WorkerNotificationsComponent` (`/worker/notifications`) with unread count badges, notification category icons, mark as read, mark all read, and target route navigation.
- [x] Updated `WorkerShellComponent` sidebar navigation and drawer with `Job Preferences`, `Notifications`, and dynamic unread badge counter.
- [x] Added 14 backend integration tests in `WorkerPreferencesTests.cs` and 12 in `WorkerNotificationTests.cs` (177/177 total backend tests passing).
- [x] Added 10 frontend unit specs in `worker-preferences.component.spec.ts` and 8 in `worker-notifications.component.spec.ts` (138/138 total frontend tests passing).
- [x] Verified solution build (`dotnet build FarmKart.sln`), backend tests (`dotnet test`), frontend tests (`npm test`), and production client bundle (`npm run build`).

## Phase 5.6 & 5.7 Deliverables — Worker Ratings & Reviews and Worker Earnings

- [x] Confirmed `Review` entity (`Communication.cs`) and `Reviews` table with `CK_Review_Rating_Range` (`1 <= Rating <= 5`) already exist in SQL Server schema; no database migration required.
- [x] Implemented `IWorkerReviewService` / `WorkerReviewService` for rating workers (1 to 5 stars, optional comment up to 2000 chars), fetching assignment reviews, and calculating worker rating summaries (average rating, total reviews count, star breakdown, recent reviews).
- [x] Enforced completion rules (ratings only allowed for completed or finished worker assignments) and security rules (only assigned farmers can rate workers, workers cannot rate themselves, customers cannot rate workers).
- [x] Integrated in-app notification trigger ("New Review Received", "{FarmerName} rated your work {Rating} stars.") when a review is created.
- [x] Implemented `IWorkerEarningsService` / `WorkerEarningsService` deriving earnings dynamically from `WorkerAssignment`, `Job`, and `Attendance` records (`Present` = 100% daily wage, `HalfDay` = 50% daily wage, `Absent`/`Leave` = ₹0).
- [x] Exposed REST endpoints in `FarmerController.cs` and `WorkerController.cs`:
  - `POST /api/farmer/assignments/{assignmentId}/review` & `GET /api/farmer/assignments/{assignmentId}/review`
  - `GET /api/worker/reviews` & `GET /api/worker/earnings`
- [x] Updated Angular `FarmerJobAssignmentsComponent` with a "Rate Worker" / "Edit Rating" button and interactive star rating modal for completed worker assignments.
- [x] Created Angular `WorkerEarningsComponent` (`/worker/earnings`) displaying Total Earnings banner, Completed Jobs counter, This Month earnings, and Earnings History table.
- [x] Updated Angular `WorkerProfileComponent` (`/worker/profile`) displaying Received Ratings & Reviews summary (Average Rating, Total Reviews, Star breakdown chart, Recent Reviews).
- [x] Updated `WorkerShellComponent` sidebar/drawer navigation and `app.routes.ts` with `/worker/earnings`.
- [x] Added 14 backend integration tests in `WorkerRatingTests.cs` and 12 in `WorkerEarningsTests.cs` (203/203 total backend tests passing).
## Phase 5.8 & 5.9 Deliverables — Worker Work History & Worker Profile Verification & Completion

- [x] Derived Worker Work History 100% dynamically from existing `WorkerAssignment`, `Job`, `Attendance`, `Review`, and `WorkerEarnings` records without creating duplicate WorkHistory tables.
- [x] Added `VerificationStatus` (`nvarchar(50)`, default `'Not Verified'`) property to `WorkerProfile` entity and `WorkerProfileConfiguration` with EF Core migration `AddWorkerVerificationStatus`.
- [x] Implemented `IWorkerWorkHistoryService` / `WorkerWorkHistoryService` returning completed job history cards with Job Title, Work Category, Farmer Name, Farm Location, Work Dates, Daily Wage, Attendance Summary breakdown (`5 Present, 1 Half Day`), Earned Amount, Farmer Star Rating & Comment, and Completion Status, as well as summary metrics (Total Completed Jobs, Total Work Days, Total Earnings).
- [x] Implemented `IWorkerProfileCompletionService` / `WorkerProfileCompletionService` calculating profile completion dynamically across 5 weighted sections (Basic Information: 20%, Skills & Experience: 25%, Availability: 20%, Job Preferences: 25%, Profile Photo: 10%) and exposing application verification status.
- [x] Enforced strict security: Server-side identity resolution from JWT claims prevents accessing or modifying another worker's history or completion status; workers cannot manually mark themselves `Verified`.
- [x] Exposed REST endpoints in `WorkerController.cs` under `[Authorize(Roles = Roles.Worker)]`:
  - `GET /api/worker/work-history`
  - `GET /api/worker/profile/completion`
- [x] Created Angular `WorkerWorkHistoryComponent` (`/worker/work-history`) with summary metric cards, completed job cards, search & rating filters, loading, error, and empty states.
- [x] Updated Angular `WorkerProfileComponent` (`/worker/profile`) displaying Profile Completion Progress Bar (`85%`), Verification Status Badge (`Not Verified`), section checklist with `[ Complete Section ]` action buttons navigating to incomplete sections.
- [x] Updated `WorkerShellComponent` sidebar/drawer navigation and `app.routes.ts` with `/worker/work-history`.
- [x] Added 12 backend integration tests in `WorkerWorkHistoryTests.cs` and 9 in `WorkerProfileCompletionTests.cs` (**224 / 224 total backend tests passing**).
- [x] Added 10 frontend unit specs in `worker-work-history.component.spec.ts` and updated `worker-profile.component.spec.ts` (**159 / 159 total frontend tests passing**).
- [x] Verified solution build (`dotnet build FarmKart.sln`), backend tests (`dotnet test`), frontend tests (`npm test`), and production client bundle (`npm run build`).

## Phase 6.1 Deliverables — Farmer Crop Management

- [x] Updated `Crop` domain entity (`Crops.cs`) with `CropType` (string, max length 120, required) and `AreaUnit` (`FarmSizeUnit` enum: `Vigha` / `Bigha` = 1, `Acre` = 2, `Hectare` = 3).
- [x] Configured EF Core properties in `CropConfigurations.cs` and generated migration `AddCropTypeAndAreaUnit`.
- [x] Created `FarmerCropDtos.cs` (`CropResponse`, `CreateCropRequest`, `UpdateCropRequest`).
- [x] Implemented `IFarmerCropService` / `FarmerCropService` handling farmer profile resolution, input validation, date range checking (`ExpectedHarvestDate >= SowingDate`, `ActualHarvestDate >= SowingDate`), area > 0 enforcement, lifecycle status parsing (`Planned`, `Growing`, `ReadyForHarvest`, `Harvested`), and ownership security.
- [x] Exposed REST endpoints in `FarmerCropController.cs` under `[Authorize(Roles = Roles.Farmer)]`:
  - `GET /api/farmer/crops`
  - `GET /api/farmer/crops/{id}`
  - `POST /api/farmer/crops`
  - `PUT /api/farmer/crops/{id}`
  - `DELETE /api/farmer/crops/{id}`
- [x] Enforced strict security: Server-side identity resolution from JWT claims prevents accessing, modifying, or deleting another farmer's crop. Workers, Customers, and unauthenticated users receive 401/403.
- [x] Created Angular `FarmerCropService`, `FarmerCropsComponent` (`/farmer/crops`), `FarmerCropFormComponent` (`/farmer/crops/new` and `/farmer/crops/:id/edit`), and `FarmerCropDetailComponent` (`/farmer/crops/:id`).
- [x] Activated "My Crops" navigation item in `farmer-shell.component.ts`, dashboard card in `farmer-dashboard.component.ts`, and protected routes in `app.routes.ts`.
- [x] Added 29 backend integration tests in `FarmerCropTests.cs` covering crop creation, updates, deletes, ownership isolation, 401/403/404 security, image uploads, multiple image uploads, file format validation, file size limits, primary image selection, and image deletion (**248 / 248 total backend tests passing**).
- [x] Added 20 frontend unit specs across crop components and service (**179 / 179 total frontend tests passing**).
- [x] Verified solution build (`dotnet build FarmKart.sln`), backend integration tests (`dotnet test`), frontend tests (`npm test`), production client bundle (`npm run build`), and backend API startup (`dotnet run --project backend/FarmKart.API`).

## Phase 6.2 Deliverables — Crop Inventory / Stock Management

- [x] Added `CropStockTransactionType` enum (`Harvest`, `Adjustment`, `Correction`) to `DomainEnums.cs`.
- [x] Added `CropStockTransaction` entity to `Crops.cs` with `CropId`, `Quantity`, `Unit`, `QuantityInBaseUnit` (Kg), `TransactionType`, and `Notes`.
- [x] Configured EF Core mapping in `CropConfigurations.cs` (precision, FK cascade, indexes) and generated migration `AddCropStockManagement`.
- [x] Implemented `IFarmerCropStockService` / `FarmerCropStockService` with unit normalization (Kg / Quintal / Ton → Kg), lifecycle validation (Planned/Growing crops cannot receive stock), negative-balance guard, and stock summary formatting.
- [x] **Fixed EF Core relationship-fixup double-counting bug**: `AddCropStockAsync` and `AdjustCropStockAsync` now use `crop.Quantity + quantityInBaseUnit` instead of re-summing `crop.StockTransactions` after `DbContext.Add()` — EF Core adds the unsaved entity to the navigation collection immediately, causing double-counting.
- [x] `Crop.Quantity` is the single authoritative running total in Kg, updated atomically on every stock transaction.
- [x] Added `AvailableQuantityKg` and `AvailableQuantityFormatted` fields to `CropResponse` DTO, populated from `crop.Quantity` in `FarmerCropService.MapToResponse()`.
- [x] Exposed REST endpoints in `FarmerCropStockController.cs` under `[Authorize(Roles = Roles.Farmer)]`:
  - `GET /api/farmer/crops/{cropId}/stock` — current stock summary
  - `POST /api/farmer/crops/{cropId}/stock` — add harvest record
  - `POST /api/farmer/crops/{cropId}/stock/adjust` — adjustment / correction
  - `GET /api/farmer/crops/{cropId}/stock/history` — full transaction log
- [x] Enforced strict security: farmer profile resolved server-side from JWT claims; 404 returned for other farmers' crops.
- [x] Updated Angular `FarmerCropService` with stock API methods (`getCropStock`, `addCropStock`, `getCropStockHistory`).
- [x] Updated Angular `FarmerCropDetailComponent` with stock summary section, Add Stock modal (quantity / unit / type / notes), and Stock History modal (transaction list, loading/empty states).
- [x] Added "Available Stock" row to the Crop Card metric banner in `FarmerCropsComponent` — displays formatted stock (e.g. "5 Quintals") for harvest-eligible crops, "No stock" for zero-stock crops, hidden for Planned/Growing.
- [x] Added 23 backend integration tests in `FarmerCropStockTests.cs` (Test01–Test23) covering: stock add, associations, summary retrieval, cumulative totals, unit conversions (Kg/Quintal/Ton), history, quantity validation, invalid units, negative balance guard, crop lifecycle enforcement, role security (Worker/Customer/Unauthenticated → 401/403/404), farmer isolation, crop list stock display sync, and formatted display (**271 / 271 total backend tests passing**).
- [x] Verified backend build (`dotnet build FarmKart.Infrastructure`), test suite (`dotnet test --filter FarmerCropStockTests`: 23/23 passing), and frontend build (`npx ng build`).

## Phase 6.4 Deliverables — Customer Dashboard and Navigation Shell

- [x] Created Angular `CustomerShellComponent` (`/customer`) with responsive desktop sidebar, mobile menu drawer, FarmKart branding, dynamic customer name/email, customer role badge, and logout action.
- [x] Created Angular `CustomerDashboardComponent` displaying customer hero greeting ("Welcome, {Customer Name}"), workspace subtitle, and 6 customer service cards (Browse Auctions - ACTIVE, My Bids - COMING SOON, My Orders - COMING SOON, Payments - COMING SOON, Notifications - COMING SOON, My Profile - COMING SOON).
- [x] Created Angular customer `ComingSoonComponent` displaying module title, "Coming Soon" badge, future phase explanation message, and "Back to Dashboard" navigation button.
- [x] Protected `/customer` route structure in `app.routes.ts` using existing `authGuard` and `roleGuard` (`roles: ['Customer']`), preserving existing Farmer (`/farmer`) and Worker (`/worker`) route guards intact.
- [x] Dynamically resolved customer name and email from `AuthService.currentUser$` observable without hardcoded values.
- [x] Added Angular unit tests for `CustomerShellComponent`, `CustomerDashboardComponent`, and customer `ComingSoonComponent` (**190 / 190 total frontend unit tests passing**).
- [x] Verified frontend build (`npx ng build`) and test suite (`npm test`).

## Phase 6.5 Deliverables — Customer Auction Marketplace

- [x] Enforced strictly **AUCTION-ONLY marketplace business rule**: Zero direct buy, cart, checkout, or payment functionality added.
- [x] Created `CustomerAuctionResponse` and `CustomerAuctionFilterRequest` DTOs in `CustomerAuctionDtos.cs`.
- [x] Implemented `ICustomerAuctionService` / `CustomerAuctionService` with dynamic status computation (`LIVE`, `UPCOMING`, `ENDED`), search (crop name, variety, farmer name, location), category filter, status filter, location filter, sorting (Ending soon, Newest, Price asc/desc), and primary image resolution.
- [x] Excluded `Draft` and `Cancelled` auctions from customer marketplace queries.
- [x] Exposed REST endpoints in `CustomerAuctionsController.cs` under `[Authorize(Roles = Roles.Customer)]`:
  - `GET /api/customer/auctions` — query marketplace with filters & sorting
  - `GET /api/customer/auctions/{id}` — retrieve single auction details
- [x] Created Angular `customer-auction.models.ts` and `CustomerAuctionService` API client.
- [x] Created Angular `CustomerAuctionsComponent` (`/customer/auctions`) with search bar, category select, status select, sort dropdown, clear filters action, responsive auction card grid (primary image, crop type badge, status overlay, quantity, starting price, current highest bid, min increment, farmer info, start/end dates, "View Auction" button), loading skeleton, error retry state, and empty result state.
- [x] Created Angular `CustomerAuctionDetailComponent` (`/customer/auctions/:id`) displaying large primary crop photo with gallery thumbnails, crop description, farmer profile, auction schedule, starting price & min increment, and a guarded "Live Bidding" section showing notice `"Bidding will be available in the next phase."`.
- [x] Added 9 backend integration tests in `CustomerAuctionTests.cs` verifying marketplace retrieval, cancelled/draft exclusion, search/category/status filters, single auction resolution, 404 for invalid IDs, 401 unauthenticated rejection, and 403 role security for Farmer/Worker (**280 / 280 total backend tests passing**).
- [x] Added Angular unit specs for `CustomerAuctionsComponent` and `CustomerAuctionDetailComponent` (**196 / 196 total frontend unit tests passing**).
- [x] Verified solution build (`dotnet build backend/FarmKart.API`), backend tests (`dotnet test`), frontend build (`npx ng build`), and frontend tests (`npm test`).

## Phase 6.5 Extension — Auction Durations & Real-Time Countdown Timer

- [x] **Predefined & Custom Auction Durations**:
  - Refactored `CreateFarmerAuctionRequest` and `UpdateFarmerAuctionRequest` DTOs to replace `EndTimeUtc` with `Duration` string parameter.
  - Implemented `FarmerAuctionService.ParseDurationToHours` supporting predefined options (`5 Hours`, `12 Hours`, `1 Day` (24h, default), `3 Days` (72h), `7 Days` (168h)) and custom manual hours entry (e.g. `"8 Hours"` or `"36"`).
  - Server automatically calculates `EndTimeUtc = StartTimeUtc + DurationHours`.
- [x] **Authoritative UTC & Server Time Offset Sync**:
  - Added `ServerTimeUtc` to `FarmerAuctionResponse` and `CustomerAuctionResponse` DTOs.
  - Clients compute server time offset (`serverTimeUtc - clientUtcNow`) to ensure all user clocks align seamlessly across devices regardless of local clock drift.
- [x] **Dynamic Auction Lifecycle & Reusable Countdown Timer Component**:
  - Built standalone `AuctionCountdownComponent` (`app-auction-countdown`) emitting real-time tick-by-tick countdowns (`DD:HH:MM:SS` or `HH:MM:SS`).
  - Automatically handles status transitions (`SCHEDULED` -> `LIVE` -> `ENDED` / `CANCELLED`) derived directly from server-authoritative UTC end times.
  - Guarantees countdowns stop at `00:00:00` without displaying negative numbers.
  - Cleans up `setInterval` handle on component destroy to eliminate memory leaks.
- [x] **Farmer & Customer Workspace UI Refactoring**:
  - Updated Farmer Create Auction Modal with Duration select dropdown, custom hours field, and live End Date & Time preview label (`Auction ends on: {date}`).
  - Fixed input field styling (`.app-input`) and card contrast in light and dark modes for high visibility.
  - Updated Farmer crop auction list and Customer marketplace auction cards to feature live countdown timers.
- [x] **Unit & Integration Tests**:
  - Added 26 backend tests in `FarmerAuctionDurationTests.cs` verifying duration parsing, end-time calculation, dynamic status checks, and API integration.
  - Added 12 frontend unit tests in `auction-countdown.component.spec.ts` covering status transitions, formatting, server offset sync, and lifecycle cleanup (**208 / 208 total frontend unit tests passing**).

## Phase 6.9 Deliverables — Farmer Auction Management & Live Bids Dashboard

- [x] **Farmer Sidebar & Routing**:
  - Added `My Auctions` (`/farmer/auctions`) link with `gavel` icon to `farmer-shell.component.ts`.
  - Registered `/farmer/auctions`, `/farmer/auctions/:id`, and `/farmer/auctions/:id/bids` in `app.routes.ts` protected by `authGuard` and `roleGuard`.
- [x] **My Auctions Page (`/farmer/auctions`)**:
  - Real-time summary header cards (Total Auctions, Upcoming, Live, Ended).
  - Client-side status filter tabs (All, Live, Upcoming, Ended, Cancelled) and search box (crop name & variety).
  - Auction cards displaying crop image, variety, quantity (Kg & Man), starting price (₹/Man), current highest bid (₹/Man), total bids count, total demand (Kg & %), status badge (🔴 LIVE, 🟡 UPCOMING, ⚫ ENDED, ⚪ CANCELLED), `app-auction-countdown`, and action buttons `[ VIEW AUCTION ]` & `[ VIEW BIDS ]`.
- [x] **View Auction Page (`/farmer/auctions/:id`)**:
  - Complete details sections: Auction Information, Crop Info, Quantity/Stock, Pricing, Timeline, Live Bidding Activity banner, Final Allocation Summary table (for Ended auctions), and Payment Summary.
- [x] **View Bids Page (`/farmer/auctions/:id/bids`)**:
  - Metric banner header displaying Total Demand Kg, Demand %, Total Bids.
  - Custom sort dropdown (Highest Bid, Lowest Bid, Highest Quantity, Latest Bid, Earliest Bid).
  - Responsive table and cards displaying Customer Name (Public full name ONLY), Requested Quantity (Kg & Man), Bid Price / Man, Bid Time (Local timezone), Bid Status (`VALID`), and Allocation Status (`WON`, `PARTIALLY_WON`, `LOST`).
- [x] **Security & Authorization**:
  - Authenticated user + Farmer role + Farmer ownership check enforced across all backend APIs. Returns 404/403 for unowned requests. Customer credentials (email, password, tokens, internal IDs) are strictly omitted.
- [x] **Real-Time Polling & Pricing Consistency**:
  - Safe 5-second polling on live views for active live auctions.
  - All pricing consistently rendered in **₹ / Man** ($1\text{ Man} = 20\text{ Kg}$).
- [x] **Automated Tests**:
  - Backend integration tests in `FarmerAuctionManagementTests.cs` (4/4 passing).
  - Frontend unit tests for `FarmerAuctionsComponent`, `FarmerAuctionDetailComponent`, and `FarmerAuctionBidsComponent` (**217 / 217 total frontend unit tests passing**).

## Phase 8.5 Deliverables — Farmer Public Profile + Machinery Ratings & Reviews

- [x] **Farmer Public Profile Backend**:
  - Implemented `IFarmerProfileService` / `FarmerProfileService` retrieving public farmer profile data: Farmer Name, Farm Name, Location (City, State), Member Since Date, Average Farmer Rating (calculated ONLY from Crop Order Reviews), Verified Reviews list, Active Auctions list, and Listed Machinery owned strictly by this farmer (`Machinery.OwnerUserId == FarmerUserId`).
  - Sensitive information (passwords, auth data, payment details, private emails) is strictly omitted.
  - Exposed `GET /api/farmers/{farmerId}/profile` in `FarmerProfileController.cs`.
- [x] **Machinery Ratings & Reviews Backend**:
  - Reused existing Phase 8.1 `Review` entity using `ReviewEntityType.MachineryRental` and `RelatedEntityId = rentalId`.
  - Implemented `IMachineryReviewService` / `MachineryReviewService` validating completed rental eligibility, renter-only authorization, 1-5 rating range enforcement, duplicate review prevention, owner-authorized machinery review management (`GET /api/my-machinery/{id}/reviews` verifying `machinery.OwnerUserId == authenticatedUserId`), and unified My Reviews (`GET /api/my-reviews`).
  - Exposed endpoints in `MachineryReviewController.cs`.
- [x] **Frontend UI & Components**:
  - Created `FarmerPublicProfileComponent` (`/customer/farmers/:farmerId` & `/farmer/farmers/:farmerId`) with header profile card, average rating badge, filter tabs for Active Auctions (`All`, `Live`, `Upcoming`, `Ended`), filter tabs for Listed Machinery (`All`, `Available`, `Rented`, `Unavailable`), and Customer Reviews grid.
  - Created `OwnerMachineryReviewsDialogComponent` allowing machinery owners (Farmers and Customers) to view received reviews directly from `My Machinery`.
  - Updated `CustomerReviewsComponent` and `FarmerReviewsComponent` with tabbed filter buttons (`All`, `Crop Reviews`, `Machinery Reviews`), metric summary cards, and dynamic empty states.
  - Updated `CustomerMachineryDetailComponent` displaying owner's Farmer Profile link and Machinery Reviews & Ratings section.
  - Updated `CustomerMyRentalsComponent` adding `[ RATE MACHINERY ]` button for completed rentals.
- [x] **Automated Integration Tests**:
  - 8 integration tests in `FarmerProfileAndMachineryReviewTests.cs` (100% passing). Total backend suite: 501 / 501 tests passing.
  - Verified backend build (`dotnet build`) and Angular frontend build (`npx ng build`).

- [x] **Phase AI-1: Multilingual AI + Voice Foundation**:
  - **Skipped Intentional Phases**: Phase 8.8 (Notifications) and Phase 8.9 (Reports & Disputes) were intentionally skipped as requested.
  - **Backend AI Architecture**: Created `OpenAiOptions`, `.env.example` templates, `IAiProvider` and `IAiService` abstractions, `OpenAiProvider` HTTP client, `AiService`, and `POST /api/ai/chat` endpoint.
  - **Frontend AI Assistant Component**: Built floating `AiAssistantComponent` with Web Speech Recognition API voice integration (`en-IN`, `hi-IN`, `gu-IN`), multilingual support (`en`, `hi`, `gu`), loading/error states, and keyboard accessibility.
  - **Testing**: 10 backend unit tests in `AiServiceTests.cs` (100% passing) and 10 frontend unit tests (100% passing).
- [x] **Phase AI-2: Context-Aware Conversational Form Engine**:
  - **Generic Engine Architecture**: Built reusable task context (`AiTaskContext`), form field definitions (`AiFormFieldDefinition`), and session state (`AiConversationSession`) in `FarmKart.Application.DTOs`.
  - **Backend Services & API**: Created `IAiConversationSessionStore` / `InMemoryAiConversationSessionStore` (thread-safe in-memory session store with user ownership isolation and 30-minute session cleanup) and `IAiConversationEngine` / `AiConversationEngine` (`backend/FarmKart.Infrastructure/Services/AI/AiConversationEngine.cs`).
  - **Endpoints**: `POST /api/ai/conversation/start`, `POST /api/ai/conversation/message`, and `POST /api/ai/conversation/cancel` in `AiConversationController.cs`.
  - **Intelligent Flow & Extraction**: Extracts single or multiple fields from natural language user input; updates user corrections; enforces one-question-at-a-time flow; enforces required vs optional field rules; handles intent commands (`cancel`, `restart`, `skip`); performs field format validation (`phone`, `number`, `decimal`, `boolean`); generates confirmation summaries without writing to the database.
  - **Frontend Integration & Voice**: Created `AiConversationService` (`frontend/src/app/core/services/ai-conversation.service.ts`) with RxJS subjects (`fieldUpdated$`, `formCompleted$`, `formCancelled$`); integrated task mode in `AiAssistantComponent` with confirmation summary card; reuses AI-1 microphone voice input.
  - **Testing & Health**: Added 9 backend integration tests in `AiConversationEngineTests.cs` (100% passing). Total backend suite: 540 / 540 tests passing. Added 14 frontend specs (100% passing). `npx ng build` and `dotnet build` verified with 0 errors.
- [x] **Phase AI-3: Profile AI Assistant (Farmer, Customer, Worker)**:
  - **Contextual Actions**: Added `[ 🤖 Fill Profile with AI ]` action buttons directly on the profile views for Farmer (`farmer-profile.component.ts`), Customer (`customer-profile.component.ts`), and Worker (`worker-profile.component.ts`).
  - **Task Initialization**: Pre-populates existing profile form values (`fullName`, `phone`, `address`, `farmName`, `farmSize`, `city`, `experienceYears`, etc.) into `StartAiConversationRequest.initialData`. AI ONLY asks for missing values or requested edits.
  - **Live Reactive Form Sync**: Subscribed components to `fieldUpdated$` to automatically update Angular Form Controls / Signals live as fields are extracted.
  - **Confirmation & Security**: Requires explicit user confirmation in the AI panel (`Confirm & Save`) before calling existing backend profile APIs (`FarmerProfileService`, `CustomerProfileService`, `WorkerJobService`). No database writes occur without explicit user confirmation.
- [x] **Phase AI-4: Crop AI Assistant (Farmer Crop Add/Edit)**:
  - **Contextual Actions**: Added `[ 🤖 Add Crop with AI ]` and `[ 🤖 Edit Crop with AI ]` action buttons on `farmer-crop-form.component.ts`.
  - **Task Context**: Supports `create_farmer_crop` and `update_farmer_crop` tasks with fields `cropName`, `cropType`, `variety`, `area`, `areaUnit`, `status`, `description`. Pre-populates existing crop values when editing.
  - **Live Form Sync & Save**: Updates crop form signals live upon field extraction (`fieldUpdated$`); invokes existing `FarmerCropService.createCrop()` / `updateCrop()` only after user clicks `Confirm & Save`.
  - **Role Security & Verification**: Customer/Worker roles cannot create crops; only authenticated Farmers can use Crop AI. All backend tests (545/545 passing) and frontend specs (15/15 passing) verified. `npx ng build` and `dotnet build` pass with 0 errors.
- [x] **Global Website Localization Phase (English, Hindi, Gujarati)**:
  - **Localization Engine**: Built `LanguageService` (`frontend/src/app/core/services/language.service.ts`) with reactive signal state `currentLanguage = signal<SupportedLanguage>('en')`, `TranslatePipe`, and translation dictionaries (`frontend/src/app/core/i18n/translations.ts`).
  - **Authentication Selection**: Added Language Selection bar (`English` | `हिंदी` | `ગુજરાતી`) on login/registration screens. Selected language switches form UI instantly and persists as global application language upon login.
  - **App Shell Switchers**: Added `🌐 Language` dropdown in Customer, Farmer, and Worker shell headers. Changing language updates all navigation items, buttons, status labels, and form titles instantly.
  - **Zero Untranslated Page Audit**: Audit and localized 100% of user-facing components across Farmer, Customer, Worker, and Shared screens (Dashboards, Profiles, Crops, Auctions, Orders, Reviews, Machinery, Rentals, Jobs, Applications, Assignments, Attendance, Earnings, Notifications, Reports, Disputes, Settings, Auth, Shared Dialogs).
  - **AI & Voice Synchronization**: FarmKart AI assistant freeform chat, Web Speech microphone recognition (`en-IN`, `hi-IN`, `gu-IN`), and contextual task engines (Profile AI, Crop AI) automatically inherit the global site language.
  - **Data Preservation & Safe Fallback**: Database content, backend enums, and API contracts remain untouched; missing translation keys fall back safely to English.
  - **Testing & Verification**: Added 4 unit tests in `language.service.spec.ts` (100% passing). Total backend test suite: **545 / 545 tests passing**. Total frontend vitest specs: **20 / 20 passing**. `npx ng build` and `dotnet build` verified with 0 errors. Committed & pushed (`fix: complete global multilingual localization`).

