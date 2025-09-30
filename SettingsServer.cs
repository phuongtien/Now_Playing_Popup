using System.Net;
using System.Text;
using System.Text.Json;
using System.IO;


namespace NowPlayingPopup
{
    public class SettingsHttpServer
    {
        private readonly HttpListener listener;
        private readonly MainWindow mainWindow;

        public SettingsHttpServer(MainWindow window, string prefix = "http://+:5005/")
        {
            mainWindow = window;
            listener = new HttpListener();
            listener.Prefixes.Add(prefix);
        }

        public void Start()
        {
            listener.Start();
            Task.Run(HandleRequests);
            Console.WriteLine("[SettingsHttpServer] Listening on http://localhost:5005/");
        }

        public void Stop() => listener.Stop();

        private async Task HandleRequests()
        {
            while (listener.IsListening)
            {
                try
                {
                    var ctx = await listener.GetContextAsync();
                    var req = ctx.Request;
                    var res = ctx.Response;

                    // CORS headers
                    res.Headers.Add("Access-Control-Allow-Origin", "*");
                    res.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                    res.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

                    if (req.HttpMethod == "OPTIONS")
                    {
                        res.StatusCode = 200;
                        res.Close();
                        continue;
                    }

                    if (req.Url.AbsolutePath == "/settings")
                    {
                        if (req.HttpMethod == "GET")
                        {
                            var json = JsonSerializer.Serialize(mainWindow.GetCurrentSettings());
                            var buf = Encoding.UTF8.GetBytes(json);
                            res.ContentType = "application/json";
                            await res.OutputStream.WriteAsync(buf, 0, buf.Length);
                        }
                        else if (req.HttpMethod == "POST")
                        {
                            using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
                            var body = await reader.ReadToEndAsync();
                            var newSettings = JsonSerializer.Deserialize<WidgetSettings>(body);

                            if (newSettings != null)
                            {
                                mainWindow.UpdateSettingsFromWeb(newSettings);
                                res.StatusCode = 200;
                            }
                            else
                            {
                                res.StatusCode = 400;
                            }
                        }
                    }
                    else res.StatusCode = 404;

                    res.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SettingsHttpServer] Error: {ex.Message}");
                }
            }
        }
    }
}
