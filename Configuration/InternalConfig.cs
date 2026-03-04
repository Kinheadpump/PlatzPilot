namespace PlatzPilot.Configuration;

public sealed class InternalConfig
{
    public string BuildingAggregateIdPrefix { get; set; } = string.Empty;
    public string BuildingAggregateIdSeparator { get; set; } = string.Empty;
    public string BuildingAggregateName { get; set; } = string.Empty;
    public string ApiDateTimeFormat { get; set; } = string.Empty;
    public string DatePickerFormat { get; set; } = string.Empty;
    public string TimePickerFormat { get; set; } = string.Empty;
    public string MainPageRoute { get; set; } = string.Empty;
    public string DetailPageRoute { get; set; } = string.Empty;
    public string BackNavigationRoute { get; set; } = string.Empty;
    public string LocationDataKey { get; set; } = string.Empty;
    public string SpaceFeaturesLoadFailedFormat { get; set; } = string.Empty;
    public string ConfigLoadFailedFormat { get; set; } = string.Empty;
    public string JsonpParseErrorText { get; set; } = string.Empty;
    public string HttpRequestErrorFormat { get; set; } = string.Empty;
    public string UrlOpenFailedFormat { get; set; } = string.Empty;
    public string UrlSchemeHttp { get; set; } = string.Empty;
    public string UrlSchemeHttps { get; set; } = string.Empty;
    public string UrlSchemeDefaultPrefix { get; set; } = string.Empty;
    public string MapSearchUrlFormat { get; set; } = string.Empty;
    public string LevelNumberRegex { get; set; } = string.Empty;
}