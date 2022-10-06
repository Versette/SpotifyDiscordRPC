using System.Diagnostics;
using Memory;
using SpotifyDiscordRPC.Models;

namespace SpotifyDiscordRPC;

public class SpotifyMemoryClient
{
    private const string SongIdAddress = "chrome_elf.dll+E59C2";
    private const string ThumbnailAddress = "base+01306FE0,4c,48,1f8,10,2c,234,1e4,1f8,c";
    private const string SongDurationAddress = "base+01319820,18,8,24,11f4,174,d4,2fc,24,1c8";
    private const string CurrentTimeAddress = "base+01307004,20,10,14,8,0,20,200";

    private const string VolumeAddress = "base+01307064,284,c,18,10,60";

    // Memory Addresses
    private static readonly string[]
        TitleArtistAddresses =
        {
            "base+00DE0AA0,24,38,e0,14,20,74,20,18,0", "libcef.dll+08BAE974,80,8,38,28,8,4,10,88,0",
            "base+00DE0AA0,64,50,50,74,20,18,0", "libcef.dll+08C55400,122c,a8,8,18,8,14,4,c4,0",
            "libcef.dll+08C55400,15c,8,8,8,14,4,c4,0"
        }; // Workaround, couldn't find good pointer

    private readonly Mem _memory = new();
    private readonly Thread _updateThread;
    private bool _disposed;
    public SpotifySong? CurrentSong;
    public bool IsPlaying;
    public TimeSpan? PlayerCurrentTime;

    public double PlayerVolume;

    public SpotifyMemoryClient(Process spotifyProcess)
    {
        if (!_memory.OpenProcess(spotifyProcess.Id)) throw new Exception("Memory: Couldn't open the process.");

        // Add process exit handler
        spotifyProcess.EnableRaisingEvents = true;
        spotifyProcess.Disposed += (sender, args) => _disposed = true;
        spotifyProcess.Exited += (sender, args) => _disposed = true;

        // Start update thread
        _updateThread = new Thread(ThreadWork);
        _updateThread.Start();
    }

    private void ThreadWork()
    {
        while (!_disposed)
        {
            // Current Song
            var titleArtistString = "";
            try
            {
                while (titleArtistString == "") // Omg these workarounds though :cc
                {
                    foreach (var titleArtistAddress in TitleArtistAddresses)
                    {
                        var result = _memory.ReadString(titleArtistAddress, length: 200);

                        if (!string.IsNullOrWhiteSpace(result))
                        {
                            titleArtistString = result;
                            break;
                        }
                    }

                    Thread.Sleep(1);
                }
            }
            catch
            {
            }

            if (titleArtistString.Contains("•"))
            {
                IsPlaying = true;

                var parts = titleArtistString.Split(" • ");
                var title = parts[0].Replace(" •", "");
                bool localFile = false;
                string[] artists;

                if (parts.Length < 2)
                {
                    // Local music probably, no artists
                    artists = new[] { "Local Music" };
                    localFile = true;
                }
                else
                    // Has artist
                {
                    artists = parts[1].Split(", ");
                }

                // Song Id
                var songId = _memory.ReadString(SongIdAddress, length: 200).Replace("k:", "");
                if (songId.Contains("+-+"))
                    songId = songId.Split("+-+").First();
                else
                    songId = songId.Split("1-+").First();

                // Thumbnail
                var coverArtId = "";
                if (!localFile)
                    coverArtId = "https://i.scdn.co/image/" +
                                 _memory.ReadString(ThumbnailAddress, length: 200).Replace("e:", "");

                // Song Duration
                var totalSongDurationMs = _memory.ReadLong(SongDurationAddress);

                // Update CurrentSong
                CurrentSong = new SpotifySong
                {
                    Title = title,
                    Artists = artists,
                    Id = songId,
                    URL = "https://open.spotify.com/track/" + songId,
                    CoverArtURL = coverArtId,
                    Duration = TimeSpan.FromMilliseconds(totalSongDurationMs),
                    IsLocalFile = localFile
                };
            }
            else
            {
                IsPlaying = false;

                // Let's keep these values just in case we need last song (more accurate like this anyway, as this is what's displayed in the player)
                //CurrentSong = null;
                //PlayerCurrentTime = null;
            }

            // Current Time
            var currentTimeMs = _memory.ReadLong(CurrentTimeAddress) / 10000;
            PlayerCurrentTime = TimeSpan.FromMilliseconds(currentTimeMs);

            // Volume
            double volume = _memory.ReadInt(VolumeAddress);
            var volumePercent = Math.Round(volume.Map(0, 65535, 0, 100), 2);
            PlayerVolume = volumePercent;

            Thread.Sleep(1);
        }
    }
}