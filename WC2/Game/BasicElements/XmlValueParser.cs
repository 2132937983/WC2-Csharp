namespace WC2;

/// <summary>Strict, culture-independent parsers for numeric XML values.</summary>
public static class XmlValueParser
{
    public static bool ParseIntegerText(string text, out long result)
    {
        result = 0;
        if (string.IsNullOrEmpty(text)) return false;
        if (!ScanInteger(text, 0, out long value, out int end) || end != text.Length) return false;
        result = value;
        return true;
    }

    public static bool ParseUInt8Text(string text, out byte result)
    {
        result = 0;
        if (string.IsNullOrEmpty(text)) return false;
        if (!ScanUnsignedInteger(text, 0, out ulong value, out int end) ||
            end != text.Length || value > byte.MaxValue) return false;
        result = (byte)value;
        return true;
    }

    public static bool ParseUInt8Attribute(TiXmlElement element, string name, out byte result)
    {
        result = 0;
        return element != null && name != null && ParseUInt8Text(element.Attribute(name), out result);
    }

    public static bool ParseUInt16Text(string text, out ushort result)
    {
        result = 0;
        if (string.IsNullOrEmpty(text)) return false;
        if (!ScanUnsignedInteger(text, 0, out ulong value, out int end) ||
            end != text.Length || value > ushort.MaxValue) return false;
        result = (ushort)value;
        return true;
    }

    public static bool ParseUInt16Attribute(TiXmlElement element, string name, out ushort result)
    {
        result = 0;
        return element != null && name != null && ParseUInt16Text(element.Attribute(name), out result);
    }

    internal static bool ScanInteger(string text, int start, out long value, out int end)
    {
        value = 0;
        end = start;
        if (text == null) return false;
        int i = start;
        while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
        bool negative = false;
        if (i < text.Length && (text[i] == '+' || text[i] == '-'))
        {
            negative = text[i] == '-';
            i++;
        }
        int digitsStart = i;
        long accumulator = 0;
        bool overflow = false;
        while (i < text.Length && text[i] >= '0' && text[i] <= '9')
        {
            int digit = text[i] - '0';
            if (accumulator > (long.MaxValue - digit) / 10) overflow = true;
            else accumulator = accumulator * 10 + digit;
            i++;
        }
        if (i == digitsStart) return false;
        if (overflow) accumulator = long.MaxValue;
        value = negative ? -accumulator : accumulator;
        end = i;
        return true;
    }

    internal static bool ScanUnsignedInteger(string text, int start, out ulong value, out int end)
    {
        value = 0;
        end = start;
        if (text == null) return false;
        int i = start;
        while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
        bool negative = false;
        if (i < text.Length && (text[i] == '+' || text[i] == '-'))
        {
            negative = text[i] == '-';
            i++;
        }
        int digitsStart = i;
        ulong accumulator = 0;
        bool overflow = false;
        while (i < text.Length && text[i] >= '0' && text[i] <= '9')
        {
            uint digit = (uint)(text[i] - '0');
            if (accumulator > (ulong.MaxValue - digit) / 10) overflow = true;
            else accumulator = accumulator * 10 + digit;
            i++;
        }
        if (i == digitsStart) return false;
        if (overflow || (negative && accumulator != 0)) accumulator = ulong.MaxValue;
        value = accumulator;
        end = i;
        return true;
    }
}
