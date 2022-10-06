namespace SpotifyDiscordRPC.Models;

public class SpotifySong
{
    public string Title { get; set; }
    public string[] Artists { get; set; }
    public string Id { get; set; }
    public string URL { get; set; }
    public string CoverArtURL { get; set; }
    public TimeSpan Duration { get; set; }
    public bool IsLocalFile { get; set; }
}