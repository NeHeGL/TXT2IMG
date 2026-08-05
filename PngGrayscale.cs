using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;

namespace TXT2IMG;

// Prompt negatives alone can't kill color in DreamShaper (live Black & White: orange water, blue hull).
// Force true grayscale on the PNG pixels after generation.
public static class PngGrayscale
{
    public static async Task<byte[]> ConvertAsync(byte[] pngBytes)
    {
        using var input = new InMemoryRandomAccessStream();
        await input.WriteAsync(pngBytes.AsBuffer());
        input.Seek(0);

        var decoder = await BitmapDecoder.CreateAsync(input);
        var pixelData = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            new BitmapTransform(),
            ExifOrientationMode.IgnoreExifOrientation,
            ColorManagementMode.DoNotColorManage);
        var pixels = pixelData.DetachPixelData();

        for (var i = 0; i + 3 < pixels.Length; i += 4)
        {
            // BGRA — Rec.601 luminance
            var y = (byte)(0.114 * pixels[i] + 0.587 * pixels[i + 1] + 0.299 * pixels[i + 2]);
            pixels[i] = y;
            pixels[i + 1] = y;
            pixels[i + 2] = y;
        }

        using var output = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            decoder.PixelWidth,
            decoder.PixelHeight,
            decoder.DpiX,
            decoder.DpiY,
            pixels);
        await encoder.FlushAsync();

        var reader = new DataReader(output.GetInputStreamAt(0));
        var size = (uint)output.Size;
        await reader.LoadAsync(size);
        var result = new byte[size];
        reader.ReadBytes(result);
        return result;
    }
}
