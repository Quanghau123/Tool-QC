using System.Text.Json;
namespace AutoTest.Http;
public static class SimpleJsonSchema
{
    public static void Validate(JsonElement value,JsonElement schema,string path)
    {
        if(schema.TryGetProperty("type",out var type)&&!Matches(value,type.GetString()!))throw new InvalidOperationException($"JSON Schema: {path} không phải kiểu {type.GetString()}.");
        if(schema.TryGetProperty("required",out var required)&&value.ValueKind==JsonValueKind.Object)foreach(var item in required.EnumerateArray())if(!value.TryGetProperty(item.GetString()!,out _))throw new InvalidOperationException($"JSON Schema: thiếu trường bắt buộc {path}.{item.GetString()}.");
        if(schema.TryGetProperty("properties",out var properties)&&value.ValueKind==JsonValueKind.Object)foreach(var property in properties.EnumerateObject())if(value.TryGetProperty(property.Name,out var child))Validate(child,property.Value,$"{path}.{property.Name}");
        if(schema.TryGetProperty("items",out var items)&&value.ValueKind==JsonValueKind.Array){int index=0;foreach(var child in value.EnumerateArray())Validate(child,items,$"{path}[{index++}]");}
        if(schema.TryGetProperty("minItems",out var minItems)&&value.ValueKind==JsonValueKind.Array&&value.GetArrayLength()<minItems.GetInt32())throw new InvalidOperationException($"JSON Schema: {path} có ít phần tử hơn minItems.");
        if(schema.TryGetProperty("enum",out var values)&&!values.EnumerateArray().Any(candidate=>candidate.GetRawText()==value.GetRawText()))throw new InvalidOperationException($"JSON Schema: {path} không thuộc enum.");
    }
    static bool Matches(JsonElement value,string type)=>type.ToLowerInvariant() switch{"object"=>value.ValueKind==JsonValueKind.Object,"array"=>value.ValueKind==JsonValueKind.Array,"string"=>value.ValueKind==JsonValueKind.String,"integer"=>value.ValueKind==JsonValueKind.Number&&value.TryGetInt64(out _),"number"=>value.ValueKind==JsonValueKind.Number,"boolean"=>value.ValueKind is JsonValueKind.True or JsonValueKind.False,"null"=>value.ValueKind==JsonValueKind.Null,_=>throw new InvalidDataException($"JSON Schema type chưa hỗ trợ: {type}")};
}
