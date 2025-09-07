using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SkPlaygroundTests;

/// <summary>
/// Custom JsonConverter for polymorphic Pet deserialization
/// Uses the 'type' discriminator property to determine which concrete type to deserialize
/// </summary>
public class PolymorphicPetConverter : JsonConverter<Pet>
{
    public override bool CanWrite => false; // Only handle reading/deserialization
    
    public override Pet ReadJson(JsonReader reader, Type objectType, Pet? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null!;
        }

        // Load the JSON object
        var jsonObject = JObject.Load(reader);
        
        // Get the type discriminator
        var typeToken = jsonObject["type"];
        if (typeToken == null)
        {
            throw new JsonSerializationException("Missing 'type' discriminator property for polymorphic Pet deserialization");
        }
        
        var typeValue = typeToken.Value<string>();
        
        // Create the appropriate concrete type based on discriminator
        Pet target = typeValue?.ToLower() switch
        {
            "dog" => new Dog(),
            "cat" => new Cat(),
            _ => throw new JsonSerializationException($"Unknown pet type: {typeValue}")
        };
        
        // Populate the target object with the JSON data
        serializer.Populate(jsonObject.CreateReader(), target);
        
        return target;
    }
    
    public override void WriteJson(JsonWriter writer, Pet? value, JsonSerializer serializer)
    {
        throw new NotImplementedException("CanWrite is false, this method should not be called");
    }
}

/// <summary>
/// Utility class for creating JsonSerializerSettings with polymorphic support
/// </summary>
public static class PolymorphicJsonSettings
{
    /// <summary>
    /// Creates JsonSerializerSettings configured for polymorphic Pet deserialization
    /// </summary>
    public static JsonSerializerSettings CreateSettings()
    {
        return new JsonSerializerSettings
        {
            Converters = { new PolymorphicPetConverter() },
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore
        };
    }
}