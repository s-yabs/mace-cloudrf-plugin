using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Collections;

namespace CloudRFPlugin
{
    internal sealed class CloudRFClient
    {
        private readonly CloudRFSettings _settings;

        public CloudRFClient(CloudRFSettings settings)
        {
            _settings = settings;
        }

        public async Task<CloudRFAreaResult> RunAreaAsync(string requestJson, CancellationToken cancellationToken)
        {
            string url = _settings.BaseUrl.TrimEnd('/') + "/area";

            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Headers.Add("key", _settings.ApiKey);
                request.Content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException(
                            string.Format(CultureInfo.InvariantCulture, "CloudRF returned HTTP {0}: {1}", (int)response.StatusCode, body));
                    }

                    var json = JsonTools.DeserializeObject(body);
                    return new CloudRFAreaResult
                    {
                        RawJson = body,
                        Id = FirstString(json, "sid", "id"),
                        TiffUrl = FirstString(json, "tiff_4326", "tiff", "tiff_3857"),
                        KmzUrl = FirstString(json, "kmz"),
                        PngWgs84Url = FirstString(json, "PNG_WGS84", "png_wgs84"),
                        ArchiveUrl = FirstString(json, "url"),
                        LegendUrl = FirstString(json, "legend", "legend_url", "chart", "chart_url", "Chart image"),
                        LegendEntries = ParseLegendEntries(json)
                    };
                }
            }
        }

        public async Task<string> DownloadGeoTiffAsync(CloudRFAreaResult result, string baseFileName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(result.TiffUrl))
            {
                if (string.IsNullOrWhiteSpace(result.Id))
                {
                    throw new InvalidOperationException("CloudRF response did not include a GeoTIFF URL or archive id.");
                }

                result.TiffUrl = _settings.BaseUrl.TrimEnd('/') + "/archive/" + result.Id + "/tiff";
            }

            Directory.CreateDirectory(_settings.OutputDirectory);
            string outputPath = Path.Combine(_settings.OutputDirectory, SanitizeFileName(baseFileName) + ".tif");

            using (var client = new HttpClient())
            using (HttpResponseMessage response = await client.GetAsync(result.TiffUrl, cancellationToken).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode)
                {
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new InvalidOperationException(
                        string.Format(CultureInfo.InvariantCulture, "GeoTIFF download returned HTTP {0}: {1}", (int)response.StatusCode, body));
                }

                byte[] bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                File.WriteAllBytes(outputPath, bytes);
            }

            return outputPath;
        }

        public async Task<string> DownloadLegendAsync(CloudRFAreaResult result, string baseFileName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(result.LegendUrl))
            {
                return "";
            }

            if (!Uri.TryCreate(result.LegendUrl, UriKind.Absolute, out Uri legendUri))
            {
                return "";
            }

            Directory.CreateDirectory(_settings.OutputDirectory);
            string outputPath = Path.Combine(_settings.OutputDirectory, SanitizeFileName(baseFileName) + "-legend.png");

            using (var client = new HttpClient())
            using (HttpResponseMessage response = await client.GetAsync(legendUri, cancellationToken).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode)
                {
                    return "";
                }

                byte[] bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                File.WriteAllBytes(outputPath, bytes);
            }

            return outputPath;
        }

        private static string FirstString(Dictionary<string, object> json, params string[] keys)
        {
            foreach (string key in keys)
            {
                if (json.TryGetValue(key, out object value) && value is string)
                {
                    return Convert.ToString(value, CultureInfo.InvariantCulture);
                }
            }

            return "";
        }

        private static List<CloudRFLegendEntry> ParseLegendEntries(Dictionary<string, object> json)
        {
            var entries = new List<CloudRFLegendEntry>();

            if (!json.TryGetValue("key", out object keyValue) || !(keyValue is ArrayList keyList))
            {
                return entries;
            }

            foreach (object item in keyList)
            {
                if (!(item is Dictionary<string, object> entry))
                {
                    continue;
                }

                entries.Add(new CloudRFLegendEntry
                {
                    Label = FirstString(entry, "l"),
                    R = ToInt(entry, "r"),
                    G = ToInt(entry, "g"),
                    B = ToInt(entry, "b")
                });
            }

            return entries;
        }

        private static int ToInt(Dictionary<string, object> json, string key)
        {
            if (!json.TryGetValue(key, out object value) || value == null)
            {
                return 0;
            }

            return int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)
                ? Math.Max(0, Math.Min(255, result))
                : 0;
        }

        private static string SanitizeFileName(string text)
        {
            string value = string.IsNullOrWhiteSpace(text) ? "CloudRF" : text;

            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value;
        }
    }

    internal sealed class CloudRFAreaResult
    {
        public string RawJson { get; set; }
        public string Id { get; set; }
        public string TiffUrl { get; set; }
        public string KmzUrl { get; set; }
        public string PngWgs84Url { get; set; }
        public string ArchiveUrl { get; set; }
        public string LegendUrl { get; set; }
        public List<CloudRFLegendEntry> LegendEntries { get; set; } = new List<CloudRFLegendEntry>();
    }

    internal sealed class CloudRFLegendEntry
    {
        public string Label { get; set; }
        public int R { get; set; }
        public int G { get; set; }
        public int B { get; set; }
    }
}
