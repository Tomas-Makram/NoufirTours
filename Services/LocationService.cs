using System;
using System.Globalization;
using System.Net;
using System.Text.Json;

namespace NoufirTours.Services
{
    // Records
    public sealed record LocationResult(bool IsValid, string? Error, decimal? Latitude, decimal? Longitude, string? Address)
    {
        public static LocationResult Empty(string? address) => new(true, null, null, null, address);
        public static LocationResult Fail(string error) => new(false, error, null, null, null);
        public static LocationResult Ok(decimal lat, decimal lng, string? address) => new(true, null, lat, lng, address);
    }

    public sealed record IpLocationResult(decimal Latitude, decimal Longitude, string? AddressFromIp);

    public sealed record AddressGeoResult(decimal Latitude, decimal Longitude, string? NormalizedAddress);

    public interface ILocationService
    {
        // Validate lat/lng strings and normalize address (max 255). If lat/lng missing => OK
        LocationResult ExtractAndValidateFromForm(string? latitude, string? longitude, string? address);

        // IP -> approximate location (city/region/country)
        Task<IpLocationResult?> GetLocationFromIpAsync(string ip, CancellationToken ct = default);

        // Address hint -> lat/lng (geocoding)
        Task<AddressGeoResult?> GetLocationFromAddressAsync(string address, CancellationToken ct = default);

        // lat/lng -> address
        Task<string?> ReverseGeocodeAsync(decimal latitude, decimal longitude, CancellationToken ct = default);

        // Extracts best client IP from request (proxy aware) and returns normalized public IP if possible
        string? GetClientPublicIp(HttpContext httpContext);

        // Search by Distance and Location
        IReadOnlyList<T> SortByDistance<T>(IEnumerable<T> items, decimal userLat, decimal userLng, Func<T, decimal?> latSelector, Func<T, decimal?> lngSelector, bool ascending);

        IReadOnlyList<T> FilterWithinKm<T>(IEnumerable<T> items, decimal userLat, decimal userLng, decimal maxKm, Func<T, decimal?> latSelector, Func<T, decimal?> lngSelector);

    }

