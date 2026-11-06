using System.Globalization;

namespace WC2;

/// <summary>
/// C string helpers used throughout the port: a printf style formatter, strcmp-like helpers,
/// and conversions between the fixed char buffers of the original structures and strings.
/// </summary>
public static class CStr
{
    /// <summary>
    /// Formats like C sprintf. Supports %d %i %u %ld %lu %f %.Nf %e %g %s %c %x %X %% with
    /// optional flags (-, 0, +, space), width and precision. Unsupported specifiers are
    /// emitted verbatim.
    /// </summary>
    public static string Sprintf(string format, params object[] args)
    {
        if (format == null) return "";
        if (args == null || args.Length == 0) return format.Replace("%%", "%");

        var sb = new StringBuilder(format.Length + 16);
        int argIndex = 0;
        int i = 0;
        while (i < format.Length)
        {
            char c = format[i];
            if (c != '%') { sb.Append(c); i++; continue; }
            i++;
            if (i >= format.Length) { sb.Append('%'); break; }
            if (format[i] == '%') { sb.Append('%'); i++; continue; }

            // flags
            bool leftAlign = false, zeroPad = false, plus = false, space = false;
            while (i < format.Length)
            {
                char f = format[i];
                if (f == '-') leftAlign = true;
                else if (f == '0') zeroPad = true;
                else if (f == '+') plus = true;
                else if (f == ' ') space = true;
                else if (f == '#') { }
                else break;
                i++;
            }
            // width
            int width = -1;
            if (i < format.Length && format[i] == '*')
            {
                width = ToInt(NextArg(args, ref argIndex)); i++;
            }
            else
            {
                int start = i;
                while (i < format.Length && char.IsDigit(format[i])) i++;
                if (i > start) width = int.Parse(format.AsSpan(start, i - start));
            }
            // precision
            int precision = -1;
            if (i < format.Length && format[i] == '.')
            {
                i++;
                if (i < format.Length && format[i] == '*') { precision = ToInt(NextArg(args, ref argIndex)); i++; }
                else
                {
                    int start = i;
                    while (i < format.Length && char.IsDigit(format[i])) i++;
                    precision = i > start ? int.Parse(format.AsSpan(start, i - start)) : 0;
                }
            }
            // length modifiers
            while (i < format.Length && (format[i] == 'l' || format[i] == 'h' || format[i] == 'z' || format[i] == 'j' || format[i] == 't' || format[i] == 'L')) i++;
            if (i >= format.Length) break;
            char spec = format[i++];

            string text;
            switch (spec)
            {
                case 'd':
                case 'i':
                {
                    long v = ToLong(NextArg(args, ref argIndex));
                    text = Math.Abs(v).ToString(CultureInfo.InvariantCulture);
                    if (precision >= 0) text = text.PadLeft(precision, '0');
                    string sign = v < 0 ? "-" : plus ? "+" : space ? " " : "";
                    text = PadNumber(sign, text, width, leftAlign, zeroPad && precision < 0);
                    break;
                }
                case 'u':
                {
                    ulong v = unchecked((ulong)ToLong(NextArg(args, ref argIndex)));
                    text = v.ToString(CultureInfo.InvariantCulture);
                    if (precision >= 0) text = text.PadLeft(precision, '0');
                    text = PadNumber("", text, width, leftAlign, zeroPad && precision < 0);
                    break;
                }
                case 'x':
                case 'X':
                {
                    long v = ToLong(NextArg(args, ref argIndex));
                    text = spec == 'x' ? v.ToString("x", CultureInfo.InvariantCulture) : v.ToString("X", CultureInfo.InvariantCulture);
                    if (precision >= 0) text = text.PadLeft(precision, '0');
                    text = PadNumber("", text, width, leftAlign, zeroPad && precision < 0);
                    break;
                }
                case 'f':
                case 'F':
                {
                    double v = ToDouble(NextArg(args, ref argIndex));
                    int p = precision < 0 ? 6 : precision;
                    text = Math.Abs(v).ToString("F" + p, CultureInfo.InvariantCulture);
                    string sign = (v < 0 || (v == 0 && double.IsNegative(v) && false)) ? "-" : plus ? "+" : space ? " " : "";
                    text = PadNumber(sign, text, width, leftAlign, zeroPad);
                    break;
                }
                case 'e':
                case 'E':
                {
                    double v = ToDouble(NextArg(args, ref argIndex));
                    int p = precision < 0 ? 6 : precision;
                    text = v.ToString((spec == 'e' ? "e" : "E") + p, CultureInfo.InvariantCulture);
                    text = Pad(text, width, leftAlign);
                    break;
                }
                case 'g':
                case 'G':
                {
                    double v = ToDouble(NextArg(args, ref argIndex));
                    text = v.ToString("G" + (precision < 0 ? 6 : Math.Max(1, precision)), CultureInfo.InvariantCulture);
                    text = Pad(text, width, leftAlign);
                    break;
                }
                case 's':
                {
                    object a = NextArg(args, ref argIndex);
                    text = a?.ToString() ?? "";
                    if (precision >= 0 && text.Length > precision) text = text.Substring(0, precision);
                    text = Pad(text, width, leftAlign);
                    break;
                }
                case 'c':
                {
                    object a = NextArg(args, ref argIndex);
                    text = a is char ch ? ch.ToString() : ((char)ToInt(a)).ToString();
                    text = Pad(text, width, leftAlign);
                    break;
                }
                case 'p':
                {
                    object a = NextArg(args, ref argIndex);
                    text = "0x" + (a?.GetHashCode() ?? 0).ToString("x");
                    break;
                }
                default:
                    text = "%" + spec;
                    break;
            }
            sb.Append(text);
        }
        return sb.ToString();
    }

