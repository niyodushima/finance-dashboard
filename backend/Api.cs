using System;
using System.Net;
using System.Text;
using System.IO;
using System.Data;
using Mono.Data.Sqlite;
using System.Collections.Generic;

class Api
{
    const string ConnString = "URI=file:finance.db";

    static void Main()
    {
        EnsureDatabase();

        string port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
        string prefix = $"http://0.0.0.0:{port}/";

        HttpListener listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();
        Console.WriteLine($"Finance API running on {prefix}");

        while (true)
        {
            var ctx = listener.GetContext();
            var req = ctx.Request;
            var res = ctx.Response;

            string responseString = HandleRequest(req);
            byte[] buffer = Encoding.UTF8.GetBytes(responseString);

            res.ContentType = "application/json";
            res.ContentLength64 = buffer.Length;
            res.OutputStream.Write(buffer, 0, buffer.Length);
            res.OutputStream.Close();
        }
    }

    static string HandleRequest(HttpListenerRequest req)
    {
        try
        {
            if (req.Url.AbsolutePath == "/api/health")
            {
                return "{\"status\":\"ok\"}";
            }
            else if (req.Url.AbsolutePath == "/api/customers" && req.HttpMethod == "GET")
            {
                return ListCustomers();
            }
            else if (req.Url.AbsolutePath == "/api/summary" && req.HttpMethod == "GET")
            {
                return ShowSummary();
            }
            else
            {
                return "{\"error\":\"Unknown endpoint\"}";
            }
        }
        catch (Exception ex)
        {
            return $"{{\"error\":\"{ex.Message}\"}}";
        }
    }

    static void EnsureDatabase()
    {
        using (var conn = new SqliteConnection(ConnString))
        {
            conn.Open();

            string[] sqls = {
                @"CREATE TABLE IF NOT EXISTS Users (
                  Id INTEGER PRIMARY KEY AUTOINCREMENT,
                  Username TEXT UNIQUE NOT NULL,
                  Password TEXT NOT NULL
                );",
                @"CREATE TABLE IF NOT EXISTS Customers (
                  Id INTEGER PRIMARY KEY AUTOINCREMENT,
                  Name TEXT NOT NULL
                );",
                @"CREATE TABLE IF NOT EXISTS Income (
                  Id INTEGER PRIMARY KEY AUTOINCREMENT,
                  CustomerId INTEGER,
                  Amount REAL NOT NULL,
                  Date TEXT DEFAULT CURRENT_TIMESTAMP,
                  Description TEXT,
                  FOREIGN KEY(CustomerId) REFERENCES Customers(Id)
                );",
                @"CREATE TABLE IF NOT EXISTS Expenses (
                  Id INTEGER PRIMARY KEY AUTOINCREMENT,
                  CustomerId INTEGER,
                  Amount REAL NOT NULL,
                  Date TEXT DEFAULT CURRENT_TIMESTAMP,
                  Description TEXT,
                  FOREIGN KEY(CustomerId) REFERENCES Customers(Id)
                );"
            };

            foreach (var sql in sqls)
            {
                using (var cmd = new SqliteCommand(sql, conn))
                    cmd.ExecuteNonQuery();
            }

            string checkAdmin = "SELECT COUNT(*) FROM Users WHERE Username='admin';";
            using (var cmd = new SqliteCommand(checkAdmin, conn))
            {
                long count = Convert.ToInt64(cmd.ExecuteScalar() ?? 0);
                if (count == 0)
                {
                    string seed = "INSERT INTO Users (Username, Password) VALUES ('admin', 'Admin@123');";
                    using (var seedCmd = new SqliteCommand(seed, conn))
                        seedCmd.ExecuteNonQuery();
                    Console.WriteLine("Seeded default admin user: admin / Admin@123");
                }
            }
        }
    }

    static string ListCustomers()
    {
        var customers = new List<string>();
        using (var conn = new SqliteConnection(ConnString))
        {
            conn.Open();
            string sql = "SELECT Id, Name FROM Customers ORDER BY Id ASC;";
            using (var cmd = new SqliteCommand(sql, conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    int id = reader.GetInt32(0);
                    string name = reader.GetString(1);
                    customers.Add($"{{\"id\":{id},\"name\":\"{name}\"}}");
                }
            }
        }
        return "[" + string.Join(",", customers) + "]";
    }

    static string ShowSummary()
    {
        var rows = new List<string>();
        using (var conn = new SqliteConnection(ConnString))
        {
            conn.Open();
            string sql = @"
                SELECT c.Id, c.Name,
                       IFNULL(SUM(i.Amount),0) AS TotalIncome,
                       IFNULL(SUM(e.Amount),0) AS TotalExpense,
                       IFNULL(SUM(i.Amount),0) - IFNULL(SUM(e.Amount),0) AS Balance
                FROM Customers c
                LEFT JOIN Income i ON c.Id = i.CustomerId
                LEFT JOIN Expenses e ON c.Id = e.CustomerId
                GROUP BY c.Id, c.Name
                ORDER BY c.Name ASC;";
            using (var cmd = new SqliteCommand(sql, conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    int id = reader.GetInt32(0);
                    string name = reader.GetString(1);
                    double income = reader.GetDouble(2);
                    double expense = reader.GetDouble(3);
                    double balance = reader.GetDouble(4);
                    rows.Add($"{{\"id\":{id},\"name\":\"{name}\",\"income\":{income},\"expense\":{expense},\"balance\":{balance}}}");
                }
            }
        }
        return "[" + string.Join(",", rows) + "]";
    }
}
