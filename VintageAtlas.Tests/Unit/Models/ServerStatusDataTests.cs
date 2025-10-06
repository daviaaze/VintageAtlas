using FluentAssertions;
using VintageAtlas.Models;
using Xunit;

namespace VintageAtlas.Tests.Unit.Models;

public class ServerStatusDataTests
{
    [Fact]
    public void ServerStatusData_HasDefaultValues()
    {
        // Act
        var data = new ServerStatusData();

        // Assert
        data.SpawnPoint.Should().BeNull();
        data.Date.Should().BeNull();
        data.SpawnTemperature.Should().BeNull();
        data.SpawnRainfall.Should().BeNull();
        data.Weather.Should().BeNull();
        data.Players.Should().NotBeNull().And.BeEmpty();
        data.Animals.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void SpawnPoint_CanSetProperties()
    {
        // Act
        var spawn = new SpawnPoint
        {
            X = 100.5,
            Y = 64.0,
            Z = -200.3
        };

        // Assert
        spawn.X.Should().Be(100.5);
        spawn.Y.Should().Be(64.0);
        spawn.Z.Should().Be(-200.3);
    }

    [Fact]
    public void DateInfo_CanSetProperties()
    {
        // Act
        var date = new DateInfo
        {
            Year = 2025,
            Month = 3,
            Day = 15,
            Hour = 14,
            Minute = 30
        };

        // Assert
        date.Year.Should().Be(2025);
        date.Month.Should().Be(3);
        date.Day.Should().Be(15);
        date.Hour.Should().Be(14);
        date.Minute.Should().Be(30);
    }

    [Fact]
    public void WeatherInfo_CanSetProperties()
    {
        // Act
        var weather = new WeatherInfo
        {
            Temperature = 25.5,
            Rainfall = 0.7,
            WindSpeed = 5.2
        };

        // Assert
        weather.Temperature.Should().Be(25.5);
        weather.Rainfall.Should().Be(0.7);
        weather.WindSpeed.Should().Be(5.2);
    }

    [Fact]
    public void PlayerData_CanSetProperties()
    {
        // Act
        var player = new PlayerData
        {
            Name = "TestPlayer",
            Uid = "uuid-123",
            Coordinates = new CoordinateData { X = 100, Y = 64, Z = 200 },
            Health = new HealthData { Current = 15, Max = 20 },
            Hunger = new HealthData { Current = 1200, Max = 1500 },
            Temperature = 20.5,
            BodyTemp = 37.0
        };

        // Assert
        player.Name.Should().Be("TestPlayer");
        player.Uid.Should().Be("uuid-123");
        player.Coordinates.Should().NotBeNull();
        player.Health.Should().NotBeNull();
        player.Hunger.Should().NotBeNull();
        player.Temperature.Should().Be(20.5);
        player.BodyTemp.Should().Be(37.0);
    }

    [Fact]
    public void AnimalData_CanSetProperties()
    {
        // Act
        var animal = new AnimalData
        {
            Type = "Wolf",
            Name = "Grey Wolf",
            Coordinates = new CoordinateData { X = 150, Y = 70, Z = 250 },
            Health = new HealthData { Current = 10, Max = 15 },
            Temperature = 18.0,
            Rainfall = 0.5,
            Wind = new WindData { Percent = 0.3 }
        };

        // Assert
        animal.Type.Should().Be("Wolf");
        animal.Name.Should().Be("Grey Wolf");
        animal.Coordinates.Should().NotBeNull();
        animal.Health.Should().NotBeNull();
        animal.Temperature.Should().Be(18.0);
        animal.Rainfall.Should().Be(0.5);
        animal.Wind.Should().NotBeNull();
    }

    [Fact]
    public void CoordinateData_CanSetProperties()
    {
        // Act
        var coords = new CoordinateData
        {
            X = 123.45,
            Y = 67.89,
            Z = -234.56
        };

        // Assert
        coords.X.Should().Be(123.45);
        coords.Y.Should().Be(67.89);
        coords.Z.Should().Be(-234.56);
    }

    [Fact]
    public void HealthData_CanSetProperties()
    {
        // Act
        var health = new HealthData
        {
            Current = 15.5,
            Max = 20.0
        };

        // Assert
        health.Current.Should().Be(15.5);
        health.Max.Should().Be(20.0);
    }

    [Fact]
    public void WindData_CanSetProperties()
    {
        // Act
        var wind = new WindData
        {
            Percent = 0.75
        };

        // Assert
        wind.Percent.Should().Be(0.75);
    }
}
