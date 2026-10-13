# BdjobsMIS AuthenticationSubsystem: Architecture Reference

Source: `D:\BdjobsMIS\AuthenticationSubsystem`, branch `major-architecture-refactor` (captured 2026-10-13).
Use this as a template for new projects. It is self-contained, so you don't need the original repo to apply it.

---

## 1. Stack

- .NET 8 Web API
- EF Core 8 (SQL Server)
- AutoMapper 13
- FluentValidation (11.3 AspNetCore / 12.1 core)
- JWT Bearer 8
- Serilog (Console + rolling File, configured from `appsettings`)
- Swashbuckle 6.6
- Tests: xUnit, Moq, FluentAssertions

## 2. Solution shape: modular monolith of isolated subsystems

```
                         MisContentOrchestrator   (only project allowed to cross subsystem boundaries)
            ┌──────────────┬──────┴───────┬──────────────┐
            v              v              v              v
    CompanySubsystem  JobSubsystem  ReportSubsystem  TenderSubsystem

AuthenticationSubsystem  CommentSubsystem  SupportingSubsystem   (standalone)

                 all of the above
                        │
                        v
            ServiceBus.Handlers ──> SharedSubsystem (DTOs, Handlers, Aggregators, Repositories)
```

- Business subsystems **never reference each other**.
- No HTTP between subsystems. Everything runs in-process through the ServiceBus.
- Each subsystem has its own `.APIs` host (Auth runs on `https://localhost:7101` / `http://localhost:5101`).

## 3. Five projects per subsystem

```
<X>Subsystem/
  <X>Subsystem.APIs          -> Handlers, DTOs, ServiceBus.Handlers, SharedSubsystem.Handlers
  <X>Subsystem.Handlers      -> Repositories, DTOs, SharedSubsystem.Handlers
  <X>Subsystem.Repositories  -> Aggregators, DTOs
  <X>Subsystem.Aggregators   -> DTOs
  <X>Subsystem.DTOs          -> SharedSubsystem.DTOs (+ SharedSubsystem.Handlers if it needs Event)
```

| Layer | Contains | Auth example files |
|---|---|---|
| **DTOs** | Commands (`: ICommand`), Queries (`: IQuery<TResult>`), request/response DTOs, settings POCOs | `LoginCommand`, `LogoutCommand`, `GetMyPermissionsQuery`, `VerifyModuleQuery`, `CheckCookieQuery`, `GetUserMenuQuery`, `ModuleDTO`, `MyPermissionsDTO`, `VerifyModuleResultDTO`, `UserSession`, `SecuritySettings` |
| **Aggregators** | EF entities (`Entities/`, `[Table]`, `IEntity<TKey>`), AutoMapper `Profile`, `Abstractions/I*Aggregator` | `Entities/Admin`, `LogTable`, `ModuleList`, `MISAdmin`; `LoginMappingProfile` |
| **Repositories** | `Data/*DbContext`, `Abstractions/I*Repository`, implementations | `LoginDbContext`, `ILoginRepository`/`LoginRepository`, `IModuleRepository`/`ModuleRepository` |
| **Handlers** | `<Feature>/*Handler.cs`, `Validators/*Validator.cs` | `Login/LoginHandler`, `LogoutHandler`, `GetMyPermissionsHandler`, `VerifyModuleHandler`, `CheckCookieHandler`, `GetUserMenuHandler`; `Validators/LoginCommandValidator` |
| **APIs** | `Program.cs`, `Extentions/ServiceCollectionExtentions.cs`, thin `Controllers/` | `LoginController` |

Conventions:
- Repositories take and return **entities**. Handlers map DTO/Command → Entity with AutoMapper before calling them.
- DbContext uses Fluent config: `OnModelCreating` calls a private `Configure<Entity>(EntityTypeBuilder<T>)` per entity.
- Validators use `RuleFor(...).Cascade(CascadeMode.Stop).NotNull().NotEmpty().MaximumLength(n).Must(...)`.

## 4. Shared infrastructure

### Marker interfaces (`SharedSubsystem.DTOs.Abstractions`)
```csharp
public interface ICommand { }
public interface IQuery<TResult>;
public interface IEntity<TKey> { TKey Id { get; set; } }
```

### Handler interfaces (`SharedSubsystem.Handlers.Abstructions`)
```csharp
public interface ICommandHandler<TCommand> where TCommand : ICommand
{ Task<IEnumerable<Event>> HandleAsync(TCommand command); }

public interface IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>
{ Task<TResult> HandleAsync(TQuery query); }
```

