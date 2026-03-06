using System.Globalization;
using System.Linq;
using PlatzPilot.Configuration;
using PlatzPilot.Models;
using PlatzPilot.Resources.Strings;

namespace PlatzPilot.Services;

public sealed class MensaForecastService
{
    public const string MensaVirtualSpaceId = "MENSA_VIRTUAL";
    private const double MensaFluxThreshold = 15;
    private const int MensaFluxLookbackMinutes = 15;
    private const int MensaQueueBufferMinutes = 15;
    private const int MensaCapacityMin = 200;
    private const double MensaCoverageMinRatio = 0.5;
    private const int MensaCoverageStaleMinutes = 30;

    private readonly AppConfig _config;
    private readonly SafeArrivalForecastService _safeArrivalForecastService;

    public MensaForecastService(AppConfig config, SafeArrivalForecastService safeArrivalForecastService)
    {
        _config = config;
        _safeArrivalForecastService = safeArrivalForecastService;
    }

    /// <summary>
    /// Builds the Mensa forecast by approximating Mensa occupancy from the campus-wide
    /// occupancy deficit during the lunch window when direct Mensa sensors are missing.
    /// Steps:
    /// 1) Determine the lunch window (window start + eating buffer) and the time step.
    /// 2) Build per-space snapshots for recent open days (ignoring small-capacity spaces).
    /// 3) For each day and time bin, compute a baseline occupancy by linear interpolation
    ///    between window-start and window-end anchors; deficit = max(0, baseline - observed).
    ///    Sum deficits across all spaces to form D_campus(t).
    /// 4) Build a virtual history from D_campus(t), compute the peak daily deficit across
    ///    recent days, and clamp capacity to a minimum to stabilize low-traffic periods.
    /// 5) Create a virtual StudySpace and run SafeArrival; apply a fixed queue buffer and
    ///    a coverage-based confidence guard (low live-coverage lowers confidence).
    /// 6) For today, blend live data via hybrid deficits and compute the flux label.
    /// </summary>
    public MensaForecastResult? BuildForecast(
        IReadOnlyList<StudySpace> spaces,
        IReadOnlyDictionary<string, List<SeatHistoryPoint>> historyByLocation,
        DateTime referenceDate,
        DateTime referenceTime,
        DateTime now)
    {
        if (spaces.Count == 0 || historyByLocation.Count == 0)
        {
            return null;
        }

        var mensaConfig = _config.MensaForecast;

        var stepMinutes = Math.Max(1, mensaConfig.StepMinutes);
        var windowStart = mensaConfig.WindowStart;
        var bufferMinutes = Math.Max(0, mensaConfig.EatingBufferMinutes);
        var windowEnd = mensaConfig.CashDeskClose.Add(TimeSpan.FromMinutes(bufferMinutes));
        var openingHoursEnd = mensaConfig.CashDeskClose;

        if (windowEnd <= windowStart)
        {
            return null;
        }

        var windowStartMinute = NormalizeToStepMinutes(windowStart, stepMinutes);
        var windowEndMinute = NormalizeToStepMinutes(windowEnd, stepMinutes);
        if (windowEndMinute <= windowStartMinute)
        {
            return null;
        }

        var historyDays = Math.Max(1, _config.SafeArrival.HistoryWindowDays);
        var endDate = referenceDate.Date.AddDays(-1);
        var startDate = endDate.AddDays(-(historyDays - 1));

        var snapshots = BuildMensaSpaceSnapshots(
            spaces,
            historyByLocation,
            startDate,
            endDate,
            windowStartMinute,
            windowEndMinute,
            stepMinutes,
            mensaConfig);

        var fluxLabel = BuildMensaFluxLabel(
            now,
            windowStartMinute,
            windowEndMinute,
            stepMinutes,
            snapshots);

        var coverageRatio = CalculateMensaCoverageRatio(spaces, referenceTime);

        if (snapshots.Count == 0)
        {
            return new MensaForecastResult(
                null,
                fluxLabel,
                snapshots,
                windowStartMinute,
                windowEndMinute,
                stepMinutes,
                windowStart,
                windowEnd,
                openingHoursEnd,
                referenceDate,
                Array.Empty<float>(),
                0);
        }

        var history = BuildMensaVirtualHistory(
            startDate,
            endDate,
            windowStartMinute,
            windowEndMinute,
            stepMinutes,
            snapshots);
        AppendMensaLiveHistoryPoints(
            history,
            DateTime.Today,
            now,
            windowStartMinute,
            windowEndMinute,
            stepMinutes,
            snapshots);

        var peakDeficitHistory = CalculateMensaPeakDeficitHistory(
            referenceDate,
            historyDays,
            historyDays,
            windowStartMinute,
            windowEndMinute,
            stepMinutes,
            snapshots);
        var capacity = Math.Max(peakDeficitHistory, MensaCapacityMin);
        NormalizeMensaHistoryCapacity(history, capacity);

        SafeArrivalRecommendation? recommendation = null;
        IReadOnlyList<float> chartSeries = Array.Empty<float>();
        if (history.Count > 0)
        {
            history.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));

