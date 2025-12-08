using System;
using System.Text;
using System.Net;
using System.IO;
using System.Web;
using Mono.Data.Sqlite;
using System.Data;
using System.Globalization;

class ApiServer {
    const string ConnString = "URI=file:finance.db";
    const string Prefix = "https://finance-api.onrender.com";

    static void Main() {
        EnsureDatabase();
        var listener = new HttpListener();
        listener.Prefixes.Add(Prefix);
        listener.Start();
        Console.WriteLine("API running at " + Prefix);

        while (true) {
            var ctx = listener.GetContext();
            try {
                HandleRequest(ctx);
            } catch (Exception ex) {
                Console.WriteLine("Error: " + ex.Message);
                WriteJson(ctx, 500, "{\"error\":\"internal server error\"}");
            }
        }
    }

    static void HandleRequest(HttpListenerContext ctx) {
        // Basic CORS
        ctx.Response.AddHeader("Access-Control-Allow-Origin", "*");
        ctx.Response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        ctx.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type");

        if (ctx.Request.HttpMethod == "OPTIONS") {
            ctx.Response.StatusCode = 200;
            ctx.Response.Close();
            return;
        }

        var path = ctx.Request.Url.AbsolutePath.TrimEnd('/');
        var method = ctx.Request.HttpMethod.ToUpperInvariant();

        if (path == "" || path == "/") {
            WriteJson(ctx, 200, "{\"message\":\"Personal Finance API\",\"endpoints\":[\"/login\",\"/customers\",\"/income\",\"/expenses\",\"/summary\"]}");
            return;
        }

        if (path == "/login" && method == "POST") {
            var body = ReadBody(ctx);
            var form = ParseForm(body);
            var username = Get(form, "username");
            var password = Get(form, "password");
            bool ok = ValidateLogin(username, password);
            WriteJson(ctx, 200, "{\"success\":" + (ok ? "true" : "false") + "}");
            return;
        }

        if (path == "/customers") {
            if (method == "GET") {
                WriteJson(ctx, 200, ListCustomersJson());
                return;
            } else if (method == "POST") {
                var body = ReadBody(ctx);
                var form = ParseForm(body);
                var name = Get(form, "name");
                if (string.IsNullOrWhiteSpace(name)) {
                    WriteJson(ctx, 400, "{\"error\":\"name is required\"}");
                    return;
                }
                long id = InsertCustomer(name);
                WriteJson(ctx, 200, "{\"id\":" + id + ",\"name\":\"" + JsonEscape(name) + "\"}");
                return;
            }
        }

        if (path == "/income" && method == "POST") {
            var body = ReadBody(ctx);
            var form = ParseForm(body);
            if (!int.TryParse(Get(form, "customerId"), out int customerId) || customerId <= 0) {
                WriteJson(ctx, 400, "{\"error\":\"valid customerId required\"}");
                return;
            }
            if (!double.TryParse(Get(form, "amount"), NumberStyles.Float, CultureInfo.InvariantCulture, out double amount) || amount <= 0) {
                WriteJson(ctx, 400, "{\"error\":\"valid amount required\"}");
                return;
            }
            var desc = Get(form, "description");
            if (!CustomerExists(customerId)) {
                WriteJson(ctx, 404, "{\"error\":\"customer not found\"}");
                return;
            }
            InsertIncome(customerId, amount, desc);
            WriteJson(ctx, 200, "{\"message\":\"income recorded\",\"customerId\":" + customerId + ",\"amount\":" + amount.ToString(CultureInfo.InvariantCulture) + "}");
            return;
        }

        if (path == "/expenses" && method == "POST") {
            var body = ReadBody(ctx);
            var form = ParseForm(body);
            if (!int.TryParse(Get(form, "customerId"), out int customerId) || customerId <= 0) {
                WriteJson(ctx, 400, "{\"error\":\"valid customerId required\"}");
                return;
            }
            if (!double.TryParse(Get(form, "amount"), NumberStyles.Float, CultureInfo.InvariantCulture, out double amount) || amount <= 0) {
                WriteJson(ctx, 400, "{\"error\":\"valid amount required\"}");
                return;
            }
            var desc = Get(form, "description");
            if (!CustomerExists(customerId)) {
                WriteJson(ctx, 404, "{\"error\":\"customer not found\"}");
                return;
            }
            InsertExpense(customerId, amount, desc);
            WriteJson(ctx, 200, "{\"message\":\"expense recorded\",\"customerId\":" + customerId + ",\"amount\":" + amount.ToString(CultureInfo.InvariantCulture) + "}");
            return;
        }

        if (path == "/summary" && method == "GET") {
            WriteJson(ctx, 200, SummaryJson());
            return;
        }

        WriteJson(ctx, 404, "{\"error\":\"not found\"}");
    }

