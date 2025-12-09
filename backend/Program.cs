using System;
using System.Net;
using System.Text;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        // Ensure DB exists and is seeded
        Api.EnsureDatabase();

        // Bind to Render's PORT environment variable (fallback to 8080 locally)
        string port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
        string url = $"http://*:{port}/";

        var listener = new HttpListener();
        listener.Prefixes.Add(url);

        try
        {
            listener.Start();
            Console.WriteLine($"Listening on {url}");
        }
        catch (HttpListenerException ex)
        {
            Console.Error.WriteLine($"Failed to start listener: {ex.Message}");
            return;
        }

        while (true)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = listener.GetContext();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Listener error: {ex.Message}");
                continue;
            }

            var req = ctx.Request;
            var res = ctx.Response;

            // CORS headers
            res.Headers.Add("Access-Control-Allow-Origin", "*");
            res.Headers.Add("Access-Control-Allow-Methods", "GET,POST,PUT,DELETE,OPTIONS");
            res.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");

            // Handle preflight
            if (req.HttpMethod == "OPTIONS")
            {
                res.StatusCode = 204;
                res.Close();
                continue;
            }

            try
            {
                RouteRequest(req, res);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Request error: {ex}");
                WriteJson(res, 500, "{\"error\":\"internal_server_error\"}");
            }
        }
    }

    static void RouteRequest(HttpListenerRequest req, HttpListenerResponse res)
    {
        string path = req.Url.AbsolutePath.TrimEnd('/').ToLowerInvariant();
        string method = req.HttpMethod.ToUpperInvariant();

        // Health
        if (method == "GET" && path == "/api/health")
        {
            WriteJson(res, 200, "{\"status\":\"ok\"}");
            return;
        }

        // Customers
        if (method == "GET" && path == "/api/customers")
        {
            string json = Api.ListCustomersJson();
            WriteJson(res, 200, json);
            return;
        }

        // Summary
        if (method == "GET" && path == "/api/summary")
        {
            string json = Api.SummaryJson();
            WriteJson(res, 200, json);
            return;
        }

        // Fallback: not found
        WriteHtml(res, 404, "<pre>Not Found</pre>");
    }

    static void WriteJson(HttpListenerResponse res, int statusCode, string json)
    {
        res.StatusCode = statusCode;
        res.ContentType = "application/json; charset=utf-8";
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        res.ContentLength64 = bytes.Length;
        using (var output = res.OutputStream)
        {
            output.Write(bytes, 0, bytes.Length);
        }
    }

    static void WriteHtml(HttpListenerResponse res, int statusCode, string html)
    {
        res.StatusCode = statusCode;
        res.ContentType = "text/html; charset=utf-8";
        byte[] bytes = Encoding.UTF8.GetBytes($"<!DOCTYPE html><html><body>{html}</body></html>");
        res.ContentLength64 = bytes.Length;
        using (var output = res.OutputStream)
        {
            output.Write(bytes, 0, bytes.Length);
        }
    }
}
