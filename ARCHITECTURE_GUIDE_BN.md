# বিডিজবস মডুলার মনোলিথ আর্কিটেকচার গাইড (Bdjobs Modular Monolith Architecture)

এই ডকুমেন্টেশনটি নতুন ও অভিজ্ঞ ডেভেলপারদের জন্য একটি পূর্ণাঙ্গ রেফারেন্স গাইড। এই আর্কিটেকচার প্যাটার্ন অনুসরণ করে যে কোনো সাবসিস্টেম বা এন্টারপ্রাইজ প্রজেক্ট স্বাধীন ও ত্রুটিমুক্তভাবে ডেভেলপ করা সম্ভব।

---

## ১. আর্কিটেকচারের মূল দর্শন (Core Philosophy)

1. **মডুলার মনোলিথ (Modular Monolith):** পুরো অ্যাপ্লিকেশনটি একটি রিপোজিটরিতে থাকবে, কিন্তু সাবসিস্টেমগুলো একে অপর থেকে সম্পূর্ণ স্বাধীন থাকবে।
2. **জিরো ডিরেক্ট ডিপেন্ডেন্সি (No Direct Coupling):** এক সাবসিস্টেমের কোড অন্য সাবসিস্টেমকে সরাসরি রেফারেন্স করবে না বা ডিরেক্ট মেথড কল করবে না।
3. **সার্ভিস বাস মেমোরি রাউটিং (In-Process ServiceBus):** সকল ক্রস-সাবসিস্টেম এবং কন্ট্রোলার কমিউনিকেশন ঘটবে `IServiceBus`-এর মাধ্যমে।
4. **থিন কন্ট্রোলার (Thin Controllers):** কন্ট্রোলারে কোনো বিজনেস লজিক, ডাটাবেজ কোয়েরি বা থার্ড-পার্টি এপিআই কল থাকবে না; কন্ট্রোলার কেবল রিকোয়েস্ট গ্রহণ করে সার্ভিস বাসে পাঠাবে এবং রেসপন্স রিটার্ন করবে।
5. **CQRS বিভাজন:** ডেটা পরিবর্তনের জন্য **Command** এবং ডেটা পড়ার জন্য **Query** সম্পূর্ণ আলাদাভাবে পরিচালিত হবে।

---

## ২. পাঁচ স্তরের সাবসিস্টেম কাঠামো (5-Layer Subsystem Structure)

প্রতিটি ব্যবসায়িক সাবসিস্টেম (যেমন: `AuthSubsystem`, `JobSubsystem`, `BillingSubsystem`) নিচের ৫টি প্রজেক্টে বিভক্ত থাকে:

```
[Business]Subsystem/
├── [Business]Subsystem.DTOs           (কমান্ড, কোয়েরি, রিকোয়েস্ট/রেসপন্স মডেল)
├── [Business]Subsystem.Aggregators    (EF কোর এন্টিটি, ডাটাবেজ মডেল, অটোম্যাপার প্রোফাইল)
├── [Business]Subsystem.Repositories   (DbContext, রিপোজিটরি ইন্টারফেস ও ইমপ্লিমেন্টেশন)
├── [Business]Subsystem.Handlers       (কমান্ড/কোয়েরি হ্যান্ডলার, ভ্যালিডেটর, বিজনেস লজিক)
└── [Business]Subsystem.APIs           (কন্ট্রোলার, প্রোগ্রাম.cs, ডিপেন্ডেন্সি ইনজেকশন)
```

### ডিপেন্ডেন্সি ফ্লো (Dependencies Rule):
```
APIs ──► Handlers ──► Repositories ──► Aggregators ──► DTOs
  │          │                                          ▲
  └──────────┴──────────────────────────────────────────┘
```
- **DTOs:** কোনো ইন্টারনাল লেয়ারকে চেনে না। কেবল `SharedSubsystem.DTOs` রেফারেন্স করে।
- **Aggregators:** ডাটাবেজ এন্টিটি ধারণ করে, রেফারেন্স করে `DTOs`।
- **Repositories:** ডাটাবেজ হ্যান্ডল করে, রেফারেন্স করে `Aggregators` ও `DTOs`।
- **Handlers:** সব মূল বিজনেস লজিক ধারণ করে, রেফারেন্স করে `Repositories`, `DTOs`, এবং `SharedSubsystem.Handlers`।
- **APIs:** এন্ট্রি পয়েন্ট (হোস্টিং লেয়ার), রেফারেন্স করে `Handlers`, `DTOs` এবং `ServiceBus.Handlers`।

---

## ৩. শেয়ার্ড লেয়ার (Shared Infrastructure)

সলিউশনে একটি কমন `Shared/` ফোল্ডার থাকে যা সব সাবসিস্টেম শেয়ার করে:

