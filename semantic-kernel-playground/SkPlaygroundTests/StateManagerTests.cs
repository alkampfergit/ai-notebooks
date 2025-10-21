using SkPlayground.Services;
using SkPlayground.Models;

namespace SkPlaygroundTests;

[TestFixture]
public class StateManagerTests
{
    [SetUp]
    public void Setup()
    {
        // Clear state before each test
        StateManager.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        // Clear state after each test
        StateManager.Clear();
    }

    [Test]
    public void Start_ShouldInitializeNewState()
    {
        // Act
        var state = StateManager.Start();

        // Assert
        Assert.That(state, Is.Not.Null);
        Assert.That(state.TotalInputTokens, Is.EqualTo(0));
        Assert.That(state.TotalOutputTokens, Is.EqualTo(0));
        Assert.That(state.Memory, Is.Not.Null);
        Assert.That(state.Memory.Count, Is.EqualTo(0));
    }

    [Test]
    public void GetCurrent_WhenNotInitialized_ShouldReturnNull()
    {
        // Act
        var state = StateManager.GetCurrent();

        // Assert
        Assert.That(state, Is.Null);
    }

    [Test]
    public void GetCurrent_WhenInitialized_ShouldReturnState()
    {
        // Arrange
        var initialState = StateManager.Start();

        // Act
        var currentState = StateManager.GetCurrent();

        // Assert
        Assert.That(currentState, Is.Not.Null);
        Assert.That(currentState, Is.SameAs(initialState));
    }

    [Test]
    public void Clear_ShouldRemoveState()
    {
        // Arrange
        StateManager.Start();

        // Act
        StateManager.Clear();
        var state = StateManager.GetCurrent();

        // Assert
        Assert.That(state, Is.Null);
    }

    [Test]
    public void GetTotalInputTokens_WhenNotInitialized_ShouldReturnZero()
    {
        // Act
        var tokens = StateManager.GetTotalInputTokens();

        // Assert
        Assert.That(tokens, Is.EqualTo(0));
    }