### ServiceBus (`ServiceBus.Handlers`)
```csharp
public interface IServiceBus
{
    Task<IEnumerable<Event>> SendCommandAsync<TCommand>(TCommand command) where TCommand : ICommand;
    Task<TResult> SendQueryAsync<TQuery, TResult>(TQuery query) where TQuery : IQuery<TResult>;
}

public class ServiceBus(IServiceProvider sp) : IServiceBus
{
    // resolves ICommandHandler<TCommand> / IQueryHandler<TQuery,TResult> from DI and calls HandleAsync
    // query: throws InvalidOperationException if no handler; command: returns null (inconsistent, fix in new project)
}

public static IServiceCollection AddServiceeBus(this IServiceCollection s)   // (sic) typo in original
    => s.AddScoped<IServiceBus, ServiceBus>();
```

### Event model: the uniform response envelope (`SharedSubsystem.Handlers`)
```csharp
public enum EventType { Success = 1, Fail = 2 }

public class Event
{
    public EventType EventType { get; set; }
    public List<KeyValuePair<string, object>>? EventData { get; set; }
    public EventId EventId { get; set; }
}

public static class EventFactory
{
    // => [ { EventType: Success, EventId, EventData: [ ("data", data) ] } ]
    public static IEnumerable<Event> CreateSuccessEvent(EventId id, object data);
    // => [ { EventType: Fail, EventId, EventData: [ ("message", <[Description] of enum>) ] } ]
    public static IEnumerable<Event> CreateFailureEvent(EventId id);
}
```
The `EventId` enum carries `[Description("...")]` on every member, and that text is the user-facing failure message. Code ranges:
- 1000s: CRUD (ResourceCreated 1000, Updated 1001, Deleted 1002, Retrieved 1003, NotFound 1005, UserInputError 1006)
- 2000s: processing
- 3000s: auth and validation (UserAuthenticated 3000, UserAuthorizationFailed 3001, UserLoggedOut 3002, ValidationError 3003, SecurityMismatch 3011, LoginFailed 3012, CookieNotFound 3013, InvalidSessionData 3014, LogoutFailed 3015, ModuleAccessDenied 3016)
- 4000s: system
- 5000s: DB
- 6000s–8000s: domain
- 9000: InternalServerError
- UnauthorizedAccess: 403

Rules:
- Commands **always** return `IEnumerable<Event>`.
- Queries return `IEnumerable<Event>` or a raw DTO. If they return a raw DTO, the controller wraps it with `EventFactory`.
- Controllers always `return Ok(...)`. HTTP status is 200, and success or failure is carried in `EventType`/`EventId`. The only real 401 comes from the JWT `OnChallenge`.

### Other shared pieces
- `IVerifyTokenAgainstCookieHandler.VerifyTokenAgainstCookie(ClaimsPrincipal user, string? rawCookie, string requestedModuleId)`: decodes the cookie JWT and requires that its `UserId` and `Modules` equal the principal's claims. If `requestedModuleId != "000"`, it also requires `Modules` to contain that ID.
- `ExceptionMiddleware`: maps ValidationException→400 with an errors list, KeyNotFound→404, anything else→500. It's broken as written (no constructor) and isn't wired in.
- `ApiResponse<T>(Success, StatusCode, Message, Data)`: exists but is unused by Auth.

## 5. Composition root

