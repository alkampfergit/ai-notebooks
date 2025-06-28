using System.Text.Json;

namespace DocExtraction.Examples;

/// <summary>
/// Simple in-memory vector store for similarity search using cosine similarity
/// </summary>
public class SimpleVectorStore
{
    private readonly List<VectorEntry> _vectors = new();

    public record VectorEntry(string Id, float[] Vector, Dictionary<string, object> Metadata);

    /// <summary>
    /// Add a vector with metadata to the store
    /// </summary>
    public void Add(string id, float[] vector, Dictionary<string, object>? metadata = null)
    {
        _vectors.Add(new VectorEntry(id, vector, metadata ?? new Dictionary<string, object>()));
    }

    /// <summary>
    /// Search for the top K most similar vectors using cosine similarity
    /// </summary>
    public List<(VectorEntry Entry, float Similarity)> Search(float[] queryVector, int topK = 5)
    {
        var results = _vectors
            .Select(entry => (Entry: entry, Similarity: CosineSimilarity(queryVector, entry.Vector)))
            .OrderByDescending(x => x.Similarity)
            .Take(topK)
            .ToList();

        return results;
    }

    /// <summary>
    /// Calculate cosine similarity between two vectors
    /// </summary>
    private static float CosineSimilarity(float[] vector1, float[] vector2)
    {
        if (vector1.Length != vector2.Length)
            throw new ArgumentException("Vectors must have the same length");

        float dotProduct = 0f;
        float magnitude1 = 0f;
        float magnitude2 = 0f;

        for (int i = 0; i < vector1.Length; i++)
        {
            dotProduct += vector1[i] * vector2[i];
            magnitude1 += vector1[i] * vector1[i];
            magnitude2 += vector2[i] * vector2[i];
        }

        magnitude1 = MathF.Sqrt(magnitude1);
        magnitude2 = MathF.Sqrt(magnitude2);

        if (magnitude1 == 0 || magnitude2 == 0)
            return 0f;

        return dotProduct / (magnitude1 * magnitude2);
    }

    /// <summary>
    /// Get the total number of vectors in the store
    /// </summary>
    public int Count => _vectors.Count;

    /// <summary>
    /// Clear all vectors from the store
    /// </summary>
    public void Clear() => _vectors.Clear();

    /// <summary>
    /// Save the vector store to a JSON file
    /// </summary>
    public void SaveToFile(string filePath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(_vectors, options);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Load the vector store from a JSON file
    /// </summary>
    public static SimpleVectorStore LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Vector store file not found: {filePath}");
        }

        var json = File.ReadAllText(filePath);
        var vectors = JsonSerializer.Deserialize<List<VectorEntry>>(json);

        var store = new SimpleVectorStore();
        if (vectors != null)
        {
            foreach (var entry in vectors)
            {
                store._vectors.Add(entry);
            }
        }

        return store;
    }
}