    [Test]
    public void SetTotalInputTokens_WhenNotInitialized_ShouldThrowException()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => StateManager.SetTotalInputTokens(100));
    }

    [Test]
    public void SetTotalInputTokens_WhenInitialized_ShouldUpdateTokens()
    {
        // Arrange
        StateManager.Start();

        // Act
        StateManager.SetTotalInputTokens(100);
        var tokens = StateManager.GetTotalInputTokens();

        // Assert
        Assert.That(tokens, Is.EqualTo(100));
    }

    [Test]
    public void AddInputTokens_WhenNotInitialized_ShouldThrowException()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => StateManager.AddInputTokens(50));
    }

    [Test]
    public void AddInputTokens_WhenInitialized_ShouldAccumulateTokens()
    {
        // Arrange
        StateManager.Start();
        StateManager.SetTotalInputTokens(100);

        // Act
        StateManager.AddInputTokens(50);
        StateManager.AddInputTokens(25);
        var tokens = StateManager.GetTotalInputTokens();

        // Assert
        Assert.That(tokens, Is.EqualTo(175));
    }

    [Test]
    public void GetTotalOutputTokens_WhenNotInitialized_ShouldReturnZero()
    {
        // Act
        var tokens = StateManager.GetTotalOutputTokens();

        // Assert
        Assert.That(tokens, Is.EqualTo(0));
    }

    [Test]
    public void SetTotalOutputTokens_WhenNotInitialized_ShouldThrowException()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => StateManager.SetTotalOutputTokens(100));
    }

    [Test]
    public void SetTotalOutputTokens_WhenInitialized_ShouldUpdateTokens()
    {
        // Arrange
        StateManager.Start();

        // Act
        StateManager.SetTotalOutputTokens(200);
        var tokens = StateManager.GetTotalOutputTokens();

        // Assert
        Assert.That(tokens, Is.EqualTo(200));
    }

    [Test]
    public void AddOutputTokens_WhenNotInitialized_ShouldThrowException()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => StateManager.AddOutputTokens(50));
    }

    [Test]
    public void AddOutputTokens_WhenInitialized_ShouldAccumulateTokens()
    {
        // Arrange
        StateManager.Start();
        StateManager.SetTotalOutputTokens(200);

        // Act
        StateManager.AddOutputTokens(50);
        StateManager.AddOutputTokens(75);
        var tokens = StateManager.GetTotalOutputTokens();

        // Assert
        Assert.That(tokens, Is.EqualTo(325));
    }

    [Test]
    public void GetMemory_WhenNotInitialized_ShouldReturnEmptyDictionary()
    {
        // Act
        var memory = StateManager.GetMemory();

        // Assert
        Assert.That(memory, Is.Not.Null);
        Assert.That(memory.Count, Is.EqualTo(0));
    }

    [Test]
    public void GetMemory_WhenInitialized_ShouldReturnMemoryDictionary()
    {
        // Arrange
        StateManager.Start();
        StateManager.SetMemoryValue("test", "value");

        // Act
        var memory = StateManager.GetMemory();

        // Assert
        Assert.That(memory, Is.Not.Null);
        Assert.That(memory.Count, Is.EqualTo(1));
        Assert.That(memory.ContainsKey("test"), Is.True);
    }

    [Test]
    public void SetMemoryValue_WhenNotInitialized_ShouldThrowException()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => StateManager.SetMemoryValue("key", "value"));
    }

    [Test]
    public void SetMemoryValue_WhenInitialized_ShouldStoreValue()
    {
        // Arrange
        StateManager.Start();

        // Act
        StateManager.SetMemoryValue("username", "John");
        var value = StateManager.GetMemoryValue("username");

        // Assert
        Assert.That(value, Is.EqualTo("John"));
    }

    [Test]
    public void GetMemoryValue_WhenKeyNotFound_ShouldReturnNull()
    {
        // Arrange
        StateManager.Start();

        // Act
        var value = StateManager.GetMemoryValue("nonexistent");

        // Assert
        Assert.That(value, Is.Null);
    }

    [Test]
    public void GetMemoryValue_Generic_ShouldReturnTypedValue()
    {
        // Arrange
        StateManager.Start();
        StateManager.SetMemoryValue("count", 42);

        // Act
        var value = StateManager.GetMemoryValue<int>("count");

        // Assert
        Assert.That(value, Is.EqualTo(42));
    }

    [Test]
    public void GetMemoryValue_Generic_WhenWrongType_ShouldReturnDefault()
    {
        // Arrange
        StateManager.Start();
        StateManager.SetMemoryValue("text", "hello");

        // Act
        var value = StateManager.GetMemoryValue<int>("text");

        // Assert
        Assert.That(value, Is.EqualTo(0));
    }

    [Test]
    public void TryGetMemoryValue_WhenKeyExists_ShouldReturnTrue()
    {
        // Arrange
        StateManager.Start();
        StateManager.SetMemoryValue("key", "value");

        // Act
        var result = StateManager.TryGetMemoryValue("key", out var value);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(value, Is.EqualTo("value"));
    }

    [Test]
    public void TryGetMemoryValue_WhenKeyNotFound_ShouldReturnFalse()
    {
        // Arrange
        StateManager.Start();

        // Act
        var result = StateManager.TryGetMemoryValue("nonexistent", out var value);

        // Assert
        Assert.That(result, Is.False);
        Assert.That(value, Is.Null);
    }

    [Test]
    public void TryGetMemoryValue_Generic_WhenKeyExistsAndTypeMatches_ShouldReturnTrue()
    {
        // Arrange
        StateManager.Start();
        StateManager.SetMemoryValue("number", 123);

        // Act
        var result = StateManager.TryGetMemoryValue<int>("number", out var value);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(value, Is.EqualTo(123));
    }

    [Test]
    public void TryGetMemoryValue_Generic_WhenTypeDoesNotMatch_ShouldReturnFalse()
    {
        // Arrange
        StateManager.Start();
        StateManager.SetMemoryValue("text", "hello");

        // Act
        var result = StateManager.TryGetMemoryValue<int>("text", out var value);

        // Assert
        Assert.That(result, Is.False);
        Assert.That(value, Is.EqualTo(0));
    }

    [Test]
    public void RemoveMemoryValue_WhenKeyExists_ShouldReturnTrueAndRemove()
    {
        // Arrange
        StateManager.Start();
        StateManager.SetMemoryValue("key", "value");

        // Act
        var result = StateManager.RemoveMemoryValue("key");
        var exists = StateManager.ContainsMemoryKey("key");

        // Assert
        Assert.That(result, Is.True);
        Assert.That(exists, Is.False);
    }

    [Test]
    public void RemoveMemoryValue_WhenKeyNotFound_ShouldReturnFalse()
    {
        // Arrange
        StateManager.Start();

        // Act
        var result = StateManager.RemoveMemoryValue("nonexistent");

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void ContainsMemoryKey_WhenKeyExists_ShouldReturnTrue()
    {
        // Arrange
        StateManager.Start();
        StateManager.SetMemoryValue("key", "value");

        // Act
        var result = StateManager.ContainsMemoryKey("key");

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void ContainsMemoryKey_WhenKeyNotFound_ShouldReturnFalse()
    {
        // Arrange
        StateManager.Start();

        // Act
        var result = StateManager.ContainsMemoryKey("nonexistent");

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void ClearMemory_ShouldRemoveAllMemoryEntries()
    {
        // Arrange
        StateManager.Start();
        StateManager.SetMemoryValue("key1", "value1");
        StateManager.SetMemoryValue("key2", "value2");
        StateManager.SetMemoryValue("key3", "value3");

        // Act
        StateManager.ClearMemory();
        var count = StateManager.GetMemoryCount();

        // Assert
        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public void GetMemoryCount_WhenNotInitialized_ShouldReturnZero()
    {
        // Act
        var count = StateManager.GetMemoryCount();

        // Assert
        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public void GetMemoryCount_ShouldReturnCorrectCount()
    {
        // Arrange
        StateManager.Start();
        StateManager.SetMemoryValue("key1", "value1");
        StateManager.SetMemoryValue("key2", "value2");

        // Act
        var count = StateManager.GetMemoryCount();

        // Assert
        Assert.That(count, Is.EqualTo(2));
    }

    [Test]
    public void StateManager_ShouldStoreComplexObjects()
    {
        // Arrange
        StateManager.Start();
        var customer = new Customer { Name = "John", Surname = "Doe", Email = "john@example.com" };

        // Act
        StateManager.SetMemoryValue("customer", customer);
        var retrievedCustomer = StateManager.GetMemoryValue<Customer>("customer");

        // Assert
        Assert.That(retrievedCustomer, Is.Not.Null);
        Assert.That(retrievedCustomer!.Name, Is.EqualTo("John"));
        Assert.That(retrievedCustomer.Surname, Is.EqualTo("Doe"));
        Assert.That(retrievedCustomer.Email, Is.EqualTo("john@example.com"));
    }

    [Test]
    public async Task StateManager_ShouldIsolateStateAcrossAsyncContexts()
    {
        // Arrange & Act
        var task1 = Task.Run(() =>
        {
            StateManager.Start();
            StateManager.SetTotalInputTokens(100);
            StateManager.SetMemoryValue("task", "task1");
            Thread.Sleep(50); // Simulate some work
            return new
            {
                Tokens = StateManager.GetTotalInputTokens(),
                Task = StateManager.GetMemoryValue<string>("task")
            };
        });

        var task2 = Task.Run(() =>
        {
            StateManager.Start();
            StateManager.SetTotalInputTokens(200);
            StateManager.SetMemoryValue("task", "task2");
            Thread.Sleep(50); // Simulate some work
            return new
            {
                Tokens = StateManager.GetTotalInputTokens(),
                Task = StateManager.GetMemoryValue<string>("task")
            };
        });

        var results = await Task.WhenAll(task1, task2);

        // Assert
        Assert.That(results[0].Tokens, Is.EqualTo(100));
        Assert.That(results[0].Task, Is.EqualTo("task1"));
        Assert.That(results[1].Tokens, Is.EqualTo(200));
        Assert.That(results[1].Task, Is.EqualTo("task2"));
    }

    [Test]
    public async Task StateManager_ShouldPreserveStateAcrossAwait()
    {
        // Arrange
        StateManager.Start();
        StateManager.SetTotalInputTokens(100);
        StateManager.SetMemoryValue("before", "value");

        // Act
        await Task.Delay(10);
        StateManager.AddInputTokens(50);
        StateManager.SetMemoryValue("after", "value2");

        await Task.Delay(10);
        var tokens = StateManager.GetTotalInputTokens();
        var beforeValue = StateManager.GetMemoryValue<string>("before");
        var afterValue = StateManager.GetMemoryValue<string>("after");

        // Assert
        Assert.That(tokens, Is.EqualTo(150));
        Assert.That(beforeValue, Is.EqualTo("value"));
        Assert.That(afterValue, Is.EqualTo("value2"));
    }

    [Test]
    public void StateManager_ShouldSupportMultipleStarts()
    {
        // Arrange
        var state1 = StateManager.Start();
        StateManager.SetTotalInputTokens(100);

        // Act
        var state2 = StateManager.Start();
        var tokens = StateManager.GetTotalInputTokens();

        // Assert
        Assert.That(state1, Is.Not.SameAs(state2));
        Assert.That(tokens, Is.EqualTo(0)); // New state should have reset tokens
    }

    [Test]
    public void StateManager_MemoryShouldBeThreadSafe()
    {
        // Arrange
        StateManager.Start();
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < 100; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                StateManager.SetMemoryValue($"key{index}", $"value{index}");
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        Assert.That(StateManager.GetMemoryCount(), Is.EqualTo(100));
        for (int i = 0; i < 100; i++)
        {
            Assert.That(StateManager.ContainsMemoryKey($"key{i}"), Is.True);
        }
    }
}
