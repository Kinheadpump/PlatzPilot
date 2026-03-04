using System;
using System.Linq;
using PlatzPilot.Configuration;
using PlatzPilot.Models;

namespace PlatzPilot.Services;

internal static class SafeArrivalSchedule
{
    internal static bool[] BuildOpenBinMask(OpeningHoursDto? openingHours, DateTime dayStart, int binsPerDay, int binMinutes)
    {
        var openBins = new bool[binsPerDay];

        // If opening hours are unknown, do not block the recommendation by schedule.
        if (openingHours == null)
        {
            Array.Fill(openBins, true);
            return openBins;
        }

        for (var bin = 0; bin < binsPerDay; bin++)
        {
            var binTime = dayStart.AddMinutes(bin * binMinutes);
            openBins[bin] = openingHours.IsCurrentlyOpen(binTime);
        }

        return openBins;
    }

    internal static bool[] BuildOpenBinMaskForSpace(StudySpace space, DateTime dayStart, int binsPerDay, int binMinutes, StudentAccessConfig studentAccess)
    {
        if (IsStudentAccessLocation(space, studentAccess))
        {
            return BuildStudentAccessBinMask(dayStart, binsPerDay, binMinutes, studentAccess);
        }

        return BuildOpenBinMask(space.OpeningHours, dayStart, binsPerDay, binMinutes);
    }

    internal static bool[] BuildStudentAccessBinMask(DateTime dayStart, int binsPerDay, int binMinutes, StudentAccessConfig studentAccess)
    {
        var openBins = new bool[binsPerDay];
        for (var bin = 0; bin < binsPerDay; bin++)
        {
            var binTime = dayStart.AddMinutes(bin * binMinutes).TimeOfDay;
            openBins[bin] = binTime >= studentAccess.Start && binTime < studentAccess.End;
        }

        return openBins;
    }

    internal static bool IsOpenAtTime(StudySpace space, DateTime timestamp, StudentAccessConfig studentAccess)
    {
        if (IsStudentAccessLocation(space, studentAccess))
        {
            var time = timestamp.TimeOfDay;
            return time >= studentAccess.Start && time < studentAccess.End;
        }

        return space.OpeningHours?.IsCurrentlyOpen(timestamp) ?? true;
    }

    internal static bool IsStudentAccessLocation(StudySpace space, StudentAccessConfig studentAccess)
    {
        if (studentAccess.LocationIds.Any(id => string.Equals(space.Id, id, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(space.Building) &&
            studentAccess.BuildingIds.Any(id => string.Equals(space.Building.Trim(), id, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return studentAccess.NameContains.Any(token =>
            space.Name.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    internal static bool HasAnyOpenBin(bool[] openBins)
    {
        foreach (var isOpen in openBins)
        {
            if (isOpen)
            {
                return true;
            }
        }

        return false;
    }
}
