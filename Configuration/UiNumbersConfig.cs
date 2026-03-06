using System;

namespace PlatzPilot.Configuration;

public sealed class UiNumbersConfig
{
    public int MinOpeningHours { get; set; } = 0;
    public int MaxOpeningHours { get; set; } = 12;
    public int UnknownYearThreshold { get; set; } = 2000;
    public int StudentClosedLabelTopMargin { get; set; } = 4;
    public TimeSpan DefaultBeforeTime { get; set; } = new(12, 0, 0);
    public int InitialLoadDelayMs { get; set; } = 100;
    public int ResumeRefreshThresholdMinutes { get; set; } = 20;
    public int FilterSheetAnimationDurationMs { get; set; } = 160;
    public double FilterSheetTranslationOffset { get; set; } = -8;
    public double OpeningHoursSliderSnapEpsilon { get; set; } = 0.001;
    public int SkeletonItemCount { get; set; } = 6;
    public int OfflineBannerDurationMs { get; set; } = 2500;
    public int HapticFallbackDurationMs { get; set; } = 40;
}
