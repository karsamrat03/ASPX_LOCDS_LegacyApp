using System;

namespace LOCDS.Web.Security
{
    internal static class DataMasking
    {
        internal static string MaskPan(string pan)
        {
            if (string.IsNullOrWhiteSpace(pan))
            {
                return string.Empty;
            }

            string clean = pan.Trim().ToUpperInvariant();
            if (clean.Length != 10)
            {
                return clean;
            }

            return "XXXXX" + clean.Substring(5, 4) + clean.Substring(9, 1);
        }

        internal static string MaskAadhaar(string aadhaar)
        {
            if (string.IsNullOrWhiteSpace(aadhaar))
            {
                return string.Empty;
            }

            string clean = aadhaar.Trim();
            if (clean.Length <= 4)
            {
                return clean;
            }

            return new string('X', clean.Length - 4) + clean.Substring(clean.Length - 4);
        }
    }
}
