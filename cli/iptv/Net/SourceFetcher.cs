using Spectre.Console;

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

    public async Task<string> GetStringWithProgressAsync(string source, IAnsiConsole console, CancellationToken cancellationToken)
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

                using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    throw new CliException($"Authentication failed when requesting {uri}", ExitCodes.AuthError);
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new CliException($"Request to {uri} failed with status {(int)response.StatusCode}.", ExitCodes.NetworkError);
                }

                var total = response.Content.Headers.ContentLength ?? -1L;
                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var ms = new MemoryStream();
                var buffer = new byte[8192];
                long read = 0;
                
                string result = string.Empty;
                await console.Progress()
                    .AutoClear(true)
                    .Columns(
                        new TaskDescriptionColumn(),
                        new ProgressBarColumn(),
                        new PercentageColumn(),
                        new DownloadedColumn(),
                        new TransferSpeedColumn())
                    .StartAsync(async ctx =>
                    {
                        var task = ctx.AddTask("Downloading", maxValue: total > 0 ? total : 100);
                        int bytesRead;
                        while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
                        {
                            ms.Write(buffer, 0, bytesRead);
                            read += bytesRead;
                            if (total > 0) 
                            {
                                task.Value = read;
                            }
                            else 
                            {
                                task.IsIndeterminate = true;
                            }
                        }
                        task.StopTask();
                        result = System.Text.Encoding.UTF8.GetString(ms.ToArray());
                    });
                return result;
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
