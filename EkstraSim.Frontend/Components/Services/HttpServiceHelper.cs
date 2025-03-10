using EkstraSim.Shared.Results;

namespace EkstraSim.Frontend.Components.Services;

public class HttpServiceHelper
{
    private readonly HttpClient _httpClient;

    public HttpServiceHelper(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<EkstraSimResult<T>> SendPutRequestAsync<T>(string url, object? request = null)
    {
        try
        {
            HttpResponseMessage response;
            if (request != null)
            {
                response = await _httpClient.PutAsJsonAsync(url, request);
            }
            else
            {
                response = await _httpClient.PutAsync(url, null);
            }

            if (response.IsSuccessStatusCode)
            {
                return new EkstraSimResult<T>
                {
                    Success = true,
                    Data = default
                };
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                return new EkstraSimResult<T>
                {
                    Success = false,
                    Data = default,
                    ErrorMessage = error
                };
            }
        }
        catch (Exception ex)
        {
            return new EkstraSimResult<T>
            {
                Success = false,
                Data = default,
                ErrorMessage = ex.Message
            };
        }
    }
}
