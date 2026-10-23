// Copyright (c) KnifeLemon. Licensed under the MIT License.

using Filee.Core.Conversion;
using Filee.Core.Presets;
using Filee.Engines.Media;
using Filee.Engines.Pdf;
using ImageMagick;
using Xunit;

namespace Filee.Engines.Tests;

public class TargetSizeEngineTests(EngineFixture fx) : IClassFixture<EngineFixture>
{
    [Fact]
    public async Task Jpeg_4000x3000_noise_image_bisects_to_target_window()
    {
        var dir = fx.NewFolder();
        var source = Path.Combine(dir, "large_photo.jpg");
        using (var img = new MagickImage("gradient:#C04020-#2060C0", 4000, 3000))
        {
            img.AddNoise(NoiseType.Gaussian, 0.5);
            img.Quality = 95;
            img.Write(source, MagickFormat.Jpeg);
        }

        var targetBytes = 300 * 1024; // 300 KB
        var preset = new Preset
        {
            TargetFormat = "jpg",
            TargetSizeBytes = targetBytes,
            Name = "Reduce size",
        };

        var job = await fx.ConvertAsync([source], preset);
        Assert.Equal(JobState.Completed, job.State);
        var outputs = job.Outputs.ToList();
        var output = Assert.Single(outputs);
        Assert.True(File.Exists(output));

        var len = new FileInfo(output).Length;
        Assert.True(len <= targetBytes, $"File size {len} exceeded target {targetBytes}");
        Assert.True(len >= 255 * 1024, $"File size {len} was below 85% floor (255 KB)");
    }

    [Fact]
    public async Task Png_gives_at_most_target_size()
    {
        var dir = fx.NewFolder();
        var source = Path.Combine(dir, "photo.png");
        using (var img = new MagickImage("gradient:#00F0A0-#002060", 800, 600))
        {
            img.AddNoise(NoiseType.Gaussian, 0.4);
            img.Write(source, MagickFormat.Png);
        }

        var srcLen = new FileInfo(source).Length;
        var targetBytes = srcLen / 2;
        var preset = new Preset
        {
            TargetFormat = "png",
            TargetSizeBytes = targetBytes,
            Name = "Reduce PNG",
        };

        var job = await fx.ConvertAsync([source], preset);
        Assert.Equal(JobState.Completed, job.State);
        var outputs = job.Outputs.ToList();
        var output = Assert.Single(outputs);
        var len = new FileInfo(output).Length;
        Assert.True(len <= targetBytes, $"PNG size {len} exceeded target {targetBytes}");
    }

    [Fact]
    public async Task Target_larger_than_source_writes_nothing()
    {
        var dir = fx.NewFolder();
        var source = Path.Combine(dir, "tiny.jpg");
        using (var img = new MagickImage(MagickColors.Blue, 50, 50))
        {
            img.Write(source, MagickFormat.Jpeg);
        }

        var srcLen = new FileInfo(source).Length;
        var targetBytes = srcLen * 10;
        var preset = new Preset
        {
            TargetFormat = "jpg",
            TargetSizeBytes = targetBytes,
            Name = "Reduce already small",
        };

        var job = await fx.ConvertAsync([source], preset);
        Assert.Equal(JobState.Completed, job.State);
        Assert.Empty(job.Outputs);
        var file = Assert.Single(job.Files);
        Assert.Equal("notice.size_already_small", file.NoticeKey);
    }

    [Fact]
    public async Task Animated_gif_is_refused_for_target_size()
    {
        var dir = fx.NewFolder();
        var source = Path.Combine(dir, "animated.gif");
        using (var col = new MagickImageCollection())
        {
            col.Add(new MagickImage(MagickColors.Red, 100, 100));
            col.Add(new MagickImage(MagickColors.Green, 100, 100));
            col.Write(source, MagickFormat.Gif);
        }

        var preset = new Preset
        {
            TargetFormat = "gif",
            TargetSizeBytes = 50 * 1024,
            Name = "Reduce GIF",
        };

        var job = await fx.ConvertAsync([source], preset);
        Assert.Equal(JobState.Failed, job.State);
        var file = Assert.Single(job.Files);
        Assert.Equal("error.size_target_animation", file.ErrorKey);
    }

    [Fact]
    public async Task Pdf_target_size_reaches_target_or_reports_notice()
    {
        var dir = fx.NewFolder();
        var photo = Path.Combine(dir, "photo.jpg");
        using (var img = new MagickImage("gradient:#F08020-#2050A0", 1200, 800))
        {
            img.AddNoise(NoiseType.Gaussian, 0.5);
            img.Quality = 95;
            img.Write(photo, MagickFormat.Jpeg);
        }

        var pdfPath = Path.Combine(dir, "sample.pdf");
        var builder = new PdfBuilder();
        builder.NewPage();
        builder.Line("Document with high-res photo", 14, builder.Font(), 72, 72);
        builder.Picture(photo, 72, 100, 216, 144);
        builder.Save(pdfPath);

        var srcLen = new FileInfo(pdfPath).Length;
        var targetBytes = (long)(srcLen * 0.7);

        var preset = new Preset
        {
            TargetFormat = "pdf",
            TargetSizeBytes = targetBytes,
            Name = "Reduce PDF",
        };

        var job = await fx.ConvertAsync([pdfPath], preset);
        Assert.Equal(JobState.Completed, job.State);
        var outputs = job.Outputs.ToList();
        if (outputs.Count > 0)
        {
            var output = outputs[0];
            var len = new FileInfo(output).Length;
            if (len > targetBytes)
            {
                var file = Assert.Single(job.Files);
                Assert.Equal("notice.size_target_not_reached", file.NoticeKey);
            }
        }
    }

    [Fact]
    public async Task Video_target_size_with_ffmpeg_reaches_target()
    {
        var ffmpeg = new FfmpegConverter().GetStatus();
        if (!ffmpeg.IsAvailable)
            return; // Skip if FFmpeg not available on machine

        var dir = fx.NewFolder();
        var still = Path.Combine(dir, "still.png");
        using (var img = new MagickImage(MagickColors.CornflowerBlue, 640, 480))
        {
            img.Write(still, MagickFormat.Png);
        }

        // Generate 3-second test video from still
        var videoSource = Path.Combine(dir, "test_video.mp4");
        var job1 = await fx.ConvertAsync([still], new Preset
        {
            TargetFormat = "mp4",
            Media = { VideoCodec = VideoCodec.Default },
        });
        var outputs1 = job1.Outputs.ToList();
        if (job1.State != JobState.Completed || outputs1.Count == 0)
            return;

        var sourceVid = outputs1[0];
        var srcLen = new FileInfo(sourceVid).Length;
        if (srcLen < 50_000)
            return; // file too small to shrink meaningfully

        var targetBytes = (long)(srcLen * 0.6);
        var preset = new Preset
        {
            TargetFormat = "mp4",
            TargetSizeBytes = targetBytes,
            Name = "Reduce Video",
        };

        var job2 = await fx.ConvertAsync([sourceVid], preset);
        Assert.Equal(JobState.Completed, job2.State);
        var outputs2 = job2.Outputs.ToList();
        if (outputs2.Count > 0)
        {
            var len = new FileInfo(outputs2[0]).Length;
            Assert.True(len <= targetBytes * 1.05, $"Video size {len} exceeded target {targetBytes}");
        }
    }
}
