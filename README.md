# Book Catalog Platform

A .NET 10 API with PostgreSQL. Manage books, authors, and users. Borrow available books, return them, and view loan history.

## Run with Docker

Install Docker with Compose support and start Docker. Run this command from the repository root:

```sh
docker compose up --build -d
```

This starts the API and database. Database migrations run automatically.

- API: http://localhost:8080/api/books
- Swagger: http://localhost:8080/swagger

To stop the application:

```sh
docker compose down
```

The database volume keeps your data.

## Run tests

Install the .NET 10 SDK and start Docker with Linux container support. Allow package and container image downloads.

```sh
dotnet test BookCatalog.sln
```

Tests start their own API servers and PostgreSQL containers. They remove test databases and containers after execution.

## Configuration

For Docker Compose, set these environment variables before startup:

| Variable | Default |
| --- | --- |
| `API_PORT` | `8080` |
| `POSTGRES_DB` | `bookcatalog` |
| `POSTGRES_USER` | `bookcatalog` |
| `POSTGRES_PASSWORD` | `bookcatalog_dev_only` |

Example for a different API port:

```sh
API_PORT=8081 docker compose up --build -d
```

The supplied credentials are for local development. Database variables initialize a new database volume; they do not update existing database credentials.

### Run without Docker Compose

Install the .NET 10 SDK and supply a running PostgreSQL database. In a Linux or macOS terminal:

```sh
export Database__ConnectionString='Host=localhost;Port=5432;Database=bookcatalog;Username=bookcatalog;Password=YOUR_PASSWORD'
export Database__ApplyMigrationsOnStartup=true
dotnet run --project src/BookCatalog.Api --launch-profile http
```

Swagger is at http://localhost:5113/swagger. The Compose database does not publish a port for this setup.

Environment variables override application settings. The connection string must specify Host, Database, and Username. Set `Database__ApplyMigrationsOnStartup=false` if migrations are applied separately.

## Check the service

- `/health/live`: the server can respond.
- `/health/ready`: the service can read the catalog database.

To view JSON logs:

```sh
docker compose logs --no-log-prefix -f api
```

## More information

- [Design Note](docs/Design-Note.md)

