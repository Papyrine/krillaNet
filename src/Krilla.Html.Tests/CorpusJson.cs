/// <summary>Serialization for the reference geometry files.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(List<BoxGeometry>))]
partial class CorpusJson : JsonSerializerContext;