```
Shared/
├── SharedSubsystem.DTOs/
│   └── Abstractions/
│       ├── ICommand.cs              (ICommand, ICommand<TResult>)
│       ├── IQuery.cs                (IQuery<TResult>)
│       └── IEvent.cs                (IEvent ইভেন্ট মার্কার)
│
└── SharedSubsystem.Handlers/
    ├── Abstructions/
    │   ├── ICommandHandler.cs       (ICommandHandler<TCommand>)
    │   └── IQueryHandler.cs         (IQueryHandler<TQuery, TResult>)
    ├── Abstractions/
    │   └── IMessaging.cs            (IServiceBus ইন্টারফেস)
    └── Events/
        └── Event.cs                 (Event, EventType, EventId, EventFactory)

ServiceBus.Handlers/                (আলাদা ক্লাস লাইব্রেরি - Shared এর বাইরে)
├── ServiceBus.cs                    (মূল সার্ভিস বাস রাউটার)
└── ServiceCollectionExtensions.cs
```

---

## ৪. সার্ভিস বাস (ServiceBus) ব্যবহারের নিয়ম

সার্ভিস বাসে দুটি প্রধান দায়িত্ব রয়েছে: **কমান্ড পাঠানো** এবং **কোয়েরি এক্সিকিউট করা**।

### সার্ভিস বাস ইন্টারফেস (`IServiceBus`):
```csharp
public interface IServiceBus
{
    // ১. কমান্ড পাঠানো (ডেটা পরিবর্তন/অ্যাকশন)
    Task<TResult> SendCommandAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default);
    Task<IEnumerable<Event>> SendCommandAsync<TCommand>(TCommand command) where TCommand : ICommand;

    // ২. কোয়েরি পাঠানো (ডেটা রিড করা)
    Task<TResult> SendQueryAsync<TQuery, TResult>(TQuery query) where TQuery : IQuery<TResult>;

    // ৩. ইভেন্ট ব্রডকাস্ট (অন্য সাবসিস্টেমের জন্য)
    Task BroadcastEventAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IEvent;
}
```

---

## ৫. ধাপে ধাপে নতুন ফিচার তৈরির নিয়ম (Developer Walkthrough)

ধরা যাক, আমরা ব্যবহারকারীর প্রোফাইল দেখার ফিচার (`GetUserProfile`) এবং ব্যবহারকারীর স্ট্যাটাস আপডেট করার ফিচার (`UpdateUserStatus`) তৈরি করব।

### ধাপ ১: DTOs ডিফাইন করা (`[Business]Subsystem.DTOs`)

```csharp
using SharedSubsystem.DTOs.Abstractions;

namespace UserSubsystem.DTOs;

// কোয়েরি: ডেটা পড়ার জন্য (রিটার্ন টাইপ: UserProfileDto)
public record GetUserProfileQuery(Guid UserId) : IQuery<UserProfileDto>;

// কমান্ড: ডেটা পরিবর্তনের জন্য (রিটার্ন টাইপ: bool অথবা AuthResult<T>)
public record UpdateUserStatusCommand(Guid UserId, bool IsActive) : ICommand<bool>;

// রেসপন্স DTO
public class UserProfileDto
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
```

---

### ধাপ ২: হ্যান্ডলার তৈরি করা (`[Business]Subsystem.Handlers`)

#### ২.১ কোয়েরি হ্যান্ডলার (Query Handler):
```csharp
using SharedSubsystem.Handlers.Abstructions;
using UserSubsystem.DTOs;
using UserSubsystem.Repositories;

namespace UserSubsystem.Handlers;

public class GetUserProfileQueryHandler : IQueryHandler<GetUserProfileQuery, UserProfileDto>
{
    private readonly IUserRepository _userRepo;

    public GetUserProfileQueryHandler(IUserRepository userRepo)
    {
        _userRepo = userRepo;
    }

    public async Task<UserProfileDto> HandleAsync(GetUserProfileQuery query)
    {
        var user = await _userRepo.GetByIdAsync(query.UserId);
        if (user == null)
            throw new KeyNotFoundException($"User with ID {query.UserId} not found");

        return new UserProfileDto
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email
        };
    }
}
```

#### ২.২ কমান্ড হ্যান্ডলার (Command Handler):
```csharp
using SharedSubsystem.Handlers.Abstractions;
using UserSubsystem.DTOs;
using UserSubsystem.Repositories;

namespace UserSubsystem.Handlers;

public class UpdateUserStatusCommandHandler : ICommandHandler<UpdateUserStatusCommand, bool>
{
    private readonly IUserRepository _userRepo;

    public UpdateUserStatusCommandHandler(IUserRepository userRepo)
    {
        _userRepo = userRepo;
    }

    public async Task<bool> HandleAsync(UpdateUserStatusCommand command, CancellationToken cancellationToken = default)
    {
        return await _userRepo.UpdateStatusAsync(command.UserId, command.IsActive);
    }
}
```

