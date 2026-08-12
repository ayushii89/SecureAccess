FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/SecureAccess.Api/SecureAccess.Api.csproj src/SecureAccess.Api/
RUN dotnet restore src/SecureAccess.Api/SecureAccess.Api.csproj

COPY src/SecureAccess.Api/ src/SecureAccess.Api/
RUN dotnet publish src/SecureAccess.Api/SecureAccess.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "SecureAccess.Api.dll"]
