using System;
using System.Net.Http;
using System.Threading.Tasks;
using ClassicUO;

namespace ClassicUO.Dust765
{
    internal static class SplashReleaseChecker
    {
        private const string ReleasesApiUrl = "https://api.github.com/repos/dust765/ClassicUO/releases/latest";

        public static string TryGetUpdateNotice()
        {
            try
            {
                return TryGetUpdateNoticeAsync().GetAwaiter().GetResult();
            }
            catch
            {
                return null;
            }
        }

        private static async Task<string> TryGetUpdateNoticeAsync()
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Dust765-Client/" + (CUOEnviroment.Version?.ToString() ?? "0"));
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/vnd.github+json");

            using HttpResponseMessage response = await client.GetAsync(ReleasesApiUrl).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!TryExtractTagName(json, out string tag) || !TryParseReleaseVersion(tag, out Version remote))
            {
                return null;
            }

            Version local = CUOEnviroment.Version;
            if (local == null || remote <= local)
            {
                return null;
            }

            return $"New release available: {tag} (you have {local}) — see github.com/dust765/ClassicUO/releases";
        }

        private static bool TryExtractTagName(string json, out string tag)
        {
            tag = null;
            if (string.IsNullOrEmpty(json))
            {
                return false;
            }
            const string key = "\"tag_name\":";
            int i = json.IndexOf(key, StringComparison.Ordinal);
            if (i < 0)
            {
                return false;
            }
            i += key.Length;
            while (i < json.Length && char.IsWhiteSpace(json[i]))
            {
                i++;
            }
            if (i >= json.Length || json[i] != '"')
            {
                return false;
            }
            i++;
            int start = i;
            while (i < json.Length && json[i] != '"')
            {
                i++;
            }
            if (i <= start)
            {
                return false;
            }
            tag = json.Substring(start, i - start);
            return true;
        }

        private static bool TryParseReleaseVersion(string tag, out Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }
            string s = tag.Trim();
            if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                s = s.Substring(1);
            }
            int dash = s.IndexOf('-');
            if (dash > 0)
            {
                s = s.Substring(0, dash);
            }
            return Version.TryParse(s, out version);
        }
    }
}