    public static class GeoDistance
    {
        // Haversine distance (KM)
        public static double DistanceKm(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
        {
            double R = 6371.0;
            double dLat = ToRad((double)(lat2 - lat1));
            double dLon = ToRad((double)(lon2 - lon1));

            double a =
                Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRad((double)lat1)) * Math.Cos(ToRad((double)lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private static double ToRad(double deg) => deg * (Math.PI / 180.0);
    }

    public sealed class LocationService : ILocationService
    {
        private readonly HttpClient _http;
        
        // api Connection
        private string UserAgent;

        public LocationService(HttpClient http, IConfiguration configuration)
        {
            _http = http;
            UserAgent = $"{configuration["Info:Name"]}/{configuration["Info:Version"]} ({configuration["Info:Website"]}; {configuration["Info:Email"]})";
        }

        // Check if lan/lon and information location is valid and true or not (Get input data from map)
        public LocationResult ExtractAndValidateFromForm(string? latitude, string? longitude, string? address)
        {
            latitude = latitude?.Trim();
            longitude = longitude?.Trim();
            address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();

            if (address != null && address.Length > 255)
                address = address[..255];

            // lat/lng not provided => not an error (Controller will decide: map pin? IP? address?)
            if (string.IsNullOrWhiteSpace(latitude) && string.IsNullOrWhiteSpace(longitude))
                return LocationResult.Empty(address);

            // must be both or none
            if (string.IsNullOrWhiteSpace(latitude) || string.IsNullOrWhiteSpace(longitude))
                return LocationResult.Fail("Latitude and Longitude must be provided together.");

            if (!decimal.TryParse(latitude, NumberStyles.Any, CultureInfo.InvariantCulture, out var lat) ||
                !decimal.TryParse(longitude, NumberStyles.Any, CultureInfo.InvariantCulture, out var lng))
            {
                return LocationResult.Fail("Invalid location coordinates.");
            }

            if (lat < -90 || lat > 90)
                return LocationResult.Fail("Latitude must be between -90 and 90.");

            if (lng < -180 || lng > 180)
                return LocationResult.Fail("Longitude must be between -180 and 180.");

            return LocationResult.Ok(lat, lng, address);
        }

        // Get Lat/Lon and information location From IP Address
        public async Task<IpLocationResult?> GetLocationFromIpAsync(string ip, CancellationToken ct = default)
        {
            ip = NormalizeIp(ip)!;
            if (string.IsNullOrWhiteSpace(ip) || string.IsNullOrEmpty(ip))
                return null;

            // ipapi.co: approximate
            var url = $"https://ipapi.co/{Uri.EscapeDataString(ip)}/json/";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("Accept", "application/json");
            req.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

            using var res = await _http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
                return null;

            var json = await res.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("error", out var errEl) && errEl.ValueKind == JsonValueKind.True)
                    return null;

                var latStr = root.TryGetProperty("latitude", out var latEl) ? latEl.ToString() : null;
                var lngStr = root.TryGetProperty("longitude", out var lngEl) ? lngEl.ToString() : null;

                if (!decimal.TryParse(latStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var lat) ||
                    !decimal.TryParse(lngStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var lng))
                {
                    return null;
                }

                var city = root.TryGetProperty("city", out var c) ? c.GetString() : null;
                var region = root.TryGetProperty("region", out var r) ? r.GetString() : null;
                var country = root.TryGetProperty("country_name", out var cn) ? cn.GetString() : null;

                var composed = string.Join(", ", new[] { city, region, country }.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (!string.IsNullOrWhiteSpace(composed) && composed.Length > 255)
                    composed = composed[..255];

                return new IpLocationResult(lat, lng, string.IsNullOrWhiteSpace(composed) ? null : composed);
            }
            catch
            {
                return null;
            }
        }

        // Convert From information location to Lat/Lng
        public async Task<AddressGeoResult?> GetLocationFromAddressAsync(string address, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(address))
                return null;

            address = address.Trim();

            // Nominatim search
            var url = $"https://nominatim.openstreetmap.org/search?format=jsonv2&q={Uri.EscapeDataString(address)}&limit=1";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("Accept", "application/json");
            req.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

            using var res = await _http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
                return null;

            var json = await res.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
                    return null;

                var first = root[0];

                var latStr = first.TryGetProperty("lat", out var latEl) ? latEl.GetString() : null;
                var lngStr = first.TryGetProperty("lon", out var lngEl) ? lngEl.GetString() : null;

                if (!decimal.TryParse(latStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var lat) ||
                    !decimal.TryParse(lngStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var lng))
                {
                    return null;
                }

                var display = first.TryGetProperty("display_name", out var d) ? d.GetString() : null;
                if (!string.IsNullOrWhiteSpace(display) && display.Length > 255)
                    display = display[..255];

                return new AddressGeoResult(lat, lng, display);
            }
            catch
            {
                return null;
            }
        }

        // Convert From Lat/Lng to information location 
        public async Task<string?> ReverseGeocodeAsync(decimal latitude, decimal longitude, CancellationToken ct = default)
        {
            var latStr = latitude.ToString(CultureInfo.InvariantCulture);
            var lngStr = longitude.ToString(CultureInfo.InvariantCulture);

            var url = $"https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat={Uri.EscapeDataString(latStr)}&lon={Uri.EscapeDataString(lngStr)}";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("Accept", "application/json");
            req.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

            using var res = await _http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
                return null;

            var json = await res.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var display = root.TryGetProperty("display_name", out var d) ? d.GetString() : null;
                if (!string.IsNullOrWhiteSpace(display) && display.Length > 255)
                    display = display[..255];

                return string.IsNullOrWhiteSpace(display) ? null : display;
            }
            catch
            {
                return null;
            }
        }

