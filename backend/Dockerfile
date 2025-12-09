FROM mono:latest

WORKDIR /app

# Copy backend sources
COPY . .

# Build the API (compile Program.cs + Api.cs)
RUN mcs -r:Microsoft.Data.Sqlite.dll -out:FinanceApi.exe Program.cs Api.cs || true

# Run the API
CMD ["mono", "FinanceApi.exe"]
