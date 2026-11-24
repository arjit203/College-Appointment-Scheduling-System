
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build

WORKDIR /src

COPY CampusConnect/CampusConnect.csproj CampusConnect/

WORKDIR /src/CampusConnect

RUN dotnet restore

COPY CampusConnect/. ./

RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:11.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:10000

EXPOSE 10000

ENTRYPOINT ["dotnet", "CampusConnect.dll"]
