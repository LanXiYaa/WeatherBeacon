using WeatherBeacon.Core.Models;
namespace WeatherBeacon.Core.Interfaces;
public interface IWeatherProvider
{
    string Id { get; }
    Task<WeatherSnapshot> GetWeatherAsync(WeatherLocation location, CancellationToken cancellationToken);
}
