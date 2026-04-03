using Microsoft.JSInterop;

namespace Client.Services;

public class LocalStorageService
{
    private readonly IJSRuntime _js;

    public LocalStorageService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<string?> GetAsync(string key)
    {
        return await _js.InvokeAsync<string?>("localStorageInterop.get", key);
    }

    public async Task SetAsync(string key, string value)
    {
        await _js.InvokeVoidAsync("localStorageInterop.set", key, value);
    }

    public async Task RemoveAsync(string key)
    {
        await _js.InvokeVoidAsync("localStorageInterop.remove", key);
    }
}
