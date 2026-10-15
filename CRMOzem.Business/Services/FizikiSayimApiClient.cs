using CRMOzem.Business.Interfaces;
using CRMOzem.Core.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Services
{
    public class FizikiSayimApiClient : IFizikiSayimApiClient
    {
        private readonly HttpClient _http;
        private readonly ILogger<FizikiSayimApiClient> _log;
        private readonly string _apiKey;

        public FizikiSayimApiClient(HttpClient http, IConfiguration config, ILogger<FizikiSayimApiClient> log)
        {
            _http = http;
            _log = log;

            _apiKey = config["StokSayimApi:ApiKey"]
                      ?? throw new InvalidOperationException("appsettings 'StokSayimApi:ApiKey' tanımlı değil.");

            var baseUrl = config["StokSayimApi:BaseUrl"]
                      ?? throw new InvalidOperationException("appsettings 'StokSayimApi:BaseUrl' tanımlı değil.");

            if (_http.BaseAddress == null)
                _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");

            _http.Timeout = TimeSpan.FromSeconds(60);
            _http.DefaultRequestHeaders.Remove("X-Api-Key");
            _http.DefaultRequestHeaders.Add("X-Api-Key", _apiKey);
        }

        public async Task<StokApiResponseDto?> GetStocksAsync(
            int? hospitalId = null,
            int? sayimNo = null,
            CancellationToken ct = default)
        {
            var qs = new List<string>();
            if (hospitalId.HasValue) qs.Add($"hospitalId={hospitalId.Value}");
            if (sayimNo.HasValue) qs.Add($"sayimNo={sayimNo.Value}");
            var query = qs.Count > 0 ? "?" + string.Join("&", qs) : "";

            var url = $"api/stock{query}";

            try
            {
                _log.LogInformation("Stok API çağrısı: {Url}", url);

                var response = await _http.GetAsync(url, ct);

                if (!response.IsSuccessStatusCode)
                {
                    var errBody = await response.Content.ReadAsStringAsync(ct);
                    _log.LogError("Stok API hatası {Status}: {Body}", (int)response.StatusCode, errBody);
                    return null;
                }

                var dto = await response.Content.ReadFromJsonAsync<StokApiResponseDto>(cancellationToken: ct);

                if (dto == null)
                {
                    _log.LogError("Stok API cevabı deserialize edilemedi (null).");
                    return null;
                }

                _log.LogInformation("Stok API cevabı: {Count} satır alındı.", dto.Count);
                return dto;
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                _log.LogError(ex, "Stok API timeout.");
                return null;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Stok API çağrısı başarısız.");
                return null;
            }
        }

        public async Task<bool> PingAsync(CancellationToken ct = default)
        {
            try
            {
                var response = await _http.GetAsync("api/stock/ping", ct);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Stok API ping başarısız.");
                return false;
            }
        }
    }
}
