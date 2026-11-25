using Vortice.DXGI;

namespace TXT2IMG;

public sealed record GpuCapabilities(
    ulong DedicatedVideoMemoryBytes,
    string AdapterDescription,
    bool SupportsHighVramQuality,
    bool SupportsExtendedLightningSteps)
{
    public double DedicatedVideoMemoryGigabytes =>
        DedicatedVideoMemoryBytes / (1024.0 * 1024.0 * 1024.0);

    public string Summary =>
        DedicatedVideoMemoryBytes == 0
            ? "GPU memory unknown"
            : $"{AdapterDescription} ({DedicatedVideoMemoryGigabytes:0.#} GiB dedicated)";

    public string LightningStepCapSummary => SupportsExtendedLightningSteps
        ? "Lightning steps: up to 44 at 896"
        : SupportsHighVramQuality
            ? "Lightning steps: capped at 32 (896 uses stronger base recipe)"
            : "Lightning steps: capped at 32";
}

/// <summary>
/// DXGI dedicated VRAM on the adapter Windows prefers for high-performance GPU work (aligns with DirectML device 0).
/// </summary>
public static class GpuCapabilitiesProbe
{
    public static GpuCapabilities Detect()
    {
        try
        {
            using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
            var (vram, name) = TryGetHighPerformanceAdapter(factory)
                ?? TryGetLargestDedicatedAdapter(factory);

            var minHighBytes = HardwareBudget.MinDedicatedVramBytesForHighMode;
            var extendedBytes = HardwareBudget.MinDedicatedVramBytesForExtendedSteps;
            return new GpuCapabilities(
                vram,
                name,
                vram >= minHighBytes,
                vram >= extendedBytes);
        }
        catch
        {
            return new GpuCapabilities(0, "Unknown", false, false);
        }
    }

    private static (ulong Vram, string Name)? TryGetHighPerformanceAdapter(IDXGIFactory1 _)
    {
        try
        {
            using var factory6 = DXGI.CreateDXGIFactory1<IDXGIFactory6>();
            if (factory6.EnumAdapterByGpuPreference(0, GpuPreference.HighPerformance, out IDXGIAdapter1? adapter).Failure
                || adapter is null)
                return null;

            using (adapter)
            {
                var desc = adapter.Description1;
                if ((desc.Flags & AdapterFlags.Software) != 0)
                    return null;
                return (desc.DedicatedVideoMemory, desc.Description);
            }
        }
        catch
        {
            return null;
        }
    }

    private static (ulong Vram, string Name) TryGetLargestDedicatedAdapter(IDXGIFactory1 factory)
    {
        ulong bestVram = 0;
        var bestName = "Unknown";

        for (uint i = 0; ; i++)
        {
            var result = factory.EnumAdapters1(i, out var adapter);
            if (result.Failure)
                break;

            using (adapter)
            {
                var desc = adapter.Description1;
                if ((desc.Flags & AdapterFlags.Software) != 0)
                    continue;

                var vram = desc.DedicatedVideoMemory;
                if (vram > bestVram)
                {
                    bestVram = vram;
                    bestName = desc.Description;
                }
            }
        }

        return (bestVram, bestName);
    }
}