using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace InvoiceApp.Services
{
    public class AresService
    {
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(12)
        };

        public AresService()
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("InvoiceApp/1.0 (+https://example)");
            _http.DefaultRequestHeaders.Accept.Clear();
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("cs,en;q=0.8");
        }

        public async Task<(string? Name, string? Address, string? City, string? Dic)> GetByIcoAsync(string ico)
        {
            if (string.IsNullOrWhiteSpace(ico))
                return (null, null, null, null);

            // jen číslice
            ico = new string(ico.Where(char.IsDigit).ToArray());

            // 1) funkční REST endpointy
            var urls = new[]
            {
                $"https://ares.gov.cz/ekonomicke-subjekty-v-be/rest/ekonomicke-subjekty/{ico}",
                $"https://ares.gov.cz/ekonomicke-subjekty-v-be/rest/ekonomicke-subjekty-standard/{ico}",
                // poslední pokus – někde bývá i „?ico=“
                $"https://ares.gov.cz/ekonomicke-subjekty-v-be/api/v2/ekonomicke-subjekty?ico={ico}"
            };

            foreach (var url in urls)
            {
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Get, url);
                    req.Headers.Accept.Clear();
                    req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    using var resp = await TrySendAsync(req);
                    if (resp is null || !resp.IsSuccessStatusCode) continue;

                    using var stream = await resp.Content.ReadAsStreamAsync();
                    using var doc = await JsonDocument.ParseAsync(stream);

                    if (TryParseAresJson(doc, out var name, out var address, out var city, out var dic))
                        return (name, address, city, dic);
                }
                catch
                {
                    // zkus další URL
                }
            }

            // 2) nouzově – oškrábeme HTML z veřejné stránky
            var scraped = await TryHtmlSearch(ico);
            if (scraped != null) return scraped.Value;

            return (null, null, null, null);
        }

        // -------- Retry helper -------------------------------------------------
        private static async Task<HttpResponseMessage?> TrySendAsync(HttpRequestMessage req)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    var resp = await _http.SendAsync(req);
                    if ((int)resp.StatusCode >= 500)
                    {
                        if (attempt < 2) await Task.Delay(300 * (attempt + 1));
                        else return resp;
                    }
                    else
                    {
                        return resp;
                    }
                }
                catch (TaskCanceledException) when (attempt < 2)
                {
                    await Task.Delay(300 * (attempt + 1));
                }
                catch (HttpRequestException) when (attempt < 2)
                {
                    await Task.Delay(300 * (attempt + 1));
                }
            }
            return null;
        }

        // -------- JSON parsing -------------------------------------------------

        private static bool TryParseAresJson(JsonDocument doc,
                                             out string? name, out string? address, out string? city, out string? dic)
        {
            name = address = city = dic = null;
            var root = doc.RootElement;

            // { "ekonomickeSubjekty": [ { ... } ] }  nebo  { "items": [ { ... } ] }  nebo přímo objekt
            JsonElement es;
            if (root.TryGetProperty("ekonomickeSubjekty", out var arr1) && arr1.ValueKind == JsonValueKind.Array && arr1.GetArrayLength() > 0)
                es = arr1[0];
            else if (root.TryGetProperty("items", out var arr2) && arr2.ValueKind == JsonValueKind.Array && arr2.GetArrayLength() > 0)
                es = arr2[0];
            else if (root.ValueKind == JsonValueKind.Object)
                es = root;
            else
                return false;

            name = TryGetString(es, "obchodniJmeno");
            dic = TryGetString(es, "dic");

            // ---- SIDLO ----
            if (es.TryGetProperty("sidlo", out var sidlo) && sidlo.ValueKind == JsonValueKind.Object)
            {
                // 1) nejdřív zkuste „textovaAdresa“ (často obsahuje už „Ulice 12, 12345 Obec“)
                var textAddr = TryGetString(sidlo, "textovaAdresa", "textovaAdresaCesky", "adresaText");
                if (!string.IsNullOrWhiteSpace(textAddr))
                {
                    // rozparsovat „Ulice 12, 12345 Obec“
                    var m = Regex.Match(textAddr, @"^\s*(.+?),\s*(\d{3}\s?\d{2})\s+(.+)$");
                    if (m.Success)
                    {
                        address = m.Groups[1].Value.Trim();
                        city = $"{m.Groups[2].Value.Trim()} {m.Groups[3].Value.Trim()}";
                    }
                    else
                    {
                        // aspoň něco – když to nepůjde rozdělit, necháme to celé jako adresu
                        address = textAddr.Trim();
                    }
                }

                // 2) když textovaAdresa není, slož to z jednotlivých polí
                if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(city))
                {
                    var ulice = TryGetString(sidlo, "uliceNazev", "nazevUlice", "ulice", "ulice_nazev");
                    var cd = TryGetAnyToString(sidlo, "cisloDomovni", "cisloPopisne");
                    var co = TryGetAnyToString(sidlo, "cisloOrientacni");
                    var psc = TryGetString(sidlo, "psc", "pscTxt");
                    var obec = TryGetString(sidlo, "obecNazev", "nazevObce", "obec");

                    string? cislo = !string.IsNullOrWhiteSpace(co) ? $"{cd}/{co}" : cd;

                    if (!string.IsNullOrWhiteSpace(ulice) || !string.IsNullOrWhiteSpace(cislo))
                        address = $"{ulice} {cislo}".Trim();

                    if (!string.IsNullOrWhiteSpace(psc) || !string.IsNullOrWhiteSpace(obec))
                        city = $"{psc} {obec}".Trim();
                }
            }

            return !string.IsNullOrWhiteSpace(name) ||
                   !string.IsNullOrWhiteSpace(address) ||
                   !string.IsNullOrWhiteSpace(city);
        }

        private static string? TryGetString(JsonElement obj, params string[] names)
        {
            foreach (var n in names)
                if (obj.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String)
                    return v.GetString();
            return null;
        }

        private static string? TryGetAnyToString(JsonElement obj, params string[] names)
        {
            foreach (var n in names)
                if (obj.TryGetProperty(n, out var v) && v.ValueKind != JsonValueKind.Null && v.ValueKind != JsonValueKind.Undefined)
                    return v.ToString().Trim('"');
            return null;
        }

        // -------- HTML fallback ------------------------------------------------

        private async Task<(string? Name, string? Address, string? City, string? Dic)?> TryHtmlSearch(string ico)
        {
            try
            {
                var url = $"https://ares.gov.cz/ekonomicke-subjekty?ico={ico}";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Accept.ParseAdd("text/html");

                using var resp = await TrySendAsync(req);
                if (resp is null || !resp.IsSuccessStatusCode) return null;

                var html = await resp.Content.ReadAsStringAsync();

                // Najdi řádek tabulky s tímto IČO a vytáhni název + sídlo
                var icoIdx = html.IndexOf(ico, StringComparison.Ordinal);
                if (icoIdx < 0) return null;

                var trStart = html.LastIndexOf("<tr", icoIdx, StringComparison.OrdinalIgnoreCase);
                var trEnd = html.IndexOf("</tr>", icoIdx, StringComparison.OrdinalIgnoreCase);
                if (trStart < 0 || trEnd < 0) return null;

                var tr = html.Substring(trStart, trEnd - trStart);

                // Vyber buňky <td>…</td>
                var tdRegex = new Regex(@"<td[^>]*>(.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                var tds = tdRegex.Matches(tr).Select(m => StripTags(m.Groups[1].Value).Trim()).ToList();
                if (tds.Count < 4) return null;

                // typicky: IČO | Obchodní název | Právní forma | Sídlo | …
                var name = tds.ElementAtOrDefault(1);
                var seat = tds.ElementAtOrDefault(3);

                string? address = null;
                string? city = null;

                if (!string.IsNullOrWhiteSpace(seat))
                {
                    // „Ulice 12, 12345 Obec“
                    var m = Regex.Match(seat, @"^\s*(.+?),\s*(\d{3}\s?\d{2})\s+(.+)$");
                    if (m.Success)
                    {
                        address = m.Groups[1].Value.Trim();
                        city = $"{m.Groups[2].Value.Trim()} {m.Groups[3].Value.Trim()}";
                    }
                    else
                    {
                        // nešlo rozdělit – necháme vše jako adresu
                        address = seat.Trim();
                    }
                }

                return (string.IsNullOrWhiteSpace(name) ? null : name,
                        string.IsNullOrWhiteSpace(address) ? null : address,
                        string.IsNullOrWhiteSpace(city) ? null : city,
                        null);
            }
            catch
            {
                return null;
            }
        }

        private static string StripTags(string html)
        {
            var text = Regex.Replace(html, "<.*?>", " ");
            text = System.Net.WebUtility.HtmlDecode(text);
            return Regex.Replace(text, @"\s+", " ").Trim();
        }
    }
}
