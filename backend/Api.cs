using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;

static class Api
{
    // Allow override via environment variable for Render persistent disk (e.g., /data/finance.db)
    static string DbPath => Environment.GetEnvironmentVariable("DB_PATH") ?? "finance.db";
    static string ConnString => $"URI=file:{DbPath}";

    public static void EnsureDatabase()
    {
        using (var conn = new SqliteConnection(ConnString))
        {
            conn.Open();

            string createUsers = @"
                CREATE TABLE IF NOT EXISTS Users (
                  Id INTEGER PRIMARY KEY AUTOINCREMENT,
                  Username TEXT UNIQUE NOT NULL,
                  Password TEXT NOT NULL
                );";
            string createCustomers = @"
                CREATE TABLE IF NOT EXISTS Customers (
                  Id INTEGER PRIMARY KEY AUTOINCREMENT,
                  Name TEXT NOT NULL
                );";
            string createIncome = @"
                CREATE TABLE IF NOT EXISTS Income (
                  Id INTEGER PRIMARY KEY AUTOINCREMENT,
                  CustomerId INTEGER,
                  Amount REAL NOT NULL,
                  Date TEXT DEFAULT CURRENT_TIMESTAMP,
                  Description TEXT,
                  FOREIGN KEY(CustomerId) REFERENCES Customers(Id)
                );";
            string createExpenses = @"
                CREATE TABLE IF NOT EXISTS Expenses (
                  Id INTEGER PRIMARY KEY AUTOINCREMENT,
                  CustomerId INTEGER,
                  Amount REAL NOT NULL,
                  Date TEXT DEFAULT CURRENT_TIMESTAMP,
                  Description TEXT,
                  FOREIGN KEY(CustomerId) REFERENCES Customers(Id)
                );";

            using (var cmd = new SqliteCommand(createUsers, conn)) cmd.ExecuteNonQuery();
            using (var cmd = new SqliteCommand(createCustomers, conn)) cmd.ExecuteNonQuery();
            using (var cmd = new SqliteCommand(createIncome, conn)) cmd.ExecuteNonQuery();
            using (var cmd = new SqliteCommand(createExpenses, conn)) cmd.ExecuteNonQuery();

            string checkAdmin = "SELECT COUNT(*) FROM Users WHERE Username='admin';";
            using (var cmd = new SqliteCommand(checkAdmin, conn))
            {
                long count = Convert.ToInt64(cmd.ExecuteScalar() ?? 0);
                if (count == 0)
                {
                    string seed = "INSERT INTO Users (Username, Password) VALUES ('admin', 'Admin@123');";
                    using (var seedCmd = new SqliteCommand(seed, conn)) seedCmd.ExecuteNonQuery();
                    Console.WriteLine("Seeded default admin user: admin / Admin@123");
                }
            }
        }
    }

    // JSON: [{"id":1,"name":"Alice"}, ...]
    public static string ListCustomersJson()
    {
        var customers = new List<(long id, string name)>();

        using (var conn = new SqliteConnection(ConnString))
        {
            conn.Open();
            string sql = "SELECT Id, Name FROM Customers ORDER BY Id ASC;";
            using (var cmd = new SqliteCommand(sql, conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    long id = reader.GetInt64(0);
                    string name = reader.GetString(1);
                    customers.Add((id, Escape(name)));
                }
            }
        }

        var sb = new StringBuilder();
        sb.Append('[');
        for (int i = 0; i < customers.Count; i++)
        {
            var c = customers[i];
            sb.Append($"{{\"id\":{c.id},\"name\":\"{c.name}\"}}");
            if (i < customers.Count - 1) sb.Append(',');
        }
        sb.Append(']');
        return sb.ToString();
    }

    // JSON: [{"id":1,"name":"Alice","income":1000,"expense":200,"balance":800}, ...]
    public static string SummaryJson()
    {
        var rows = new List<(long id, string name, double income, double expense, double balance)>();

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
                    long id = reader.GetInt64(0);
                    string name = reader.GetString(1);
                    double income = reader.IsDBNull(2) ? 0 : reader.GetDouble(2);
                    double expense = reader.IsDBNull(3) ? 0 : reader.GetDouble(3);
                    double balance = income - expense;
                    rows.Add((id, Escape(name), income, expense, balance));
                }
            }
        }

        var sb = new StringBuilder();
        sb.Append('[');
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            sb.Append($"{{\"id\":{r.id},\"name\":\"{r.name}\",\"income\":{ToJsonNumber(r.income)},\"expense\":{ToJsonNumber(r.expense)},\"balance\":{ToJsonNumber(r.balance)}}}");
            if (i < rows.Count - 1) sb.Append(',');
        }
        sb.Append(']');
        return sb.ToString();
    }

    static string Escape(string s)
    {
        if (s == null) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    static string ToJsonNumber(double d)
    {
        // Ensure '.' decimal separator regardless of culture
        return d.ToString("0.############", CultureInfo.InvariantCulture);
    }
}
