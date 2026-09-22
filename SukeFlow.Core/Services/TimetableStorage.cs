using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SukeFlow.Core.Services;

/// <summary>课程表本地存储抽象（各平台实现不同：桌面/Android 用文件，WASM 用 localStorage）。</summary>
public interface ITimetableStorage
{
    /// <summary>读取已保存的 JSON；没有则返回 <see langword="null"/>。</summary>
    Task<string?> LoadAsync();

    /// <summary>保存 JSON。</summary>
    Task SaveAsync(string json);

    /// <summary>清除已保存的数据。</summary>
    Task ClearAsync();
}

/// <summary>
/// 基于文件的存储（桌面 / Android）。
/// 默认优先写入应用目录；若安装目录不可写（如只读安装位置），可配置降级路径。
/// </summary>
public sealed class FileTimetableStorage : ITimetableStorage
{
    /// <param name="filePath">主存储路径（建议放在应用目录内）；为空时退回用户目录默认路径。</param>
    /// <param name="fallbackPath">主路径不可读写时的降级路径（可为空）。</param>
    public FileTimetableStorage(string? filePath = null, string? fallbackPath = null)
    {
        FilePath = filePath ?? GetDefaultPath();
        FallbackPath = fallbackPath;
    }

    /// <summary>主存储文件完整路径。</summary>
    public string FilePath { get; }

    /// <summary>降级存储路径（无则为 <see langword="null"/>）。</summary>
    public string? FallbackPath { get; }

    public async Task<string?> LoadAsync()
    {
        if (File.Exists(FilePath))
        {
            return await File.ReadAllTextAsync(FilePath).ConfigureAwait(false);
        }

        if (FallbackPath is null || !File.Exists(FallbackPath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(FallbackPath).ConfigureAwait(false);

        // 旧位置有数据：迁移回应用目录（失败则继续使用降级路径）
        try
        {
            await WriteAsync(FilePath, json).ConfigureAwait(false);

            // 迁移成功：清理旧位置，保持用户目录清洁
            TryDelete(FallbackPath);
            TryDeleteEmptyDirectory(Path.GetDirectoryName(FallbackPath));
        }
        catch (Exception)
        {
            // ignored：主路径不可写时保持降级存储
        }

        return json;
    }

    public async Task SaveAsync(string json)
    {
        try
        {
            await WriteAsync(FilePath, json).ConfigureAwait(false);
        }
        catch (Exception) when (FallbackPath is not null)
        {
            await WriteAsync(FallbackPath, json).ConfigureAwait(false);
        }
    }

    public Task ClearAsync()
    {
        TryDelete(FilePath);
        if (FallbackPath is not null)
        {
            TryDelete(FallbackPath);
        }

        return Task.CompletedTask;
    }

    private static async Task WriteAsync(string path, string json)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, json).ConfigureAwait(false);
    }

    private static void TryDelete(string? path)
    {
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>目录为空时尝试删除（失败忽略）。</summary>
    private static void TryDeleteEmptyDirectory(string? directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return;
        }

        try
        {
            if (!Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }
        catch (Exception)
        {
            // ignored
        }
    }

    /// <summary>默认路径（用户目录），仅在未显式指定路径时使用。</summary>
    private static string GetDefaultPath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root, "SukeFlow", "timetable.json");
    }
}

/// <summary>跨平台存储入口；平台 Head 可在启动时替换实现（WASM → localStorage）。</summary>
public static class AppStorage
{
    /// <summary>课程表存储。</summary>
    public static ITimetableStorage Timetable { get; set; } = new FileTimetableStorage();
}
