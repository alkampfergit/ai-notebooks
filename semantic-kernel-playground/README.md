# Semantic Kernel Playground

This is a playground project for experimenting with Semantic Kernel and exploring advanced features like schema generation, polymorphic deserialization, and structured output with LLMs.

## Project Structure

- **SkPlayground**: Main project containing business functions, utilities, and services
- **SkPlaygroundTests**: Test suite with comprehensive unit and integration tests

## Running Tests

### Run All Tests

To run all tests in the solution:

```bash
dotnet test
```

### Run Tests Excluding LLM Integration Tests (Recommended for Fast Feedback)

LLM integration tests make real API calls to OpenAI/Azure and are slow. To run only the fast unit tests:

```bash
dotnet test --filter "TestCategory!=LLMIntegration"
```

Or using the shorter syntax:

```bash
dotnet test --filter "Category!=LLMIntegration"
```

### Run Only LLM Integration Tests

To run only the tests that make real LLM API calls (requires API keys):

```bash
dotnet test --filter "TestCategory=LLMIntegration"
```

Or:

```bash
dotnet test --filter "Category=LLMIntegration"
```

### Run Tests for a Specific Test Class

To run tests from a specific test class:

```bash
# NextStepManager tests only
dotnet test --filter "FullyQualifiedName~NextStepManagerTests"

# PolymorphicSchemaManager tests only
dotnet test --filter "FullyQualifiedName~PolymorphicSchemaManagerTests"
```

### Run a Specific Test

To run a single test by name:

```bash
dotnet test --filter "FullyQualifiedName~GenerateSchema_ShouldReturnValidJsonSchema"
```

## Test Categories

### Unit Tests
Fast tests that don't require external dependencies or API calls. These test schema generation, polymorphic deserialization, and business logic.

### LLM Integration Tests (Category: "LLMIntegration")
These tests make real API calls to OpenAI or Azure OpenAI and require:
- Valid API keys configured in `.env` file
- Active internet connection
- API credits/quota

**Tests marked with `[Category("LLMIntegration")]`:**
- `GenerateJsonSchema_RealLLMCall_ReformatPersonData`
- `GenerateNextStepSchema_RealLLMCall_PolymorphicSendEmailToolCall`
- `GenerateSchema_RealLLMCall_PolymorphicDeserialization`
- `GenerateJsonSchema_RealLLMCall_PolymorphicCatOwner`

## Configuration

Create a `.env` file in the project root with your API credentials:

```env
OPENAI_API_KEY=your_openai_api_key
AZURE_ENDPOINT=your_azure_endpoint
AZURE_DEPLOYMENT_NAME=your_deployment_name
```

## Building the Project

```bash
# Build the entire solution
dotnet build

# Build in Release mode
dotnet build --configuration Release

# Clean and rebuild
dotnet clean && dotnet build
```

## Code Structure

### Business Functions
- `GetCustomerDataFunction`: Retrieves customer data by email
- `SendEmailFunction`: Sends emails to customers
- `IssueInvoiceFunction`: Creates invoices for customers
- Additional business functions for various operations

### Utilities
- `PolymorphicSchemaManager<TContainer, TBase>`: Generic manager for polymorphic schema generation
- `NextStepManager`: Specialized manager for NextStep/ToolCall schemas
- `SchemaGuidedReasoner`: Service for LLM-based reasoning with structured output

## Development Tips

1. **Run fast tests frequently**: Use `dotnet test --filter "Category!=LLMIntegration"` during development
2. **Run LLM tests before commits**: Verify integration tests pass before pushing changes
3. **Check test output**: Use `--logger "console;verbosity=detailed"` for more information
4. **Parallel execution**: Tests run in parallel by default for better performance

## Example Test Commands

```bash
# Fast feedback loop during development (no LLM calls)
dotnet test --filter "Category!=LLMIntegration"

# Run all NextStepManager tests without LLM calls
dotnet test --filter "FullyQualifiedName~NextStepManagerTests & Category!=LLMIntegration"

# Run schema generation tests
dotnet test --filter "FullyQualifiedName~GenerateSchema"

# Run tests with detailed output
dotnet test --filter "Category!=LLMIntegration" --logger "console;verbosity=detailed"
```

## Continuous Integration

For CI/CD pipelines, it's recommended to:
- Run unit tests (without LLM integration) on every commit
- Run LLM integration tests on pull requests or scheduled builds
- Use the `--filter "Category!=LLMIntegration"` flag for fast builds

## Contributing

When adding new tests:
- Mark tests that make real LLM API calls with `[Category("LLMIntegration")]`
- Ensure tests can run without API keys (skip gracefully)
- Add appropriate test documentation
- Keep tests fast and focused
