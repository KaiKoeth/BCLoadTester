namespace BCLoadtester.Loadtest.Utils;

public static class SafeData
{
    public static string TrimTo(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        return value.Length <= maxLength
            ? value
            : value.Substring(0, maxLength);
    }
}