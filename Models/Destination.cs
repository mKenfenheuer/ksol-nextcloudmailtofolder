using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KSol.NextCloudMailToFolder.Models;

public class Destination
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Path { get; set; }
    public string? Recipient { get; set; }
    public string? UserId { get; set; }
    [ForeignKey(nameof(UserId))]
    public NextCloudUser? User { get; set; }
}