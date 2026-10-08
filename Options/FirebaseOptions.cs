using System.ComponentModel.DataAnnotations;

namespace CoberPush.Api.Options;

public sealed class FirebaseOptions
{
    public const string SECTION = "Firebase";

    [Required]
    public string ProjectId { get; set; } = string.Empty;
}
