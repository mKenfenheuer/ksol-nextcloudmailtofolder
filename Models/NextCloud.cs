// Root myDeserializedClass = JsonSerializer.Deserialize<Root>(myJsonResponse);
using System.Text.Json.Serialization;


public class NextCloudUserInformation
{

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("quota")]
    public Quota Quota { get; set; }

    [JsonPropertyName("email")]
    public string Email { get; set; }

    [JsonPropertyName("displayname")]
    public string DisplayName { get; set; }
}

public class NexCloudOcsData
{
    [JsonPropertyName("data")]
    public NextCloudUserInformation Data { get; set; }
}

public class Quota
{
    [JsonPropertyName("relative")]
    public float Relative { get; set; }
}

public class NextCloudUserInformationResponse
{
    [JsonPropertyName("ocs")]
    public NexCloudOcsData Ocs { get; set; }
}