    // Database helpers
    static void EnsureDatabase() {
        using (var conn = new SqliteConnection(ConnString)) {
            conn.Open();

            string users = @"CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Username TEXT UNIQUE NOT NULL,
                Password TEXT NOT NULL
            );";
            string customers = @"CREATE TABLE IF NOT EXISTS Customers (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL
            );";
            string income = @"CREATE TABLE IF NOT EXISTS Income (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CustomerId INTEGER,
                Amount REAL NOT NULL,
                Date TEXT DEFAULT CURRENT_TIMESTAMP,
                Description TEXT,
                FOREIGN KEY(CustomerId) REFERENCES Customers(Id)
            );";
            string expenses = @"CREATE TABLE IF NOT EXISTS Expenses (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CustomerId INTEGER,
                Amount REAL NOT NULL,
                Date TEXT DEFAULT CURRENT_TIMESTAMP,
                Description TEXT,
                FOREIGN KEY(CustomerId) REFERENCES Customers(Id)
            );";

            Exec(conn, users);
            Exec(conn, customers);
            Exec(conn, income);
            Exec(conn, expenses);

            var cmd = new SqliteCommand("SELECT COUNT(*) FROM Users WHERE Username='admin';", conn);
            long count = Convert.ToInt64(cmd.ExecuteScalar() ?? 0);
            if (count == 0) {
                Exec(conn, "INSERT INTO Users (Username, Password) VALUES ('admin','Admin@123');");
                Console.WriteLine("Seeded admin / Admin@123");
            }
        }
    }

    static bool ValidateLogin(string username, string password) {
        using (var conn = new SqliteConnection(ConnString)) {
            conn.Open();
            var cmd = new SqliteCommand("SELECT COUNT(*) FROM Users WHERE Username=@u AND Password=@p;", conn);
            cmd.Parameters.AddWithValue("@u", username);
            cmd.Parameters.AddWithValue("@p", password);
            long count = Convert.ToInt64(cmd.ExecuteScalar() ?? 0);
            return count > 0;
        }
    }

    static long InsertCustomer(string name) {
        using (var conn = new SqliteConnection(ConnString)) {
            conn.Open();
            var cmd = new SqliteCommand("INSERT INTO Customers (Name) VALUES (@n);", conn);
            cmd.Parameters.AddWithValue("@n", name);
            cmd.ExecuteNonQuery();
            var idCmd = new SqliteCommand("SELECT last_insert_rowid();", conn);
            return Convert.ToInt64(idCmd.ExecuteScalar() ?? 0);
        }
    }

    static bool CustomerExists(int id) {
        using (var conn = new SqliteConnection(ConnString)) {
            conn.Open();
            var cmd = new SqliteCommand("SELECT COUNT(*) FROM Customers WHERE Id=@id;", conn);
            cmd.Parameters.AddWithValue("@id", id);
            return Convert.ToInt64(cmd.ExecuteScalar() ?? 0) > 0;
        }
    }

    static void InsertIncome(int customerId, double amount, string desc) {
        using (var conn = new SqliteConnection(ConnString)) {
            conn.Open();
            var cmd = new SqliteCommand("INSERT INTO Income (CustomerId, Amount, Description) VALUES (@c,@a,@d);", conn);
            cmd.Parameters.AddWithValue("@c", customerId);
            cmd.Parameters.AddWithValue("@a", amount);
            cmd.Parameters.AddWithValue("@d", string.IsNullOrEmpty(desc) ? (object)DBNull.Value : desc);
            cmd.ExecuteNonQuery();
        }
    }

    static void InsertExpense(int customerId, double amount, string desc) {
        using (var conn = new SqliteConnection(ConnString)) {
            conn.Open();
            var cmd = new SqliteCommand("INSERT INTO Expenses (CustomerId, Amount, Description) VALUES (@c,@a,@d);", conn);
            cmd.Parameters.AddWithValue("@c", customerId);
            cmd.Parameters.AddWithValue("@a", amount);
            cmd.Parameters.AddWithValue("@d", string.IsNullOrEmpty(desc) ? (object)DBNull.Value : desc);
            cmd.ExecuteNonQuery();
        }
    }

    static string ListCustomersJson() {
        using (var conn = new SqliteConnection(ConnString)) {
            conn.Open();
            var cmd = new SqliteCommand("SELECT Id, Name FROM Customers ORDER BY Id ASC;", conn);
            var reader = cmd.ExecuteReader();
            var sb = new StringBuilder();
            sb.Append("{\"customers\":[");
            bool first = true;
            while (reader.Read()) {
                if (!first) sb.Append(",");
                first = false;
                int id = reader.GetInt32(0);
                string name = reader.GetString(1);
                sb.Append("{\"id\":").Append(id).Append(",\"name\":\"").Append(JsonEscape(name)).Append("\"}");
            }
            sb.Append("]}");
            return sb.ToString();
        }
    }

    static string SummaryJson() {
        using (var conn = new SqliteConnection(ConnString)) {
            conn.Open();
            string sql = @"
            SELECT c.Id, c.Name,
                   IFNULL((SELECT SUM(Amount) FROM Income WHERE CustomerId=c.Id),0) AS income,
                   IFNULL((SELECT SUM(Amount) FROM Expenses WHERE CustomerId=c.Id),0) AS expense
            FROM Customers c
            ORDER BY c.Name ASC;";
            var cmd = new SqliteCommand(sql, conn);
            var reader = cmd.ExecuteReader();
            var sb = new StringBuilder();
            sb.Append("{\"summary\":[");
            bool first = true;
            while (reader.Read()) {
                if (!first) sb.Append(",");
                first = false;
                int id = reader.GetInt32(0);
                string name = reader.GetString(1);
                double inc = reader.IsDBNull(2) ? 0.0 : reader.GetDouble(2);
                double exp = reader.IsDBNull(3) ? 0.0 : reader.GetDouble(3);
                double bal = inc - exp;
                sb.Append("{\"id\":").Append(id)
                  .Append(",\"name\":\"").Append(JsonEscape(name)).Append("\"")
                  .Append(",\"income\":").Append(inc.ToString(CultureInfo.InvariantCulture))
                  .Append(",\"expense\":").Append(exp.ToString(CultureInfo.InvariantCulture))
                  .Append(",\"balance\":").Append(bal.ToString(CultureInfo.InvariantCulture))
                  .Append("}");
            }
            sb.Append("]}");
            return sb.ToString();
        }
    }

    // Utilities
    static void Exec(SqliteConnection conn, string sql) {
        using (var cmd = new SqliteCommand(sql, conn)) cmd.ExecuteNonQuery();
    }

    static string ReadBody(HttpListenerContext ctx) {
        using (var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding)) {
            return reader.ReadToEnd();
        }
    }

    static System.Collections.Generic.Dictionary<string,string> ParseForm(string body) {
        var dict = new System.Collections.Generic.Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(body)) return dict;
        var parts = body.Split('&');
        foreach (var part in parts) {
            var kv = part.Split(new[]{'='}, 2);
            var k = Uri.UnescapeDataString(kv[0] ?? "");
            var v = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : "";
            dict[k] = v;
        }
        return dict;
    }

    static string Get(System.Collections.Generic.Dictionary<string,string> dict, string key) {
        return dict.TryGetValue(key, out var v) ? v : "";
    }

    static void WriteJson(HttpListenerContext ctx, int status, string json) {
        var bytes = Encoding.UTF8.GetBytes(json);
        ctx.Response.ContentType = "application/json";
        ctx.Response.StatusCode = status;
        ctx.Response.ContentLength64 = bytes.Length;
        ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
        ctx.Response.Close();
    }

    static string JsonEscape(string s) {
        if (s == null) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}

