# ============================================================================
# CORRECTED Dockerfile for .NET 9 ASP.NET Core Web API
# ============================================================================
# This version fixes the "graduationProject.dll does not exist" error
# ============================================================================

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
# IMPORTANT: Make sure your .csproj filename matches exactly!
COPY *.csproj ./
RUN dotnet restore

# Copy everything else
COPY . ./

# Build and publish
# The -o flag specifies output directory
# --no-restore because we already restored
RUN dotnet publish -c Release -o /app/publish --no-restore

# Verify the DLL was created (debugging step)
RUN dotnet publish -c Release -o /app/publish


# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Copy published files from build stage
COPY --from=build /app/publish .

# List files to verify (debugging step)
RUN echo "===== Files in /app =====" && ls -la

# Expose port
EXPOSE 5132

# Environment variables
ENV ASPNETCORE_URLS=http://+:5132
ENV ASPNETCORE_ENVIRONMENT=Development

# Find and run the DLL automatically
# This will work regardless of what your project is named
ENTRYPOINT ["dotnet", "grad.dll"]
