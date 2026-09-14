# Book Catalog Platform — Design Note

> Week one decithions.

This week i build small CRUD api for our book. 
There few operation you can do.

POST /books -- Create a new book

Get /books -- Get paginated and filtered boooks(optional) books

Put /books/:Guid -- Update a book

Delete /books/:Guid -- Delete a book

# Structure
I Use simple four layer architecture.
Domain is the core of entire application.
After that is going infrastructure layer, which answer to question like how to store data.
Then is going to application layer, which answer to question like how process business logic.
Finally is going to presentation layer, which answer to question like how to present data to user.

# Decisions
For storage, i use in memory storage, simple dictionary, using locs for handling concurrency. For current application state this is simpliest and straitghtforward way to store data.

Application services use Microsoft’s `ILogger<T>` abstraction. Serilog supplies the logging implementation, so service logging calls remain independent of the output provider.

For error handling i use global exception handler, which currentl handle all exceptions.

# What to imporve
 - Add tests
 - Real DataBase would be better 
 - Maybe automapping?

# Hard things 
 - For this week, there really most of thing simple, but i found strange behavior when swagger don't get proper errors messages, and i need to write small workaround. This is maybe because i don't use swagger gen, and reuse openapi documentation.

> Week two decithions.

# How you split the layers and why exactly this way

There is simple standart layered architecutre, i explained this in week one. 
For more details. 
Domain -> Answer to simpliest entities, which represent our 'product', his layer is independend from any other layer.

Infrastructure -> Is abstraction of how we store our data. In future, if we need to change DB, we only need to change this layer.

Application -> Is abstraction of how we process business logic, if we need calculation, or do some logic, this is going here.

Presentation -> Is abstraction of how we present data to user. If we need to present data in different way, this changes to go here, even more. There may be more than one api layer in future.

# What your data access abstraction looks like and what it hides

I have `IBookRepository` which hides how we store data. This provides all crud operations, with pagination and filtering support.

Currently i implement it with in memory storage, in future i will replace it with real database. with no changes in application layer.


# How you decided what to test and what not to test

I am not very good at testing, this might be weakest from my part in this week.

Unit test are need to check small parts of code. In ideal world, we need to have cover all code, with all possible inputs.
But in real world, we test only intended behavior. (Legal inputs, illegal inputs, Expected errors). 
So it's why i write unit test for business logic, and trying to cover all intended behavior.

# What was painful to change from week 1, and what that tells you about your original design
Due to how i start write application from begginning there near zero changes in layers and arhictecture, only few changes in contracts to add pagination and filtering features.


> Week three decithions.

# Your data model and why it is shaped this way

Entity | Why it is shaped this way
--- | ---
Book | Represent borrowable book, and it's catalog details.
Author | Represents Author, Author can write multiple books. It's helping Books refereence same person.
User | Represents user, which can borrow book.
Loan | Records one borrowing event: which book, which user, when borrowed and when returned.

To able story borrowing history, loan is separate entity.
In future if book can be in more than one copy, we can simply extend this via this many to many relationship.
In example add new column `Copies` to `Book` entity which represent how many copies of book are available.
Or we make another entity `BookCopy` which represent one copy of book, so we can have borrowing story for each copy.(If this system represents real library).

# Which database you chose and why
I choose PostgreSQL. It's modern, fast, and very good document database. I have experience with it, and it's why i choose it.

# How much of your code had to change when you replaced in-memory storage, and what that says about week 2
Not so much, most of changes due to changes in contracts and introducting new entities and it's relationships.

# Where you expect performance to become a problem first
Mostly of the filtering and pagination. I use simple offset pagination, which in big amount of data is not good performance-wise.
If we need to support large amount of data, i would use token based pagination, which is more efficient but need a little more effort to implement.

