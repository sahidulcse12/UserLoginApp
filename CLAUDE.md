# CLAUDE.md

Guidance for working in this repo. The architecture follows the BdjobsMIS **AuthenticationSubsystem** reference pattern. The full source write-up is `Instruction/instruction.md`.

## Project at a glance

- A .NET 8 backend (SDK pinned in `global.json` to 8.0.412; shared project settings live in `Directory.Build.props`). It handles user login and access control on top of **Keycloak** (`https://auth.bdjobs.com`, realm `UserLogin`). It uses EF Core 8 on SQL Server (`bdjEmails` DB, `kc` schema) and stores passwords with BCrypt.
- **Host:** `AuthSubsystem.APIs` (http://localhost:5000, Swagger at `/swagger`). Only the Auth subsystem and what it depends on (Shared, DataSync) are in the solution right now; the Roles subsystem was removed and is in git history (commit `439838f`) if it is needed again.
- The Auth host keeps `<AssemblyName>UserLoginApp.Api</AssemblyName>`, so the published DLL name and the existing deployment stay unchanged. It runs behind the gateway under the `/KeycloakLogin` path prefix; its `Program.cs` rewrites `PathBase` for that and honours forwarded headers.
- **KeycloakTheme/**: a custom Keycloak login theme.
- **UserLoginApp.Web**: build output only, with no source.

Commands (run from the repo root):
```
dotnet build UserLoginApp.sln
dotnet run --project AuthSubsystem/AuthSubsystem.APIs     # :5000
dotnet tool restore                                       # dotnet-ef, from .config/dotnet-tools.json
dotnet ef migrations add <Name> --project AuthSubsystem/AuthSubsystem.Repositories --startup-project AuthSubsystem/AuthSubsystem.APIs
```
The Auth `Program.cs` runs `DbSeeder.SeedAsync` on startup. It is currently a no-op, but it receives both the DbContext and the Keycloak service.
`dotnet ef migrations has-pending-model-changes` currently reports changes. That predates the five-layer refactor: the snapshot still lists the Role/Feature tables, which the model doesn't map.

## Architecture

### Rules that matter
- **Controllers stay thin.** A controller injects `IServiceBus`, sends a command and shapes the HTTP result. It holds no business logic, no DbContext, and no Keycloak calls.
- **Everything is in-process through the ServiceBus.** No HTTP calls between subsystems.
- **Subsystems never reference each other.** Cross-subsystem side effects go through events (`BroadcastEventAsync`), and the event types that cross a boundary live in `SharedSubsystem.DTOs/IntegrationEvents`. When a flow genuinely spans subsystems, add an Orchestrator project; it's the only project allowed to reference several subsystems. None exists yet.
- **One documented exception:** `AuthSubsystem.APIs` references `DataSyncSubsystem.APIs` and calls `AddDataSyncSubsystem()`. Because the bus is in-memory, DataSync's handlers have to run in the process that raises `UserRegisteredEvent` and `UserLoggedInEvent`. DataSync has no host of its own.
- **Repositories take and return entities.** Handlers map from DTOs/commands to entities before they call a repository.

### Five layers per subsystem (required)
Every subsystem (`Auth`, `DataSync`, and any new one) is split into **five projects**. A project may reference only the projects listed on its line, and that is enforced by `ProjectReference`, not by convention:

```
<X>Subsystem/
  <X>Subsystem.APIs          -> Handlers, DTOs, ServiceBus.Handlers, SharedSubsystem.Handlers
  <X>Subsystem.Handlers      -> Repositories, DTOs, SharedSubsystem.Handlers
  <X>Subsystem.Repositories  -> Aggregators, DTOs
  <X>Subsystem.Aggregators   -> DTOs
  <X>Subsystem.DTOs          -> SharedSubsystem.DTOs
```
A layer with nothing in it yet still gets its project; DataSync's DTOs, Aggregators and Repositories are empty for now.

| Layer | Contains | Examples in this repo |
|---|---|---|
| **DTOs** | Commands, subsystem-internal events, request/response DTOs, settings POCOs | `Commands/AuthCommands.cs`, `AuthDtos.cs` (`LoginRequest`, `TokenResponse`, `AuthResult<T>`) |
| **Aggregators** | Entities (`Entities/`); later AutoMapper `Profile`s | `Entities/User` |
| **Repositories** | `Data/*DbContext`, `Migrations/`, `Abstractions/I*Repository`, implementations | `AppDbContext`, `IAuthRepository`/`AuthRepository` |
| **Handlers** | One folder per feature (`<Feature>/*Handler.cs`), external-service clients, claims transformation, seeding; later `Validators/` | `Login/`, `Register/`, `RefreshToken/`, `Keycloak/`, `Claims/FeatureClaimsTransformation`, `Seeding/DbSeeder` |
| **APIs** | `Program.cs` (hosts), `Extensions/ServiceCollectionExtensions.cs`, thin `Controllers/` | `AuthController`; `DataSyncSubsystem.APIs` is a class library holding only `AddDataSyncSubsystem()` |

Shared infrastructure (`Shared/`), which every subsystem may reference:
- **SharedSubsystem.DTOs**: marker interfaces `ICommand<TResult>` and `IEvent`, plus `IntegrationEvents/` for events that cross subsystems.
- **SharedSubsystem.Handlers**: `ICommandHandler<,>`, `IEventHandler<>` and the `IServiceBus` interface. The interface sits here so that handlers can broadcast events.
- **ServiceBus.Handlers**: `InMemoryServiceBus` and `AddServiceBus()`.

Entities never cross subsystems: another subsystem refers to an Auth user by its `Guid` id only, never through a navigation to `User`.

Conventions:
- The DbContext uses Fluent configuration: `OnModelCreating` calls a private `Configure<Entity>(EntityTypeBuilder<T>)` for each entity.
- Validators follow the `RuleFor(...).Cascade(CascadeMode.Stop).NotNull().NotEmpty().MaximumLength(n).Must(...)` shape.
- DI registration lives in each host's `Extensions/ServiceCollectionExtensions.cs` (`AddDatabase`, `Add<X>SubsystemServices`, `AddKeycloakJwtAuthentication`, `AddCorsPolicy`, `AddSwaggerDocumentation`), so that `Program.cs` reads as a list.
- Library projects that need ASP.NET Core or `Microsoft.Extensions.*` types get `<FrameworkReference Include="Microsoft.AspNetCore.App" />`. Their implicit usings don't include `Microsoft.Extensions.Logging` and similar namespaces, so add those usings explicitly.
- Use correct spelling in new names: `Extensions`, `Abstractions`, `AddServiceBus`. The reference has typos; don't copy them.

### Adding a feature
Work from the bottom layer up:
1. **DTOs:** add `record XCommand(...) : ICommand<TResult>` and the request/response DTOs. A `record XEvent(..., DateTime OccurredOn) : IEvent` goes here if only this subsystem uses it, or in `SharedSubsystem.DTOs/IntegrationEvents` if another subsystem handles it.
2. **Aggregators:** add or extend the entity, and add the DTO ↔ entity mapping. For now mapping is manual and lives in the Handlers project (for example the entity → `UserDto` mapping in `LoginUserCommandHandler`); it moves to an AutoMapper `Profile` once AutoMapper is added.
3. **Repositories:** add the method to `I<X>Repository` and its implementation. It takes and returns entities. Add a migration if the schema changes.
4. **Handlers:** add `<Feature>/XCommandHandler : ICommandHandler<XCommand, TResult>`. Validate first (inline checks for now, `Validators/` once FluentValidation is added), then map to entities, call the repositories and services, broadcast events, and return the result.
5. **APIs:** add a controller action that dispatches through `IServiceBus`. Register the handler in `Extensions/ServiceCollectionExtensions.cs` with `AddScoped<ICommandHandler<XCommand, TResult>, XCommandHandler>()`, and do the same for each `IEventHandler<>`. Registration is manual, and the bus throws `InvalidOperationException` if a handler is missing.

### Results and errors
- Auth handlers return `AuthResult<T>` (`Ok(data)` / `Fail(statusCode, message)`). Controllers map it to `Ok(result.Data)` or `StatusCode(result.StatusCode, new { message })`. **Keep returning real HTTP status codes.** The reference returns 200 for everything and carries failure in an `Event` envelope, and that is listed as a weakness.
- If a uniform envelope is introduced, model it on the reference's `Event { EventType, EventId, EventData }` plus `EventFactory.CreateSuccessEvent/CreateFailureEvent`. Give each `EventId` enum member a `[Description]` holding its user-facing message, and use the code ranges: 1000s CRUD, 2000s processing, 3000s auth/validation, 4000s system, 5000s DB, 6000–8000 domain, 9000 internal error.

## Authentication and authorization

- **Keycloak issues the tokens.** The API validates them with `AddJwtBearer` against `Keycloak:Authority`, which fetches JWKS. `MapInboundClaims = false` and `NameClaimType = "preferred_username"` are set. The issuer is validated. The audience is not validated, because access is checked via roles and scopes.
- **Login** (`LoginUserCommandHandler`):
  1. Try a Keycloak password grant.
  2. If that fails, fall back to a **legacy `dbo.ADMIN` migration**: provision the user in Keycloak as `mis`, upsert the local `User` with a BCrypt hash, then log in again. If the login fails because of required actions, clear them and retry once.
  3. Store the refresh token on the local user and broadcast `UserLoggedInEvent`.
- **Logout** (`POST /api/auth/logout`, body `{ "refreshToken": "..." }`, no Bearer token needed):
  1. Posts the refresh token to Keycloak's OpenID Connect `logout` endpoint, which ends the session. If Keycloak rejects the token, the endpoint returns 401.
  2. Marks the session id (`sid`) as revoked in `IRevokedSessionStore`. `OnTokenValidated` then rejects that session's access tokens in this API straight away. The store is in-memory (`AddDistributedMemoryCache`), so it must move to a shared cache such as Redis before running more than one instance. Other services that validate Keycloak tokens themselves don't see this list.
  3. Finds the local user by Keycloak user id (`sub`), falling back to the stored refresh token, then clears `RefreshToken`/`RefreshTokenExpiryTime` and broadcasts `UserLoggedOutEvent`. A repeat logout returns 200 without updating anything or sending the event again.
- **Keycloak id sync:** login stores the access token's `sub` as `User.KeycloakId`, since it changes if the Keycloak account is recreated.
- **Register** accepts the user types `corporate`, `mybdjobs` and `mis`. The user is created in Keycloak first, then persisted locally, then `UserRegisteredEvent` is broadcast.
- **Authorization data:** the live DB currently has only `kc.Users`; there are no role or feature tables yet. `FeatureClaimsTransformation` resolves the user by `sub` or `preferred_username` (creating them on first sight) and adds a `userType` claim.
- When doing module/feature checks, match **exact IDs**. Never use substring `Contains` on a delimited claim string, because `"5"` would match `"57"`.

## Testing pattern
There is no test project yet. When adding one, use xUnit, Moq and FluentAssertions:
- **Handler tests** use real mapping profiles and validators. Mock the repositories, `IKeycloakAdminService`, `IServiceBus` and `IHttpContextAccessor`, and use a `DefaultHttpContext`. Build config with `ConfigurationBuilder().AddInMemoryCollection(...)` and log with `NullLogger<T>.Instance`. Group tests with a `#region` per handler, name them `Method_ShouldX_WhenY`, and use `[Theory]` for invalid inputs.
- **Controller tests** mock `IServiceBus` and assert the result type, plus that the right command was sent.

## Don't repeat these (known weaknesses from the reference)
1. Never compare plaintext passwords and never log passwords. `GetLegacyAdminUserAsync` matches the plaintext `UPASS` of the legacy table; confine that to the migration path.
2. **Secrets must not be in `appsettings.json`.** Use user-secrets, env vars or Key Vault, and fail fast at startup when a required secret is missing. No hard-coded fallback keys.
3. If the API ever signs its own JWTs, validate issuer, audience, lifetime and signing key, and use a single key encoding.
4. Every ServiceBus path should throw when a handler is missing. Never return `null`.
5. Add a working global exception middleware (ValidationException → 400 with errors, KeyNotFound → 404, otherwise 500) and register it with `app.UseMiddleware<...>()`.
6. Use proper types (`int?`, not `object`), with no `Console.WriteLine` or dead variables in repositories, and no duplicate `ProjectReference`s.
7. Guard claim parsing (`int.TryParse`, not `int.Parse`).
8. Consider assembly scanning (Scrutor) instead of one manual DI line per handler.
