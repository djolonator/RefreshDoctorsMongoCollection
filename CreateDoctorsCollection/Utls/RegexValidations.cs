using System.Text.RegularExpressions;

namespace CreateDoctorsCollection.Utls
{
    public static class RegexValidations
    {
        public static bool ValidateNoDigitsString(string value)
        {
            string noDigits = "^([^0-9]*)$";
            return string.IsNullOrEmpty(value) ? false : Regex.IsMatch(value, noDigits);
        }
        public static bool ValidateLBO(string value)
        {
            string LBOAndSSNRegPattern = "^\\d{11}$";
            return string.IsNullOrEmpty(value) ? false : Regex.IsMatch(value, LBOAndSSNRegPattern);
        }
    }
}