---

### ধাপ ৩: কন্ট্রোলার তৈরি করা (`[Business]Subsystem.APIs`)

কন্ট্রোলার হবে স্লিম ও পরিষ্কার। এটি **Primary Constructor** ব্যবহার করবে এবং শুধু `IServiceBus` ইনজেক্ট করবে।

```csharp
using Microsoft.AspNetCore.Mvc;
using SharedSubsystem.Handlers.Abstractions;
using UserSubsystem.DTOs;

namespace UserSubsystem.APIs.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController(IServiceBus serviceBus) : ControllerBase
{
    // কোয়েরি কল করার নিয়ম: SendQueryAsync
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetProfile(Guid id)
    {
        var result = await serviceBus.SendQueryAsync<GetUserProfileQuery, UserProfileDto>(
            new GetUserProfileQuery(id));
        return Ok(result);
    }

    // কমান্ড কল করার নিয়ম: SendCommandAsync
    [HttpPut("status")]
    public async Task<IActionResult> UpdateStatus([FromBody] UpdateUserStatusCommand command)
    {
        var isSuccess = await serviceBus.SendCommandAsync(command);
        return isSuccess ? Ok(new { message = "Status updated successfully" }) : BadRequest();
    }
}
```

---

### ধাপ ৪: ডিপেন্ডেন্সি ইনজেকশন কনফিগারেশন (`ServiceCollectionExtensions.cs`)

সবশেষে হ্যান্ডলারগুলোকে DI কন্টেইনারে রেজিস্ট্রেশন করতে হবে:

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddUserSubsystemServices(this IServiceCollection services)
    {
        // রিপোজিটরি রেজিস্ট্রেশন
        services.AddScoped<IUserRepository, UserRepository>();

        // কোয়েরি হ্যান্ডলার রেজিস্ট্রেশন
        services.AddScoped<IQueryHandler<GetUserProfileQuery, UserProfileDto>, GetUserProfileQueryHandler>();

        // কমান্ড হ্যান্ডলার রেজিস্ট্রেশন
        services.AddScoped<ICommandHandler<UpdateUserStatusCommand, bool>, UpdateUserStatusCommandHandler>();

        return services;
    }
}
```

---

## ৬. গুরুত্বপূর্ণ নিয়মাবলী (Best Practices & Dos/Don'ts)

| বিষয় | যা করবেন (DO) | যা করবেন না (DON'T) |
|---|---|---|
| **কন্ট্রোলার** | শুধু `IServiceBus` ইনজেক্ট করবেন। | কোনো DbContext, Repository বা HttpClient ইনজেক্ট করবেন না। |
| **কোয়েরি (Query)** | ডেটা রিড করার জন্য `IQuery<TResult>` এবং `SendQueryAsync` ব্যবহার করবেন। | কোয়েরির জন্য `SendCommandAsync` ব্যবহার করবেন না। |
| **কমান্ড (Command)** | ডেটাবেজে কোনো পরিবর্তন (Insert/Update/Delete) হলে `ICommand` ও `SendCommandAsync` ব্যবহার করবেন। | কোয়েরি দিয়ে ডেটাবেজ আপডেট করবেন না। |
| **সাবসিস্টেম লিংক** | কোনো সাবসিস্টেমের ভেতরের ক্লাস অন্য সাবসিস্টেমে রেফারেন্স করবেন না। | `AuthSubsystem` থেকে `DataSyncSubsystem`-এর কোনো ক্লাস ডিরেক্ট কল করবেন না। |
| **কমন টাইপ** | কোনো DTO একাধিক সাবসিস্টেমের লাগলে তা `SharedSubsystem.DTOs`-এ রাখবেন। | এক সাবসিস্টেম থেকে আরেক সাবসিস্টেমের DTO রেফারেন্স করবেন না। |

---

## ৭. উপসংহার

এই আর্কিটেকচার মেনে চললে যেকোনো প্রজেক্ট:
- **হাইলি স্কেলেবল (Scalable):** ভবিষ্যতে যেকোনো সাবসিস্টেমকে আলাদা মাইক্রোসার্ভিসে রূপান্তর করা সহজ।
- **টেস্টেবল (Testable):** কন্ট্রোলার ও হ্যান্ডলারগুলোকে আলাদাভাবে খুব সহজে ইউনিট টেস্ট করা যায়।
- **ক্লিন ও মেইনটেইনেবল (Maintainable):** টিমের যেকোনো নতুন ডেভেলপার কোনো কনফিউশন ছাড়াই নতুন ফিচার যোগ করতে পারবেন।
