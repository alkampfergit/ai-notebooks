using SkiaSharp;

namespace DocExtraction.Examples;

/// <summary>
/// Utility class for image loading, resizing, and compression operations.
/// </summary>
public static class ImageUtils
{
    private const int DefaultJpegQuality = 80;

    /// <summary>
    /// Loads an image from a file path.
    /// </summary>
    /// <param name="filePath">Path to the image file</param>
    /// <returns>SKBitmap object or null if loading fails</returns>
    public static SKBitmap? LoadImage(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Image file not found: {filePath}");
        }

        return SKBitmap.Decode(filePath);
    }

    /// <summary>
    /// Resizes an image by reducing width while maintaining aspect ratio until the file size is below the target.
    /// Saves the result as JPEG with 80% quality.
    /// </summary>
    /// <param name="inputPath">Path to the input image file</param>
    /// <param name="outputPath">Path to save the resized image</param>
    /// <param name="targetSizeBytes">Target file size in bytes</param>
    /// <param name="widthReductionStep">Percentage to reduce width by each iteration (default: 10%)</param>
    /// <returns>True if successful, false if unable to meet target size</returns>
    public static bool ResizeToTargetSize(
        string inputPath, 
        string outputPath, 
        long targetSizeBytes, 
        double widthReductionStep = 0.10)
    {
        using var originalBitmap = LoadImage(inputPath);
        if (originalBitmap == null)
        {
            return false;
        }

        var currentWidth = originalBitmap.Width;
        var currentHeight = originalBitmap.Height;

        // Try with original size first
        if (SaveAndCheckSize(originalBitmap, outputPath, targetSizeBytes))
        {
            return true;
        }

        // Iteratively reduce width until target size is met or image becomes too small
        while (currentWidth > 100) // Minimum width threshold
        {
            currentWidth = (int)(currentWidth * (1 - widthReductionStep));
            currentHeight = (int)(originalBitmap.Height * ((double)currentWidth / originalBitmap.Width));

            using var resizedBitmap = ResizeImage(originalBitmap, currentWidth, currentHeight);
            
            if (SaveAndCheckSize(resizedBitmap, outputPath, targetSizeBytes))
            {
                return true;
            }
        }

        return false; // Unable to meet target size
    }

    /// <summary>
    /// Resizes an image to specific dimensions while maintaining aspect ratio.
    /// </summary>
    /// <param name="originalBitmap">Source bitmap</param>
    /// <param name="newWidth">Target width</param>
    /// <param name="newHeight">Target height</param>
    /// <returns>Resized SKBitmap</returns>
    public static SKBitmap ResizeImage(SKBitmap originalBitmap, int newWidth, int newHeight)
    {
        var info = new SKImageInfo(newWidth, newHeight);
        var samplingOptions = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
        var resizedBitmap = originalBitmap.Resize(info, samplingOptions);
        
        if (resizedBitmap == null)
        {
            throw new InvalidOperationException("Failed to resize image");
        }

        return resizedBitmap;
    }

    /// <summary>
    /// Saves a bitmap as JPEG with fixed 80% quality and checks if it meets the target size.
    /// </summary>
    /// <param name="bitmap">Bitmap to save</param>
    /// <param name="outputPath">Output file path</param>
    /// <param name="targetSizeBytes">Target file size in bytes</param>
    /// <returns>True if saved file size is less than or equal to target size</returns>
    private static bool SaveAndCheckSize(SKBitmap bitmap, string outputPath, long targetSizeBytes)
    {
        // Ensure output directory exists
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Save as JPEG with 80% quality
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, DefaultJpegQuality);
        using var stream = File.OpenWrite(outputPath);
        data.SaveTo(stream);

        var fileInfo = new FileInfo(outputPath);
        return fileInfo.Length <= targetSizeBytes;
    }

    /// <summary>
    /// Resizes an image to a specific width while maintaining aspect ratio.
    /// </summary>
    /// <param name="inputPath">Path to the input image file</param>
    /// <param name="outputPath">Path to save the resized image</param>
    /// <param name="targetWidth">Target width in pixels</param>
    public static void ResizeToWidth(string inputPath, string outputPath, int targetWidth)
    {
        using var originalBitmap = LoadImage(inputPath);
        if (originalBitmap == null)
        {
            throw new InvalidOperationException($"Failed to load image: {inputPath}");
        }

        var aspectRatio = (double)originalBitmap.Height / originalBitmap.Width;
        var targetHeight = (int)(targetWidth * aspectRatio);

        using var resizedBitmap = ResizeImage(originalBitmap, targetWidth, targetHeight);
        
        // Ensure output directory exists
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Save as JPEG with 80% quality
        using var image = SKImage.FromBitmap(resizedBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, DefaultJpegQuality);
        using var stream = File.OpenWrite(outputPath);
        data.SaveTo(stream);
    }
}
