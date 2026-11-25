using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;

namespace TXT2IMG;

/// <summary>
/// Catches blank/near-black PNGs that show up when the GPU driver resets mid-batch at heavy resolutions.
/// </summary>
public static class PngSanity
{
    public static async Task<(int Width, int Height)> GetPixelSizeAsync(byte[] pngBytes)
    {
        using var input = new InMemoryRandomAccessStream();
        await input.WriteAsync(pngBytes.AsBuffer());
        input.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(input);
        return ((int)decoder.PixelWidth, (int)decoder.PixelHeight);
    }

    public static async Task<bool> IsMostlyBlankAsync(byte[] pngBytes)
    {
        if (pngBytes.Length < 512)
            return true;

        try
        {
            using var input = new InMemoryRandomAccessStream();
            await input.WriteAsync(pngBytes.AsBuffer());
            input.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(input);
            var pixelData = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                new BitmapTransform { ScaledWidth = 64, ScaledHeight = 64 },
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage);
            var pixels = pixelData.DetachPixelData();

            long sum = 0;
            var samples = 0;
            for (var i = 0; i + 3 < pixels.Length; i += 4)
            {
                var y = (int)(0.114 * pixels[i] + 0.587 * pixels[i + 1] + 0.299 * pixels[i + 2]);
                sum += y;
                samples++;
            }

            if (samples == 0)
                return true;

            return sum / (double)samples < 1.5;
        }
        catch
        {
            return true;
        }
    }
}