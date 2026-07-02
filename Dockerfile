# Stage 1: Build and compile the assets
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["SecureNotesVault.Api/SecureNotesVault.Api.csproj", "SecureNotesVault.Api/"]
COPY ["SecureNotesVault.Core/SecureNotesVault.Core.csproj", "SecureNotesVault.Core/"]
RUN dotnet restore "SecureNotesVault.Api/SecureNotesVault.Api.csproj"

# Copy remaining source code and publish binaries
COPY . .
WORKDIR "/src/SecureNotesVault.Api"
RUN dotnet publish "SecureNotesVault.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Serve the application from a hardened runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Expose standard container port
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development

ENTRYPOINT ["dotnet", "SecureNotesVault.Api.dll"]
