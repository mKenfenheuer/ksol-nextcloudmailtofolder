namespace KSol.NextCloudMailToFolder.Models;

public class NextCloudUser
{
    public string? Id { get; set; }
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public string? Token { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime TokenExpiration { get; set; }
    public List<Destination>? Destinations { get; set; }
}