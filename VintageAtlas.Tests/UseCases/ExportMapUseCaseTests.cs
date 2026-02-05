using System;
using System.Threading.Tasks;
using Vintagestory.API.Server;
using VintageAtlas.Application.DTOs;
using VintageAtlas.Application.Services;
using VintageAtlas.Application.UseCases;
using VintageAtlas.Export.Extraction;

namespace VintageAtlas.Tests.UseCases;

/// <summary>
/// Unit tests for ExportMapUseCase.
/// Demonstrates testing business logic without game engine dependencies.
/// </summary>
public class ExportMapUseCaseTests
{
    private readonly Mock<ICoreServerAPI> _mockApi;
    private readonly Mock<IServerStateManager> _mockStateManager;
    private readonly Mock<ExportOrchestrator> _mockOrchestrator;

    public ExportMapUseCaseTests()
    {
        _mockApi = new Mock<ICoreServerAPI>();
        _mockStateManager = new Mock<IServerStateManager>();
        _mockOrchestrator = new Mock<ExportOrchestrator>(null!, null!);

        // Setup logger mock
        var mockLogger = new Mock<Vintagestory.API.Common.ILogger>();
        _mockApi.Setup(a => a.Logger).Returns(mockLogger.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WithSaveMode_EntersAndExitsSaveMode()
    {
        // Arrange
        var useCase = new ExportMapUseCase(_mockApi.Object, _mockStateManager.Object, _mockOrchestrator.Object);
        var options = new ExportOptions { SaveMode = true };

        _mockOrchestrator
            .Setup(o => o.ExecuteFullExportAsync(It.IsAny<IProgress<VintageAtlas.Export.Data.ExportProgress>>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await useCase.ExecuteAsync(options);

        // Assert
        result.Success.Should().BeTrue();
        _mockStateManager.Verify(m => m.EnterSaveModeAsync(), Times.Once, "Should enter save mode when requested");
        _mockStateManager.Verify(m => m.ExitSaveMode(), Times.Once, "Should exit save mode after export");
    }

    [Fact]
    public async Task ExecuteAsync_WithoutSaveMode_DoesNotEnterSaveMode()
    {
        // Arrange
        var useCase = new ExportMapUseCase(_mockApi.Object, _mockStateManager.Object, _mockOrchestrator.Object);
        var options = new ExportOptions { SaveMode = false };

        _mockOrchestrator
            .Setup(o => o.ExecuteFullExportAsync(It.IsAny<IProgress<VintageAtlas.Export.Data.ExportProgress>>()))
            .Returns(Task.CompletedTask);

        // Act
        await useCase.ExecuteAsync(options);

        // Assert
        _mockStateManager.Verify(m => m.EnterSaveModeAsync(), Times.Never, "Should not enter save mode when not requested");
        _mockStateManager.Verify(m => m.ExitSaveMode(), Times.Never, "Should not exit save mode when not entered");
    }

    [Fact]
    public async Task ExecuteAsync_WithStopOnDone_StopsServer()
    {
        // Arrange
        var useCase = new ExportMapUseCase(_mockApi.Object, _mockStateManager.Object, _mockOrchestrator.Object);
        var options = new ExportOptions { StopOnDone = true };

        _mockOrchestrator
            .Setup(o => o.ExecuteFullExportAsync(It.IsAny<IProgress<VintageAtlas.Export.Data.ExportProgress>>()))
            .Returns(Task.CompletedTask);

        // Act
        await useCase.ExecuteAsync(options);

        // Assert
        _mockStateManager.Verify(
            m => m.StopServer(It.IsAny<string>()), 
            Times.Once, 
            "Should stop server when StopOnDone is true"
        );
    }

    [Fact]
    public async Task ExecuteAsync_WhenOrchestratorFails_ReturnsFailedResult()
    {
        // Arrange
        var useCase = new ExportMapUseCase(_mockApi.Object, _mockStateManager.Object, _mockOrchestrator.Object);
        var options = new ExportOptions();
        var expectedException = new InvalidOperationException("Export failed");

        _mockOrchestrator
            .Setup(o => o.ExecuteFullExportAsync(It.IsAny<IProgress<VintageAtlas.Export.Data.ExportProgress>>()))
            .ThrowsAsync(expectedException);

        // Act
        var result = await useCase.ExecuteAsync(options);

        // Assert
        result.Success.Should().BeFalse("Export should fail when orchestrator throws");
        result.ErrorMessage.Should().Contain("Export failed");
        result.Exception.Should().Be(expectedException);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOrchestratorFails_ExitsSaveModeIfEntered()
    {
        // Arrange
        var useCase = new ExportMapUseCase(_mockApi.Object, _mockStateManager.Object, _mockOrchestrator.Object);
        var options = new ExportOptions { SaveMode = true };

        _mockOrchestrator
            .Setup(o => o.ExecuteFullExportAsync(It.IsAny<IProgress<VintageAtlas.Export.Data.ExportProgress>>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        await useCase.ExecuteAsync(options);

        // Assert
        _mockStateManager.Verify(
            m => m.ExitSaveMode(), 
            Times.Once, 
            "Should exit save mode even when export fails"
        );
    }

    [Fact]
    public async Task ExecuteAsync_WhenAlreadyRunning_ReturnsFailedResult()
    {
        // Arrange
        var useCase = new ExportMapUseCase(_mockApi.Object, _mockStateManager.Object, _mockOrchestrator.Object);
        var options = new ExportOptions();

        _mockOrchestrator
            .Setup(o => o.ExecuteFullExportAsync(It.IsAny<IProgress<VintageAtlas.Export.Data.ExportProgress>>()))
            .Returns(async () =>
            {
                await Task.Delay(100); // Simulate long-running export
                return;
            });

        // Act
        var task1 = useCase.ExecuteAsync(options);
        await Task.Delay(10); // Ensure first export starts
        var result2 = await useCase.ExecuteAsync(options); // Try to start second export

        // Assert
        result2.Success.Should().BeFalse("Should not allow concurrent exports");
        result2.ErrorMessage.Should().Contain("already running");

        await task1; // Wait for first export to complete
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSuccessfulResultWithMetrics()
    {
        // Arrange
        var useCase = new ExportMapUseCase(_mockApi.Object, _mockStateManager.Object, _mockOrchestrator.Object);
        var options = new ExportOptions();

        _mockOrchestrator
            .Setup(o => o.ExecuteFullExportAsync(It.IsAny<IProgress<VintageAtlas.Export.Data.ExportProgress>>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await useCase.ExecuteAsync(options);

        // Assert
        result.Success.Should().BeTrue();
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero, "Should track export duration");
        result.ErrorMessage.Should().BeNullOrEmpty();
        result.Exception.Should().BeNull();
    }
}

