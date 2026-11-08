# syntax=docker/dockerfile:1

# ---- build stage: SDK image, thrown away after publish ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy only the project files first so `dotnet restore` stays cached
# until a package reference changes (not on every source edit).
COPY src/OrderFlow.Domain/OrderFlow.Domain.csproj               src/OrderFlow.Domain/
COPY src/OrderFlow.Application/OrderFlow.Application.csproj     src/OrderFlow.Application/
COPY src/OrderFlow.Infrastructure/OrderFlow.Infrastructure.csproj src/OrderFlow.Infrastructure/
COPY src/OrderFlow.API/OrderFlow.API.csproj                     src/OrderFlow.API/
RUN dotnet restore src/OrderFlow.API/OrderFlow.API.csproj

COPY src/ src/
RUN dotnet publish src/OrderFlow.API/OrderFlow.API.csproj -c Release --no-restore -o /app

# ---- runtime stage: only the ASP.NET runtime plus the published output ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app .

# Port 8080 is unprivileged, so the process does not need root.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "OrderFlow.API.dll"]
