# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ExpenseTracker.csproj ./
RUN dotnet restore ExpenseTracker.csproj

# Copy the rest of the source code and build the application
COPY . .
RUN dotnet build ExpenseTracker.csproj -c Release -o /app/build

# Publish Stage
FROM build AS publish
RUN dotnet publish ExpenseTracker.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Expose port 8080 (standard ASP.NET Core 8.0 container port)
EXPOSE 8080

# Environment variables
ENV ASPNETCORE_ENVIRONMENT=Production

# Start command: dynamically bind to the $PORT env variable injected by Render (defaulting to 8080)
ENTRYPOINT ["sh", "-c", "export ASPNETCORE_URLS=\"http://*:${PORT:-8080}\" && dotnet ExpenseTracker.dll"]
