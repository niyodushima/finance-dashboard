# Use Ubuntu 20.04 (Focal) which supports Mono
FROM ubuntu:20.04

# Avoid interactive prompts during apt installs
ENV DEBIAN_FRONTEND=noninteractive

# Install Mono from Ubuntu's official repos
RUN apt-get update && \
    apt-get install -y mono-complete && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY . /app

# Build the API with mcs
RUN mcs Api.cs -r:System.Net.Http.dll -out:FinanceApi.exe

# Render sets PORT; bind to 0.0.0.0:$PORT
ENV PORT=8080
EXPOSE 8080

CMD ["mono", "FinanceApi.exe"]
