//#define USE_PROXY
using System.Text.Json;
using System.Text;
using System.Net.Http.Json;
using System.Net;
using System.CommandLine;


namespace TestClient
{
    public class Camera
    {
        public string? name { get; set; }
        public string? mac { get; set; }
        public string? id { get; set; }
    }

    internal class Program
    {
        private readonly static HttpClientHandler handler = new()
        {
#if USE_PROXY
            Proxy = new WebProxy()
            {
                Address = new Uri("http://localhost:8080")
            },
#endif
            ServerCertificateCustomValidationCallback = ((request, cert, chain, errors) => true),
        };

        private readonly static HttpClient sharedClient = new(handler);

        static void Main(string[] args)
        {
            var userOpt = new Option<string>(
                name: "--user",
                description: "User name")
            { IsRequired = true, };

            var passOpt = new Option<string>(
                name: "--pass",
                description: "Password")
            { IsRequired = true, };

            var startOpt = new Option<DateTime>(
                name: "--start",
                description: "Recording start time")
            { IsRequired = true, };

            var endOpt = new Option<DateTime>(
                name: "--end",
                description: "Recording end time")
            { IsRequired = true, };

            var hostOpt = new Option<IPAddress>(
                name: "--ip",
                description: "Protect server address")
            { IsRequired = true, };

            var cameraOpt = new Option<string[]>(
                 name: "--camera",
                 description: "Camera(s) to download")
            { AllowMultipleArgumentsPerToken = true, };

            var outputOpt = new Option<string>(
                name: "--output",
                description: "Output directory");

            var delayOpt = new Option<int>(
                name: "--delay",
                description: "Delay between video downloads",
                getDefaultValue: ()=> 2);
 
            var camerasCommand = new Command("cameras", "Get list of cameras");

            var downloadCommand = new Command("download", "Download video")
            {
                startOpt,
                endOpt,
                cameraOpt,
                outputOpt,
            };

            var rootCommand = new RootCommand("Protect Downloader");
            rootCommand.AddGlobalOption(userOpt);
            rootCommand.AddGlobalOption(passOpt);
            rootCommand.AddGlobalOption(hostOpt);
 
            downloadCommand.SetHandler(async (user, pass, start, end, ip, cam, root, delay) =>
            {
                try
                {
                    if (end <= start)
                    {
                        throw new ApplicationException("Start time must be less than end time");
                    }

                    sharedClient.BaseAddress = new Uri($"https://{ip}");
                    if (LoginAsync(sharedClient, user, pass).Result == false)
                        return;

                    Camera[]? ca = await GetCameras(sharedClient);
                    if (ca?.Length > 0)
                    {
                        if (cam.Length == 0)
                        {
                            foreach (Camera c in ca)
                            {
                                await Download(sharedClient, start, end, c, root, delay);
                            }
                        }
                        else
                        {
                            foreach (string name in cam)
                            {
                                Camera? cameraId = Array.Find(ca, (c) => (c?.name?.Equals(name)) ?? false);
                                if (cameraId is not null)
                                    await Download(sharedClient, start, end, cameraId, root, delay);
                            }
                        }
                    }
                }
                catch (Exception ex) 
                {
                    Console.WriteLine($"{ex.Message}");
                }

            }, userOpt, passOpt, startOpt, endOpt, hostOpt, cameraOpt, outputOpt, delayOpt);

            camerasCommand.SetHandler(async (user, pass, uri) =>
            {
                try
                {
                    sharedClient.BaseAddress = new Uri($"https://{uri}");
                    if (LoginAsync(sharedClient, user, pass).Result == false)
                        return;
                    Camera[]? ca = await GetCameras(sharedClient);

                    Console.WriteLine("Camera List");
                    if (ca is not null)
                    {
                        foreach (Camera c in ca)
                        {
                            Console.WriteLine($"{c.name}, {c.id}");
                        }
                    }
                    else
                        Console.WriteLine("No cameras found");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{ex.Message}");
                }

            }, userOpt, passOpt, hostOpt);

            rootCommand.AddCommand(downloadCommand);
            rootCommand.AddCommand(camerasCommand);

            var rv = rootCommand.InvokeAsync(args).Result;
        }

        static async Task<Camera[]?> GetCameras(HttpClient httpClient)
        {
            using var result = await httpClient.GetAsync("/proxy/protect/api/cameras");
            result.EnsureSuccessStatusCode();
            return await result.Content.ReadFromJsonAsync<Camera[]>();
        }

        static DateTime tlast = DateTime.MinValue;

        static async Task Download(HttpClient httpClient, DateTime start, DateTime end, Camera camera, string root, int delay)
        {
            while ((end - start) > TimeSpan.Zero)
            {
                DateTime te = start.AddHours(1);
                if (te > end) te = end;

                string dir = Path.Combine(root, $"{start:yyyy\\\\MM\\\\dd}");
                Directory.CreateDirectory(dir);
                string fileName = Path.Combine(dir, $"{start:yyyy-MM-dd_HH-mm}_{camera.name}.mp4");
                if (File.Exists(fileName))
                {
                    Console.WriteLine($"File already exists: {fileName}");
                }
                else
                {
                    string s = new DateTimeOffset(start.ToUniversalTime(), TimeSpan.Zero).ToUnixTimeMilliseconds().ToString();
                    string e = new DateTimeOffset(te.ToUniversalTime(), TimeSpan.Zero).ToUnixTimeMilliseconds().ToString();
                    try
                    {
                        var td = (DateTime.Now - tlast).TotalSeconds;
                        if (td < delay)
                            await Task.Delay((int)td * 1000);

                        DateTime ts = DateTime.Now;
                        using var result = await httpClient.GetAsync($"/proxy/protect/api/video/export?camera={camera.id}&start={s}&end={e}");
                        result.EnsureSuccessStatusCode();
                        using FileStream f = new(fileName, FileMode.CreateNew, FileAccess.Write);
                        await result.Content.CopyToAsync(f);
                        tlast = DateTime.Now;
                        Console.WriteLine($"Downloaded {fileName} in {(int)((tlast - ts).TotalSeconds)} seconds");
                    }
                    catch (IOException ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                    catch (HttpRequestException ex)
                    {
                        if (ex.StatusCode == HttpStatusCode.InternalServerError)
                            Console.WriteLine($"No data for {camera.name} at {start}");
                    }
                }
                start = start.AddHours(1);
            }
        }

        static async Task<bool> LoginAsync(HttpClient httpClient, string user, string pass)
        {
            using StringContent jsonContent = new(
                JsonSerializer.Serialize(new
                {
                    username = user,
                    password = pass
                }),
                Encoding.UTF8,
                "application/json");

            using HttpResponseMessage response = await httpClient.PostAsync(
                "api/auth/login",
                jsonContent);

            try
            {
                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (Exception ex) { Console.WriteLine($"Login failed: {ex.Message}"); }
            return false;
         }
    }
}