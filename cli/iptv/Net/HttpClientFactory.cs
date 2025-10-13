namespace Iptv.Cli.Net;

public static class HttpClientFactory
{
    public static HttpClient CreateDefault()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("IPTVGuideDog/0.1");
        return client;
    }
}
