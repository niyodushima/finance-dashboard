FROM ubuntu:22.04

# Install Mono
RUN apt-get update && \
    apt-get install -y gnupg ca-certificates curl && \
    apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys A2E3EF7B || true && \
    echo "deb https://download.mono-project.com/repo/ubuntu stable-jammy main" | tee /etc/apt/sources.list.d/mono-official-stable.list && \
    apt-get update && \
    apt-get install -y mono-complete && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY . /app

# Build the API
RUN mcs Api.cs -r:System.Net.Http.dll -out:FinanceApi.exe

# Render sets PORT; bind to 0.0.0.0:$PORT
ENV PORT=8080
EXPOSE 8080

CMD ["mono", "FinanceApi.exe"]

