using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace AlfaCredit.IntegrationTests.Infrastructure;

public enum StubScoringMode
{
    /// <summary>Отвечает по контракту: грейд A, модель stub-test.</summary>
    Ok,

    /// <summary>Отвечает 500: сервис скоринга недоступен.</summary>
    ServerError,
}

/// <summary>Минимальный HTTP-сервер по docs/scoring-contract.md для интеграционных тестов внешнего скоринга.</summary>
public sealed class StubScoringServer : IDisposable
{
    public const string ModelVersion = "stub-test";

    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _stopping = new();

    public StubScoringServer()
    {
        BaseUrl = $"http://127.0.0.1:{FreePort()}";
        _listener.Prefixes.Add(BaseUrl + "/");
    }

    public string BaseUrl { get; }

    public StubScoringMode Mode { get; set; } = StubScoringMode.Ok;

    /// <summary>Все тела запросов к /api/v1/score в порядке получения.</summary>
    public ConcurrentQueue<JsonElement> Requests { get; } = new();

    public void Start()
    {
        _listener.Start();
        _ = Task.Run(AcceptLoopAsync);
    }

    public void Dispose()
    {
        _stopping.Cancel();
        try
        {
            _listener.Stop();
            _listener.Close();
        }
        catch (ObjectDisposedException)
        {
        }

        _stopping.Dispose();
    }

    private async Task AcceptLoopAsync()
    {
        while (!_stopping.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception) when (_stopping.IsCancellationRequested || !_listener.IsListening)
            {
                return;
            }

            _ = Task.Run(() => HandleAsync(context));
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            var request = context.Request;
            var path = request.Url?.AbsolutePath ?? "/";

            if (request.HttpMethod == "GET" && path == "/health")
            {
                await WriteAsync(context.Response, 200, $$"""{"status":"ok","model_version":"{{ModelVersion}}"}""");
                return;
            }

            if (request.HttpMethod == "POST" && path == "/api/v1/score")
            {
                using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
                var body = await reader.ReadToEndAsync();
                using var document = JsonDocument.Parse(body);
                var json = document.RootElement.Clone();
                Requests.Enqueue(json);

                if (Mode == StubScoringMode.ServerError)
                {
                    await WriteAsync(context.Response, 500, """{"error":"model_unavailable"}""");
                    return;
                }

                var requestId = json.GetProperty("request_id").GetString();
                await WriteAsync(context.Response, 200, $$"""
                    {
                      "request_id": "{{requestId}}",
                      "model_version": "{{ModelVersion}}",
                      "score": 82,
                      "risk_grade": "A",
                      "probability_of_default": 0.04,
                      "factors": [
                        { "code": "credit_history", "title": "Кредитная история", "impact": 15, "value": "без просрочек", "client_explanation": "Просрочек по кредитам за два года нет." }
                      ],
                      "warnings": [],
                      "missing_data": ["company.history"]
                    }
                    """);
                return;
            }

            await WriteAsync(context.Response, 404, """{"error":"not_found"}""");
        }
        catch (Exception)
        {
            try
            {
                context.Response.Abort();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    private static async Task WriteAsync(HttpListenerResponse response, int status, string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        response.StatusCode = status;
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.Close();
    }

    private static int FreePort()
    {
        using var socket = new TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        return ((IPEndPoint)socket.LocalEndpoint).Port;
    }
}
