using CRMOzem.Business.Interfaces;
using CRMOzem.Core.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CRMOzem.Business.Services
{
    public class SiparisApiClient : ISiparisApiClient
    {
        private readonly HttpClient _http;
        private readonly ILogger<SiparisApiClient> _log;
        private readonly string _apiKey;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        public SiparisApiClient(HttpClient http, IConfiguration config, ILogger<SiparisApiClient> log)
        {
            _http = http;
            _log = log;

            _apiKey = config["SiparisApi:ApiKey"]
                      ?? throw new InvalidOperationException("appsettings 'SiparisApi:ApiKey' tanımlı değil.");

            var baseUrl = config["SiparisApi:BaseUrl"]
                      ?? throw new InvalidOperationException("appsettings 'SiparisApi:BaseUrl' tanımlı değil.");

            if (_http.BaseAddress == null)
                _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");

            _http.Timeout = TimeSpan.FromSeconds(120);  // 500+ order dönerse büyük JSON olabilir
            _http.DefaultRequestHeaders.Remove("X-Api-Key");
            _http.DefaultRequestHeaders.Add("X-Api-Key", _apiKey);
        }

        public async Task<SiparisApiResponseDto?> GetOrdersAsync(
            int status = 1,
            int sinceId = 0,
            int limit = 200,
            CancellationToken ct = default)
        {
            var qs = new List<string>
            {
                $"status={status}",
                $"limit={limit}"
            };
            if (sinceId > 0) qs.Add($"sinceId={sinceId}");
            var url = $"admin/api/orders.php?{string.Join("&", qs)}";

            try
            {
                _log.LogInformation("Sipariş API çağrısı: {Url}", url);

                var response = await _http.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode)
                {
                    var errBody = await response.Content.ReadAsStringAsync(ct);
                    _log.LogError("Sipariş API hatası {Status}: {Body}", (int)response.StatusCode, errBody);
                    return null;
                }

                var raw = await response.Content.ReadAsStringAsync(ct);

                // PHP başındaki "Deprecated: mysql_connect..." uyarısını temizle
                // JSON'un başına kadar olan HTML/warning çıktısını at
                var jsonStart = raw.IndexOf('{');
                if (jsonStart < 0)
                {
                    _log.LogError("Sipariş API cevabında JSON bulunamadı. Ham cevap: {Raw}", raw.Substring(0, Math.Min(raw.Length, 500)));
                    return null;
                }
                if (jsonStart > 0)
                {
                    _log.LogWarning("Sipariş API cevabında JSON öncesi {N} karakter atlandı (muhtemelen PHP warning).", jsonStart);
                    raw = raw.Substring(jsonStart);
                }

                var dto = JsonSerializer.Deserialize<SiparisApiResponseDto>(raw, JsonOpts);
                if (dto == null)
                {
                    _log.LogError("Sipariş API cevabı deserialize edilemedi.");
                    return null;
                }

                _log.LogInformation("Sipariş API cevabı: {Count} sipariş alındı.", dto.Count);
                return dto;
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                _log.LogError(ex, "Sipariş API timeout.");
                return null;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Sipariş API çağrısı başarısız.");
                return null;
            }
        }
    }
}
