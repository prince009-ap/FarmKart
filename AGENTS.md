# AGENTS.md

These instructions apply to AI coding agents working in this repository.

## Working Principles

- Read the current repository state before making changes.
- Respect the active phase and do not implement features from future phases.
- Prefer small, reviewable edits over sweeping rewrites.
- Keep controllers thin and move logic into the application layer.
- Add abstractions only when they solve an actual problem in the current phase.

## Architectural Guardrails

- Backend layering must remain `API -> Application -> Domain` and `API -> Infrastructure`.
- Infrastructure may depend on Application and Domain.
- Domain should stay framework-light and should not depend on Infrastructure.
- Authentication, role logic, SignalR flows, payments, and AI integrations are off-limits until explicitly requested.
- JWT storage must be prepared for HttpOnly cookies only; never use localStorage or sessionStorage for tokens.
- Preserve historical marketplace, rental, auction, and payment records with deliberate delete behaviors.

## Implementation Rules

- Use DTOs for API contracts.
- Validate all backend inputs.
- Keep secrets out of source control and configuration files committed to the repo.
- Use environment-aware configuration and dependency injection.
- Prefer async APIs for I/O-bound work.
- Do not modify unrelated modules while completing a task.
- Prefer Fluent API for EF Core precision, constraints, indexes, and relationship mapping.
- Keep authentication concerns out of profile/domain tables until the authentication phase.

## Delivery Expectations

- Update `PROGRESS.md` when a phase meaningfully advances.
- Update documentation when the architecture or setup changes.
- Build and verify affected projects before finishing when dependencies are available.
- Stop after the requested phase and wait for the next instruction.
