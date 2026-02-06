using ACI.Application.Common;

namespace ACI.Application.Tests.Common;

/// <summary>
/// Unit tests for the Result pattern implementation.
/// </summary>
public class ResultTests
{
    #region Result<T> Tests

    [Fact]
    public void ResultT_Success_HasCorrectProperties()
    {
        // Arrange & Act
        var result = Result<string>.Success("test value");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be("test value");
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void ResultT_Failure_HasCorrectProperties()
    {
        // Arrange
        var error = new Error("Test.Error", "Test error description");
        
        // Act
        var result = Result<string>.Failure(error);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void ResultT_ImplicitConversion_FromValue_CreatesSuccess()
    {
        // Arrange & Act
        Result<int> result = 42;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void ResultT_ImplicitConversion_FromError_CreatesFailure()
    {
        // Arrange
        var error = new Error("Test.Error", "Test description");
        
        // Act
        Result<int> result = error;

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    #endregion

    #region Result Tests

    [Fact]
    public void Result_Success_HasCorrectProperties()
    {
        // Arrange & Act
        var result = Result.Success();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void Result_Failure_HasCorrectProperties()
    {
        // Arrange
        var error = new Error("Test.Error", "Test error description");
        
        // Act
        var result = Result.Failure(error);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    #endregion

    #region Error Tests

    [Fact]
    public void Error_None_HasEmptyValues()
    {
        // Assert
        Error.None.Code.Should().BeEmpty();
        Error.None.Description.Should().BeEmpty();
    }

    [Fact]
    public void Error_Equality_WorksCorrectly()
    {
        // Arrange
        var error1 = new Error("Test.Error", "Test description");
        var error2 = new Error("Test.Error", "Test description");
        var error3 = new Error("Different.Error", "Different description");

        // Assert
        error1.Should().Be(error2);
        error1.Should().NotBe(error3);
    }

    #endregion
}
