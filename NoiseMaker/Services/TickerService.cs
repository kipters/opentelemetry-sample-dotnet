using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NoiseMaker.Services;

public class TickerService : BackgroundService
{
    private readonly ILogger<TickerService> _logger;
    private readonly ActivitySource _activitySource;
    private readonly Meter _meter;
    private readonly Histogram<int> _rolls;
    private readonly Counter<int> _rollCount;
    private readonly ObservableCounter<int> _observableRollCount;
    private long _rolled;

    public TickerService(ILogger<TickerService> logger)
    {
        _logger = logger;
        _activitySource = new ActivitySource("TickerService");
        _meter = new Meter("TickerService");
        _rolls = _meter.CreateHistogram<int>("DiceRoll");
        _rollCount = _meter.CreateCounter<int>("RollCounter");
        _observableRollCount = _meter.CreateObservableCounter<int>("ObservableRollCounter", () => (int) Interlocked.Read(ref _rolled));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            using var activity = _activitySource.StartActivity("DiceRoll");
            var dice = Random.Shared.Next(0, 6) + 1;
            Interlocked.Increment(ref _rolled);
            _rollCount.Add(1);
            _rolls.Record(dice);
            _logger.LogInformation("Rolled a {dice}", dice);
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        _meter.Dispose();
        _activitySource.Dispose();
    }
}