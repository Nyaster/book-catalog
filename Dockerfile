FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/BookCatalog.Api/BookCatalog.Api.csproj src/BookCatalog.Api/
COPY src/BookCatalog.Application/BookCatalog.Application.csproj src/BookCatalog.Application/
COPY src/BookCatalog.Domain/BookCatalog.Domain.csproj src/BookCatalog.Domain/
COPY src/BookCatalog.Infrastructure/BookCatalog.Infrastructure.csproj src/BookCatalog.Infrastructure/

RUN dotnet restore src/BookCatalog.Api/BookCatalog.Api.csproj

COPY src/ src/

RUN dotnet publish src/BookCatalog.Api/BookCatalog.Api.csproj \
      --configuration Release \
      --output /app/publish \
      --no-restore 

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

COPY --from=build /app/publish/ ./

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

USER app

ENTRYPOINT ["dotnet", "BookCatalog.Api.dll"]