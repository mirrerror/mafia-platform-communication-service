# Stage 1: Base image for the final runtime environment
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

# Create a non-root user and group for better security
RUN addgroup --system --gid 1000 appgroup && \
    adduser --system --uid 1000 --ingroup appgroup appuser
USER appuser

# Stage 2: Build the application
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files and restore dependencies first to leverage Docker layer caching
COPY ["MafiaCommunicationService/MafiaCommunicationService.csproj", "MafiaCommunicationService/"]
COPY ["MafiaCommunicationService.Tests/MafiaCommunicationService.Tests.csproj", "MafiaCommunicationService.Tests/"]
RUN dotnet restore "MafiaCommunicationService/MafiaCommunicationService.csproj"

# Copy the rest of the source code
COPY . .

# Stage 3: Run the unit tests
FROM build AS test
WORKDIR "/src/MafiaCommunicationService.Tests"
RUN dotnet test --no-restore -c $BUILD_CONFIGURATION

# Stage 4: Publish the application, using the result of the test stage
# This ensures that publishing only happens if the tests pass.
FROM test AS publish
WORKDIR "/src/MafiaCommunicationService"
RUN dotnet publish "MafiaCommunicationService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Stage 5: Create the final, lean image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MafiaCommunicationService.dll"]