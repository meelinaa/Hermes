namespace Hermes.Domain.Enums;

/// <summary>
/// Natural languages (identifiers in English). Each member has an ISO 639-1 code via <see cref="LanguageIsoCodeAttribute"/>.
/// </summary>
public enum Language
{
    [LanguageIsoCode("sq")]
    Albanian,

    [LanguageIsoCode("ar")]
    Arabic,

    [LanguageIsoCode("hy")]
    Armenian,

    [LanguageIsoCode("az")]
    Azerbaijani,

    [LanguageIsoCode("eu")]
    Basque,

    [LanguageIsoCode("be")]
    Belarusian,

    [LanguageIsoCode("bn")]
    Bengali,

    [LanguageIsoCode("bs")]
    Bosnian,

    [LanguageIsoCode("bg")]
    Bulgarian,

    [LanguageIsoCode("ca")]
    Catalan,

    [LanguageIsoCode("zh")]
    Chinese,

    [LanguageIsoCode("hr")]
    Croatian,

    [LanguageIsoCode("cs")]
    Czech,

    [LanguageIsoCode("da")]
    Danish,

    [LanguageIsoCode("nl")]
    Dutch,

    [LanguageIsoCode("en")]
    English,

    [LanguageIsoCode("et")]
    Estonian,

    [LanguageIsoCode("fi")]
    Finnish,

    [LanguageIsoCode("fr")]
    French,

    [LanguageIsoCode("gl")]
    Galician,

    [LanguageIsoCode("de")]
    German,

    [LanguageIsoCode("el")]
    Greek,

    [LanguageIsoCode("he")]
    Hebrew,

    [LanguageIsoCode("hi")]
    Hindi,

    [LanguageIsoCode("hu")]
    Hungarian,

    [LanguageIsoCode("is")]
    Icelandic,

    [LanguageIsoCode("id")]
    Indonesian,

    [LanguageIsoCode("ga")]
    Irish,

    [LanguageIsoCode("it")]
    Italian,

    [LanguageIsoCode("ja")]
    Japanese,

    [LanguageIsoCode("ko")]
    Korean,

    [LanguageIsoCode("lv")]
    Latvian,

    [LanguageIsoCode("lt")]
    Lithuanian,

    [LanguageIsoCode("mk")]
    Macedonian,

    [LanguageIsoCode("mt")]
    Maltese,

    [LanguageIsoCode("ms")]
    Malay,

    [LanguageIsoCode("no")]
    Norwegian,

    [LanguageIsoCode("fa")]
    Persian,

    [LanguageIsoCode("pl")]
    Polish,

    [LanguageIsoCode("pt")]
    Portuguese,

    [LanguageIsoCode("ro")]
    Romanian,

    [LanguageIsoCode("ru")]
    Russian,

    [LanguageIsoCode("sr")]
    Serbian,

    [LanguageIsoCode("sk")]
    Slovak,

    [LanguageIsoCode("sl")]
    Slovenian,

    [LanguageIsoCode("es")]
    Spanish,

    [LanguageIsoCode("sv")]
    Swedish,

    [LanguageIsoCode("th")]
    Thai,

    [LanguageIsoCode("tr")]
    Turkish,

    [LanguageIsoCode("uk")]
    Ukrainian,

    [LanguageIsoCode("ur")]
    Urdu,

    [LanguageIsoCode("vi")]
    Vietnamese,

    [LanguageIsoCode("cy")]
    Welsh
}
