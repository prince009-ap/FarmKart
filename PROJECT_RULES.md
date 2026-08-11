# Project Rules

1. Do not invent requirements.
2. Inspect existing code before modifying it.
3. Do not rewrite working code unnecessarily.
4. Keep controllers thin.
5. Keep business logic outside controllers.
6. Use DTOs for API contracts.
7. Validate data on the backend.
8. Never trust frontend input.
9. Never expose secrets.
10. Never store passwords in plain text.
11. Never store JWT in localStorage or sessionStorage.
12. JWT will be stored using HttpOnly cookies.
13. Use async/await for I/O operations.
14. Use meaningful HTTP status codes.
15. Do not add unnecessary dependencies.
16. Keep features modular.
17. Write tests for important business logic.
18. Make small incremental changes.
19. Do not modify unrelated modules.
20. Create a Git commit after completing each development phase.
21. Keep configuration outside source code.
22. Use environment-specific settings.
23. Do not implement features before their planned phase.
24. Prefer simple abstractions over speculative architecture.
25. Keep frontend and backend concerns clearly separated.
26. Use deliberate delete behaviors for historical or financial records.
27. Prefer Fluent API for database constraints, indexes, precision, and relationships.
28. Protect marketplace, auction, rental, and payment history from accidental cascade deletes.
29. Use ASP.NET Core Identity ApplicationUser (using Guid keys) as the security principal, linked 1-to-1 with profile tables.
30. Reference central Roles constants (Farmer, Worker, Customer) for authorization and role checks instead of hardcoding strings.
