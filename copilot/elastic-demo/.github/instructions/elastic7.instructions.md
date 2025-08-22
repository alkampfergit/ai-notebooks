---
applyTo: '**/*.es7.cs'
---
This files interact with Elasticsearch 7 with NEST 7 drivers. When you need to operate with elasticsearch you can manage the creation of the schema of the index with this code.

We prefer doing the mapping NOT with attribute, so the original class remains POCO.

Class that operates with Elasticsearch usually accepts a Uri on the constructor and will have an InitAsync method to perform all initialization stuff (check indexs, mapping etc)

# NEST 7 Driver Mapping Comprehensive Guide

Verify that the project depends from Nest package with major version 7.

## Standard Mapping Examples

### 1. Text and Keyword Mapping

You usually create an index and contextually create a mapping for a specific POCO class

```csharp
// Text and Keyword Mapping
client.Indices.Create("my-index", c => c
    .Map<Product>(m => m
        .Properties(p => p
            .Text(t => t
                .Name(n => n.Title)
                .Analyzer("standard")
                .Fields(f => f
                    .Keyword(k => k.Name("keyword"))
                )
            )
            .Keyword(k => k
                .Name(n => n.Category)
            )
        )
    )
);
```

You can verify if a current index exists so you create the index only if the index is missing

```csharp
var existsResponse = client.Indices.Exists("my-index-name");
bool exists = existsResponse.Exists;
```

### 2. Numeric Data Types Mapping

```csharp
// Numeric Data Types Mapping
client.Indices.Create("my-index", c => c
    .Map<Product>(m => m
        .Properties(p => p
            .Number(n => n
                .Name(x => x.Price)
                .Type(NumberType.Double)
            )
            .Number(n => n
                .Name(x => x.Quantity)
                .Type(NumberType.Integer)
            )
            .Number(n => n
                .Name(x => x.Rating)
                .Type(NumberType.Float)
            )
            .Number(n => n
                .Name(x => x.Id)
                .Type(NumberType.Long)
            )
            .Number(n => n
                .Name(x => x.SmallNumber)
                .Type(NumberType.Short)
            )
            .Number(n => n
                .Name(x => x.TinyNumber)
                .Type(NumberType.Byte)
            )
        )
    )
);
```

### 3. Date Mapping

```csharp
// Date Mapping
client.Indices.Create("my-index", c => c
    .Map<Product>(m => m
        .Properties(p => p
            .Date(d => d
                .Name(x => x.CreatedDate)
                .Format("yyyy-MM-dd||yyyy-MM-dd'T'HH:mm:ss||strict_date_optional_time")
            )
            .Date(d => d
                .Name(x => x.UpdatedAt)
                .Format("epoch_millis")
            )
        )
    )
);
```

### 4. Boolean Mapping

```csharp
// Boolean Mapping
client.Indices.Create("my-index", c => c
    .Map<Product>(m => m
        .Properties(p => p
            .Boolean(b => b
                .Name(x => x.IsActive)
            )
            .Boolean(b => b
                .Name(x => x.InStock)
            )
        )
    )
);
```

### 5. Geo Point Mapping

```csharp
// Geo Point Mapping
client.Indices.Create("my-index", c => c
    .Map<Location>(m => m
        .Properties(p => p
            .GeoPoint(g => g
                .Name(x => x.Coordinates)
            )
            .Text(t => t
                .Name(x => x.Address)
            )
        )
    )
);

// Location class example
public class Location
{
    public GeoLocation Coordinates { get; set; }
    public string Address { get; set; }
}
```

### 6. Nested Object Mapping

```csharp
// Nested Object Mapping
client.Indices.Create("my-index", c => c
    .Map<Order>(m => m
        .Properties(p => p
            .Keyword(k => k.Name(x => x.OrderId))
            .Nested<OrderItem>(n => n
                .Name(x => x.Items)
                .Properties(np => np
                    .Text(t => t.Name(ni => ni.ProductName))
                    .Number(num => num
                        .Name(ni => ni.Quantity)
                        .Type(NumberType.Integer)
                    )
                    .Number(num => num
                        .Name(ni => ni.Price)
                        .Type(NumberType.Double)
                    )
                )
            )
        )
    )
);

// Order and OrderItem classes
public class Order
{
    public string OrderId { get; set; }
    public List<OrderItem> Items { get; set; }
}

public class OrderItem
{
    public string ProductName { get; set; }
    public int Quantity { get; set; }
    public double Price { get; set; }
}
```

---

## Dynamic Mapping

### Path Match Dynamic Template

```csharp
// Path Match Dynamic Template
client.Indices.Create("my-index", c => c
    .Map<object>(m => m
        .DynamicTemplates(dt => dt
            .DynamicTemplate("dynstring", t => t
                .PathMatch("s_*")
                .MatchMappingType("string")
                .Mapping(tm => tm
                    .Text(txt => txt
                        .Fields(f => f
                            .Keyword(k => k.Name("keyword"))
                        )
                    )
                )
            )
        )
    )
);
```

---

## Complex Mapping Examples

### 1. Multi-Field Mapping

We can have multiple fields for a single property of the object to have different analyzer.

