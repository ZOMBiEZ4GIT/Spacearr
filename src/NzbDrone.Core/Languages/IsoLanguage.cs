namespace NzbDrone.Core.Languages
{
    public class IsoLanguage
    {
        public string TwoLetterCode { get; set; }
        public string ThreeLetterCode { get; set; }
        public string CountryCode { get; set; }
        public Language Language { get; set; }

        public IsoLanguage(string twoLetterCode, string countryCode, Language language)
        {
            TwoLetterCode = twoLetterCode;
            CountryCode = countryCode;
            Language = language;
        }
    }
}
