using System;
using System.Globalization;

namespace PhysicsLab.Core.Telemetry
{
    /// <summary>
    /// The CSV rules in one place: quote text, use a dot for decimals.
    /// </summary>
    public static class CsvFormat
    {
        /// <summary>
        /// Wraps text in quotes and doubles any quote inside it.
        /// </summary>
        public static string Text(string value) =>
            "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";

        /// <summary>
        /// Formats a number with a dot as the decimal separator, whatever the browser language.
        /// </summary>
        public static string Number(IFormattable value) =>
            value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}