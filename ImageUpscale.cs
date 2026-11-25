using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;

namespace TXT2IMG;

/// <summary>CPU Lanczos/Fant resize after diffusion — same long-edge target as UHD (3840 px).</summary>
public static class ImageUpscale
{
    public const int FourKLongEdgePixels = 3840;

    public static async Task<byte[]> UpscaleTo4KLongEdgeAsync(byte[] pngBytes)
    {
        using var input = new InMemoryRandomAccessStream();
        await input.WriteAsync(pngBytes.AsBuffer());
        input.Seek(0);

        var decoder = await BitmapDecoder.CreateAsync(input);
        var srcW = decoder.PixelWidth;
        var srcH = decoder.PixelHeight;
        if (srcW == 0 || srcH == 0) return pngBytes;

        var longEdge = Math.Max(srcW, srcH);
        if (longEdge >= FourKLongEdgePixels)
            return pngBytes;

        double dstW, dstH;
        if (srcW >= srcH)
        {
            dstW = FourKLongEdgePixels;
            dstH = srcH * (FourKLongEdgePixels / (double)srcW);
        }
        else
        {
            dstH = FourKLongEdgePixels;
            dstW = srcW * (FourKLongEdgePixels / (double)srcH);
        }

        var transform = new BitmapTransform
        {
            ScaledWidth = (uint)Math.Max(1, Math.Round(dstW)),
            ScaledHeight = (uint)Math.Max(1, Math.Round(dstH)),
            InterpolationMode = BitmapInterpolationMode.Fant,
        };

        var pixelData = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            transform,
            ExifOrientationMode.IgnoreExifOrientation,
            ColorManagementMode.DoNotColorManage);

        using var output = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            transform.ScaledWidth,
            transform.ScaledHeight,
            decoder.DpiX,
            decoder.DpiY,
            pixelData.DetachPixelData());
        await encoder.FlushAsync();

        var reader = new DataReader(output.GetInputStreamAt(0));
        var size = (uint)output.Size;
        await reader.LoadAsync(size);
        var result = new byte[size];
        reader.ReadBytes(result);
        return result;
    }
}