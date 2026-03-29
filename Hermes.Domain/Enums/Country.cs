using Hermes.Domain.Enums.Attribute;

namespace Hermes.Domain.Enums;

/// <summary>
/// Countries in English naming. Each member has an ISO 3166-1 alpha-2 code via <see cref="CountryIsoCodeAttribute"/> (lowercase for APIs).
/// </summary>
public enum Country
{
    // --- Europe (alphabetical) ---
    [CountryIsoCode("al")]
    Albania,

    [CountryIsoCode("ad")]
    Andorra,

    [CountryIsoCode("at")]
    Austria,

    [CountryIsoCode("by")]
    Belarus,

    [CountryIsoCode("be")]
    Belgium,

    [CountryIsoCode("ba")]
    BosniaHerzegovina,

    [CountryIsoCode("bg")]
    Bulgaria,

    [CountryIsoCode("hr")]
    Croatia,

    [CountryIsoCode("cy")]
    Cyprus,

    [CountryIsoCode("cz")]
    CzechRepublic,

    [CountryIsoCode("dk")]
    Denmark,

    [CountryIsoCode("ee")]
    Estonia,

    [CountryIsoCode("fi")]
    Finland,

    [CountryIsoCode("fr")]
    France,

    [CountryIsoCode("de")]
    Germany,

    [CountryIsoCode("gr")]
    Greece,

    [CountryIsoCode("hu")]
    Hungary,

    [CountryIsoCode("is")]
    Iceland,

    [CountryIsoCode("ie")]
    Ireland,

    [CountryIsoCode("it")]
    Italy,

    [CountryIsoCode("xk")]
    Kosovo,

    [CountryIsoCode("lv")]
    Latvia,

    [CountryIsoCode("li")]
    Liechtenstein,

    [CountryIsoCode("lt")]
    Lithuania,

    [CountryIsoCode("lu")]
    Luxembourg,

    [CountryIsoCode("mt")]
    Malta,

    [CountryIsoCode("md")]
    Moldova,

    [CountryIsoCode("mc")]
    Monaco,

    [CountryIsoCode("me")]
    Montenegro,

    [CountryIsoCode("nl")]
    Netherlands,

    [CountryIsoCode("mk")]
    NorthMacedonia,

    [CountryIsoCode("no")]
    Norway,

    [CountryIsoCode("pl")]
    Poland,

    [CountryIsoCode("pt")]
    Portugal,

    [CountryIsoCode("ro")]
    Romania,

    [CountryIsoCode("ru")]
    Russia,

    [CountryIsoCode("sm")]
    SanMarino,

    [CountryIsoCode("rs")]
    Serbia,

    [CountryIsoCode("sk")]
    Slovakia,

    [CountryIsoCode("si")]
    Slovenia,

    [CountryIsoCode("es")]
    Spain,

    [CountryIsoCode("se")]
    Sweden,

    [CountryIsoCode("ch")]
    Switzerland,

    [CountryIsoCode("ua")]
    Ukraine,

    [CountryIsoCode("gb")]
    UnitedKingdom,

    [CountryIsoCode("va")]
    VaticanCity,

    // --- Americas, Asia-Pacific, Middle East & Africa ---
    [CountryIsoCode("cn")]
    China,

    [CountryIsoCode("jp")]
    Japan,

    [CountryIsoCode("mx")]
    Mexico,

    [CountryIsoCode("kr")]
    SouthKorea,

    [CountryIsoCode("us")]
    USA,

    [CountryIsoCode("ar")]
    Argentina,

    [CountryIsoCode("am")]
    Armenia,

    [CountryIsoCode("au")]
    Australia,

    [CountryIsoCode("az")]
    Azerbaijan,

    [CountryIsoCode("bd")]
    Bangladesh,

    [CountryIsoCode("br")]
    Brazil,

    [CountryIsoCode("ca")]
    Canada,

    [CountryIsoCode("cl")]
    Chile,

    [CountryIsoCode("co")]
    Colombia,

    [CountryIsoCode("eg")]
    Egypt,

    [CountryIsoCode("ge")]
    Georgia,

    [CountryIsoCode("in")]
    India,

    [CountryIsoCode("id")]
    Indonesia,

    [CountryIsoCode("il")]
    Israel,

    [CountryIsoCode("my")]
    Malaysia,

    [CountryIsoCode("nz")]
    NewZealand,

    [CountryIsoCode("ng")]
    Nigeria,

    [CountryIsoCode("pk")]
    Pakistan,

    [CountryIsoCode("ph")]
    Philippines,

    [CountryIsoCode("sa")]
    SaudiArabia,

    [CountryIsoCode("sg")]
    Singapore,

    [CountryIsoCode("za")]
    SouthAfrica,

    [CountryIsoCode("tw")]
    Taiwan,

    [CountryIsoCode("th")]
    Thailand,

    [CountryIsoCode("tr")]
    Turkey,

    [CountryIsoCode("ae")]
    UnitedArabEmirates,

    [CountryIsoCode("vn")]
    Vietnam
}