        // Get ip Address
        public string? GetClientPublicIp(HttpContext httpContext)
        {
            // Cloudflare
            var cf = httpContext.Request.Headers["CF-Connecting-IP"].FirstOrDefault();
            var cfIp = NormalizeIp(cf);
            if (!string.IsNullOrWhiteSpace(cfIp) && IsPublicIp(cfIp))
                return cfIp;

            // X-Forwarded-For: "client, proxy1, proxy2"
            var xff = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(xff))
            {
                string? firstValid = null;

                foreach (var part in xff.Split(','))
                {
                    var candidate = NormalizeIp(part);
                    if (string.IsNullOrWhiteSpace(candidate))
                        continue;

                    firstValid ??= candidate;

                    if (IsPublicIp(candidate))
                        return candidate;
                }

                if (!string.IsNullOrWhiteSpace(firstValid))
                    return firstValid;
            }

            // X-Real-IP
            var xReal = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
            var real = NormalizeIp(xReal);
            if (!string.IsNullOrWhiteSpace(real) && IsPublicIp(real))
                return real;

            // RemoteIpAddress
            var ip = httpContext.Connection.RemoteIpAddress?.ToString();
            ip = NormalizeIp(ip);
            if (!string.IsNullOrWhiteSpace(ip) && IsPublicIp(ip))
                return ip;

            // fallback (private ip)
            return ip;
        }

        // Get IP cleaning
        private static string? NormalizeIp(string? ip)
        {
            if (string.IsNullOrWhiteSpace(ip))
                return null;

            ip = ip.Trim();

            if (ip.StartsWith("::ffff:", StringComparison.OrdinalIgnoreCase))
                ip = ip["::ffff:".Length..];

            if (ip == "127.0.0.1" || ip == "::1")
                return null;

            // IPv4:PORT
            if (ip.Contains('.') && ip.Count(c => c == ':') == 1)
            {
                var lastColon = ip.LastIndexOf(':');
                if (lastColon > 0)
                    ip = ip[..lastColon];
            }

            // Validate
            if (!IPAddress.TryParse(ip, out _))
                return null;

            return ip;
        }

        // Check if IP is Public or Private (on the network)
        private static bool IsPublicIp(string ip)
        {
            if (!IPAddress.TryParse(ip, out var address))
                return false;

            if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)
                    return false;

                return true;
            }

            var b = address.GetAddressBytes();

            if (b[0] == 10) return false;                              // 10.0.0.0/8
            if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return false; // 172.16.0.0/12
            if (b[0] == 192 && b[1] == 168) return false;              // 192.168.0.0/16
            if (b[0] == 169 && b[1] == 254) return false;              // 169.254.0.0/16

            return true;
        }

        // Search by Distance and Location
        public IReadOnlyList<T> SortByDistance<T>(IEnumerable<T> items, decimal userLat, decimal userLng, Func<T, decimal?> latSelector, Func<T, decimal?> lngSelector, bool ascending)
        {
            // Elements with coordinates are sorted first; elements without coordinates are sorted last
            var withDeist = items.Select(x =>
            {
                var lat = latSelector(x);
                var lng = lngSelector(x);
                double? d = null;

                if (lat.HasValue && lng.HasValue)
                    d = GeoDistance.DistanceKm(userLat, userLng, lat.Value, lng.Value);

                return new { Item = x, Dist = d };
            });

            var ordered = ascending
                ? withDeist.OrderBy(x => x.Dist.HasValue ? 0 : 1).ThenBy(x => x.Dist)
                : withDeist.OrderBy(x => x.Dist.HasValue ? 0 : 1).ThenByDescending(x => x.Dist);

            return ordered.Select(x => x.Item).ToList();
        }

        public IReadOnlyList<T> FilterWithinKm<T>(IEnumerable<T> items, decimal userLat, decimal userLng, decimal maxKm, Func<T, decimal?> latSelector, Func<T, decimal?> lngSelector)
        {
            return items.Where(x =>
            {
                var lat = latSelector(x);
                var lng = lngSelector(x);
                if (!lat.HasValue || !lng.HasValue) return false;

                var d = GeoDistance.DistanceKm(userLat, userLng, lat.Value, lng.Value);
                return d <= (double)maxKm;
            }).ToList();
        }
    }
}