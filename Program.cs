using System.Diagnostics;
using DiscordRPC;
using DiscordRPC.Entities;

namespace SpotifyDiscordRPC
{
    internal class Program
    {
        private static SpotifyMemoryClient _spotifyMemoryClient;
        private static readonly List<DiscordRpcClient> _rpcClients = new();

        private static void Main(string[] args)
        {
            // Make and initialize 1 client per pipe (10)
            foreach (var i in Enumerable.Range(0, 10))
            {
                DiscordRpcClient client = new DiscordRpcClient("1026857154214367312", i)
                {
                    ShutdownOnly = false,
                    SkipIdenticalPresence = true
                };

                client.ConnectionEstablishedEvent += (sender, e) =>
                {
                    Console.WriteLine("Pipe connected: {0}", e.ConnectedPipe);
                };

                client.ConnectionFailedEvent += (sender, e) =>
                {
                    Console.WriteLine("Pipe connection failed: {0}", e.FailedPipe);
                };

                client.ReadyEvent += (sender, e) =>
                {
                    Console.WriteLine("Received Ready from user {0}", e.User.Username);
                };

                client.Initialize();

                _rpcClients.Add(client);

                Thread.Sleep(500);
            }

            // Initialize memory client
            InitializeMemoryClient();

            // Start new RPC update thread
            new Thread(() =>
            {
                while (true)
                    foreach (var client in _rpcClients)
                    {
                        try
                        {
                            if (_spotifyMemoryClient != null)
                                if (_spotifyMemoryClient.IsPlaying && _spotifyMemoryClient.CurrentSong != null)
                                {
                                    var presence = new RichPresence
                                    {
                                        Details = _spotifyMemoryClient.CurrentSong.Title,
                                        State = string.Join(", ", _spotifyMemoryClient.CurrentSong.Artists)
                                    };

                                    if (!_spotifyMemoryClient.CurrentSong.IsLocalFile)
                                    {
                                        presence.Assets = new Assets
                                        {
                                            LargeImageKey = _spotifyMemoryClient.CurrentSong.CoverArtURL,
                                            LargeImageText = "Spotify Discord RPC by V3rzeT",
                                            SmallImageKey = "spotify-icon",
                                            SmallImageText = "Volume: " + string.Format("{0:0}%",
                                                _spotifyMemoryClient.PlayerVolume)
                                        };

                                        presence.Buttons = new List<Button>
                                        {
                                            new()
                                            {
                                                Label = "Open in Spotify", Url = _spotifyMemoryClient.CurrentSong.URL
                                            }
                                        }.ToArray();
                                    }
                                    else
                                    {
                                        presence.Assets = new Assets
                                        {
                                            LargeImageKey =
                                                "https://i.bcow.xyz/uZJivyf.png", // Using an image that I uploaded ("spotify-musicthumb") doesn't work at all, so I had to upload it to imgur ;-;
                                            LargeImageText = "Spotify Discord RPC by V3rzeT",
                                            SmallImageKey = "spotify-icon",
                                            SmallImageText = "Volume: " + string.Format("{0:0}%",
                                                _spotifyMemoryClient.PlayerVolume)
                                        };
                                    }

                                    // Set elapsed time
                                    if (_spotifyMemoryClient.PlayerCurrentTime.HasValue)
                                        presence.Timestamps = new Timestamps(DateTime.Now.ToUniversalTime()
                                            .Subtract(_spotifyMemoryClient.PlayerCurrentTime.Value));

                                    // Set presence
                                    client.SetPresence(presence);
                                }
                                else
                                {
                                    client.ClearPresence();
                                }
                        }
                        catch
                        {
                        }

                        Thread.Sleep(1000);
                    }
            }).Start();

            Console.ReadLine();
        }

        private static void InitializeMemoryClient()
        {
            // Try to get Spotify process
            Process? process = null;
            while (!Process.GetProcessesByName("Spotify").Any(x =>
                   {
                       bool answer = x.MainWindowTitle != string.Empty && x.MainWindowHandle != IntPtr.Zero;

                       if (answer) process = x;

                       return answer;
                   }))
                Thread.Sleep(100); // Retry every 100ms

            // Add exit handler
            process.EnableRaisingEvents = true;
            process.Exited += (sender, args) => InitializeMemoryClient();

            // Initialize Memory Client
            if (process != null)
                _spotifyMemoryClient = new SpotifyMemoryClient(process);
        }
    }
}