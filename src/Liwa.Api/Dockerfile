# Build context: repository root.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Liwa.slnx ./
COPY src/Liwa.Domain/Liwa.Domain.csproj src/Liwa.Domain/
COPY src/Liwa.Application/Liwa.Application.csproj src/Liwa.Application/
COPY src/Liwa.Infrastructure/Liwa.Infrastructure.csproj src/Liwa.Infrastructure/
COPY src/Liwa.Api/Liwa.Api.csproj src/Liwa.Api/
RUN dotnet restore src/Liwa.Api/Liwa.Api.csproj
COPY src/ src/
RUN dotnet publish src/Liwa.Api/Liwa.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# curl for the container health check only.
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Liwa.Api.dll"]
