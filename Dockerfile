# Use Ubuntu 20.04 (Focal) — stable Mono packages available
FROM ubuntu:20.04

# Avoid interactive prompts during apt installs
ENV DEBIAN_FRONTEND=noninteractive

# Install Mono
RUN apt-get update && \
    apt-get install -y mono-complete ca-certificates curl && \
    rm -rf /var/lib/apt/lists/*

# Set working directory
WORKDIR /app

# Copy your source code (Api.cs must be here)
COPY . /app

# Compile Api.cs with required references
RUN mcs Api.cs \
    -r:System.Net.Http.dll \
    -r:System.Data.dll \
    -r:Mono.Data.Sqlite.dll \
    -out:FinanceApi.exe

# Render sets PORT; bind to 0.0.0.0:$PORT
ENV PORT=8080
EXPOSE 8080

# Run the app
CMD ["mono", "FinanceApi.exe"]
