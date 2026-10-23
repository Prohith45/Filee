// Copyright (c) KnifeLemon. Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Filee.Engines.Infrastructure;

/// <summary>
/// Result of a size bisection fitting probe.
/// </summary>
public sealed record FitResult<T>(T? BestValue, long BestSize, bool ReachedTarget, int ProbesRun) where T : struct;

/// <summary>
/// Monotonic bisection helper to search for an encoding parameter that yields an output size
/// less than or equal to a target size, ideally within the 85% - 100% window.
/// </summary>
public static class SizeFitter
{
    public const double TargetRatioMin = 0.85;
    public const int MaxProbes = 9;

    /// <summary>
    /// Fits an integer parameter in [low..high] (higher parameter yields larger output size, e.g. quality or scale).
    /// </summary>
    public static async Task<FitResult<int>> FitIntegerAsync(
        int low,
        int high,
        long targetBytes,
        Func<int, CancellationToken, Task<long>> probe,
        CancellationToken cancellationToken = default)
    {
        int l = low;
        int r = high;
        int? bestVal = null;
        long bestSize = 0;
        int probes = 0;

        while (l <= r && probes < MaxProbes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int mid = l + (r - l) / 2;
            probes++;
            long size = await probe(mid, cancellationToken).ConfigureAwait(false);

            if (size <= targetBytes)
            {
                if (bestVal is null || size > bestSize)
                {
                    bestVal = mid;
                    bestSize = size;
                }
                l = mid + 1;
            }
            else
            {
                r = mid - 1;
            }
        }

        bool reached = bestVal.HasValue && bestSize <= targetBytes && bestSize >= targetBytes * TargetRatioMin;
        return new FitResult<int>(bestVal, bestSize, reached, probes);
    }

    /// <summary>
    /// Fits an integer parameter in [low..high] synchronously.
    /// </summary>
    public static FitResult<int> FitInteger(
        int low,
        int high,
        long targetBytes,
        Func<int, CancellationToken, long> probe,
        CancellationToken cancellationToken = default)
    {
        int l = low;
        int r = high;
        int? bestVal = null;
        long bestSize = 0;
        int probes = 0;

        while (l <= r && probes < MaxProbes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int mid = l + (r - l) / 2;
            probes++;
            long size = probe(mid, cancellationToken);

            if (size <= targetBytes)
            {
                if (bestVal is null || size > bestSize)
                {
                    bestVal = mid;
                    bestSize = size;
                }
                l = mid + 1;
            }
            else
            {
                r = mid - 1;
            }
        }

        bool reached = bestVal.HasValue && bestSize <= targetBytes && bestSize >= targetBytes * TargetRatioMin;
        return new FitResult<int>(bestVal, bestSize, reached, probes);
    }
}