    private static object NextArg(object[] args, ref int index) => index < args.Length ? args[index++] : null;

    private static string Pad(string text, int width, bool left)
    {
        if (width < 0 || text.Length >= width) return text;
        return left ? text.PadRight(width) : text.PadLeft(width);
    }

    private static string PadNumber(string sign, string digits, int width, bool left, bool zero)
    {
        string full = sign + digits;
        if (width < 0 || full.Length >= width) return full;
        if (left) return full.PadRight(width);
        if (zero) return sign + digits.PadLeft(width - sign.Length, '0');
        return full.PadLeft(width);
    }

    private static int ToInt(object o) => (int)ToLong(o);

    private static long ToLong(object o)
    {
        switch (o)
        {
            case null: return 0;
            case int v: return v;
            case long v: return v;
            case uint v: return v;
            case ulong v: return unchecked((long)v);
            case short v: return v;
            case ushort v: return v;
            case byte v: return v;
            case sbyte v: return v;
            case bool v: return v ? 1 : 0;
            case char v: return v;
            case float v: return (long)v;
            case double v: return (long)v;
            case Enum e: return Convert.ToInt64(e);
            default:
                long.TryParse(o.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long r);
                return r;
        }
    }

    private static double ToDouble(object o)
    {
        switch (o)
        {
            case null: return 0;
            case float v: return v;
            case double v: return v;
            case decimal v: return (double)v;
            default: return ToLong(o);
        }
    }

    // ------------------------------------------------------------------
    // C library style helpers
    // ------------------------------------------------------------------

    public static int strcmp(string a, string b) => string.CompareOrdinal(a ?? "", b ?? "");
    public static int strcasecmp(string a, string b) => string.Compare(a ?? "", b ?? "", StringComparison.OrdinalIgnoreCase);
    public static int strncmp(string a, string b, int n)
    {
        a ??= ""; b ??= "";
        if (a.Length > n) a = a.Substring(0, n);
        if (b.Length > n) b = b.Substring(0, n);
        return string.CompareOrdinal(a, b);
    }
    public static int strlen(string s) => s?.Length ?? 0;
    public static int atoi(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        s = s.Trim();
        int i = 0; bool neg = false;
        if (i < s.Length && (s[i] == '+' || s[i] == '-')) { neg = s[i] == '-'; i++; }
        long v = 0;
        while (i < s.Length && char.IsDigit(s[i])) { v = v * 10 + (s[i] - '0'); if (v > int.MaxValue) break; i++; }
        return (int)(neg ? -v : v);
    }
    public static float atof(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0f;
        s = s.Trim();
        // parse the longest numeric prefix like strtod
        int end = 0;
        while (end < s.Length && (char.IsDigit(s[end]) || s[end] == '.' || s[end] == '-' || s[end] == '+' || s[end] == 'e' || s[end] == 'E')) end++;
        while (end > 0 && !float.TryParse(s.AsSpan(0, end), NumberStyles.Float, CultureInfo.InvariantCulture, out _)) end--;
        return end == 0 ? 0f : float.Parse(s.AsSpan(0, end), NumberStyles.Float, CultureInfo.InvariantCulture);
    }
    public static double strtod(string s) => atof(s);

    /// <summary>Parses an int the way TinyXML/strtol would; returns false if no number.</summary>
    public static bool TryParseInt(string s, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        return int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    public static bool TryParseFloat(string s, out float value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        return float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Invariant culture float to string (like "%g" roughly).</summary>
    public static string F(float v) => v.ToString(CultureInfo.InvariantCulture);
    public static string F(double v) => v.ToString(CultureInfo.InvariantCulture);

    /// <summary>Truncates a string to fit a C char[n] buffer (n-1 characters).</summary>
    public static string FitBuffer(string s, int bufferSize)
    {
        if (s == null) return "";
        int max = Math.Max(0, bufferSize - 1);
        return s.Length <= max ? s : s.Substring(0, max);
    }

    /// <summary>Reads a NUL terminated ASCII string from a fixed size byte buffer.</summary>
    public static string FromFixedBytes(byte[] data, int offset, int length)
    {
        int end = offset;
        int limit = Math.Min(data.Length, offset + length);
        while (end < limit && data[end] != 0) end++;
        return Encoding.UTF8.GetString(data, offset, end - offset);
    }

    /// <summary>Writes a NUL terminated string into a fixed size byte buffer region.</summary>
    public static void ToFixedBytes(string s, byte[] data, int offset, int length)
    {
        Array.Clear(data, offset, Math.Min(length, data.Length - offset));
        if (string.IsNullOrEmpty(s)) return;
        byte[] bytes = Encoding.UTF8.GetBytes(s);
        int n = Math.Min(bytes.Length, length - 1);
        Array.Copy(bytes, 0, data, offset, n);
    }

    /// <summary>Returns the lower-case file extension including the dot ("" if none).</summary>
    public static string Ext(string fileName) => Path.GetExtension(fileName ?? "").ToLowerInvariant();

    /// <summary>Replaces the extension of a resource name ("a.png" -> "a.webp").</summary>
    public static string WithExt(string fileName, string newExtWithDot)
    {
        int dot = fileName.LastIndexOf('.');
        return (dot < 0 ? fileName : fileName.Substring(0, dot)) + newExtWithDot;
    }
}
