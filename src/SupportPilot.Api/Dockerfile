FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY SupportPilot.sln ./
COPY src/SupportPilot.Api/SupportPilot.Api.csproj src/SupportPilot.Api/
COPY src/SupportPilot.Application/SupportPilot.Application.csproj src/SupportPilot.Application/
COPY src/SupportPilot.Contracts/SupportPilot.Contracts.csproj src/SupportPilot.Contracts/
COPY src/SupportPilot.Domain/SupportPilot.Domain.csproj src/SupportPilot.Domain/
COPY src/SupportPilot.Infrastructure/SupportPilot.Infrastructure.csproj src/SupportPilot.Infrastructure/
RUN dotnet restore src/SupportPilot.Api/SupportPilot.Api.csproj

COPY . .
RUN dotnet publish src/SupportPilot.Api/SupportPilot.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "SupportPilot.Api.dll"]
