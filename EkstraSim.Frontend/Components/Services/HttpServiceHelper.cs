using EkstraSim.Shared.Results;

namespace EkstraSim.Frontend.Components.Services;

public class HttpServiceHelper
{
    private readonly HttpClient _httpClient;

    public HttpServiceHelper(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<EkstraSimResult<T>> SendGetAsync<T>(string url)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<T>(url);
            return new EkstraSimResult<T>
            {
                Success = true,
                Data = response!
            };
        }
        catch (Exception ex)
        {
            return new EkstraSimResult<T>
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<EkstraSimResult<List<T>>> SendGetListAsync<T>(string url)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<T>>(url);
            return new EkstraSimResult<List<T>>
            {
                Success = true,
                Data = response ?? new List<T>()
            };
        }
        catch (Exception ex)
        {
            return new EkstraSimResult<List<T>>
            {
                Success = false,
                Data = new List<T>(),
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<EkstraSimResult<T>> SendPostAsync<T>(string url, object? request = null)
    {
        try
        {
            var response = request != null
                ? await _httpClient.PostAsJsonAsync(url, request)
                : await _httpClient.PostAsync(url, null);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<T>();
                return new EkstraSimResult<T>
                {
                    Success = true,
                    Data = data!
                };
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                return new EkstraSimResult<T>
                {
                    Success = false,
                    ErrorMessage = error
                };
            }
        }
        catch (Exception ex)
        {
            return new EkstraSimResult<T>
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<EkstraSimResult<T>> SendPutAsync<T>(string url, object? request = null)
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

    public async Task<EkstraSimResult<bool>> SendDeleteAsync(string url)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return new EkstraSimResult<bool>
                {
                    Success = true,
                    Data = true
                };
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                return new EkstraSimResult<bool>
                {
                    Success = false,
                    Data = false,
                    ErrorMessage = error
                };
            }
        }
        catch (Exception ex)
        {
            return new EkstraSimResult<bool>
            {
                Success = false,
                Data = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
