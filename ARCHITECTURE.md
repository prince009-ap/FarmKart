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
- The `ApplicationUser` class inherits from `IdentityUser<Guid>` and acts as the central user model.
- Each profile (`FarmerProfile`, `WorkerProfile`, `CustomerProfile`) defines a `UserId` of type `Guid` which maps one-to-one with `ApplicationUser`.
- Delete behavior is set to `Restrict` on profile-user relationships to avoid accidental deletion of profile and historical data.
- The three application roles are centralized as constants in `Roles.cs`:
  - `Farmer`
  - `Worker`
  - `Customer`
- Roles are automatically seeded at startup in an idempotent manner via `IdentityRoleSeeder` in `Program.cs`. Running the application multiple times will not duplicate roles.
- The authentication flow (JWT generation/validation, HttpOnly Secure Cookie mechanism, and Angular client integration) is NOT implemented yet and will be added in later phases. Login is NOT implemented.

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

- The Angular client will communicate with the ASP.NET Core API over HTTP.
- Angular `HttpClient` and reactive forms will be used in future phases.
- Authentication will later use JWT stored in HttpOnly cookies, not browser storage.
- SignalR will be introduced later for auctions, chat, and notifications.