# What each meaningful line of your Dockerfile does
- FROM -- Base image
- COPY -- Copy files from local machine to docker image
- RUN -- Run command in docker image
- EXPOSE -- Expose port to outside world
- ENTRYPOINT -- Entrypoint of docker image

## Week four — operation and verification

### Purpose and architecture

The platform stores books, authors, users, and loans. A user can borrow an available book and return it later. The platform keeps the loan history.

Week one introduced book operations and memory storage. Week two separated responsibilities and added tests. Week three introduced PostgreSQL, related records, and lending operations. Week four added tools to check and operate the service.

The four layers remain in place. The Domain layer contains entities and business rules. The Application layer coordinates business operations. The Infrastructure layer provides database access and transactions. The API layer receives HTTP requests and produces responses.

Health checks and request logs belong to the API layer. Database validation and read retries belong to the Infrastructure layer. This separation keeps operational code out of the entities.

### Integration tests

An integration test checks several parts of the system together. These tests send real HTTP requests to Kestrel, the application web server. They use PostgreSQL through Testcontainers. They do not use a database substitute.

Each test has a separate database and an HTTP server with an assigned port. Tests create their own records. They do not depend on records from another test or a fixed test order.

Test cleanup drops each database. The container fixture checks for remaining test databases before it removes the container. Startup failure also starts cleanup. These controls make repeated test runs possible.


### Health checks

Liveness means that the HTTP server can respond. Readiness means that the service can perform the selected database check. These conditions answer different operational questions.

| Endpoint | Check | Result |
| --- | --- | --- |
| `/health/live` | No database access | HTTP 200 when the server can respond |
| `/health/ready` | Read at most one book ID | HTTP 200 on success; HTTP 503 on failure |

An empty catalog passes the readiness check. A missing Books table or an unavailable database fails the check. Responses contain only `Healthy` or `Unhealthy`. They do not contain database error details.

The readiness check has a three-second cancellation deadline. Database cancellation and cleanup can require additional time. The check does not retry. This prevents each health request from adding retry traffic during an outage.

Readiness also checks whether application shutdown has started. It checks this condition before and after the database read. After the server stops accepting connections, a health request can fail to connect.

Health endpoints accept HTTP in Production. Other API endpoints retain HTTPS redirection. Tests check this difference.

A successful readiness check does not prove that all tables exist or that database writes will succeed.

### Structured logs

The service writes one JSON object for each console log entry. Application services use `ILogger<T>`. Serilog.AspNetCore supplies the logging implementation. This keeps application services independent of the console output format.

The response header `X-Trace-Id` contains the trace ID. Error responses use the same value in the ProblemDetails `traceId` field. A valid incoming `traceparent` header continues the caller's trace. Otherwise, the server creates a trace.

Business success logs occur after database changes complete. This prevents a success log from describing a transaction that later fails.

### Configuration validation

Database configuration uses a validated options object. The connection string must specify Host, Database, and Username. Missing or malformed connection strings stop application startup. Validation errors identify the configuration key without printing its value.

Startup failure produces a JSON log entry and exit code 1. This makes the failure visible to the process manager. A test checks the exit code and safe error output for an invalid connection string.


### Graceful shutdown

Graceful shutdown gives accepted requests time to finish. The application host allows 30 seconds. Docker Compose allows 40 seconds before forced termination.

### Retries and database outages

A retry repeats a failed operation. The service retries only database reads with temporary PostgreSQL failures. Each read has at most three attempts: the initial attempt and two retries.

The first retry delay is 250 to 350 milliseconds. The second delay is 500 to 600 milliseconds. Random delay reduces simultaneous retry traffic. Database connection and command timeouts add to the total request time.

The service does not retry writes or reads inside a transaction. It also does not retry cancellation or permanent database errors. Cancellation can stop a retry delay.

# Conclution

Week 4 was really hard for me. I had to research and learn many new things. During this time, I became quite exhausted and relied too much on AI this week. If I had had more time, I would have deepened my knowledge of this week’s topics and performed better.
