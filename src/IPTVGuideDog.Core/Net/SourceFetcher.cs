using Spectre.Console;

namespace IPTVGuideDog.Core.Net;

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
                    await _diagnostics.WriteLineAsync($"Downloading {UrlRedactor.RedactUrl(uri)}...");
                }

                using var response = await _httpClient.GetAsync(uri, cancellationToken);
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    throw new CliException($"Authentication failed when requesting {UrlRedactor.RedactUrl(uri)}", ExitCodes.AuthError);
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new CliException($"Request to {UrlRedactor.RedactUrl(uri)} failed with status {(int)response.StatusCode}.", ExitCodes.NetworkError);
                }

                return await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (CliException)
            {
                throw;
            }
            catch (TaskCanceledException ex)
            {
                throw new CliException($"Request to {UrlRedactor.RedactUrl(uri)} timed out: {ex.Message}", ExitCodes.NetworkError);
            }
            catch (HttpRequestException ex)
            {
                throw new CliException($"Request to {UrlRedactor.RedactUrl(uri)} failed: {ex.Message}", ExitCodes.NetworkError);
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
                    await _diagnostics.WriteLineAsync($"Downloading {UrlRedactor.RedactUrl(uri)}...");
                }

                using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    throw new CliException($"Authentication failed when requesting {UrlRedactor.RedactUrl(uri)}", ExitCodes.AuthError);
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new CliException($"Request to {UrlRedactor.RedactUrl(uri)} failed with status {(int)response.StatusCode}.", ExitCodes.NetworkError);
                }

                var total = response.Content.Headers.ContentLength ?? -1L;
                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var ms = new MemoryStream();
                var buffer = new byte[8192];
                long read = 0;

                string result = string.Empty;
                await console.Progress()
                    .AutoClear(true)
                    .Columns(CreateColumns(total))
                    .StartAsync(async ctx =>
                    {
                        var task = ctx.AddTask("Downloading", maxValue: total > 0 ? total : double.MaxValue);
                        if (total <= 0)
                        {
                            task.IsIndeterminate = true;
                            task.Description = "Downloading (0 B)";
                        }

                        int bytesRead;
                        while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
                        {
                            ms.Write(buffer, 0, bytesRead);
                            read += bytesRead;

                            task.Increment(bytesRead);

                            if (total > 0)
                            {
                                task.Value = read;
                            }
                            else
                            {
                                task.Description = $"Downloading ({FormatBytes(read)})";
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
                throw new CliException($"Request to {UrlRedactor.RedactUrl(uri)} timed out: {ex.Message}", ExitCodes.NetworkError);
            }
            catch (HttpRequestException ex)
            {
                throw new CliException($"Request to {UrlRedactor.RedactUrl(uri)} failed: {ex.Message}", ExitCodes.NetworkError);
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

    private static ProgressColumn[] CreateColumns(long total)
    {
        if (total > 0)
        {
            return
            [
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new DownloadedColumn(),
                new TransferSpeedColumn()
            ];
        }

        return
        [
            new TaskDescriptionColumn(),
            new SpinnerColumn(),
            new TransferSpeedColumn()
        ];
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        string[] units = ["KB", "MB", "GB", "TB"];
        double value = bytes;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }
        return $"{value:0.##} {units[unitIndex]}";
    }
}
