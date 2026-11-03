using System.ComponentModel;

namespace SharedSubsystem.Handlers.Events;

public enum EventType
{
    Success = 1,
    Fail = 2
}

public enum EventId
{
    // 1000s: CRUD
    [Description("An event indicating the successful creation of a new resource.")]
    ResourceCreated = 1000,

    [Description("An event indicating the successful update of an existing resource.")]
    ResourceUpdated = 1001,

    [Description("An event indicating the successful deletion of a resource.")]
    ResourceDeleted = 1002,

    [Description("An event indicating the successful retrieval of a resource.")]
    ResourceRetrieved = 1003,

    [Description("Resource Not Found")]
    ResourceNotFound = 1005,

    [Description("User Input Error")]
    UserInputError = 1006,

    // 2000s: processing

    // 3000s: auth and validation
    [Description("User Authenticated")]
    UserAuthenticated = 3000,

    [Description("User Authorization Failed")]
    UserAuthorizationFailed = 3001,

    [Description("User Logged Out")]
    UserLoggedOut = 3002,

    [Description("Validation Error")]
    ValidationError = 3003,

    [Description("Security Mismatch")]
    SecurityMismatch = 3011,

    [Description("Login Failed")]
    LoginFailed = 3012,

    [Description("Cookie Not Found")]
    CookieNotFound = 3013,

    [Description("Invalid Session Data")]
    InvalidSessionData = 3014,

    [Description("Logout Failed")]
    LogoutFailed = 3015,

    [Description("Module Access Denied")]
    ModuleAccessDenied = 3016,

    // 9000s: system
    [Description("Internal Server Error")]
    InternalServerError = 9000,

    [Description("Unauthorized Access")]
    UnauthorizedAccess = 403
}

public class Event
{
    public EventType EventType { get; set; }
    public List<KeyValuePair<string, object>>? EventData { get; set; }
    public EventId EventId { get; set; }
}

public static class EventFactory
{
    public static IEnumerable<Event> CreateSuccessEvent(EventId id, object? data = null)
    {
        var eventData = new List<KeyValuePair<string, object>>();
        if (data != null)
        {
            eventData.Add(new KeyValuePair<string, object>("data", data));
        }

        return new List<Event>
        {
            new Event
            {
                EventType = EventType.Success,
                EventId = id,
                EventData = eventData
            }
        };
    }

    public static IEnumerable<Event> CreateFailureEvent(EventId id, string? customMessage = null)
    {
        var message = customMessage ?? GetEnumDescription(id);
        return new List<Event>
        {
            new Event
            {
                EventType = EventType.Fail,
                EventId = id,
                EventData = new List<KeyValuePair<string, object>>
                {
                    new KeyValuePair<string, object>("message", message)
                }
            }
        };
    }

    private static string GetEnumDescription(Enum value)
    {
        var field = value.GetType().GetField(value.ToString());
        if (field != null && Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) is DescriptionAttribute attribute)
        {
            return attribute.Description;
        }
        return value.ToString();
    }
}
