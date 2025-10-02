using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NowPlayingPopup.Models;

namespace NowPlayingPopup.Services
{
    /// <summary>
    /// HTTP server for settings management via REST API
    /// </summary>
    public class SettingsHttpServer : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly SettingsManager _settingsManager;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private Task? _listenerTask;
        private bool _isDisposed;

        public int Port { get; private set; }
        public bool IsRunning => _listener?.IsListening ?? false;

        public SettingsHttpServer(SettingsManager settingsManager, int port = 5005)
        {
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _listener = new HttpListener();
            _cancellationTokenSource = new CancellationTokenSource();
            Port = port;
            
            ConfigureListener(port);
        }

        private void ConfigureListener(int port)
        {
            try
            {
                _listener.Prefixes.Add($"http://+:{port}/");
                Port = port;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsHttpServer] Failed to configure port {port}: {ex.Message}");
                // Fallback to localhost only
                _listener.Prefixes.Clear();
                _listener.Prefixes.Add($"http://localhost:{port}/");
            }
        }

        public void Start()
        {
            if (IsRunning)
            {
                Debug.WriteLine("[SettingsHttpServer] Already running");
                return;
            }

            try
            {
                _listener.Start();
                _listenerTask = Task.Run(() => HandleRequestsAsync(_cancellationTokenSource.Token));
                Debug.WriteLine($"[SettingsHttpServer] Started on port {Port}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsHttpServer] Failed to start: {ex.Message}");
                throw;
            }
        }

        public void Stop()
        {
            if (!IsRunning) return;

            try
            {
                _cancellationTokenSource?.Cancel();
                _listener?.Stop();
                _listenerTask?.Wait(TimeSpan.FromSeconds(5));
                Debug.WriteLine("[SettingsHttpServer] Stopped");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsHttpServer] Error stopping: {ex.Message}");
            }
        }

        private async Task HandleRequestsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => ProcessRequestAsync(context), cancellationToken);
                }
                catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SettingsHttpServer] Error accepting request: {ex.Message}");
                }
            }
        }

        private async Task ProcessRequestAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                // Add CORS headers
                AddCorsHeaders(response);

                // Handle preflight
                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 200;
                    response.Close();
                    return;
                }

                // Route request
                await RouteRequestAsync(request, response);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsHttpServer] Error processing request: {ex.Message}");
                await SendErrorResponseAsync(response, 500, "Internal server error");
            }
            finally
            {
                try { response.Close(); } catch { }
            }
        }

        private async Task RouteRequestAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            var path = request.Url?.AbsolutePath ?? "";

            switch (path)
            {
                case "/settings":
                    await HandleSettingsEndpointAsync(request, response);
                    break;

                case "/health":
                    await HandleHealthCheckAsync(response);
                    break;

                case "/":
                    await SendJsonResponseAsync(response, new { 
                        status = "ok", 
                        endpoints = new[] { "/settings", "/health" } 
                    });
                    break;

                default:
                    await SendErrorResponseAsync(response, 404, "Endpoint not found");
                    break;
            }
        }

        private async Task HandleSettingsEndpointAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            switch (request.HttpMethod)
            {
                case "GET":
                    await HandleGetSettingsAsync(response);
                    break;

                case "POST":
                case "PUT":
                    await HandleUpdateSettingsAsync(request, response);
                    break;

                default:
                    await SendErrorResponseAsync(response, 405, "Method not allowed");
                    break;
            }
        }

        private async Task HandleGetSettingsAsync(HttpListenerResponse response)
        {
            try
            {
                var settings = _settingsManager.CurrentSettings;
                await SendJsonResponseAsync(response, settings);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsHttpServer] Error getting settings: {ex.Message}");
                await SendErrorResponseAsync(response, 500, "Failed to retrieve settings");
            }
        }

        private async Task HandleUpdateSettingsAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                var body = await reader.ReadToEndAsync();

                if (string.IsNullOrWhiteSpace(body))
                {
                    await SendErrorResponseAsync(response, 400, "Request body is empty");
                    return;
                }

                var newSettings = JsonSerializer.Deserialize<WidgetSettings>(body);
                if (newSettings == null)
                {
                    await SendErrorResponseAsync(response, 400, "Invalid JSON format");
                    return;
                }

                // Validate settings
                var validation = newSettings.Validate();
                if (!validation.IsValid)
                {
                    await SendJsonResponseAsync(response, new 
                    { 
                        success = false, 
                        errors = validation.Errors 
                    }, 400);
                    return;
                }

                // Update settings
                _settingsManager.Update(newSettings);

                await SendJsonResponseAsync(response, new 
                { 
                    success = true, 
                    message = "Settings updated successfully" 
                });
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"[SettingsHttpServer] JSON parse error: {ex.Message}");
                await SendErrorResponseAsync(response, 400, "Invalid JSON: " + ex.Message);
            }
            catch (ValidationException ex)
            {
                Debug.WriteLine($"[SettingsHttpServer] Validation error: {ex.Message}");
                await SendErrorResponseAsync(response, 400, ex.Message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsHttpServer] Error updating settings: {ex.Message}");
                await SendErrorResponseAsync(response, 500, "Failed to update settings");
            }
        }

        private async Task HandleHealthCheckAsync(HttpListenerResponse response)
        {
            await SendJsonResponseAsync(response, new 
            { 
                status = "healthy",
                timestamp = DateTime.UtcNow,
                port = Port
            });
        }

        private static void AddCorsHeaders(HttpListenerResponse response)
        {
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
        }

        private static async Task SendJsonResponseAsync(HttpListenerResponse response, object data, int statusCode = 200)
        {
            response.StatusCode = statusCode;
            response.ContentType = "application/json; charset=utf-8";

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            var buffer = Encoding.UTF8.GetBytes(json);
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        }

        private static async Task SendErrorResponseAsync(HttpListenerResponse response, int statusCode, string message)
        {
            await SendJsonResponseAsync(response, new 
            { 
                success = false, 
                error = message 
            }, statusCode);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            Stop();
            _listener?.Close();
            _cancellationTokenSource?.Dispose();
        }
    }
}