namespace Onboarding.Web.Services;

/// <summary>
/// The fixed CIP/AML rule contract shared between this app and the Foundry KYC
/// agent (Asia rule set, Hong Kong jurisdiction). Rule ids are a stable API:
/// the agent reads them from <c>get_cip_rulebook</c> and reports every result
/// back through <c>submit_policy_check</c>, which is what ticks the UI off.
/// </summary>
public static class CipRulebook
{
    public const string Jurisdiction = "HK";
    public const string Region = "Asia";

    /// <summary>Logical names of the connected IQ sources in the agent toolbox.</summary>
    public static class Iq
    {
        public const string Foundry = "Foundry IQ";
        public const string Fabric = "Fabric IQ";
        public const string Web = "Web IQ";
        public const string Work = "Work IQ";
    }

    public record CipRule(string Id, string Group, string Stage, string Title, string Question, string Iq, string Reference);

    public static readonly IReadOnlyList<CipRule> Rules =
    [
        // ---- Data enrichment -------------------------------------------------
        new("ENR-01", "Data enrichment", "enrichment", "Legal name & registration",
            "Is the full legal name, registration number and registered office address in the place of incorporation confirmed?",
            Iq.Fabric, "CIP requirements 1, 2, 5"),
        new("ENR-02", "Data enrichment", "enrichment", "Incorporation details",
            "Are the country and date of incorporation confirmed from a reliable independent source?",
            Iq.Web, "CIP requirements 6, 7"),
        new("ENR-03", "Data enrichment", "enrichment", "Entity type classification",
            "Is the entity classified as Regulated Financial Institution, Public Listed Company, Fund / Collective Investment Scheme, Pension Scheme, Government / Wholly State-Owned Entity or Partnership?",
            Iq.Foundry, "HK CIP decision tree entry points"),
        new("ENR-04", "Data enrichment", "enrichment", "Regulated status & regulator",
            "Is the entity regulated, and which regulator supervises it (including parent or head office)?",
            Iq.Web, "CIP requirement 4"),
        new("ENR-05", "Data enrichment", "enrichment", "Exchange listing",
            "Is the entity listed, and on which exchange?",
            Iq.Web, "HK CIP decision tree node Q11"),
        new("ENR-06", "Data enrichment", "enrichment", "Ownership & control structure",
            "Is the ownership chain mapped, including beneficial owners holding more than 10% and any nominee shareholders?",
            Iq.Fabric, "CIP requirements 18-21"),
        new("ENR-07", "Data enrichment", "enrichment", "Nature of business & relationship purpose",
            "Are the nature of business, product scope and purpose of the relationship captured?",
            Iq.Fabric, "CIP requirements 8, 9"),
        new("ENR-08", "Data enrichment", "enrichment", "Cross-border correspondent flag",
            "Is this a cross-border correspondent relationship?",
            Iq.Work, "HK CIP decision tree node Q2"),

        // ---- Reference lists (Asia / HK) ------------------------------------
        new("REF-01", "Reference lists", "enrichment", "Approved Regulator (AR) list",
            "Does the supervising regulator appear on the Approved Regulator list?",
            Iq.Foundry, "Approved Regulator list"),
        new("REF-02", "Reference lists", "enrichment", "Approved Exchange (AE) list",
            "Does the listing exchange appear on the Approved Exchange list?",
            Iq.Foundry, "Approved Exchange list"),
        new("REF-03", "Reference lists", "enrichment", "Jurisdictional Risk List (JRL)",
            "What JRL risk band applies to the country of incorporation and the regulator's country?",
            Iq.Foundry, "Jurisdictional Risk List"),
        new("REF-04", "Reference lists", "enrichment", "FATF membership",
            "Is the relevant country a FATF member, and is it below the Extreme JRL band?",
            Iq.Foundry, "FATF membership list"),

        // ---- Risk scoring ----------------------------------------------------
        new("RSK-01", "Risk scoring", "risk", "Ownership Type Risk (max 15)",
            "Is the client government owned, a direct relationship with an individual, or other?",
            Iq.Foundry, "Risk scoring criteria"),
        new("RSK-02", "Risk scoring", "risk", "Listed Entity Risk (max 2)",
            "Is the listing exchange an exact match on the Approved Exchange list?",
            Iq.Foundry, "Risk scoring criteria"),
        new("RSK-03", "Risk scoring", "risk", "Regulated Status Risk (max 2)",
            "Is the regulator an exact match on the Approved Regulator list?",
            Iq.Foundry, "Risk scoring criteria"),
        new("RSK-04", "Risk scoring", "risk", "Industry Risk (max 65)",
            "What industry classification applies, and what score does it carry?",
            Iq.Foundry, "Risk scoring criteria"),
        new("RSK-05", "Risk scoring", "risk", "Product Risk (max 15)",
            "What product category and product type are requested, and what score do they carry?",
            Iq.Foundry, "Risk scoring criteria"),
        new("RSK-06", "Risk scoring", "risk", "Total score & rating band",
            "What is the total score out of 112 and which rating band does it fall into?",
            Iq.Foundry, "Risk rating bands"),

        // ---- CIP decision tree (HK) -----------------------------------------
        new("CIP-Q7", "CIP decision tree", "amlRequirements", "Q7 - Public listed company regulated?",
            "For a public listed company: is the entity regulated?",
            Iq.Foundry, "HK CIP schedule decision tree"),
        new("CIP-Q11", "CIP decision tree", "amlRequirements", "Q11 - Approved exchange listing?",
            "Is the entity listed on an AE-list exchange, or a FATF-member exchange of a country not Very High to Extreme on the JRL?",
            Iq.Foundry, "HK CIP schedule decision tree"),
        new("CIP-Q2", "CIP decision tree", "amlRequirements", "Q2 - Cross-border correspondent relationship?",
            "Is the relationship a cross-border correspondent relationship?",
            Iq.Foundry, "HK CIP schedule decision tree"),
        new("CIP-Q3", "CIP decision tree", "amlRequirements", "Q3 - Approved regulator (correspondent branch)",
            "Is the regulator on the AR list, or the regulator of a FATF member country not Extreme on the JRL?",
            Iq.Foundry, "HK CIP schedule decision tree"),
        new("CIP-Q8", "CIP decision tree", "amlRequirements", "Q8 - Approved regulator (non-correspondent branch)",
            "Is the regulator on the AR list, or the regulator of a FATF member country not Extreme on the JRL?",
            Iq.Foundry, "HK CIP schedule decision tree"),
        new("CIP-Q15", "CIP decision tree", "amlRequirements", "Q15 - Fund investment manager regulated?",
            "Is the fund's investment manager regulated?",
            Iq.Foundry, "HK CIP schedule decision tree"),
        new("CIP-Q16", "CIP decision tree", "amlRequirements", "Q16 - Investment manager approved regulator?",
            "Is the investment manager, or the fund administrator conducting KYC, regulated by an AR-list regulator?",
            Iq.Foundry, "HK CIP schedule decision tree"),
        new("CIP-Q20", "CIP decision tree", "amlRequirements", "Q20 - Government pension scheme?",
            "Is the entity a government pension scheme?",
            Iq.Foundry, "HK CIP schedule decision tree"),
        new("CIP-Q23", "CIP decision tree", "amlRequirements", "Q23 - Wholly state-owned entity?",
            "Is the entity a wholly state-owned entity?",
            Iq.Foundry, "HK CIP schedule decision tree"),
        new("CIP-Q24", "CIP decision tree", "amlRequirements", "Q24 - State-owned entity regulated?",
            "Is the wholly state-owned entity regulated?",
            Iq.Foundry, "HK CIP schedule decision tree"),
        new("CIP-SCH", "CIP decision tree", "amlRequirements", "Selected CIP schedule",
            "Which CIP schedule (4.2, 4.3, 4.6, 4.7, 4.8, 4.9, 4.10, 4.11, 4.12 or 4.13) does the walk conclude on?",
            Iq.Foundry, "HK CIP schedules"),

        // ---- AML screening ---------------------------------------------------
        new("AML-01", "AML screening", "amlRequirements", "Sanctions screening - customer",
            "Does the customer return any sanctions or watch-list match?",
            Iq.Web, "CIP requirement 17"),
        new("AML-02", "AML screening", "amlRequirements", "Sanctions screening - connected parties",
            "Do directors, authorised persons and beneficial owners return any sanctions match?",
            Iq.Web, "CIP requirements 17, 23"),
        new("AML-03", "AML screening", "amlRequirements", "PEP screening",
            "Is the customer or any connected party a politically exposed person?",
            Iq.Web, "CIP requirements 17, 23"),
        new("AML-04", "AML screening", "amlRequirements", "Adverse media",
            "Do media searches on the customer and connected parties raise financial-crime findings?",
            Iq.Web, "CIP requirement 24"),
        new("AML-05", "AML screening", "amlRequirements", "Source of funds & source of wealth",
            "Are the source of funds and, for beneficial owners, the source of wealth established?",
            Iq.Fabric, "CIP requirements 10, 20"),
        new("AML-06", "AML screening", "amlRequirements", "Local AML/CFT guideline alignment",
            "Does the case meet the customer due-diligence and record-keeping expectations of the local AML/CFT guideline for this jurisdiction?",
            Iq.Foundry, "Jurisdiction AML/CFT guideline"),

        // ---- Regulatory classification --------------------------------------
        new("REG-01", "Regulatory classification", "amlRequirements", "FATCA classification",
            "What FATCA classification applies to the entity?",
            Iq.Foundry, "Regulatory classification policy"),
        new("REG-02", "Regulatory classification", "amlRequirements", "CRS & tax residency",
            "What CRS classification and tax residency self-certification apply?",
            Iq.Foundry, "Regulatory classification policy"),
        new("REG-03", "Regulatory classification", "amlRequirements", "Acting capacity & reliance",
            "Does the customer act as principal or agent, and is an AML reliance letter required?",
            Iq.Foundry, "CIP requirement 11"),

        // ---- Evidence & gap --------------------------------------------------
        new("EVD-01", "Evidence & gap", "sourcing", "Requirement list expanded",
            "Is the selected schedule expanded into the requirement list, de-duplicated across schedules, with the source kept per item?",
            Iq.Foundry, "CIP schedule expansion"),
        new("EVD-02", "Evidence & gap", "sourcing", "Internal evidence sourced",
            "Which requirements can be satisfied from evidence already held internally?",
            Iq.Fabric, "Internal document repositories"),
        new("EVD-03", "Evidence & gap", "gapAnalysis", "Gap list produced",
            "Which requirements remain outstanding after internal evidence is applied?",
            Iq.Fabric, "Gap analysis"),
        new("EVD-04", "Evidence & gap", "outreach", "Outreach & business approval",
            "Are the outstanding items packaged for client outreach, with cross-border due-diligence questionnaire and business management approval requested?",
            Iq.Work, "CIP requirements 12, 13, 25"),
    ];

    public static readonly IReadOnlyList<string> Groups =
        Rules.Select(r => r.Group).Distinct().ToList();
}
