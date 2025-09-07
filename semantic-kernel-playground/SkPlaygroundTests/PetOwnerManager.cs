using NJsonSchema;
using NJsonSchema.Generation;
using NJsonSchema.NewtonsoftJson.Generation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.ComponentModel.DataAnnotations;

namespace SkPlaygroundTests;

/// <summary>
/// Unified manager for PetOwner schema generation and polymorphic deserialization.
/// Allows configuring which derived Pet types to include and provides methods for both
/// OpenAI-compatible schema generation and JSON deserialization with polymorphic support.
/// </summary>
public class PetOwnerManager
{
    private readonly List<Type> _derivedPetTypes = new();
    private readonly string _discriminatorProperty;
    private readonly JsonSerializerSettings _jsonSettings;

    /// <summary>
    /// Creates a new PetOwnerManager instance
    /// </summary>
    /// <param name="discriminatorProperty">Name of the discriminator property (default: "type")</param>
    public PetOwnerManager(string discriminatorProperty = "type")
    {
        _discriminatorProperty = discriminatorProperty;
        _jsonSettings = new JsonSerializerSettings
        {
            Converters = { new PetOwnerPolymorphicConverter(this) },
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore
        };
    }

    /// <summary>
    /// Adds a derived Pet type to be included in schema generation and deserialization
    /// </summary>
    /// <typeparam name="T">The derived Pet type to add (must inherit from Pet)</typeparam>
    /// <returns>This instance for method chaining</returns>
    public PetOwnerManager AddDerivedType<T>() where T : Pet
    {
        var type = typeof(T);
        if (!_derivedPetTypes.Contains(type))
        {
            _derivedPetTypes.Add(type);
        }
        return this;
    }

    /// <summary>
    /// Adds multiple derived Pet types to be included in schema generation and deserialization
    /// </summary>
    /// <param name="derivedTypes">Array of derived Pet types to add</param>
    /// <returns>This instance for method chaining</returns>
    public PetOwnerManager AddDerivedTypes(params Type[] derivedTypes)
    {
        foreach (var type in derivedTypes)
        {
            if (!typeof(Pet).IsAssignableFrom(type))
            {
                throw new ArgumentException($"Type {type.Name} must inherit from Pet", nameof(derivedTypes));
            }
            
            if (!_derivedPetTypes.Contains(type))
            {
                _derivedPetTypes.Add(type);
            }
        }
        return this;
    }

    /// <summary>
    /// Generates an OpenAI-compatible JSON schema for PetOwner with the configured derived Pet types
    /// </summary>
    /// <returns>JSON schema string compatible with OpenAI structured output</returns>
    public string GenerateSchema()
    {
        if (_derivedPetTypes.Count == 0)
        {
            throw new InvalidOperationException("No derived Pet types have been added. Use AddDerivedType<T>() or AddDerivedTypes() first.");
        }

        return PolymorphicSchemaGenerator.GeneratePolymorphicJsonSchema<PetOwner>(
            _derivedPetTypes.ToArray(),
            _discriminatorProperty
        );
    }

    /// <summary>
    /// Deserializes JSON into a PetOwner object with polymorphic Pet support
    /// </summary>
    /// <param name="json">JSON string to deserialize</param>
    /// <returns>Deserialized PetOwner with correctly typed Pet property</returns>
    public PetOwner? DeserializeFromJson(string json)
    {
        if (_derivedPetTypes.Count == 0)
        {
            throw new InvalidOperationException("No derived Pet types have been added. Use AddDerivedType<T>() or AddDerivedTypes() first.");
        }

        return JsonConvert.DeserializeObject<PetOwner>(json, _jsonSettings);
    }

    /// <summary>
    /// Gets the discriminator value for a given Pet type (converts PascalCase to snake_case)
    /// </summary>
    /// <param name="type">The Pet type</param>
    /// <returns>Discriminator value string</returns>
    internal string GetDiscriminatorValue(Type type)
    {
        // Convert PascalCase to snake_case: "SendEmail" -> "send_email"
        var name = type.Name;
        var result = string.Empty;
        
        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]))
            {
                result += "_";
            }
            result += char.ToLower(name[i]);
        }
        
        return result;
    }

    /// <summary>
    /// Gets all configured derived Pet types
    /// </summary>
    internal IReadOnlyList<Type> DerivedPetTypes => _derivedPetTypes.AsReadOnly();

    /// <summary>
    /// Gets the configured discriminator property name
    /// </summary>
    internal string DiscriminatorProperty => _discriminatorProperty;
}

/// <summary>
/// Custom JsonConverter for polymorphic Pet deserialization within PetOwnerManager
/// </summary>
internal class PetOwnerPolymorphicConverter : JsonConverter<Pet>
{
    private readonly PetOwnerManager _manager;

    public PetOwnerPolymorphicConverter(PetOwnerManager manager)
    {
        _manager = manager;
    }

    public override bool CanWrite => false; // Only handle reading/deserialization
    
    public override Pet ReadJson(JsonReader reader, Type objectType, Pet? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null!;
        }

        // Load the JSON object
        var jsonObject = JObject.Load(reader);
        
        // Get the discriminator value
        var discriminatorToken = jsonObject[_manager.DiscriminatorProperty];
        if (discriminatorToken == null)
        {
            throw new JsonSerializationException($"Missing '{_manager.DiscriminatorProperty}' discriminator property for polymorphic Pet deserialization");
        }
        
        var discriminatorValue = discriminatorToken.Value<string>()?.ToLower();
        
        // Find the matching Pet type based on discriminator
        Type? targetType = null;
        foreach (var petType in _manager.DerivedPetTypes)
        {
            var expectedDiscriminator = _manager.GetDiscriminatorValue(petType);
            if (string.Equals(discriminatorValue, expectedDiscriminator, StringComparison.OrdinalIgnoreCase))
            {
                targetType = petType;
                break;
            }
        }

        if (targetType == null)
        {
            var availableTypes = string.Join(", ", _manager.DerivedPetTypes.Select(t => _manager.GetDiscriminatorValue(t)));
            throw new JsonSerializationException($"Unknown pet type: '{discriminatorValue}'. Available types: {availableTypes}");
        }

        // Create and populate the target object
        var target = (Pet)Activator.CreateInstance(targetType)!;
        serializer.Populate(jsonObject.CreateReader(), target);
        
        return target;
    }
    
    public override void WriteJson(JsonWriter writer, Pet? value, JsonSerializer serializer)
    {
        throw new NotImplementedException("CanWrite is false, this method should not be called");
    }
}