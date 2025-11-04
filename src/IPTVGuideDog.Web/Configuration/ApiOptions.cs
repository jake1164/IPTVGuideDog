using System.ComponentModel.DataAnnotations;

namespace IPTVGuideDog.Web.Configuration;

public sealed class ApiOptions
{
    [Required]
    [Url]
    public string BaseAddress { get; set; } = "http://localhost:5080";

    [Required]
    public string PlaylistPath { get; set; } = "/playlist.m3u";
}
