FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY SupportPilot.sln ./
COPY src/SupportPilot.Domain/SupportPilot.Domain.csproj src/SupportPilot.Domain/
COPY src/SupportPilot.Contracts/SupportPilot.Contracts.csproj src/SupportPilot.Contracts/
COPY src/SupportPilot.Application/SupportPilot.Application.csproj src/SupportPilot.Application/
COPY src/SupportPilot.Infrastructure/SupportPilot.Infrastructure.csproj src/SupportPilot.Infrastructure/
COPY src/SupportPilot.Notifications.Worker/SupportPilot.Notifications.Worker.csproj src/SupportPilot.Notifications.Worker/
RUN dotnet restore src/SupportPilot.Notifications.Worker/SupportPilot.Notifications.Worker.csproj

COPY . .
RUN dotnet publish src/SupportPilot.Notifications.Worker/SupportPilot.Notifications.Worker.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SupportPilot.Notifications.Worker.dll"]
