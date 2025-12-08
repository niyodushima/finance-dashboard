using System;
using System.Globalization;
using Mono.Data.Sqlite;
using System.Data;

class Program {
    const string ConnString = "URI=file:finance.db";

    static void Main() {
        EnsureDatabase();

        Console.WriteLine("=== Personal Finance System ===");

        // Login loop
        while (true) {
            Console.Write("Username: ");
            string username = (Console.ReadLine() ?? "").Trim();
            Console.Write("Password: ");
            string password = (Console.ReadLine() ?? "").Trim();

            if (ValidateLogin(username, password)) {
                Console.WriteLine("Login successful! Redirecting to dashboard...");
                break;
            } else {
                Console.WriteLine("Invalid credentials. Try again.\n");
            }
        }

        Dashboard();
    }

    // Initialize DB: create tables if missing and seed admin user
    static void EnsureDatabase() {
        using (var conn = new SqliteConnection(ConnString)) {
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

            // Seed admin if not exists
            string checkAdmin = "SELECT COUNT(*) FROM Users WHERE Username='admin';";
            using (var cmd = new SqliteCommand(checkAdmin, conn)) {
                long count = Convert.ToInt64(cmd.ExecuteScalar() ?? 0);
                if (count == 0) {
                    string seed = "INSERT INTO Users (Username, Password) VALUES ('admin', 'Admin@123');";
                    using (var seedCmd = new SqliteCommand(seed, conn)) seedCmd.ExecuteNonQuery();
                    Console.WriteLine("Seeded default admin user: admin / Admin@123");
                }
            }
        }
    }

    static bool ValidateLogin(string username, string password) {
        using (var conn = new SqliteConnection(ConnString)) {
            conn.Open();
            string sql = "SELECT COUNT(*) FROM Users WHERE Username=@u AND Password=@p;";
            using (var cmd = new SqliteCommand(sql, conn)) {
                cmd.Parameters.AddWithValue("@u", username);
                cmd.Parameters.AddWithValue("@p", password);
                long count = Convert.ToInt64(cmd.ExecuteScalar() ?? 0);
                return count > 0;
            }
        }
    }

    static void Dashboard() {
        while (true) {
            Console.WriteLine();
            Console.WriteLine("=== Dashboard ===");
            Console.WriteLine("1. Record customer");
            Console.WriteLine("2. Record income");
            Console.WriteLine("3. Record expense");
            Console.WriteLine("4. Show summary (income vs expenses)");
            Console.WriteLine("5. List customers");
            Console.WriteLine("6. Exit");
            Console.Write("Choose: ");
            string choice = (Console.ReadLine() ?? "").Trim();

            if (choice == "1") RecordCustomer();
            else if (choice == "2") RecordIncome();
            else if (choice == "3") RecordExpense();
            else if (choice == "4") ShowSummary();
            else if (choice == "5") ListCustomers();
            else if (choice == "6") { Console.WriteLine("Goodbye!"); break; }
            else Console.WriteLine("Invalid option. Try again.");
        }
    }

    static void RecordCustomer() {
        Console.Write("Enter customer name: ");
        string name = (Console.ReadLine() ?? "").Trim();

        if (string.IsNullOrEmpty(name)) {
            Console.WriteLine("Name cannot be empty.");
            return;
        }

        using (var conn = new SqliteConnection(ConnString)) {
            conn.Open();
            string sql = "INSERT INTO Customers (Name) VALUES (@n);";
            using (var cmd = new SqliteCommand(sql, conn)) {
                cmd.Parameters.AddWithValue("@n", name);
                cmd.ExecuteNonQuery();
            }

            string lastIdSql = "SELECT last_insert_rowid();";
            using (var cmd = new SqliteCommand(lastIdSql, conn)) {
                long id = Convert.ToInt64(cmd.ExecuteScalar() ?? 0);
                Console.WriteLine($"Customer '{name}' recorded successfully with ID {id}.");
            }
        }
    }

    static void RecordIncome() {
        Console.Write("Enter customer ID: ");
        int customerId = int.Parse(Console.ReadLine());

        Console.Write("Enter income amount: ");
        double amount = double.Parse(Console.ReadLine());

        Console.Write("Enter description (optional): ");
        string description = Console.ReadLine();

        using (var conn = new SqliteConnection(ConnString)) {
            conn.Open();
            string sql = "INSERT INTO Income (CustomerId, Amount, Description) VALUES (@c, @a, @d)";
            using (var cmd = new SqliteCommand(sql, conn)) {
                cmd.Parameters.AddWithValue("@c", customerId);
                cmd.Parameters.AddWithValue("@a", amount);
                cmd.Parameters.AddWithValue("@d", string.IsNullOrEmpty(description) ? (object)DBNull.Value : description);
                cmd.ExecuteNonQuery();
            }
        }
        Console.WriteLine($"Income of {amount} recorded successfully for customer {customerId}!");
    }

    static void RecordExpense() {
        Console.Write("Enter customer ID: ");
        int customerId = int.Parse(Console.ReadLine());

        Console.Write("Enter expense amount: ");
        double amount = double.Parse(Console.ReadLine());

        Console.Write("Enter description (optional): ");
        string description = Console.ReadLine();

        using (var conn = new SqliteConnection(ConnString)) {
            conn.Open();
            string sql = "INSERT INTO Expenses (CustomerId, Amount, Description) VALUES (@c, @a, @d)";
            using (var cmd = new SqliteCommand(sql, conn)) {
                cmd.Parameters.AddWithValue("@c", customerId);
                cmd.Parameters.AddWithValue("@a", amount);
                cmd.Parameters.AddWithValue("@d", string.IsNullOrEmpty(description) ? (object)DBNull.Value : description);
                cmd.ExecuteNonQuery();
            }
        }
        Console.WriteLine($"Expense of {amount} recorded successfully for customer {customerId}!");
    }

    static void ShowSummary() {
        using (var conn = new SqliteConnection(ConnString)) {
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
            using (var reader = cmd.ExecuteReader()) {
                Console.WriteLine("=== Financial Summary ===");
                while (reader.Read()) {
                    int id = reader.GetInt32(0);
                    string name = reader.GetString(1);
                    double income = reader.GetDouble(2);
                    double expense = reader.GetDouble(3);
                    double balance = reader.GetDouble(4);
                    Console.WriteLine($"[{id}] {name} | Income: {income}, Expense: {expense}, Balance: {balance}");
                }
            }
        }
    }

    static void ListCustomers() {
        using (var conn = new SqliteConnection(ConnString)) {
            conn.Open();
            string sql = "SELECT Id, Name FROM Customers ORDER BY Id ASC;";
            using (var cmd = new SqliteCommand(sql, conn))
            using (var reader = cmd.ExecuteReader()) {
                Console.WriteLine("=== Customers ===");
                while (reader.Read()) {
                    int id = reader.GetInt32(0);
                    string name = reader.GetString(1);
                    Console.WriteLine($"[{id}] {name}");
                }
            }
        }
    }
}

