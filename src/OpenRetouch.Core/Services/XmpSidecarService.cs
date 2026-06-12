using Microsoft.Extensions.Logging;
using OpenRetouch.Core.Editing;
using OpenRetouch.Core.Models;

namespace OpenRetouch.Core.Services;

/// <inheritdoc cref="IXmpSidecarService"/>
public sealed class XmpSidecarService : IXmpSidecarService
{
    private readonly ILogger<XmpSidecarService> _logger;

    public XmpSidecarService(ILogger<XmpSidecarService> logger)
    {
        _logger = logger;
    }

    public string GetSidecarPath(string rawFilePath) => Path.ChangeExtension(rawFilePath, ".xmp");

    public async Task<XmpSidecarData?> TryReadAsync(Photo photo, CancellationToken ct = default)
    {
        if (!RawFileTypes.IsRaw(photo.FileExtension))
        {
            return null;
        }

        var sidecarPath = GetSidecarPath(photo.FilePath);
        if (!File.Exists(sidecarPath))
        {
            return null;
        }

        try
        {
            var xml = await File.ReadAllTextAsync(sidecarPath, ct);
            var data = XmpConverter.FromXmp(xml);
            _logger.LogInformation("XMP sidecar loaded: {Path}", sidecarPath);
            return data;
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Failed to read XMP sidecar: {Path}", sidecarPath);
            return null;
        }
    }

    public async Task WriteAsync(Photo photo, EditSettings settings, CancellationToken ct = default)
    {
        if (!RawFileTypes.IsRaw(photo.FileExtension))
        {
            return;
        }

        var sidecarPath = GetSidecarPath(photo.FilePath);
        try
        {
            string? existingXml = null;
            if (File.Exists(sidecarPath))
            {
                existingXml = await File.ReadAllTextAsync(sidecarPath, ct);
            }

            var xml = XmpConverter.ToXmp(settings, photo.Rating, photo.ColorLabel, existingXml);

            // 並行書き込みに備えて一意な一時ファイル→アトミックMove
            var tempPath = sidecarPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await File.WriteAllTextAsync(tempPath, xml, ct);
                File.Move(tempPath, sidecarPath, overwrite: true);
            }
            catch
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }

                throw;
            }

            _logger.LogDebug("XMP sidecar written: {Path}", sidecarPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // サイドカー書き込み失敗で編集保存全体を失敗させない(DB保存は成功している)
            _logger.LogWarning(ex, "Failed to write XMP sidecar: {Path}", sidecarPath);
        }
    }
}
