using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using SukeFlow.Core.Models;

namespace SukeFlow.Core.Services;

/// <summary>课程表 JSON 序列化上下文（源生成，兼容 WASM/AOT 裁剪）。</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Timetable))]
public sealed partial class TimetableJsonContext : JsonSerializerContext
{
}

/// <summary>课程表 JSON 序列化（用于本地持久化）。</summary>
public static class TimetableSerializer
{
    /// <summary>序列化为 JSON 文本。</summary>
    public static string Serialize(Timetable timetable) =>
        JsonSerializer.Serialize(timetable, Context.Timetable);

    /// <summary>从 JSON 文本反序列化；失败返回 <see langword="null"/>。</summary>
    public static Timetable? Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, Context.Timetable);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>使用不转义中文的编码器，方便查看本地保存的 JSON。</summary>
    private static readonly JsonSerializerOptions Options = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly TimetableJsonContext Context = new(Options);
}
