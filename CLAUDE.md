# CLAUDE.md

Guidance for working in this repo. The architecture follows the BdjobsMIS **AuthenticationSubsystem** reference pattern. The full source write-up is `Instruction/instruction.md`.

## Project at a glance

- **UserLoginApp.Api**: a .NET 8 Web API (SDK pinned in `global.json` to 8.0.412). It handles user login and access control on top of **Keycloak** (`https://auth.bdjobs.com`, realm `UserLogin`). It uses EF Core 8 on SQL Server (`bdjEmails` DB, `kc` schema) and stores passwords with BCrypt.
- **KeycloakTheme/**: a custom Keycloak login theme.
- **UserLoginApp.Web**: build output only, with no source.
- The API is hosted behind a gateway under the `/KeycloakLogin` path prefix. `Program.cs` rewrites `PathBase` for it and honours forwarded headers.

Commands (run from the repo root):
```
dotnet build UserLoginApp.sln
dotnet run --project UserLoginApp.Api            # http://localhost:5000, Swagger at /swagger
dotnet ef migrations add <Name> --project UserLoginApp.Api
```
`Program.cs` runs `DbSeeder.SeedAsync` on startup, and that call hits both the DB and Keycloak.

## Architecture

### Rules that matter
- **Controllers stay thin.** A controller only dispatches to an orchestrator or the service bus and shapes the HTTP result. It holds no business logic, no DbContext, and no Keycloak calls.
- **Everything is in-process through the ServiceBus.** No HTTP calls between subsystems.
- **Subsystems never reference each other.** Only `Orchestrators/` may coordinate across subsystems. Cross-subsystem side effects go through events (`BroadcastEventAsync`), as in `Subsystems/DataSync`.
- **Repositories take and return entities.** Handlers map from DTOs/commands to entities before they call a repository.

### Five layers per subsystem (required)
Every subsystem (`Auth`, `Roles`, `DataSync`, and any new one) is split into **five projects**. A project may reference only the projects listed on its line, and that is enforced by `ProjectReference`, not by convention:

```
<X>Subsystem/
  <X>Subsystem.APIs          -> Handlers, DTOs, ServiceBus, Shared.Handlers
  <X>Subsystem.Handlers      -> Repositories, DTOs, Shared.Handlers
  <X>Subsystem.Repositories  -> Aggregators, DTOs
  <X>Subsystem.Aggregators   -> DTOs
  <X>Subsystem.DTOs          -> Shared.DTOs (+ Shared.Handlers only if it needs Event types)
```

| Layer | Contains | Auth examples |
|---|---|---|
| **DTOs** | Commands, queries, events, request/response DTOs, settings POCOs | `LoginUserCommand`, `RegisterUserCommand`, `RefreshTokenCommand`, `UserRegisteredEvent`, `LoginRequest`, `TokenResponse`, `KeycloakConfig` |
| **Aggregators** | EF entities (`Entities/`, `[Table]`, `IEntity<TKey>`), AutoMapper `Profile`, `Abstractions/I*Aggregator` | `Entities/User`, `Role`, `Feature`, `RoleFeature`, `UserRole`; `AuthMappingProfile` |
| **Repositories** | `Data/*DbContext`, `Abstractions/I*Repository` and implementations, migrations | `AppDbContext`, `IAuthRepository`/`AuthRepository` |
| **Handlers** | `<Feature>/*Handler.cs`, `Validators/*Validator.cs`, external-service clients the handlers use | `Login/LoginUserCommandHandler`, `Register/RegisterUserCommandHandler`; `Validators/LoginRequestValidator`; Keycloak clients |
| **APIs** | `Program.cs`, `Extensions/ServiceCollectionExtensions.cs`, thin `Controllers/` | `AuthController`, `RolesController` |

Shared infrastructure sits outside the subsystems, and every subsystem may reference it:
- **Shared.DTOs**: marker interfaces (`ICommand<TResult>`, `IEvent`, `IEntity<TKey>`).
- **Shared.Handlers**: handler interfaces (`ICommandHandler<,>`, `IEventHandler<>`) and cross-cutting handlers such as `FeatureClaimsTransformation`.
- **ServiceBus**: `IServiceBus` / `InMemoryServiceBus` and an `AddServiceBus()` extension.
- **Orchestrators**: the only project allowed to reference more than one subsystem.

The current code (`UserLoginApp.Api` with `Controllers/`, `Core/Messaging/`, `Subsystems/`, `Data/`, `Models/`, `Services/`) is a single project and has to be moved into this layout. New code goes straight into the five-layer structure; don't add more code to the old folders.

Conventions:
- The DbContext uses Fluent configuration: `OnModelCreating` calls a private `Configure<Entity>(EntityTypeBuilder<T>)` for each entity.
- Validators follow the `RuleFor(...).Cascade(CascadeMode.Stop).NotNull().NotEmpty().MaximumLength(n).Must(...)` shape.
- Move DI registration out of `Program.cs` into extension methods (`AddDatabase`, `Add<X>SubsystemServices`, `AddJwtAuthentication`, `AddCorsPolicy`, `AddSwaggerDocumentation`) so that `Program.cs` reads as a list.
- Use correct spelling in new names: `Extensions`, `Abstractions`, `AddServiceBus`. The reference has typos; don't copy them.

### Adding a feature
Work from the bottom layer up:
1. **DTOs:** add `record XCommand(...) : ICommand<TResult>`, the request/response DTOs, and any `record XEvent(..., DateTime OccurredOn) : IEvent`.
2. **Aggregators:** add or extend the entity, and add the DTO ↔ entity mapping to the subsystem's AutoMapper `Profile`.
3. **Repositories:** add the method to `I<X>Repository` and its implementation. It takes and returns entities. Add a migration if the schema changes.
4. **Handlers:** add `<Feature>/XCommandHandler : ICommandHandler<XCommand, TResult>` and `Validators/XValidator`. Validate first, then map to entities, call the repositories and services, broadcast events, and return the result.
5. **APIs:** add a controller action that dispatches through the bus (or through an orchestrator if the flow spans subsystems). Register the handler in `Extensions/ServiceCollectionExtensions.cs` with `AddScoped<ICommandHandler<XCommand, TResult>, XCommandHandler>()`, and do the same for each `IEventHandler<>`. Registration is manual, and the bus throws `InvalidOperationException` if a handler is missing.

### Results and errors
- Auth handlers return `AuthResult<T>` (`Ok(data)` / `Fail(statusCode, message)`). Controllers map it to `Ok(result.Data)` or `StatusCode(result.StatusCode, new { message })`. **Keep returning real HTTP status codes.** The reference returns 200 for everything and carries failure in an `Event` envelope, and that is listed as a weakness.
- If a uniform envelope is introduced, model it on the reference's `Event { EventType, EventId, EventData }` plus `EventFactory.CreateSuccessEvent/CreateFailureEvent`. Give each `EventId` enum member a `[Description]` holding its user-facing message, and use the code ranges: 1000s CRUD, 2000s processing, 3000s auth/validation, 4000s system, 5000s DB, 6000–8000 domain, 9000 internal error.

## Authentication and authorization

- **Keycloak issues the tokens.** The API validates them with `AddJwtBearer` against `Keycloak:Authority`, which fetches JWKS. `MapInboundClaims = false` and `NameClaimType = "preferred_username"` are set. The issuer is validated. The audience is not validated, because access is checked via roles and scopes.
- **Login** (`LoginUserCommandHandler`):
  1. Try a Keycloak password grant.
  2. If that fails, fall back to a **legacy `dbo.ADMIN` migration**: provision the user in Keycloak as `mis`, upsert the local `User` with a BCrypt hash, then log in again. If the login fails because of required actions, clear them and retry once.
  3. Store the refresh token on the local user and broadcast `UserLoggedInEvent`.
- **Register** accepts the user types `corporate`, `mybdjobs` and `mis`. The user is created in Keycloak first, then persisted locally, then `UserRegisteredEvent` is broadcast.
- **Authorization data** lives in the local DB (Users → UserRoles → Roles → RoleFeatures → Features). `FeatureClaimsTransformation` resolves the user by `sub` or `preferred_username`, creating them on first sight, and adds the feature claims.
- When doing module/feature checks, match **exact IDs**. Never use substring `Contains` on a delimited claim string, because `"5"` would match `"57"`.

## Testing pattern
There is no test project yet. When adding one, use xUnit, Moq and FluentAssertions:
- **Handler tests** use real mapping profiles and validators. Mock the repositories, `IKeycloakAdminService`, `IServiceBus` and `IHttpContextAccessor`, and use a `DefaultHttpContext`. Build config with `ConfigurationBuilder().AddInMemoryCollection(...)` and log with `NullLogger<T>.Instance`. Group tests with a `#region` per handler, name them `Method_ShouldX_WhenY`, and use `[Theory]` for invalid inputs.
- **Controller tests** mock the orchestrator or bus and assert the result type, plus that the right command was sent.

## Don't repeat these (known weaknesses from the reference)
1. Never compare plaintext passwords and never log passwords. `GetLegacyAdminUserAsync` matches the plaintext `UPASS` of the legacy table; confine that to the migration path.
2. **Secrets must not be in `appsettings.json`.** Use user-secrets, env vars or Key Vault, and fail fast at startup when a required secret is missing. No hard-coded fallback keys.
3. If the API ever signs its own JWTs, validate issuer, audience, lifetime and signing key, and use a single key encoding.
4. Every ServiceBus path should throw when a handler is missing. Never return `null`.
5. Add a working global exception middleware (ValidationException → 400 with errors, KeyNotFound → 404, otherwise 500) and register it with `app.UseMiddleware<...>()`.
6. Use proper types (`int?`, not `object`), with no `Console.WriteLine` or dead variables in repositories, and no duplicate `ProjectReference`s.
7. Guard claim parsing (`int.TryParse`, not `int.Parse`).
8. Consider assembly scanning (Scrutor) instead of one manual DI line per handler.
