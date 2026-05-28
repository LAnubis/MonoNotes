using System.Text.Json.Serialization;
using MonoNotes.Core.Models;

namespace MonoNotes.Core.Serialization
{
    // [JsonSerializable] 标签会让编译器在编译时自动生成高性能的序列化/反序列化代码
    // 这是实现 PublishAot 零反射运行的必备条件
    [JsonSerializable(typeof(List<NoteIndexItem>))]
    [JsonSerializable(typeof(NoteIndexItem))]
    public partial class MonoNotesJsonContext : JsonSerializerContext
    {
    }
}