```csharp
// Multi-Field Mapping
client.Indices.Create("my-index", c => c
    .Map<Document>(m => m
        .Properties(p => p
            .Text(t => t
                .Name(n => n.Content)
                .Analyzer("standard")
                .Fields(f => f
                    .Text(ft => ft
                        .Name("english")
                        .Analyzer("english")
                    )
                    .Text(ft => ft
                        .Name("shingles") 
                        .Analyzer("shingle_analyzer")
                    )
                    .Keyword(k => k
                        .Name("keyword")
                        .IgnoreAbove(256)
                    )
                )
            )
        )
    )
);
```

### 2. Custom Analyzer with Mapping

```csharp
// Custom Analyzer with Mapping
client.Indices.Create("my-index", c => c
    .Settings(s => s
        .Analysis(a => a
            .Analyzers(an => an
                .Custom("custom_analyzer", ca => ca
                    .Tokenizer("standard")
                    .Filters("lowercase", "stop", "stemmer")
                )
            )
            .TokenFilters(tf => tf
                .Stemmer("stemmer", st => st
                    .Language("english")
                )
            )
        )
    )
    .Map<Document>(m => m
        .Properties(p => p
            .Text(t => t
                .Name(n => n.Content)
                .Analyzer("custom_analyzer")
            )
        )
    )
);
```

---

## Best Practices

### 1. Mapping Validation

```csharp
// Validate Mapping Before Creating
var mappingValidation = client.Indices.ValidateQuery<Product>(v => v
    .Index("products")
    .Query(q => q.MatchAll())
);

if (!mappingValidation.IsValid)
{
    // Handle mapping validation errors
    Console.WriteLine($"Mapping validation failed: {mappingValidation.ServerError}");
}
```

### 2. Index Template for Consistent Mapping

```csharp
// Index Template for Consistent Mapping
client.Indices.PutTemplate("product_template", t => t
    .IndexPatterns("products-*")
    .Settings(s => s
        .NumberOfShards(2)
        .NumberOfReplicas(1)
    )
    .Map<Product>(m => m
        .AutoMap()
        .Properties(p => p
            .Text(txt => txt
                .Name(n => n.Title)
                .Analyzer("standard")
                .Fields(f => f
                    .Keyword(k => k.Name("keyword"))
                )
            )
        )
    )
);
```

### 3. Update Existing Mapping

```csharp
// Update Existing Mapping (Add New Fields)
client.Map<Product>(m => m
    .Index("products")
    .Properties(p => p
        .Text(t => t
            .Name("new_field")
            .Analyzer("standard")
        )
        .Keyword(k => k
            .Name("another_new_field")
        )
    )
);
```

---

## Common Data Types Reference

### Core Data Types

| NEST Type | Elasticsearch Type | C# Type | Example |
|-----------|-------------------|---------|---------|
| `.Text()` | `text` | `string` | Full-text search |
| `.Keyword()` | `keyword` | `string` | Exact match, sorting |
| `.Number(NumberType.Long)` | `long` | `long` | Large integers |
| `.Number(NumberType.Integer)` | `integer` | `int` | Standard integers |
| `.Number(NumberType.Short)` | `short` | `short` | Small integers |
| `.Number(NumberType.Byte)` | `byte` | `byte` | Tiny integers |
| `.Number(NumberType.Double)` | `double` | `double` | Large decimals |
| `.Number(NumberType.Float)` | `float` | `float` | Standard decimals |
| `.Boolean()` | `boolean` | `bool` | True/false values |
| `.Date()` | `date` | `DateTime` | Date/time values |
| `.Binary()` | `binary` | `byte[]` | Binary data |

### Complex Data Types

| NEST Type | Elasticsearch Type | Usage |
|-----------|-------------------|--------|
| `.Object<T>()` | `object` | Single JSON object |
| `.Nested<T>()` | `nested` | Array of objects |
| `.GeoPoint()` | `geo_point` | Latitude/longitude |
| `.GeoShape()` | `geo_shape` | Complex geographic shapes |
| `.Ip()` | `ip` | IP addresses |
| `.Completion()` | `completion` | Auto-complete suggestions |

### Advanced Features

- **Multi-fields**: Index same field multiple ways
- **Copy To**: Combine multiple fields into one search field
- **Dynamic Templates**: Rules for dynamically added fields
- **Analyzers**: Custom text processing
- **Normalizers**: Keyword field preprocessing

---

## Usage Examples

### Basic Document Indexing

```csharp
// Index a document
var product = new Product
{
    Id = 1,
    Title = "Sample Product",
    Category = "Electronics",
    Price = 99.99m,
    IsActive = true,
    CreatedDate = DateTime.Now
};

var response = await client.IndexDocumentAsync(product);
```

### Search with Mapping

```csharp
// Search using mapped fields
var searchResponse = await client.SearchAsync<Product>(s => s
    .Index("products")
    .Query(q => q
        .Bool(b => b
            .Must(m => m
                .Match(mt => mt
                    .Field(f => f.Title)
                    .Query("sample")
                )
            )
            .Filter(f => f
                .Term(t => t
                    .Field(field => field.IsActive)
                    .Value(true)
                )
            )
        )
    )
);
```

This comprehensive guide covers all essential aspects of NEST 7 mapping in Elasticsearch, providing you with reference code for both standard and dynamic mapping scenarios.