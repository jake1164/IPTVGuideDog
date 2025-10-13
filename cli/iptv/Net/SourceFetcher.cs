namespace Iptv.Cli.Net;

public sealed class SourceFetcher
{
    private readonly HttpClient _httpClient;
    private readonly TextWriter _diagnostics;

    public SourceFetcher(HttpClient httpClient, TextWriter diagnostics)
    {
        _httpClient = httpClient;
        _diagnostics = diagnostics;
    }

    public async Task<string> GetStringAsync(string source, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new CliException("Playlist URL was not provided.", ExitCodes.ConfigError);
        }

        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            try
            {
                if (_diagnostics != TextWriter.Null)
                {
                    await _diagnostics.WriteLineAsync($"Downloading {uri.GetLeftPart(UriPartial.Path)}...");
                }

                using var response = await _httpClient.GetAsync(uri, cancellationToken);
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    throw new CliException($"Authentication failed when requesting {uri}", ExitCodes.AuthError);
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new CliException($"Request to {uri} failed with status {(int)response.StatusCode}.", ExitCodes.NetworkError);
                }

                return await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (CliException)
            {
                throw;
            }
            catch (TaskCanceledException ex)
            {
                throw new CliException($"Request to {uri} timed out: {ex.Message}", ExitCodes.NetworkError);
            }
            catch (HttpRequestException ex)
            {
                throw new CliException($"Request to {uri} failed: {ex.Message}", ExitCodes.NetworkError);
            }
        }
        else
        {
            try
            {
                if (_diagnostics != TextWriter.Null)
                {
                    await _diagnostics.WriteLineAsync($"Reading file {source}...");
                }

                return await File.ReadAllTextAsync(source, cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new CliException($"Failed to read file {source}: {ex.Message}", ExitCodes.IoError);
            }
        }
    }
}
