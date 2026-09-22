using System;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;
using SukeFlow.Core.Services;

namespace SukeFlow.Browser;

/// <summary>WASM 平台的课程表存储：浏览器 localStorage。</summary>
internal sealed partial class LocalStorageTimetableStorage : ITimetableStorage
{
    private const string Key = "sukeflow.timetable.json";

    public Task<string?> LoadAsync()
    {
        try
        {
            return Task.FromResult(LocalStorage.Get(Key));
        }
        catch (Exception)
        {
            return Task.FromResult<string?>(null);
        }
    }

    public Task SaveAsync(string json)
    {
        try
        {
            LocalStorage.Set(Key, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("浏览器本地存储不可用（可能处于隐私模式，或存储空间已满）", ex);
        }

        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        try
        {
            LocalStorage.Remove(Key);
        }
        catch (Exception)
        {
            // ignored
        }

        return Task.CompletedTask;
    }

    private static partial class LocalStorage
    {
        [JSImport("globalThis.localStorage.getItem")]
        internal static partial string? Get(string key);

        [JSImport("globalThis.localStorage.setItem")]
        internal static partial void Set(string key, string value);

        [JSImport("globalThis.localStorage.removeItem")]
        internal static partial void Remove(string key);
    }
}
