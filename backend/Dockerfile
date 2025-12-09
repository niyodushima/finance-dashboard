# Build stage
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /app

# Copy csproj and restore first for better caching
COPY Finance.csproj ./
RUN dotnet restore

# Copy source
COPY . .

# Publish a self-contained app (framework-dependent)
RUN dotnet publish -c Release -o /out

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:6.0
WORKDIR /app

# Copy published output
COPY --from=build /out ./

# Expose the port (Render uses PORT env var; app binds to it in code)
ENV PORT=8080
# Optional: if you add a persistent disk later
# ENV DB_PATH=/data/finance.db

# Run the app
CMD ["dotnet", "Finance.dll"]
