using Moq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using VintageAtlas.Commands;
using VintageAtlas.Core;
using Xunit;

namespace VintageAtlas.Tests.Unit.Commands;

public class ExportCommandTests
{
    private readonly Mock<ICoreServerAPI> _mockApi;
    private readonly Mock<IMapExporter> _mockExporter;
    private readonly Mock<IChatCommandApi> _mockChatCommands;

    public ExportCommandTests()
    {
        _mockApi = new Mock<ICoreServerAPI>();
        _mockExporter = new Mock<IMapExporter>();
        _mockChatCommands = new Mock<IChatCommandApi>();
        
        _mockApi.Setup(x => x.ChatCommands).Returns(_mockChatCommands.Object);
    }

    [Fact]
    public void Register_RegistersAtlasCommand()
    {
        // Arrange
        var mockCommand = new Mock<IChatCommand>();
        _mockChatCommands.Setup(x => x.GetOrCreate(It.IsAny<string>()))
            .Returns(mockCommand.Object);
        mockCommand.Setup(x => x.WithDescription(It.IsAny<string>()))
            .Returns(mockCommand.Object);
        mockCommand.Setup(x => x.WithAlias(It.IsAny<string>()))
            .Returns(mockCommand.Object);
        mockCommand.Setup(x => x.RequiresPrivilege(It.IsAny<string>()))
            .Returns(mockCommand.Object);
        mockCommand.Setup(x => x.BeginSubCommand(It.IsAny<string>()))
            .Returns(mockCommand.Object);
        mockCommand.Setup(x => x.HandleWith(It.IsAny<OnCommandDelegate>()))
            .Returns(mockCommand.Object);
        mockCommand.Setup(x => x.EndSubCommand())
            .Returns(mockCommand.Object);

        // Act
        ExportCommand.Register(_mockApi.Object, _mockExporter.Object);

        // Assert
        _mockChatCommands.Verify(x => x.GetOrCreate("atlas"), Times.Once);
    }

    [Fact]
    public void Register_AddsExportSubcommand()
    {
        // Arrange
        var mockCommand = new Mock<IChatCommand>();
        _mockChatCommands.Setup(x => x.GetOrCreate(It.IsAny<string>()))
            .Returns(mockCommand.Object);
        mockCommand.Setup(x => x.WithDescription(It.IsAny<string>()))
            .Returns(mockCommand.Object);
        mockCommand.Setup(x => x.WithAlias(It.IsAny<string>()))
            .Returns(mockCommand.Object);
        mockCommand.Setup(x => x.RequiresPrivilege(It.IsAny<string>()))
            .Returns(mockCommand.Object);
        mockCommand.Setup(x => x.BeginSubCommand(It.IsAny<string>()))
            .Returns(mockCommand.Object);
        mockCommand.Setup(x => x.HandleWith(It.IsAny<OnCommandDelegate>()))
            .Returns(mockCommand.Object);
        mockCommand.Setup(x => x.EndSubCommand())
            .Returns(mockCommand.Object);

        // Act
        ExportCommand.Register(_mockApi.Object, _mockExporter.Object);

        // Assert
        mockCommand.Verify(x => x.BeginSubCommand("export"), Times.Once);
    }

    [Fact]
    public void Register_RequiresControlServerPrivilege()
    {
        // Arrange
        var mockCommand = new Mock<IChatCommand>();
        _mockChatCommands.Setup(x => x.GetOrCreate(It.IsAny<string>()))
            .Returns(mockCommand.Object);
        mockCommand.Setup(x => x.WithDescription(It.IsAny<string>()))
            .Returns(mockCommand.Object);
        mockCommand.Setup(x => x.WithAlias(It.IsAny<string>()))
            .Returns(mockCommand.Object);
        mockCommand.Setup(x => x.RequiresPrivilege(It.IsAny<string>()))
            .Returns(mockCommand.Object);
        mockCommand.Setup(x => x.BeginSubCommand(It.IsAny<string>()))
            .Returns(mockCommand.Object);
        mockCommand.Setup(x => x.HandleWith(It.IsAny<OnCommandDelegate>()))
            .Returns(mockCommand.Object);
        mockCommand.Setup(x => x.EndSubCommand())
            .Returns(mockCommand.Object);

        // Act
        ExportCommand.Register(_mockApi.Object, _mockExporter.Object);

        // Assert
        mockCommand.Verify(x => x.RequiresPrivilege(Privilege.controlserver), Times.Once);
    }

    [Fact]
    public void Register_AddsVaAlias()
    {
        // Arrange
        var mockCommand = new Mock<IChatCommand>();
        _mockChatCommands.Setup(x => x.GetOrCreate(It.IsAny<string>()))
            .Returns(mockCommand.Object);
        mockCommand.Setup(x => x.WithDescription(It.IsAny<string>()))
            .Returns(mockCommand.Object);
        mockCommand.Setup(x => x.WithAlias(It.IsAny<string>()))
            .Returns(mockCommand.Object);
        mockCommand.Setup(x => x.RequiresPrivilege(It.IsAny<string>()))
            .Returns(mockCommand.Object);
        mockCommand.Setup(x => x.BeginSubCommand(It.IsAny<string>()))
            .Returns(mockCommand.Object);
        mockCommand.Setup(x => x.HandleWith(It.IsAny<OnCommandDelegate>()))
            .Returns(mockCommand.Object);
        mockCommand.Setup(x => x.EndSubCommand())
            .Returns(mockCommand.Object);

        // Act
        ExportCommand.Register(_mockApi.Object, _mockExporter.Object);

        // Assert
        mockCommand.Verify(x => x.WithAlias("va"), Times.Once);
    }
}
