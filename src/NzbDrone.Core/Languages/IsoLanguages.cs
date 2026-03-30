using System.Collections.Generic;
using System.Linq;

namespace NzbDrone.Core.Languages
{
    public static class IsoLanguages
    {
        private static readonly List<IsoLanguage> All = new List<IsoLanguage>
        {
            new IsoLanguage("en", "", Language.English),
            new IsoLanguage("fr", "", Language.French),
            new IsoLanguage("es", "", Language.Spanish),
            new IsoLanguage("de", "", Language.German),
            new IsoLanguage("it", "", Language.Italian),
            new IsoLanguage("da", "", Language.Danish),
            new IsoLanguage("nl", "", Language.Dutch),
            new IsoLanguage("ja", "", Language.Japanese),
            new IsoLanguage("is", "", Language.Icelandic),
            new IsoLanguage("zh", "CN", Language.Chinese),
            new IsoLanguage("ru", "", Language.Russian),
            new IsoLanguage("pl", "", Language.Polish),
            new IsoLanguage("vi", "", Language.Vietnamese),
            new IsoLanguage("sv", "", Language.Swedish),
            new IsoLanguage("no", "", Language.Norwegian),
            new IsoLanguage("fi", "", Language.Finnish),
            new IsoLanguage("tr", "", Language.Turkish),
            new IsoLanguage("pt", "BR", Language.PortugueseBR),
            new IsoLanguage("pt", "", Language.Portuguese),
            new IsoLanguage("el", "", Language.Greek),
            new IsoLanguage("ko", "", Language.Korean),
            new IsoLanguage("hu", "", Language.Hungarian),
            new IsoLanguage("he", "", Language.Hebrew),
            new IsoLanguage("cs", "", Language.Czech),
            new IsoLanguage("hi", "", Language.Hindi),
            new IsoLanguage("th", "", Language.Thai),
            new IsoLanguage("uk", "", Language.Ukrainian),
            new IsoLanguage("ar", "", Language.Arabic),
            new IsoLanguage("bg", "", Language.Bulgarian),
            new IsoLanguage("ro", "", Language.Romanian),
            new IsoLanguage("sk", "", Language.Slovak),
            new IsoLanguage("hr", "", Language.Croatian),
            new IsoLanguage("sr", "", Language.Serbian),
            new IsoLanguage("lt", "", Language.Lithuanian),
        };

        public static IsoLanguage Get(Language language)
        {
            return All.FirstOrDefault(l => l.Language == language);
        }
    }
}
