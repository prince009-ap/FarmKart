# FarmKart

FarmKart is an AI-powered agriculture platform being built in small, controlled phases. This repository now contains the frontend foundation plus the Phase 2 backend domain model, Entity Framework Core configuration, and the initial SQL Server migration.

## Technology Stack

- Frontend: Angular, TypeScript, Angular Router, RxJS, Reactive Forms, Angular Material, Tailwind CSS
- Backend: ASP.NET Core Web API, C#, Entity Framework Core, SQL Server, Dependency Injection, LINQ
- Testing: xUnit for backend, Angular testing tools for frontend
- Tooling: VS Code, Git, GitHub, Postman, SQL Server Management Studio

## Repository Layout

- `frontend/` contains the `FarmKart.Client` Angular application shell.
- `backend/` contains the backend solution projects.
- `backend/FarmKart.Infrastructure/Persistence/Migrations/` contains the generated EF Core migration history.
- Root documentation files define architecture, guardrails, and delivery progress.

## Basic Setup

### Backend

```powershell
dotnet restore
dotnet build FarmKart.sln
dotnet test backend/FarmKart.Tests/FarmKart.Tests.csproj --no-build --no-restore
dotnet run --project backend/FarmKart.API
```

### Database Migration

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update --project backend/FarmKart.Infrastructure --startup-project backend/FarmKart.Infrastructure
```

### Frontend

```powershell
cd frontend
npm install
npm run build
npm start
```

## Current Scope

The repository currently includes:

- Angular frontend foundation and feature folder structure
- Clean-layered ASP.NET Core backend solution
- SQL Server-ready domain model for profiles, jobs, machinery rentals, crops, marketplace, auctions, chat, notifications, and reviews
- Initial EF Core migration: `InitialFarmKartDomain`

Farmer registration currently stores farm area as `FarmSize` with an explicit `FarmSizeUnit`. The active registration unit is **Vigha**. Location coordinates are not part of farmer registration; map-based location selection will be added later.

Authentication, controllers, business workflows, SignalR, payments integration, and AI features are still intentionally deferred.