### Program.cs
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((ctx, sp, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerDocumentation();
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddAuthenticationSubsystemServices();
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddCorsPolicy();

var app = builder.Build();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseHttpsRedirection();
app.UseCors("AllowAngularApp");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
```

### Extentions/ServiceCollectionExtentions.cs
```csharp
AddDatabase(cfg)          -> AddDbContext<LoginDbContext>(UseSqlServer(cfg.GetConnectionString("SqlConnectionStringMISLogin")))
AddAuthenticationSubsystemServices():
    AddAutoMapper(typeof(LoginMappingProfile).Assembly)
    AddFluentValidationAutoValidation(); AddValidatorsFromAssemblyContaining<...>()
    AddHttpContextAccessor(); AddServiceeBus()
    AddScoped<ILoginRepository, LoginRepository>(); AddScoped<IModuleRepository, ModuleRepository>()
    AddScoped<IVerifyTokenAgainstCookieHandler, VerifyTokenAgainstCookieHandler>()
    // explicit bus registrations, one per handler:
    AddScoped<ICommandHandler<LoginCommand>, LoginHandler>()
    AddScoped<ICommandHandler<LogoutCommand>, LogoutHandler>()
    AddScoped<IQueryHandler<GetMyPermissionsQuery, MyPermissionsDTO>, GetMyPermissionsHandler>()
    AddScoped<IQueryHandler<VerifyModuleQuery, IEnumerable<Event>>, VerifyModuleHandler>()
    AddScoped<IQueryHandler<CheckCookieQuery, IEnumerable<Event>>, CheckCookieHandler>()
    AddScoped<IQueryHandler<GetUserMenuQuery, IEnumerable<Event>>, GetUserMenuHandler>()
AddJwtAuthentication(cfg) -> JwtBearer; token read from cookie; JSON 401; validate signing key only
AddCorsPolicy()           -> "AllowAngularApp": localhost:4200 + prod origins, AllowAnyHeader/Method, AllowCredentials
AddSwaggerDocumentation() -> SwaggerGen + "Bearer" security definition/requirement
```

### Controller pattern
```csharp
[Route("api/login")] [ApiController]
public class LoginController(IServiceBus bus) : ControllerBase
{
    [HttpPost("login")]  public async Task<IActionResult> loginMIS([FromForm] LoginCommand c) => Ok(await bus.SendCommandAsync(c));
    [Authorize][HttpPost("logout")] public async Task<IActionResult> Logout() => Ok(await bus.SendCommandAsync(new LogoutCommand()));
    [Authorize][HttpGet("my-permissions")]  // raw DTO query -> wrapped by EventFactory in controller
    [Authorize][HttpGet("verify-module/{moduleId}")] => SendQueryAsync<VerifyModuleQuery, IEnumerable<Event>>(new(moduleId))
    [HttpGet("check-cookie")]               => SendQueryAsync<CheckCookieQuery, IEnumerable<Event>>(new())
    [Authorize][HttpPost("get-menu")]       => SendQueryAsync<GetUserMenuQuery, IEnumerable<Event>>(new())
}
```

## 6. Authentication flow

### Login (`POST api/login/login`, form data `user_Name`, `Password`)
1. Validate `LoginCommand` with FluentValidation. Username and password are required, max 20 and 10 characters, no spaces. On failure → `ValidationError`.
2. `mapper.Map<Admin>(command)`.
3. `LoginRepository.GetLoginRepository(admin, new LogTable())`:
   - `dbo.ADMIN` lookup: `USER_NAME == x && UPASS == y`.
   - If the user is found, insert a `LogTable` row with `UserId`, `IPAddress = "{RemoteIp} {X-Forwarded-For}"` and `LoggedIn = Now`. Save, then set `user.LastLogId = logEntry.LogId`.
4. If the user is null → `LoginFailed`.
5. Build the JWT: HmacSha256 with `Jwt:Key`, 8-hour expiry, Issuer and Audience from config. Claims:
   `ClaimTypes.Name`, `"UserId"`, `"Modules"` (ModuleAccess), `"ViewAccess"`, `"LastLogId"`, `ClaimTypes.Email`.
6. Append cookie **`AuthTokenMIS`** with `HttpOnly`, `Secure`, `SameSite=None`, `Path=/`, `IsEssential`, and Expires equal to the token's. Set `Domain=".bdjobs.com"` only under `#if !DEBUG`.
7. Return `UserAuthenticated` with `{ Token, ExpiresIn, TokenStartTime, TokenEndTime, LogId }`.

### Authenticating requests
- `JwtBearerEvents.OnMessageReceived`: `context.Token = Request.Cookies["AuthTokenMIS"]`. The browser sends the cookie, so no Authorization header is needed.
- `OnChallenge` → `HandleResponse()`, then 401 JSON `{ success:false, statusCode:401, message, data:null }`.
- `TokenValidationParameters`: `ValidateIssuerSigningKey = true` with a symmetric key. Issuer and audience are **not** validated.

### Logout (`POST api/login/logout`)
Read the `LastLogId` claim. If it's missing or not an int → `InvalidSessionData`. If it's ≤0 or the repo can't find the row → `LogoutFailed`. Otherwise set `LogTable.LoggedOut = Now`, delete the `AuthTokenMIS` cookie, and return `UserLoggedOut`.

### Module-based authorization
- The `Modules` claim is a comma-wrapped list of IDs, e.g. `",25,47,32,50,"`.
- `VerifyModuleHandler`: `modules.Contains($",{moduleId},")` gives an exact-ID match. Success returns `VerifyModuleResultDTO{Status,Message,Details{User,Modules,HasAccess}}`. Otherwise it returns `ModuleAccessDenied`.
- Module IDs per feature live in config: `ModuleNameAndId: { Login: "000", HotJob: "57", ... }`. `"000"` means "no specific module".
- `GetUserMenuHandler`:
  1. Run `VerifyTokenAgainstCookie(user, cookie, "000")`. On failure → `SecurityMismatch`.
  2. Build `ModuleDTO{Id=UserId, ModuleName=Modules}` from the claims, validate it, and map it to `ModuleList`.
  3. The repo splits the IDs and queries `dbo.ModuleList WHERE Id IN (...) ORDER BY SerialNo, Id`.
  4. Return `ResourceRetrieved` with the list, or `ResourceNotFound`.
- `CheckCookieHandler`: cookie present → success, otherwise `CookieNotFound`.
- `GetMyPermissionsHandler`: returns `MyPermissionsDTO{Message="Hello User {id}", YourModules}`, taken from the claims.

### DB tables (auth DB = `bdjEmails` via `SqlConnectionStringMISLogin`)
- `dbo.ADMIN(ID, USER_NAME, UPASS, ModuleAccess, ViewAccess, AdminChk, SubLinkAccess, E_mail)`. `LastLogId` is `[NotMapped]`.
- `dbo.LogTable(LogId PK identity, UserId, IPAddress, LoggedIn, LoggedOut?)`
- `dbo.ModuleList(Id, ModuleName, URLAddress, SerialNo smallint, ModuleDesc?)`

## 7. Configuration (appsettings.json sections)
```json
{
  "Serilog": { "MinimumLevel": {...}, "WriteTo": [Console, File "Logs/<subsystem>-log-.txt" daily, 5MB, 31 files], "Enrich": ["FromLogContext"] },
  "ConnectionStrings": { "SqlConnectionStringMISLogin": "...", "SqlConnectionString": "...", ... },
  "SecuritySettings": { "AllowedIPs": [...], "AllowedCompanyIDs": [...] },
  "Jwt": { "Key": "...", "Issuer": "BdjobsMIS", "Audience": "BdjobsMIS_Users" },
  "ModuleNameAndId": { "Login": "000", "HotJob": "57", ... }
}
```

## 8. Testing pattern (`Tests/AuthenticationSubsystem/`)
- **Handler tests** (`LoginHandlersTests`):
  - Use the real `MapperConfiguration(c => c.AddProfile<LoginMappingProfile>())` and real validators.
  - Use `Mock<ILoginRepository>`/`Mock<IModuleRepository>`/`Mock<IVerifyTokenAgainstCookieHandler>`, plus `Mock<IHttpContextAccessor>` returning a `DefaultHttpContext`.
  - Build `IConfiguration` with `ConfigurationBuilder().AddInMemoryCollection(...)` and log with `NullLogger<T>.Instance`.
  - Helpers: `SetUser(params Claim[])`, `AssertSingleEvent(events, EventType, EventId)`, `Payload(ev)` (reads the `"data"` value) and `Prop<T>(obj, name)` (reflection on anonymous payloads).
  - Organized with `#region` per handler and named `Method_ShouldX_WhenY`. Uses `[Theory]` for invalid inputs.
- **Controller tests** (`LoginControllerTests`): mock `IServiceBus`, and assert that the result is `OkObjectResult` and that the correct command or query was sent.

## 9. Known weaknesses: don't copy these
1. Plaintext password comparison (`UPASS == input`). Hash it with BCrypt/Argon2 or ASP.NET `PasswordHasher`.
2. `LoginHandler` logs the **password** on failed login.
3. JWT key and DB credentials are committed in `appsettings.json`, and a fallback key is hard-coded in `AddJwtAuthentication`. Use user-secrets, env vars or Key Vault, and fail fast if the key is missing.
4. Issuer and audience aren't validated even though they're set. Enable `ValidateIssuer`/`ValidateAudience`/`ValidateLifetime`.
5. Login signs with `Encoding.ASCII` but validation uses `Encoding.UTF8`. That works only because the key is pure ASCII. Use one encoding.
6. `ServiceBus.SendCommandAsync` returns `null` when the handler is missing, while the query path throws. Make both throw.
7. `ExceptionMiddleware` has no constructor and isn't registered. Fix it and add `app.UseMiddleware<ExceptionMiddleware>()`.
8. Every response is HTTP 200, even failures. Consider mapping `EventType.Fail` to a proper status code.
9. `Admin.LastLogId` is typed `object`. Use `int?`.
10. `Console.WriteLine` and unused `dbName` variables in the repositories.
11. `ModuleRequestDTOValidators` is empty. The `int.Parse` on the Modules claim can throw.
12. Module check uses string `Contains` on the claim. VerifyTokenAgainstCookie's check lacks the comma-wrapping, so `"5"` matches `",57,"`.
13. Duplicate `ProjectReference` to Handlers in `AuthenticationSubsystem.APIs.csproj`.
14. Handler registration is manual per handler. Consider assembly scanning (Scrutor) or a source generator.
15. Misspellings are baked into names (`Extentions`, `Abstructions`, `AddServiceeBus`). Fix them in a new project.
