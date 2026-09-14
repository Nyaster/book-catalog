using Microsoft.Extensions.Logging;

namespace BookCatalog.Application.Common.Logging;

public static class LogEvents
{
    public static readonly EventId RequestCompleted = new EventId(1000, nameof(RequestCompleted));
    public static readonly EventId RequestAborted = new EventId(1001, nameof(RequestAborted));

    public static readonly EventId BookCreated = new EventId(2001, nameof(BookCreated));
    public static readonly EventId BookUpdated = new EventId(2002, nameof(BookUpdated));
    public static readonly EventId BookDeleted = new EventId(2003, nameof(BookDeleted));
    public static readonly EventId AuthorCreated = new EventId(2004, nameof(AuthorCreated));
    public static readonly EventId UserCreated = new EventId(2005, nameof(UserCreated));
    public static readonly EventId BookBorrowed = new EventId(2006, nameof(BookBorrowed));
    public static readonly EventId BookReturned = new EventId(2007, nameof(BookReturned));

    public static readonly EventId DatabaseReadRetry = new EventId(3001, nameof(DatabaseReadRetry));
    public static readonly EventId MigrationsStarted = new EventId(4001, nameof(MigrationsStarted));
    public static readonly EventId MigrationsCompleted = new EventId(4002, nameof(MigrationsCompleted));
    public static readonly EventId ApplicationFailed = new EventId(5001, nameof(ApplicationFailed));
}
