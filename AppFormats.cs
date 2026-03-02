using PlatzPilot.Configuration;

namespace PlatzPilot;

public static class AppFormats
{
    private static InternalConfig Internal => AppConfigProvider.Current.Internal;

    public static string DatePicker => Internal.DatePickerFormat;
    public static string TimePicker => Internal.TimePickerFormat;
}
