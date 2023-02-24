using System.Diagnostics.Metrics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NoiseMaker.Services;

public class DiskSpaceService : IHostedService, IDisposable
{
    private readonly ILogger<DiskSpaceService> _logger;
    private readonly Meter _meter;
    private readonly ObservableGauge<long> _diskSpaceGauge;

    public DiskSpaceService(ILogger<DiskSpaceService> logger)
    {
        _logger = logger;
        _meter = new Meter("SystemInfo");
        _diskSpaceGauge = _meter.CreateObservableGauge("DiskSpace", GetFreeDiskSpace, "bytes");
    }

    private long GetFreeDiskSpace()
    {
        var drives = DriveInfo.GetDrives();
        var mainDrive = GetMainDrive(drives);
        var totalFreeSpace = mainDrive.TotalFreeSpace;
        return totalFreeSpace;
    }

    private DriveInfo GetMainDrive(DriveInfo[] drives)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return drives.Single(d => d.Name == "/");
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting disk space monitoring, instrument enabled: {instrumentEnabled}", _diskSpaceGauge.Enabled);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public void Dispose()
    {
        _meter.Dispose();
    }
}
