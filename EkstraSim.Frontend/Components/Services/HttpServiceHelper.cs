using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Resources;
using EkstraSim.Shared.Results;
using MudBlazor;
using System.Text.Json;

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
            var httpResponse = await _httpClient.GetAsync(url);
            var content = await httpResponse.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<EkstraSimResult<T>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null)
            {
                return new EkstraSimResult<T>
                {
                    Success = false,
                    Data = default,
                    ErrorMessage = SnackbarMessages.Error_Deserialize
                };
            }

            return result;
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

    public async Task<EkstraSimResult<T>> SendPostEnvelopeAsync<T>(string url, object? request = null)
    {
        return await SendWithEnvelopeAsync<T>(HttpMethod.Post, url, request);
    }

    public async Task<EkstraSimResult<T>> SendPutEnvelopeAsync<T>(string url, object? request = null)
    {
        return await SendWithEnvelopeAsync<T>(HttpMethod.Put, url, request);
    }

    private async Task<EkstraSimResult<T>> SendWithEnvelopeAsync<T>(HttpMethod method, string url, object? request)
    {
        try
        {
            using var message = new HttpRequestMessage(method, url);

            if (request != null)
            {
                message.Content = JsonContent.Create(request);
            }

            var httpResponse = await _httpClient.SendAsync(message);
            var content = await httpResponse.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<EkstraSimResult<T>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null)
            {
                return new EkstraSimResult<T>
                {
                    Success = false,
                    Data = default,
                    ErrorMessage = SnackbarMessages.Error_Deserialize
                };
            }

            return result;
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
