using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hermes.Application.DTOs.NewsArticle;
using Hermes.Application.Mapping;
using Hermes.Application.Options.External;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Hermes.Application.Services.Newsletter;

/// <summary>
/// Service implementation for fetching live articles from news data providers with fallback support.
/// </summary>
public sealed class ArticleFetchingService(
    INewsArticleProvider newsArticleProvider,
    IOptions<NewsDataIoOptions> newsDataOptions) : IArticleFetchingService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<NewsArticle>> FetchArticlesForSubscriptionAsync(NewsletterSubscription subscription, CancellationToken cancellationToken = default)
    {
        string? apiKey = newsDataOptions.Value.Key?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Configure NewsDataIo:Key.");

        NewsArticleQueryDto? query = BuildArticleQuery(apiKey, subscription);
        if (query is null)
            return Array.Empty<NewsArticle>();

        return await newsArticleProvider.GetLatestAsync(query, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NewsArticle>> FetchPreviewArticlesAsync(NewsPreviewRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string? apiKey = newsDataOptions.Value.Key?.Trim();

        if (string.IsNullOrWhiteSpace(apiKey))
            return GetFallbackPreviewArticles(request);

        List<string>? countries = request.Countries is { Count: > 0 }
            ? request.Countries.Select(CountryIsoCodeMapper.ToIso3166Alpha2).ToList()
            : null;
        List<string>? languages = request.Languages is { Count: > 0 }
            ? request.Languages.Select(LanguageIsoCodeMapper.ToIso639Code).ToList()
            : null;
        List<string>? categories = request.Categories is { Count: > 0 }
            ? request.Categories.Select(category => category.ToString().ToLowerInvariant()).ToList()
            : null;

        string? keywordsQuery = null;
        if (!string.IsNullOrWhiteSpace(request.Keywords))
        {
            string[] terms = request.Keywords.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (terms.Length > 0)
                keywordsQuery = string.Join(" OR ", terms);
        }

        NewsArticleQueryDto query = new()
        {
            ApiKey = apiKey,
            Countries = countries,
            Languages = languages,
            Categories = categories,
            KeywordsQuery = keywordsQuery
        };

        try
        {
            IReadOnlyList<NewsArticle> articles = await newsArticleProvider.GetLatestAsync(query, cancellationToken).ConfigureAwait(false);
            return articles.Count > 0 ? articles : GetFallbackPreviewArticles(request);
        }
        catch
        {
            return GetFallbackPreviewArticles(request);
        }
    }

    /// <summary>
    /// Generates structured fallback articles covering all domain categories for preview and exploration when external API is unreachable or omitted.
    /// Filters the pool precisely by requested categories, languages, countries, and search terms.
    /// </summary>
    /// <param name="request">The search and multi-criteria filter parameters.</param>
    /// <returns>A list of matching articles, or an empty collection if no articles match the specified filters.</returns>
    private static IReadOnlyList<NewsArticle> GetFallbackPreviewArticles(NewsPreviewRequestDto request)
    {
        var pool = GetFallbackArticlePool();
        IEnumerable<FallbackArticleItem> filtered = pool;

        // 1. Language Filter (e.g. English -> 'en', German -> 'de')
        if (request.Languages is { Count: > 0 })
        {
            var langCodes = request.Languages.Select(LanguageIsoCodeMapper.ToIso639Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
            filtered = filtered.Where(item => langCodes.Contains(item.LanguageCode));
        }

        // 2. Country Filter (e.g. Germany -> 'de', UnitedStates -> 'us', GreatBritain -> 'gb')
        if (request.Countries is { Count: > 0 })
        {
            var countryCodes = request.Countries.Select(CountryIsoCodeMapper.ToIso3166Alpha2).ToHashSet(StringComparer.OrdinalIgnoreCase);
            filtered = filtered.Where(item => countryCodes.Contains(item.CountryCode));
        }

        // 3. Category Filter (e.g. sports, technology, business)
        if (request.Categories is { Count: > 0 })
        {
            var catNames = request.Categories.Select(c => c.ToString().ToLowerInvariant()).ToHashSet();
            filtered = filtered.Where(item => item.Article.Category != null && item.Article.Category.Any(c => catNames.Contains(c.ToLowerInvariant())));
        }

        // 4. Keyword Search Filter
        if (!string.IsNullOrWhiteSpace(request.Keywords))
        {
            string[] terms = request.Keywords.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (terms.Length > 0)
            {
                filtered = filtered.Where(item =>
                    terms.Any(term =>
                        (item.Article.Title != null && item.Article.Title.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                        (item.Article.Description != null && item.Article.Description.Contains(term, StringComparison.OrdinalIgnoreCase))));
            }
        }

        return filtered.Select(item => item.Article).ToList();
    }

    /// <summary>
    /// Helper record associating a news article with its ISO language and country metadata.
    /// </summary>
    private sealed record FallbackArticleItem(NewsArticle Article, string LanguageCode, string CountryCode);

    /// <summary>
    /// Constructs the rich fallback dataset covering all news categories across German and English publications.
    /// </summary>
    /// <returns>A list of predefined news articles with language and country tags.</returns>
    private static List<FallbackArticleItem> GetFallbackArticlePool() =>
    [
        // === GERMAN ARTICLES (de / de) ===
        new(new("de-tech-1", "https://www.heise.de/news/Kuenstliche-Intelligenz-und-Quantencomputing-9876543.html", "Künstliche Intelligenz und Quantencomputing: Die nächste Welle in der Softwarearchitektur", "Neueste Entwicklungen bei generativen KI-Modellen und autonomen Agentensystemen verändern die Arbeitsweise von Entwicklerteams nachhaltig.", ["technology"], "https://images.unsplash.com/photo-1518770660439-4636190af475?w=600&auto=format&fit=crop&q=80"), "de", "de"),
        new(new("de-tech-2", "https://www.golem.de/news/net-10-und-webassembly-hoehere-performance-2408-182345.html", ".NET 10 und WebAssembly: Höhere Performance für moderne Webanwendungen", "Mit Ahead-of-Time-Kompilierung und optimiertem Speichermanagement setzen moderne WebAssembly-Frameworks neue Maßstäbe bei Reaktionszeiten.", ["technology"], "https://images.unsplash.com/photo-1555066931-4365d14bab8c?w=600&auto=format&fit=crop&q=80"), "de", "de"),
        new(new("de-biz-1", "https://www.handelsblatt.com/unternehmen/it-medien/europaeische-tech-startups-verzeichnen-rekord-wachstum/29871234.html", "Europäische Tech-Startups verzeichnen starkes Wachstum bei nachhaltigen Technologien", "Investitionen in grüne Rechenzentren und energieeffiziente Cloud-Infrastruktur erreichen neue Höchststände im ersten Quartal.", ["business"], "https://images.unsplash.com/photo-1486406146926-c627a92ad1ab?w=600&auto=format&fit=crop&q=80"), "de", "de"),
        new(new("de-biz-2", "https://www.manager-magazin.de/finanzen/geldanlage/globale-zinstrends-und-die-auswirkungen-auf-unternehmen-a-1928374.html", "Globale Zinstrends und die Auswirkungen auf europäische Unternehmensfinanzierungen", "Analysten bewerten die aktuellen geldpolitischen Signale der Zentralbanken und deren Implikationen für den Mittelstand.", ["business"], "https://images.unsplash.com/photo-1590283603385-17ffb3a7f29f?w=600&auto=format&fit=crop&q=80"), "de", "de"),
        new(new("de-sci-1", "https://www.spektrum.de/news/james-webb-teleskop-findet-spuren-ferner-atmosphaeren/2219876", "Neue Entdeckungen in der Raumfahrt: James-Webb-Teleskop findet Spuren ferner Atmosphären", "Astronomen analysieren hochauflösende Spektraldaten neu entdeckter Exoplaneten mit bislang unerreichter Präzision.", ["science"], "https://images.unsplash.com/photo-1451187580459-43490279c0fa?w=600&auto=format&fit=crop&q=80"), "de", "de"),
        new(new("de-sci-2", "https://www.wissenschaft.de/astronomie-physik/rekord-bei-plasma-einschlusszeiten-in-der-kernfusion/8765432", "Fortschritte in der Fusionsforschung: Neuer Rekord bei Plasma-Einschlusszeiten", "Internationale Forschungsteams vermelden signifikante Fortschritte auf dem Weg zur kommerziellen Kernfusionsenergie.", ["science"], "https://images.unsplash.com/photo-1507668077129-56e32842fceb?w=600&auto=format&fit=crop&q=80"), "de", "de"),
        new(new("de-sports-1", "https://www.kicker.de/champions-league-taktische-innovationen-in-der-k-o-runde-1049281/artikel", "Champions League: Taktische Innovationen dominieren die K.o.-Runde", "Hochgeschwindigkeits-Umschaltspiel und datenbasierte Positionsanalysen prägen die aktuellen Spitzenbegegnungen im europäischen Spitzenfußball.", ["sports"], "https://images.unsplash.com/photo-1508098682722-e99c43a406b2?w=600&auto=format&fit=crop&q=80"), "de", "de"),
        new(new("de-sports-2", "https://www.sportschau.de/fussball/bundesliga/bundesliga-topspiel-spitzenteams-im-meisterschaftsduell-102.html", "Bundesliga Topspiel: Spitzenteams im Duell um die Meisterschaft", "Ein packender Schlagabtausch vor ausverkaufter Kulisse sorgt für Hochspannung im Titelrennen.", ["sports"], "https://images.unsplash.com/photo-1461896836934-ffe607ba8211?w=600&auto=format&fit=crop&q=80"), "de", "de"),
        new(new("de-health-1", "https://www.aerzteblatt.de/nachrichten/154321/Personalisierte-Medizin-Genomanalysen-bei-chronischen-Krankheiten", "Personalisierte Medizin: Wie Genomanalysen Therapien bei chronischen Krankheiten revolutionieren", "Maßgeschneiderte Behandlungsansätze ermöglichen gezieltere Wirkstoffabstimmungen und reduzieren Nebenwirkungen deutlich.", ["health"], "https://images.unsplash.com/photo-1576091160399-112ba8d25d1d?w=600&auto=format&fit=crop&q=80"), "de", "de"),
        new(new("de-health-2", "https://www.apotheken-umschau.de/gesund-bleiben/sport-und-fitness/praevention-und-kardiovaskulaere-gesundheit-892134.html", "Prävention im Fokus: Die Rolle von Bewegung und Ernährung für die kardiovaskuläre Gesundheit", "Aktuelle Studien belegen die präventive Wirkung gezielter Mikro-Workouts auf das Herz-Kreislauf-System.", ["health"], "https://images.unsplash.com/photo-1505751172876-fa1923c5c528?w=600&auto=format&fit=crop&q=80"), "de", "de"),
        new(new("de-politics-1", "https://www.zeit.de/digital/2026-08/eu-gipfel-digitalpaket-technologische-souveraenitaet", "EU-Gipfel beschließt umfassendes Digitalpaket zur Stärkung der technologischen Souveränität", "Die Mitgliedsstaaten einigen sich auf gemeinsame Investitionsprogramme in europäische Cloud- und Halbleiter-Kapazitäten.", ["politics"], "https://images.unsplash.com/photo-1541872703-74c5e44368f9?w=600&auto=format&fit=crop&q=80"), "de", "de"),
        new(new("de-environment-1", "https://www.klimareporter.de/strom/erneuerbare-energien-neuer-hoechstanteil-im-netz", "Erneuerbare Energien decken neuen Höchstanteil des nationalen Strombedarfs ab", "Der kontinuierliche Zubau von Windkraft- und Photovoltaikanlagen sorgt für Rekordwerte bei der nachhaltigen Stromerzeugung.", ["environment"], "https://images.unsplash.com/photo-1497440001374-f26997328c1b?w=600&auto=format&fit=crop&q=80"), "de", "de"),
        new(new("de-entertainment-1", "https://www.rollingstone.de/filmfestspiele-autorenfilme-und-digitale-kinoproduktionen-2718293/", "Filmfestspiele präsentieren wegweisende Autorenfilme und digitale Kinoproduktionen", "Internationale Regisseure loten die Grenzen visueller Erzählformen im Spannungsfeld klassischer Kinematografie und digitaler Ästhetik aus.", ["entertainment"], "https://images.unsplash.com/photo-1514525253161-7a46d19cd819?w=600&auto=format&fit=crop&q=80"), "de", "de"),
        new(new("de-food-1", "https://www.essen-und-trinken.de/saisonales/nachhaltige-gastronomie-zero-waste-in-der-spitzenkueche-1349821.html", "Nachhaltige Gastronomie: Regionale Zutaten und Zero-Waste-Konzepte im Trend", "Immer mehr Spitzenküchen setzen auf direkte Erzeugerpartnerschaften und ressourcenschonende Zubereitungsmethoden.", ["food"], "https://images.unsplash.com/photo-1498837167922-ddd27525d352?w=600&auto=format&fit=crop&q=80"), "de", "de"),
        new(new("de-tourism-1", "https://www.geo.de/reisen/reiseziele/nachhaltiger-tourismus-sanfte-mobilitaet-in-reisedestinationen-34591823.html", "Nachhaltiger Tourismus: Beliebte Reisedestinationen setzen auf sanfte Mobilität", "Innovative Konzepte zur Besucherlenkung und umweltfreundliche Reiseangebote schützen sensible Natur- und Kulturlandschaften.", ["tourism"], "https://images.unsplash.com/photo-1488646953014-85cb44e25828?w=600&auto=format&fit=crop&q=80"), "de", "de"),
        new(new("de-world-1", "https://www.tagesschau.de/ausland/energiekonferenz-digitale-netze-102.html", "Internationale Energiekonferenz beschließt neue Standards für digitale Netze", "Vertreter aus über 40 Nationen verständigen sich auf einheitliche Richtlinien zur Resilienz kritischer digitaler Netzinfrastrukturen.", ["world"], "https://images.unsplash.com/photo-1509391365360-2e959784a276?w=600&auto=format&fit=crop&q=80"), "de", "de"),

        // === ENGLISH ARTICLES (en / us or gb) ===
        new(new("en-tech-1", "https://www.theverge.com/news/592819/next-gen-ai-architectures-autonomous-agents", "Next-Generation AI Architectures and Autonomous Agent Systems", "Frontier research teams reveal new multimodal foundation models capable of complex multi-step reasoning and autonomous tooling.", ["technology"], "https://images.unsplash.com/photo-1526374965328-7f61d4dc18c5?w=600&auto=format&fit=crop&q=80"), "en", "us"),
        new(new("en-tech-2", "https://www.wired.com/story/future-of-webassembly-microservices-edge-compute/", "The Future of WebAssembly: Cloud-Native Microservices and Edge Compute", "How lightweight Wasm modules are redefining serverless computing and low-latency microservices architectures worldwide.", ["technology"], "https://images.unsplash.com/photo-1517694712202-14dd9538aa97?w=600&auto=format&fit=crop&q=80"), "en", "us"),
        new(new("en-biz-1", "https://www.bloomberg.com/news/articles/2026-08-15/global-markets-surge-clean-energy-investments", "Global Markets Surge as Sustainable Tech and Clean Energy Investments Boom", "Venture capital and institutional funds allocate record capital to clean tech infrastructure and green data centers.", ["business"], "https://images.unsplash.com/photo-1611974789855-9c2a0a7236a3?w=600&auto=format&fit=crop&q=80"), "en", "us"),
        new(new("en-biz-2", "https://www.reuters.com/markets/europe/central-banks-signal-coordinated-monetary-easing-2026-08-15/", "Central Banks Signal Coordinated Monetary Easing to Stimulate Growth", "Global financial institutions observe shifting yields and resilient labor markets across major international economies.", ["business"], "https://images.unsplash.com/photo-1450133064473-71024230f91b?w=600&auto=format&fit=crop&q=80"), "en", "gb"),
        new(new("en-sci-1", "https://www.nature.com/articles/d41586-026-02391-4", "James Webb Space Telescope Identifies Atmospheric Biomarkers on Habitable-Zone Exoplanet", "Astrophysicists announce the detection of methane and carbon dioxide in the atmosphere of a super-Earth in a nearby planetary system.", ["science"], "https://images.unsplash.com/photo-1462331940025-496dfbfc7564?w=600&auto=format&fit=crop&q=80"), "en", "gb"),
        new(new("en-sci-2", "https://www.newscientist.com/article/2418291-quantum-advantage-reached-in-complex-chemical-simulations/", "Quantum Advantage Reached in Complex Chemical Simulation Benchmarks", "Researchers demonstrate scalable quantum error correction, unlocking new avenues for catalyst design and battery chemistry.", ["science"], "https://images.unsplash.com/photo-1635070041078-e363dbe005cb?w=600&auto=format&fit=crop&q=80"), "en", "gb"),
        new(new("en-sports-1", "https://www.bbc.com/sport/football/articles/c207559e211o", "Premier League Title Race: Dramatic Late Winner Keeps Championship Hopes Alive", "An electrifying stoppage-time goal in front of 60,000 fans reshapes the top of the league table in a thrilling weekend showdown.", ["sports"], "https://images.unsplash.com/photo-1574629810360-7efbbe195018?w=600&auto=format&fit=crop&q=80"), "en", "gb"),
        new(new("en-sports-2", "https://www.espn.com/nba/story/_/id/40981726/nba-finals-preview-high-powered-offenses", "NBA Finals Preview: Historic Matchup Set Between High-Powered Offenses", "Superstars collide in a highly anticipated best-of-seven championship series that promises high tempo and record viewership.", ["sports"], "https://images.unsplash.com/photo-1546519638-68e109498ffc?w=600&auto=format&fit=crop&q=80"), "en", "us"),
        new(new("en-health-1", "https://www.medicalnewstoday.com/articles/crispr-gene-editing-breakthrough-clinical-trials-2026", "CRISPR Gene Editing Breakthrough Offers New Hope for Hereditary Disorders", "Clinical trial results demonstrate remarkable efficacy and long-term remission in patients with genetic blood conditions.", ["health"], "https://images.unsplash.com/photo-1532938911079-1b06ac7ceec7?w=600&auto=format&fit=crop&q=80"), "en", "us"),
        new(new("en-health-2", "https://www.healthline.com/nutrition/longevity-plant-rich-diets-evidence-2026", "Nutrition and Longevity: New Clinical Evidence on Mediterranean Plant-Rich Diets", "Longitudinal cohort studies highlight significant improvements in metabolic resilience and cognitive vitality through diet optimization.", ["health"], "https://images.unsplash.com/photo-1490645935967-10de6ba17061?w=600&auto=format&fit=crop&q=80"), "en", "us"),
        new(new("en-politics-1", "https://www.theguardian.com/environment/2026/aug/15/international-climate-accord-landmark-carbon-limits", "International Climate Accord Enforces Landmark Binding Carbon Limits", "Delegates from 190 countries finalize binding agreements on phasing out unabated fossil fuels and subsidizing clean grid transitions.", ["politics"], "https://images.unsplash.com/photo-1529107386315-e1a2ed48a620?w=600&auto=format&fit=crop&q=80"), "en", "gb"),
        new(new("en-environment-1", "https://www.nationalgeographic.com/environment/article/global-coral-reef-restoration-recovery-2026", "Global Coral Reef Restoration Initiatives Show Promising Recovery Signs", "Marine biologists deploy climate-resilient coral micro-fragments across tropical reefs with unprecedented survival rates.", ["environment"], "https://images.unsplash.com/photo-1544551763-46a013bb70d5?w=600&auto=format&fit=crop&q=80"), "en", "us"),
        new(new("en-entertainment-1", "https://www.variety.com/2026/film/box-office/global-box-office-independent-cinema-surge-1235918231/", "Global Box Office Hits Multi-Year High as Acclaimed Independent Cinema Rises", "Audiences celebrate original storytelling and visually daring cinema, driving historic attendance across international theaters.", ["entertainment"], "https://images.unsplash.com/photo-1489599849927-2ee91cede3ba?w=600&auto=format&fit=crop&q=80"), "en", "us"),
        new(new("en-food-1", "https://www.eater.com/2026/8/15/24189201/zero-waste-dining-revolution-sustainable-restaurants", "The Zero-Waste Dining Revolution: How Modern Chefs Pioneer Circular Cuisine", "From regenerative farms to sustainable fermentation labs, the culinary world embraces minimal waste and rich flavors.", ["food"], "https://images.unsplash.com/photo-1555396273-367ea4eb4db5?w=600&auto=format&fit=crop&q=80"), "en", "us"),
        new(new("en-tourism-1", "https://www.lonelyplanet.com/articles/top-sustainable-travel-destinations-2026", "Top Sustainable Travel Destinations Leading the Future of Eco-Tourism", "Pristine national parks and community-led conservation projects offer unforgettable and responsible travel adventures.", ["tourism"], "https://images.unsplash.com/photo-1469854523086-cc02fe5d8800?w=600&auto=format&fit=crop&q=80"), "en", "us"),
        new(new("en-world-1", "https://www.reuters.com/technology/global-digital-connectivity-satellite-broadband-remote-areas-2026-08-15/", "Global Digital Connectivity Initiative Expands High-Speed Internet Access", "A multilateral coalition delivers satellite broadband infrastructure to remote communities across Latin America and Africa.", ["world"], "https://images.unsplash.com/photo-1526778548025-fa2f459cd5c1?w=600&auto=format&fit=crop&q=80"), "en", "us")
    ];

    private static NewsArticleQueryDto? BuildArticleQuery(string apiKey, NewsletterSubscription subscription)
    {
        List<string>? countries = subscription.Countries is { Count: > 0 }
            ? subscription.Countries.Select(CountryIsoCodeMapper.ToIso3166Alpha2).ToList()
            : null;
        List<string>? languages = subscription.Languages is { Count: > 0 }
            ? subscription.Languages.Select(LanguageIsoCodeMapper.ToIso639Code).ToList()
            : null;
        List<string>? categories = subscription.Category is { Count: > 0 }
            ? subscription.Category.Select(category => category.ToString().ToLowerInvariant()).ToList()
            : null;

        string? keywordsQuery = null;
        if (subscription.Keywords is { Count: > 0 })
        {
            List<string> terms = subscription.Keywords.Where(keyword => !string.IsNullOrWhiteSpace(keyword)).Select(keyword => keyword.Trim()).ToList();
            if (terms.Count > 0)
                keywordsQuery = string.Join(" OR ", terms);
        }

        if (countries is null && languages is null && categories is null && string.IsNullOrWhiteSpace(keywordsQuery))
            return null;

        return new NewsArticleQueryDto
        {
            ApiKey = apiKey,
            Countries = countries,
            Languages = languages,
            Categories = categories,
            KeywordsQuery = keywordsQuery
        };
    }
}