            var virtualSpace = new StudySpace
            {
                Id = MensaVirtualSpaceId,
                Name = AppResources.MensaVirtualName,
                TotalSeats = capacity,
                OpeningHours = BuildMensaOpeningHours(referenceDate, windowStart, windowEnd),
                ReferenceTime = referenceDate
            };

            recommendation = _safeArrivalForecastService.Calculate(virtualSpace, history, referenceDate);
            recommendation = ApplyMensaQueueBuffer(recommendation, windowStart);
            recommendation = ApplyMensaCoverageGuard(recommendation, coverageRatio);

            var chartConfig = _config.Charts;
            var binMinutes = Math.Max(1, chartConfig.BinMinutes);
            var endTime = OccupancySeriesBuilder.NormalizeToChartBin(now, binMinutes);
            var startTime = endTime.AddHours(-chartConfig.HistoryHours);
            chartSeries = OccupancySeriesBuilder.BuildSeries(history, startTime, endTime, binMinutes);
        }

        return new MensaForecastResult(
            recommendation,
            fluxLabel,
            snapshots,
            windowStartMinute,
            windowEndMinute,
            stepMinutes,
            windowStart,
            windowEnd,
            openingHoursEnd,
            referenceDate,
            chartSeries,
            capacity);
    }

    public StudySpace? BuildVirtualSpace(MensaForecastResult? result, DateTime referenceTime)
    {
        if (result == null || result.Snapshots.Count == 0)
        {
            return null;
        }

        var capacity = result.Capacity;
        if (capacity <= 0)
        {
            return null;
        }

        var mensaConfig = _config.MensaForecast;
        var occupiedSeats = 0;
        var hasOccupancy = TryGetMensaOccupancy(referenceTime, result, out occupiedSeats);
        var freeSeats = hasOccupancy
            ? Math.Max(0, capacity - occupiedSeats)
            : 0;

        return new StudySpace
        {
            Id = MensaVirtualSpaceId,
            Name = AppResources.MensaDisplayName,
            TotalSeats = capacity,
            OccupiedSeats = occupiedSeats,
            FreeSeats = freeSeats,
            IsManualCount = false,
            IsMensaVirtual = true,
            Latitude = mensaConfig.Latitude,
            Longitude = mensaConfig.Longitude,
            OpeningHours = BuildMensaOpeningHours(result.ReferenceDate, result.WindowStart, result.OpeningHoursEnd),
            LastUpdated = hasOccupancy ? referenceTime : DateTime.MinValue,
            ReferenceTime = referenceTime,
            SafeArrivalRecommendation = result.Recommendation
        };
    }

    private static SafeArrivalRecommendation? ApplyMensaQueueBuffer(
        SafeArrivalRecommendation? recommendation,
        TimeSpan windowStart)
    {
        if (recommendation == null || !recommendation.HasRecommendation)
        {
            return recommendation;
        }

        var adjustedLatest = recommendation.LatestSafeTime - TimeSpan.FromMinutes(MensaQueueBufferMinutes);
        if (adjustedLatest < windowStart)
        {
            adjustedLatest = windowStart;
        }

        return new SafeArrivalRecommendation
        {
            HasRecommendation = recommendation.HasRecommendation,
            LatestSafeTime = adjustedLatest,
            Probability = recommendation.Probability,
            ExpectedFreeSeats = recommendation.ExpectedFreeSeats,
            ConfidenceFlag = recommendation.ConfidenceFlag,
            HasPeakData = recommendation.HasPeakData,
            PeakTime = recommendation.PeakTime,
            PeakExpectedFreeSeats = recommendation.PeakExpectedFreeSeats,
            PeakOccupancyRate = recommendation.PeakOccupancyRate,
            PeakTrendMinutesPerDay = recommendation.PeakTrendMinutesPerDay
        };
    }

    private List<MensaSpaceSnapshot> BuildMensaSpaceSnapshots(
        IReadOnlyList<StudySpace> spaces,
        IReadOnlyDictionary<string, List<SeatHistoryPoint>> historyByLocation,
        DateTime startDate,
        DateTime endDate,
        int windowStartMinute,
        int windowEndMinute,
        int stepMinutes,
        MensaForecastConfig mensaConfig)
    {
        var snapshots = new List<MensaSpaceSnapshot>();

        foreach (var space in spaces)
        {
            if (space.TotalSeats < mensaConfig.MinSpaceCapacity)
            {
                continue;
            }

            if (!historyByLocation.TryGetValue(space.Id, out var history) || history.Count == 0)
            {
                continue;
            }

            var occupiedByDay = BuildOccupiedByDay(
                history,
                startDate,
                endDate,
                windowStartMinute,
                windowEndMinute,
                stepMinutes);

            if (occupiedByDay.Count == 0)
            {
                continue;
            }

            var occupiedToday = BuildOccupiedForDay(
                history,
                DateTime.Today,
                windowStartMinute,
                windowEndMinute,
                stepMinutes);

            snapshots.Add(new MensaSpaceSnapshot(occupiedByDay, occupiedToday));
        }

        return snapshots;
    }

    private static Dictionary<DateTime, SortedList<int, int>> BuildOccupiedByDay(
        IReadOnlyList<SeatHistoryPoint> history,
        DateTime startDate,
        DateTime endDate,
        int windowStartMinute,
        int windowEndMinute,
        int stepMinutes)
    {
        var occupiedByDay = new Dictionary<DateTime, SortedList<int, int>>();

        foreach (var point in history)
        {
            var day = point.Timestamp.Date;
            if (day < startDate || day > endDate)
            {
                continue;
            }

            if (!IsMensaOpenDay(day))
            {
                continue;
            }

            var minute = NormalizeToStepMinutes(point.Timestamp.TimeOfDay, stepMinutes);
            if (minute < windowStartMinute || minute > windowEndMinute)
            {
                continue;
            }

            if (!occupiedByDay.TryGetValue(day, out var dayMap))
            {
                dayMap = new SortedList<int, int>();
                occupiedByDay[day] = dayMap;
            }

            if (!dayMap.ContainsKey(minute))
            {
                dayMap[minute] = Math.Max(0, point.OccupiedSeats);
            }
        }

        return occupiedByDay;
    }

    private static SortedList<int, int> BuildOccupiedForDay(
        IReadOnlyList<SeatHistoryPoint> history,
        DateTime day,
        int windowStartMinute,
        int windowEndMinute,
        int stepMinutes)
    {
        var occupiedByMinute = new SortedList<int, int>();

        foreach (var point in history)
        {
            if (point.Timestamp.Date != day)
            {
                continue;
            }

            var minute = NormalizeToStepMinutes(point.Timestamp.TimeOfDay, stepMinutes);
            if (minute < windowStartMinute || minute > windowEndMinute)
            {
                continue;
            }

            if (!occupiedByMinute.ContainsKey(minute))
            {
                occupiedByMinute[minute] = Math.Max(0, point.OccupiedSeats);
            }
        }

        return occupiedByMinute;
    }

    /// <summary>
    /// Builds a virtual Mensa history by converting the campus deficit D_campus(t)
    /// into occupied-seat samples for each open day and time bin in the lunch window.
    /// </summary>
    private static List<SeatHistoryPoint> BuildMensaVirtualHistory(
        DateTime startDate,
        DateTime endDate,
        int windowStartMinute,
        int windowEndMinute,
        int stepMinutes,
        List<MensaSpaceSnapshot> snapshots)
    {
        var history = new List<SeatHistoryPoint>();

        for (var day = startDate; day <= endDate; day = day.AddDays(1))
        {
            if (!IsMensaOpenDay(day))
            {
                continue;
            }

            for (var minute = windowStartMinute; minute <= windowEndMinute; minute += stepMinutes)
            {
                if (!TryCalculateTotalDeficit(day, minute, windowStartMinute, windowEndMinute, snapshots, out var totalDeficit))
                {
                    continue;
                }

                history.Add(new SeatHistoryPoint
                {
                    Timestamp = day.AddMinutes(minute),
                    FreeSeats = 0,
                    OccupiedSeats = Math.Max(0, (int)Math.Round(totalDeficit)),
                    IsManualCount = false
                });
            }
        }

        return history;
    }

    /// <summary>
    /// Computes the peak campus deficit across recent open days by taking the maximum
    /// D_campus(t) within the lunch window for each day, then the maximum of those peaks.
    /// Used as the dynamic capacity normalization baseline.
    /// </summary>
    private static int CalculateMensaPeakDeficitHistory(
        DateTime referenceDate,
        int maxDays,
        int historyWindowDays,
        int windowStartMinute,
        int windowEndMinute,
        int stepMinutes,
        List<MensaSpaceSnapshot> snapshots)
    {
        var recentDays = GetRecentMensaOpenDays(referenceDate.Date, maxDays, historyWindowDays);
        if (recentDays.Count == 0)
        {
            return 0;
        }

        var peak = 0d;

        foreach (var day in recentDays)
        {
            var dayPeak = 0d;

            for (var minute = windowStartMinute; minute <= windowEndMinute; minute += stepMinutes)
            {
                if (!TryCalculateTotalDeficit(day, minute, windowStartMinute, windowEndMinute, snapshots, out var totalDeficit))
                {
                    continue;
                }

                if (totalDeficit > dayPeak)
                {
                    dayPeak = totalDeficit;
                }
            }

            if (dayPeak > peak)
            {
                peak = dayPeak;
            }
        }

        return peak <= 0 ? 0 : (int)Math.Round(peak);
    }

    private double CalculateMensaCoverageRatio(IReadOnlyList<StudySpace> spaces, DateTime referenceTime)
    {
        if (spaces.Count == 0)
        {
            return 0;
        }

        var cutoff = referenceTime - TimeSpan.FromMinutes(MensaCoverageStaleMinutes);
        var totalSeats = 0;
        var coveredSeats = 0;

        foreach (var space in spaces)
        {
            var seats = Math.Max(0, space.TotalSeats);
            if (seats == 0)
            {
                continue;
            }

            totalSeats += seats;
            if (space.LastUpdated >= cutoff &&
                space.LastUpdated.Year > _config.UiNumbers.UnknownYearThreshold)
            {
                coveredSeats += seats;
            }
        }

        return totalSeats > 0 ? coveredSeats / (double)totalSeats : 0;
    }

    private static SafeArrivalRecommendation? ApplyMensaCoverageGuard(
        SafeArrivalRecommendation? recommendation,
        double coverageRatio)
    {
        if (recommendation == null || coverageRatio >= MensaCoverageMinRatio)
        {
            return recommendation;
        }

        return new SafeArrivalRecommendation
        {
            HasRecommendation = recommendation.HasRecommendation,
            LatestSafeTime = recommendation.LatestSafeTime,
            Probability = recommendation.Probability,
            ExpectedFreeSeats = recommendation.ExpectedFreeSeats,
            ConfidenceFlag = false,
            HasPeakData = recommendation.HasPeakData,
            PeakTime = recommendation.PeakTime,
            PeakExpectedFreeSeats = recommendation.PeakExpectedFreeSeats,
            PeakOccupancyRate = recommendation.PeakOccupancyRate,
            PeakTrendMinutesPerDay = recommendation.PeakTrendMinutesPerDay
        };
    }

    private static void NormalizeMensaHistoryCapacity(List<SeatHistoryPoint> history, int capacity)
    {
        if (history.Count == 0)
        {
            return;
        }

        var normalizedCapacity = Math.Max(0, capacity);
        for (var i = 0; i < history.Count; i++)
        {
            var point = history[i];
            history[i] = new SeatHistoryPoint
            {
                Timestamp = point.Timestamp,
                FreeSeats = Math.Max(0, normalizedCapacity - point.OccupiedSeats),
                OccupiedSeats = point.OccupiedSeats,
                IsManualCount = point.IsManualCount
            };
        }
    }

    private static void AppendMensaLiveHistoryPoints(
        List<SeatHistoryPoint> history,
        DateTime today,
        DateTime now,
        int windowStartMinute,
        int windowEndMinute,
        int stepMinutes,
        List<MensaSpaceSnapshot> snapshots)
    {
        if (!IsMensaOpenDay(today))
        {
            return;
        }

        if (!TryGetLastMensaReferenceDay(today, snapshots, out var referenceDay))
        {
            return;
        }

        var nowMinute = NormalizeToStepMinutes(now.TimeOfDay, stepMinutes);
        if (nowMinute < windowStartMinute)
        {
            return;
        }

        var limitMinute = Math.Min(nowMinute, windowEndMinute);
        for (var minute = windowStartMinute; minute <= limitMinute; minute += stepMinutes)
        {
            if (!TryCalculateHybridTotalDeficit(referenceDay, minute, windowStartMinute, windowEndMinute, snapshots, out var totalDeficit))
            {
                continue;
            }

            history.Add(new SeatHistoryPoint
            {
                Timestamp = today.AddMinutes(minute),
                FreeSeats = 0,
                OccupiedSeats = Math.Max(0, (int)Math.Round(totalDeficit)),
                IsManualCount = false
            });
        }

        if (nowMinute >= windowEndMinute)
        {
            var endTimestamp = today.AddMinutes(windowEndMinute);
            for (var i = history.Count - 1; i >= 0; i--)
            {
                if (history[i].Timestamp == endTimestamp)
                {
                    history.RemoveAt(i);
                }
            }

            history.Add(new SeatHistoryPoint
            {
                Timestamp = endTimestamp,
                FreeSeats = 0,
                OccupiedSeats = 0,
                IsManualCount = false
            });
        }
    }

    private static bool TryGetLastMensaReferenceDay(
        DateTime today,
        List<MensaSpaceSnapshot> snapshots,
        out DateTime referenceDay)
    {
        referenceDay = DateTime.MinValue;

        foreach (var snapshot in snapshots)
        {
            foreach (var day in snapshot.OccupiedByDay.Keys.OrderByDescending(day => day))
            {
                if (day >= today || !IsMensaOpenDay(day))
                {
                    continue;
                }

                referenceDay = day;
                return true;
            }
        }

        return false;
    }

    private static List<DateTime> GetRecentMensaOpenDays(DateTime referenceDay, int maxDays, int historyWindowDays)
    {
        var days = new List<DateTime>();

        var currentDay = referenceDay.AddDays(-1);
        var cutoff = referenceDay.AddDays(-historyWindowDays);

        while (days.Count < maxDays && currentDay >= cutoff)
        {
            if (IsMensaOpenDay(currentDay))
            {
                days.Add(currentDay);
            }

            currentDay = currentDay.AddDays(-1);
        }

        return days;
    }

    private string BuildMensaFluxLabel(
        DateTime now,
        int windowStartMinute,
        int windowEndMinute,
        int stepMinutes,
        List<MensaSpaceSnapshot> snapshots)
    {
        var nowDate = now.Date;
        if (!IsMensaOpenDay(nowDate))
        {
            return string.Empty;
        }

        var nowMinute = NormalizeToStepMinutes(now.TimeOfDay, stepMinutes);
        var past = now.AddMinutes(-MensaFluxLookbackMinutes);
        var pastMinute = NormalizeToStepMinutes(past.TimeOfDay, stepMinutes);

        if (nowMinute < windowStartMinute || nowMinute > windowEndMinute || pastMinute < windowStartMinute)
        {
            return string.Empty;
        }

        if (!TryCalculateHybridTotalDeficit(nowDate, nowMinute, windowStartMinute, windowEndMinute, snapshots, out var nowDeficit) ||
            !TryCalculateHybridTotalDeficit(nowDate, pastMinute, windowStartMinute, windowEndMinute, snapshots, out var pastDeficit))
        {
            return string.Empty;
        }

        var deltaMinutes = nowMinute - pastMinute;
        if (deltaMinutes <= 0)
        {
            return string.Empty;
        }

        var flux = (nowDeficit - pastDeficit) / deltaMinutes * MensaFluxLookbackMinutes;
        if (flux > MensaFluxThreshold)
        {
            return AppResources.MensaFluxFillingFastLabel;
        }

        if (flux < -MensaFluxThreshold)
        {
            return AppResources.MensaFluxEmptyingLabel;
        }

        return string.Empty;
    }

    /// <summary>
    /// Calculates D_campus(t) for a specific day and minute by summing per-space deficits.
    /// For each space, a baseline is linearly interpolated between window-start and window-end
    /// anchors; deficit = max(0, baseline - observed occupancy at the requested minute).
    /// Returns false if no usable data exists for the timestamp.
    /// </summary>
    private static bool TryCalculateTotalDeficit(
        DateTime day,
        int minute,
        int windowStartMinute,
        int windowEndMinute,
        List<MensaSpaceSnapshot> snapshots,
        out double totalDeficit)
    {
        totalDeficit = 0;
        if (minute < windowStartMinute || minute > windowEndMinute || windowEndMinute <= windowStartMinute)
        {
            return false;
        }

        var hasData = false;

        foreach (var snapshot in snapshots)
        {
            if (!snapshot.OccupiedByDay.TryGetValue(day, out var dayMap))
            {
                continue;
            }

            if (!TryGetBaselineAnchors(
                    dayMap,
                    windowStartMinute,
                    windowEndMinute,
                    out var anchorStartMinute,
                    out var anchorEndMinute,
                    out var occStart,
                    out var occEnd))
            {
                continue;
            }

            if (minute < anchorStartMinute || minute > anchorEndMinute)
            {
                continue;
            }

            if (!TryGetNearestOccupied(dayMap, minute, out var occNow))
            {
                continue;
            }

            hasData = true;
            var baseline = (double)occStart;
            if (anchorEndMinute > anchorStartMinute)
            {
                var frac = (minute - anchorStartMinute) / (double)(anchorEndMinute - anchorStartMinute);
                baseline = occStart + (occEnd - occStart) * frac;
            }

            var deficit = Math.Max(0, baseline - occNow);
            totalDeficit += deficit;
        }

        return hasData;
    }

    private static bool TryGetNearestOccupied(SortedList<int, int> dayMap, int targetMinute, out int occupied)
    {
        occupied = 0;
        if (dayMap.Count == 0)
        {
            return false;
        }

        var keys = dayMap.Keys;
        var values = dayMap.Values;

        var lo = 0;
        var hi = keys.Count - 1;
        while (lo <= hi)
        {
            var mid = lo + ((hi - lo) / 2);
            var key = keys[mid];
            if (key == targetMinute)
            {
                occupied = values[mid];
                return true;
            }

            if (key < targetMinute)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        var insertIndex = lo;
        int bestIndex;
        if (insertIndex <= 0)
        {
            bestIndex = 0;
        }
        else if (insertIndex >= keys.Count)
        {
            bestIndex = keys.Count - 1;
        }
        else
        {
            var leftDiff = Math.Abs(keys[insertIndex - 1] - targetMinute);
            var rightDiff = Math.Abs(keys[insertIndex] - targetMinute);
            bestIndex = leftDiff <= rightDiff ? insertIndex - 1 : insertIndex;
        }

        var bestDiff = Math.Abs(keys[bestIndex] - targetMinute);
        if (bestDiff > 30)
        {
            return false;
        }

        occupied = values[bestIndex];
        return true;
    }

    private static bool TryGetBaselineAnchors(
        SortedList<int, int> dayMap,
        int windowStartMinute,
        int windowEndMinute,
        out int anchorStartMinute,
        out int anchorEndMinute,
        out int occStart,
        out int occEnd)
    {
        anchorStartMinute = 0;
        anchorEndMinute = 0;
        occStart = 0;
        occEnd = 0;

        if (dayMap.Count == 0)
        {
            return false;
        }

        var keys = dayMap.Keys;
        var values = dayMap.Values;

        var startIndex = 0;
        while (startIndex < keys.Count && keys[startIndex] < windowStartMinute)
        {
            startIndex++;
        }

        var endIndex = keys.Count - 1;
        while (endIndex >= 0 && keys[endIndex] > windowEndMinute)
        {
            endIndex--;
        }

        if (startIndex >= keys.Count || endIndex < 0 || startIndex > endIndex)
        {
            return false;
        }

        anchorStartMinute = keys[startIndex];
        anchorEndMinute = keys[endIndex];
        occStart = values[startIndex];
        occEnd = values[endIndex];
        return true;
    }

    /// <summary>
    /// Calculates a live D_campus(t) for today by anchoring to the most recent historical
    /// reference day and applying the hybrid deficit model (historical slope with live data).
    /// </summary>
    private static bool TryCalculateLiveTotalDeficit(
        DateTime today,
        int minute,
        int windowStartMinute,
        int windowEndMinute,
        List<MensaSpaceSnapshot> snapshots,
        out double totalDeficit)
    {
        totalDeficit = 0;
        if (minute < windowStartMinute || minute > windowEndMinute || windowEndMinute <= windowStartMinute)
        {
            return false;
        }

        if (!TryGetLastMensaReferenceDay(today, snapshots, out var referenceDay))
        {
            return false;
        }

        return TryCalculateHybridTotalDeficit(
            referenceDay,
            minute,
            windowStartMinute,
            windowEndMinute,
            snapshots,
            out totalDeficit);
    }

    /// <summary>
    /// Hybrid deficit model for the current day: uses the historical reference day's
    /// window slope as baseline shape, but anchors it to today's live occupancy at the
    /// window start and compares against today's live occupancy at the target minute.
    /// </summary>
    private static bool TryCalculateHybridTotalDeficit(
        DateTime referenceDay,
        int minute,
        int windowStartMinute,
        int windowEndMinute,
        List<MensaSpaceSnapshot> snapshots,
        out double totalDeficit)
    {
        totalDeficit = 0;
        var hasData = false;

        foreach (var snapshot in snapshots)
        {
            if (!snapshot.OccupiedByDay.TryGetValue(referenceDay, out var referenceDayMap))
            {
                continue;
            }

            if (!TryGetBaselineAnchors(
                    referenceDayMap,
                    windowStartMinute,
                    windowEndMinute,
                    out var anchorStartMinute,
                    out var anchorEndMinute,
                    out var occStartReference,
                    out var occEndReference))
            {
                continue;
            }

            if (minute < anchorStartMinute || minute > anchorEndMinute)
            {
                continue;
            }

            if (!TryGetNearestOccupied(snapshot.OccupiedToday, anchorStartMinute, out var occStartToday) ||
                !TryGetNearestOccupied(snapshot.OccupiedToday, minute, out var occNow))
            {
                continue;
            }

            var deltaHist = occEndReference - occStartReference;
            var progressRatio = anchorEndMinute > anchorStartMinute
                ? (minute - anchorStartMinute) / (double)(anchorEndMinute - anchorStartMinute)
                : 0;
            var baseline = occStartToday + (progressRatio * deltaHist);
            var deficit = Math.Max(0, baseline - occNow);
            totalDeficit += deficit;
            hasData = true;
        }

        return hasData;
    }

    private static bool TryGetMensaOccupancy(
        DateTime referenceTime,
        MensaForecastResult cache,
        out int occupiedSeats)
    {
        occupiedSeats = 0;

        if (!IsMensaOpenDay(referenceTime.Date))
        {
            return false;
        }

        var minute = NormalizeToStepMinutes(referenceTime.TimeOfDay, cache.StepMinutes);
        if (!TryCalculateTotalDeficit(
                referenceTime.Date,
                minute,
                cache.WindowStartMinute,
                cache.WindowEndMinute,
                cache.Snapshots,
                out var totalDeficit))
        {
            if (referenceTime.Date != DateTime.Today ||
                !TryCalculateLiveTotalDeficit(
                    referenceTime.Date,
                    minute,
                    cache.WindowStartMinute,
                    cache.WindowEndMinute,
                    cache.Snapshots,
                    out totalDeficit))
            {
                return false;
            }
        }

        occupiedSeats = Math.Max(0, (int)Math.Round(totalDeficit));
        return true;
    }

    private OpeningHoursDto BuildMensaOpeningHours(DateTime referenceDate, TimeSpan windowStart, TimeSpan windowEnd)
    {
        var format = string.IsNullOrWhiteSpace(_config.SeatFinder.TimestampFormat)
            ? "yyyy-MM-dd HH:mm:ss.ffffff"
            : _config.SeatFinder.TimestampFormat;

        var daysSinceMonday = ((int)referenceDate.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var monday = referenceDate.Date.AddDays(-daysSinceMonday);

        var weeklyOpeningHours = new List<List<TimestampDto>>();
        for (var dayOffset = 0; dayOffset < 5; dayOffset++)
        {
            var day = monday.AddDays(dayOffset);
            weeklyOpeningHours.Add(
            [
                new TimestampDto { Date = day.Add(windowStart).ToString(format, CultureInfo.InvariantCulture) },
                new TimestampDto { Date = day.Add(windowEnd).ToString(format, CultureInfo.InvariantCulture) }
            ]);
        }

        return new OpeningHoursDto
        {
            WeeklyOpeningHours = weeklyOpeningHours
        };
    }

    private static int NormalizeToStepMinutes(TimeSpan timeOfDay, int stepMinutes)
    {
        if (stepMinutes <= 0)
        {
            return (int)timeOfDay.TotalMinutes;
        }

        var totalMinutes = (int)Math.Round(timeOfDay.TotalMinutes);
        return (totalMinutes / stepMinutes) * stepMinutes;
    }

    private static bool IsMensaOpenDay(DateTime date)
    {
        return date.DayOfWeek >= DayOfWeek.Monday && date.DayOfWeek <= DayOfWeek.Friday;
    }
}

public sealed record MensaForecastResult(
    SafeArrivalRecommendation? Recommendation,
    string FluxLabel,
    List<MensaSpaceSnapshot> Snapshots,
    int WindowStartMinute,
    int WindowEndMinute,
    int StepMinutes,
    TimeSpan WindowStart,
    TimeSpan WindowEnd,
    TimeSpan OpeningHoursEnd,
    DateTime ReferenceDate,
    IReadOnlyList<float> ChartSeries,
    int Capacity);

public sealed record MensaSpaceSnapshot(
    Dictionary<DateTime, SortedList<int, int>> OccupiedByDay,
    SortedList<int, int> OccupiedToday);

