using DispatchApp.Server.Data.DataTypes;
using System.Text.RegularExpressions;

namespace DispatchApp.Server.Services
{
    /// <summary>
    /// Service for calculating ride pricing based on:
    /// 1. Set prices for known routes (from price sheets)
    /// 2. Formula-based pricing for unknown routes
    /// 3. Rush hour adjustments (2-5:30pm)
    /// 4. Minimum fare ($65 for trips < 1 hour)
    /// </summary>
    public class PricingService
    {
        private readonly GoogleMapsService _googleMapsService;

        #region Hourly Rates (already doubled for round trip in formula)
        private static readonly Dictionary<CarType, decimal> HourlyRates = new()
        {
            { CarType.Car, 60m },
            { CarType.SUV, 60m },           // SUV uses Car pricing
            { CarType.MiniVan, 70m },
            { CarType.LuxurySUV, 140m },
            { CarType.TwelvePass, 200m },
            { CarType.FifteenPass, 250m },
            { CarType.MercSprinter, 300m }
        };
        #endregion

        #region Location Detection Patterns
        private static readonly Dictionary<string, Func<string, bool>> LocationDetectors = new()
        {
            { "LAKEWOOD", addr => IsLakewood(addr) },
            { "BROOKLYN", addr => IsBrooklyn(addr) },
            { "FLATBUSH", addr => IsFlatbush(addr) },
            { "BP", addr => IsBoroughPark(addr) },
            { "WILLIAMSBURG", addr => IsWilliamsburg(addr) },
            { "MONSEY", addr => IsMonsey(addr) },
            { "MONROE", addr => IsMonroe(addr) },
            { "UPSTATE", addr => IsUpstate(addr) },
            { "MANHATTAN", addr => IsManhattan(addr) },
            { "STATEN_ISLAND", addr => IsStatenIsland(addr) },
            { "JFK", addr => IsJFK(addr) },
            { "LGA", addr => IsLGA(addr) },
            { "EWR", addr => IsEWR(addr) },
            { "PHL", addr => IsPHL(addr) },
            { "PHILLY", addr => IsPhilly(addr) },
            { "PASSAIC", addr => IsPassaic(addr) },
            { "LINDEN", addr => IsLinden(addr) }
        };

        // Location detection methods

        /// <summary>
        /// Lakewood area: Lakewood (08701), Jackson (08527), Toms River (08753-08757), 
        /// Howell (07731-07733), Brick (08723-08724), Manchester (08759), 
        /// Freehold (07728), Ocean Township area
        /// </summary>
        private static bool IsLakewood(string addr) =>
            addr.Contains("Lakewood", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Jackson", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Toms River", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Howell", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Brick", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Manchester", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Freehold", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Ocean Township", StringComparison.OrdinalIgnoreCase) ||
            // Lakewood: 08701
            // Jackson: 08527
            // Toms River: 08753, 08754, 08755, 08756, 08757
            // Howell: 07731, 07732, 07733
            // Brick: 08723, 08724
            // Manchester: 08759
            // Freehold: 07728
            Regex.IsMatch(addr, @"\b(08701|08527|0875[3-9]|0773[1-3]|0872[34]|08759|07728)\b");

        /// <summary>
        /// Brooklyn general: All Brooklyn addresses not matched by specific neighborhoods
        /// Brooklyn zips: 11201-11256
        /// </summary>
        private static bool IsBrooklyn(string addr) =>
            addr.Contains("Brooklyn", StringComparison.OrdinalIgnoreCase) ||
            Regex.IsMatch(addr, @"\b112[0-5]\d\b");

        /// <summary>
        /// Flatbush: 11210, 11226, 11230, 11234, 11203 (East Flatbush)
        /// </summary>
        private static bool IsFlatbush(string addr) =>
            addr.Contains("Flatbush", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("East Flatbush", StringComparison.OrdinalIgnoreCase) ||
            Regex.IsMatch(addr, @"\b(11210|11226|11230|11234|11203|11225)\b");

        /// <summary>
        /// Borough Park: 11204, 11218, 11219, 11214 (Bensonhurst/BP border)
        /// </summary>
        private static bool IsBoroughPark(string addr) =>
            addr.Contains("Borough Park", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Boro Park", StringComparison.OrdinalIgnoreCase) ||
            Regex.IsMatch(addr, @"\b(11204|11218|11219|11214)\b");

        /// <summary>
        /// Williamsburg: 11206, 11211, 11249, 11205 (parts)
        /// </summary>
        private static bool IsWilliamsburg(string addr) =>
            addr.Contains("Williamsburg", StringComparison.OrdinalIgnoreCase) ||
            Regex.IsMatch(addr, @"\b(11206|11211|11249)\b");

        /// <summary>
        /// Monsey area: Monsey (10952), Spring Valley (10977), New Square (10954),
        /// Wesley Hills, Pomona, Airmont, Suffern (10901), Hillburn, Ramapo area
        /// </summary>
        private static bool IsMonsey(string addr) =>
            addr.Contains("Monsey", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Spring Valley", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("New Square", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Wesley Hills", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Pomona", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Airmont", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Suffern", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Hillburn", StringComparison.OrdinalIgnoreCase) ||
            // Monsey: 10952, Spring Valley: 10977, New Square: 10954, Suffern: 10901
            Regex.IsMatch(addr, @"\b(10952|10977|10954|10901|10956|10970|10965|10994|10960|10980|10993|10913)\b");

        /// <summary>
        /// Monroe/Kiryas Joel area: Monroe (10950), Kiryas Joel (10950), Harriman (10926), 
        /// Chester (10918), Woodbury (10917, 10930)
        /// </summary>
        private static bool IsMonroe(string addr) =>
            addr.Contains("Monroe", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Kiryas Joel", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Harriman", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Chester", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Woodbury", StringComparison.OrdinalIgnoreCase) ||
            // Monroe/KJ: 10950, Harriman: 10926, Chester: 10918, Woodbury: 10917/10930
            Regex.IsMatch(addr, @"\b(10950|10926|10918|10917|10930)\b");

        /// <summary>
        /// Upstate/Catskills: Woodbourne (12788), South Fallsburg (12779), Liberty (12754),
        /// Monticello (12701), Ellenville (12428), Loch Sheldrake (12759), Hurleyville (12747),
        /// Swan Lake (12783), Kiamesha Lake (12751), Mountaindale (12763), Woodridge (12789),
        /// Ferndale (12734), Glen Wild, Bloomingburg, Wurtsboro
        /// </summary>
        private static bool IsUpstate(string addr) =>
            addr.Contains("Woodbourne", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("South Fallsburg", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Liberty", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Monticello", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Ellenville", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Catskill", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Loch Sheldrake", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Hurleyville", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Swan Lake", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Kiamesha", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Mountaindale", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Woodridge", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Ferndale", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Glen Wild", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Bloomingburg", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Wurtsboro", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Fallsburg", StringComparison.OrdinalIgnoreCase) ||
            // 12788 Woodbourne, 12779 S.Fallsburg, 12754 Liberty, 12701 Monticello,
            // 12428 Ellenville, 12759 Loch Sheldrake, 12747 Hurleyville, 12783 Swan Lake,
            // 12751 Kiamesha, 12763 Mountaindale, 12789 Woodridge, 12734 Ferndale,
            // 12780 Bloomingburg, 12790 Wurtsboro
            Regex.IsMatch(addr, @"\b(12788|12779|12754|12701|12428|12759|12747|12783|12751|12763|12789|12734|12780|12790)\b");

        /// <summary>
        /// Manhattan: All NYC Manhattan addresses
        /// Manhattan zips: 10001-10282 (pattern 100xx, 101xx, 102xx up to 10282)
        /// </summary>
        private static bool IsManhattan(string addr) =>
            addr.Contains("Manhattan", StringComparison.OrdinalIgnoreCase) ||
            (addr.Contains("New York", StringComparison.OrdinalIgnoreCase) &&
             addr.Contains("NY", StringComparison.OrdinalIgnoreCase) &&
             !addr.Contains("Brooklyn", StringComparison.OrdinalIgnoreCase) &&
             !addr.Contains("Queens", StringComparison.OrdinalIgnoreCase) &&
             !addr.Contains("Bronx", StringComparison.OrdinalIgnoreCase) &&
             !addr.Contains("Staten Island", StringComparison.OrdinalIgnoreCase)) ||
            // Manhattan zips: 10001-10282
            Regex.IsMatch(addr, @"\b(100\d{2}|101\d{2}|102[0-7]\d|1028[0-2])\b");

        /// <summary>
        /// Staten Island: All Staten Island addresses
        /// Staten Island zips: 10301-10314
        /// </summary>
        private static bool IsStatenIsland(string addr) =>
            addr.Contains("Staten Island", StringComparison.OrdinalIgnoreCase) ||
            Regex.IsMatch(addr, @"\b103(0[1-9]|1[0-4])\b");

        /// <summary>
        /// JFK Airport: John F. Kennedy International Airport
        /// Address patterns: JFK, John F Kennedy, Kennedy Airport
        /// Zip: 11430 (Jamaica, Queens - JFK area)
        /// </summary>
        private static bool IsJFK(string addr) =>
            addr.Contains("JFK", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("John F. Kennedy", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("John F Kennedy", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Kennedy Airport", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Kennedy International", StringComparison.OrdinalIgnoreCase) ||
            Regex.IsMatch(addr, @"\b11430\b");

        /// <summary>
        /// LGA Airport: LaGuardia Airport
        /// Address patterns: LGA, LaGuardia, La Guardia
        /// Zip: 11371 (Flushing/East Elmhurst - LGA area)
        /// </summary>
        private static bool IsLGA(string addr) =>
            addr.Contains("LGA", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("LaGuardia", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("La Guardia", StringComparison.OrdinalIgnoreCase) ||
            Regex.IsMatch(addr, @"\b11371\b");

        /// <summary>
        /// EWR Airport: Newark Liberty International Airport
        /// Address patterns: EWR, Newark Airport, Newark Liberty
        /// Zip: 07114 (Newark Airport area)
        /// Note: Must check for "Airport" with Newark to avoid matching Newark city addresses
        /// </summary>
        private static bool IsEWR(string addr) =>
            addr.Contains("EWR", StringComparison.OrdinalIgnoreCase) ||
            (addr.Contains("Newark", StringComparison.OrdinalIgnoreCase) &&
             addr.Contains("Airport", StringComparison.OrdinalIgnoreCase)) ||
            addr.Contains("Newark Liberty", StringComparison.OrdinalIgnoreCase) ||
            Regex.IsMatch(addr, @"\b07114\b");

        /// <summary>
        /// PHL Airport: Philadelphia International Airport
        /// Address patterns: PHL, Philadelphia Airport, Philadelphia International
        /// Zip: 19153 (Philadelphia Airport area)
        /// Note: Must check for "Airport" or "International" with Philadelphia to avoid matching Philly city addresses
        /// </summary>
        private static bool IsPHL(string addr) =>
            addr.Contains("PHL", StringComparison.OrdinalIgnoreCase) ||
            (addr.Contains("Philadelphia", StringComparison.OrdinalIgnoreCase) &&
             addr.Contains("Airport", StringComparison.OrdinalIgnoreCase)) ||
            (addr.Contains("Philadelphia", StringComparison.OrdinalIgnoreCase) &&
             addr.Contains("International", StringComparison.OrdinalIgnoreCase)) ||
            Regex.IsMatch(addr, @"\b19153\b");

        /// <summary>
        /// Philadelphia area: All Philadelphia addresses (city, not airport)
        /// Philly zips: 19xxx range
        /// </summary>
        private static bool IsPhilly(string addr) =>
            addr.Contains("Philadelphia", StringComparison.OrdinalIgnoreCase) ||
            addr.Contains("Philly", StringComparison.OrdinalIgnoreCase) ||
            Regex.IsMatch(addr, @"\b19\d{3}\b");

        /// <summary>
        /// Passaic: Passaic city, NJ
        /// Zip: 07055
        /// </summary>
        private static bool IsPassaic(string addr) =>
            addr.Contains("Passaic", StringComparison.OrdinalIgnoreCase) ||
            Regex.IsMatch(addr, @"\b07055|b07054\b");

        /// <summary>
        /// Linden: Linden city, NJ
        /// Zip: 07036
        /// </summary>
        private static bool IsLinden(string addr) =>
            addr.Contains("Linden", StringComparison.OrdinalIgnoreCase) ||
            Regex.IsMatch(addr, @"\b07036\b");
        #endregion

        #region Set Price Data Structure
        /// <summary>
        /// Set prices from price sheets. Key format: "ORIGIN-DESTINATION"
        /// Values: Dictionary of CarType -> (Price, OwnerCut)
        /// </summary>
        private static readonly Dictionary<string, Dictionary<CarType, (decimal Price, decimal OwnerCut)>> SetPrices = new()
        {
            // ==================== LAKEWOOD ROUTES ====================

            // LKWD-NEWARK/EWR
            ["LAKEWOOD-EWR"] = new()
            {
                { CarType.Car, (105m, 30m) },
                { CarType.SUV, (105m, 30m) },
                { CarType.MiniVan, (115m, 30m) },
                { CarType.LuxurySUV, (170m, 30m) },
                { CarType.MercSprinter, (350m, 35m) }
            },
            ["EWR-LAKEWOOD"] = new()
            {
                { CarType.Car, (105m, 30m) },
                { CarType.SUV, (105m, 30m) },
                { CarType.MiniVan, (115m, 30m) },
                { CarType.LuxurySUV, (170m, 30m) },
                { CarType.MercSprinter, (350m, 35m) }
            },

            // LKWD-PHILLY 
            ["LAKEWOOD-PHILLY"] = new()
            {
                { CarType.Car, (145m, 35m) },
                { CarType.SUV, (145m, 35m) },
                { CarType.MiniVan, (160m, 35m) },
                { CarType.LuxurySUV, (220m, 40m) },
                { CarType.TwelvePass, (340m, 50m) },
                { CarType.FifteenPass, (425m, 55m) },
                { CarType.MercSprinter, (510m, 60m) }
            },
            ["PHILLY-LAKEWOOD"] = new()
            {
                { CarType.Car, (145m, 35m) },
                { CarType.SUV, (145m, 35m) },
                { CarType.MiniVan, (160m, 35m) },
                { CarType.LuxurySUV, (220m, 40m) },
                { CarType.TwelvePass, (340m, 50m) },
                { CarType.FifteenPass, (425m, 55m) },
                { CarType.MercSprinter, (510m, 60m) }
            },

            // LKWD-JFK 
            ["LAKEWOOD-JFK"] = new()
            {
                { CarType.Car, (185m, 35m) },
                { CarType.SUV, (185m, 35m) },
                { CarType.MiniVan, (195m, 35m) },
                { CarType.LuxurySUV, (270m, 40m) },
                { CarType.TwelvePass, (415m, 55m) },
                { CarType.FifteenPass, (520m, 60m) },
                { CarType.MercSprinter, (620m, 70m) }
            },
            ["JFK-LAKEWOOD"] = new()
            {
                { CarType.Car, (185m, 35m) },
                { CarType.SUV, (185m, 35m) },
                { CarType.MiniVan, (195m, 35m) },
                { CarType.LuxurySUV, (270m, 40m) },
                { CarType.TwelvePass, (415m, 55m) },
                { CarType.FifteenPass, (520m, 60m) },
                { CarType.MercSprinter, (620m, 70m) }
            },

            // LKWD-LGA
            ["LAKEWOOD-LGA"] = new()
            {
                { CarType.Car, (200m, 35m) },
                { CarType.SUV, (200m, 35m) },
                { CarType.MiniVan, (215m, 35m) },
                { CarType.LuxurySUV, (295m, 45m) },
                { CarType.TwelvePass, (450m, 55m) },
                { CarType.FifteenPass, (565m, 65m) },
                { CarType.MercSprinter, (675m, 75m) }
            },
            ["LGA-LAKEWOOD"] = new()
            {
                { CarType.Car, (200m, 35m) },
                { CarType.SUV, (200m, 35m) },
                { CarType.MiniVan, (215m, 35m) },
                { CarType.LuxurySUV, (295m, 45m) },
                { CarType.TwelvePass, (450m, 55m) },
                { CarType.FifteenPass, (565m, 65m) },
                { CarType.MercSprinter, (675m, 75m) }
            },

            // LKWD-MONSEY
            ["LAKEWOOD-MONSEY"] = new()
            {
                { CarType.Car, (180m, 35m) },
                { CarType.SUV, (180m, 35m) },
                { CarType.MiniVan, (195m, 35m) },
                { CarType.LuxurySUV, (250m, 40m) },
                { CarType.TwelvePass, (385m, 50m) },
                { CarType.FifteenPass, (480m, 55m) },
                { CarType.MercSprinter, (575m, 65m) }
            },
            ["MONSEY-LAKEWOOD"] = new()
            {
                { CarType.Car, (180m, 35m) },
                { CarType.SUV, (180m, 35m) },
                { CarType.MiniVan, (195m, 35m) },
                { CarType.LuxurySUV, (250m, 40m) },
                { CarType.TwelvePass, (385m, 50m) },
                { CarType.FifteenPass, (480m, 55m) },
                { CarType.MercSprinter, (575m, 65m) }
            },

            // LKWD-MONROE
            ["LAKEWOOD-MONROE"] = new()
            {
                { CarType.Car, (200m, 35m) },
                { CarType.SUV, (200m, 35m) },
                { CarType.MiniVan, (215m, 35m) },
                { CarType.LuxurySUV, (320m, 40m) },
                { CarType.TwelvePass, (410m, 50m) },
                { CarType.FifteenPass, (500m, 55m) },
                { CarType.MercSprinter, (560m, 60m) }
            },
            ["MONROE-LAKEWOOD"] = new()
            {
                { CarType.Car, (200m, 35m) },
                { CarType.SUV, (200m, 35m) },
                { CarType.MiniVan, (215m, 35m) },
                { CarType.LuxurySUV, (320m, 40m) },
                { CarType.TwelvePass, (410m, 50m) },
                { CarType.FifteenPass, (500m, 55m) },
                { CarType.MercSprinter, (560m, 60m) }
            },

            // LKWD-STATEN ISLAND
            ["LAKEWOOD-STATEN_ISLAND"] = new()
            {
                { CarType.Car, (140m, 30m) },
                { CarType.SUV, (140m, 30m) },
                { CarType.MiniVan, (155m, 30m) },
                { CarType.LuxurySUV, (210m, 35m) },
                { CarType.TwelvePass, (325m, 45m) },
                { CarType.FifteenPass, (405m, 50m) },
                { CarType.MercSprinter, (485m, 55m) }
            },
            ["STATEN_ISLAND-LAKEWOOD"] = new()
            {
                { CarType.Car, (140m, 30m) },
                { CarType.SUV, (140m, 30m) },
                { CarType.MiniVan, (155m, 30m) },
                { CarType.LuxurySUV, (210m, 35m) },
                { CarType.TwelvePass, (325m, 45m) },
                { CarType.FifteenPass, (405m, 50m) },
                { CarType.MercSprinter, (485m, 55m) }
            },

            // LKWD-MANHATTAN
            ["LAKEWOOD-MANHATTAN"] = new()
            {
                { CarType.Car, (180m, 35m) },
                { CarType.SUV, (180m, 35m) },
                { CarType.MiniVan, (195m, 35m) },
                { CarType.LuxurySUV, (260m, 40m) },
                { CarType.TwelvePass, (400m, 50m) },
                { CarType.FifteenPass, (500m, 55m) },
                { CarType.MercSprinter, (600m, 65m) }
            },
            ["MANHATTAN-LAKEWOOD"] = new()
            {
                { CarType.Car, (180m, 35m) },
                { CarType.SUV, (180m, 35m) },
                { CarType.MiniVan, (195m, 35m) },
                { CarType.LuxurySUV, (260m, 40m) },
                { CarType.TwelvePass, (400m, 50m) },
                { CarType.FifteenPass, (500m, 55m) },
                { CarType.MercSprinter, (600m, 65m) }
            },

            // LKWD-BROOKLYN 
            ["LAKEWOOD-BROOKLYN"] = new()
            {
                { CarType.Car, (180m, 30m) },
                { CarType.SUV, (180m, 30m) },
                { CarType.MiniVan, (195m, 30m) },
                { CarType.LuxurySUV, (270m, 40m) },
                { CarType.TwelvePass, (310m, 45m) },
                { CarType.FifteenPass, (375m, 50m) },
                { CarType.MercSprinter, (400m, 55m) }
            },
            ["BROOKLYN-LAKEWOOD"] = new()
            {
                { CarType.Car, (180m, 30m) },
                { CarType.SUV, (180m, 30m) },
                { CarType.MiniVan, (195m, 30m) },
                { CarType.LuxurySUV, (270m, 40m) },
                { CarType.TwelvePass, (310m, 45m) },
                { CarType.FifteenPass, (375m, 50m) },
                { CarType.MercSprinter, (400m, 55m) }
            },

            // LKWD-UPSTATE
            ["LAKEWOOD-UPSTATE"] = new()
            {
                { CarType.Car, (265m, 45m) },
                { CarType.SUV, (265m, 45m) },
                { CarType.MiniVan, (285m, 45m) },
                { CarType.LuxurySUV, (420m, 55m) },
                { CarType.TwelvePass, (615m, 70m) },
                { CarType.FifteenPass, (770m, 80m) },
                { CarType.MercSprinter, (750m, 80m) }
            },
            ["UPSTATE-LAKEWOOD"] = new()
            {
                { CarType.Car, (265m, 45m) },
                { CarType.SUV, (265m, 45m) },
                { CarType.MiniVan, (285m, 45m) },
                { CarType.LuxurySUV, (420m, 55m) },
                { CarType.TwelvePass, (615m, 70m) },
                { CarType.FifteenPass, (770m, 80m) },
                { CarType.MercSprinter, (750m, 80m) }
            },

            // ==================== BROOKLYN ROUTES ====================

            // FLATBUSH/BP-NEWARK (using as general BK-EWR)
            ["BROOKLYN-EWR"] = new()
            {
                { CarType.Car, (120m, 30m) },
                { CarType.SUV, (120m, 30m) },
                { CarType.MiniVan, (135m, 30m) },
                { CarType.LuxurySUV, (175m, 25m) },
                { CarType.TwelvePass, (245m, 35m) },
                { CarType.FifteenPass, (305m, 40m) },
                { CarType.MercSprinter, (300m, 35m) }
            },
            ["EWR-BROOKLYN"] = new()
            {
                { CarType.Car, (120m, 30m) },
                { CarType.SUV, (120m, 30m) },
                { CarType.MiniVan, (135m, 30m) },
                { CarType.LuxurySUV, (175m, 25m) },
                { CarType.TwelvePass, (245m, 35m) },
                { CarType.FifteenPass, (305m, 40m) },
                { CarType.MercSprinter, (300m, 35m) }
            },

            // FLATBUSH/BP-JFK 
            ["BROOKLYN-JFK"] = new()
            {
                { CarType.Car, (70m, 20m) },
                { CarType.SUV, (70m, 20m) },
                { CarType.MiniVan, (85m, 20m) },
                { CarType.LuxurySUV, (125m, 25m) },
                { CarType.TwelvePass, (140m, 20m) },
                { CarType.FifteenPass, (165m, 20m) },
                { CarType.MercSprinter, (200m, 20m) }
            },
            ["JFK-BROOKLYN"] = new()
            {
                { CarType.Car, (70m, 20m) },
                { CarType.SUV, (70m, 20m) },
                { CarType.MiniVan, (85m, 20m) },
                { CarType.LuxurySUV, (125m, 25m) },
                { CarType.TwelvePass, (140m, 20m) },
                { CarType.FifteenPass, (165m, 20m) },
                { CarType.MercSprinter, (200m, 20m) }
            },

            // BK-LGA
            ["BROOKLYN-LGA"] = new()
            {
                { CarType.Car, (75m, 15m) },
                { CarType.SUV, (75m, 15m) },
                { CarType.MiniVan, (90m, 15m) },
                { CarType.LuxurySUV, (150m, 25m) },
                { CarType.TwelvePass, (195m, 30m) },
                { CarType.FifteenPass, (245m, 35m) },
                { CarType.MercSprinter, (290m, 40m) }
            },
            ["LGA-BROOKLYN"] = new()
            {
                { CarType.Car, (75m, 15m) },
                { CarType.SUV, (75m, 15m) },
                { CarType.MiniVan, (90m, 15m) },
                { CarType.LuxurySUV, (150m, 25m) },
                { CarType.TwelvePass, (195m, 30m) },
                { CarType.FifteenPass, (245m, 35m) },
                { CarType.MercSprinter, (290m, 40m) }
            },

            // WILLIAMSBURG-LGA (special pricing)
            ["WILLIAMSBURG-LGA"] = new()
            {
                { CarType.Car, (60m, 15m) },
                { CarType.SUV, (60m, 15m) },
                { CarType.MiniVan, (70m, 15m) },
                { CarType.LuxurySUV, (120m, 25m) },
                { CarType.TwelvePass, (160m, 30m) },
                { CarType.FifteenPass, (200m, 35m) },
                { CarType.MercSprinter, (240m, 40m) }
            },
            ["LGA-WILLIAMSBURG"] = new()
            {
                { CarType.Car, (60m, 15m) },
                { CarType.SUV, (60m, 15m) },
                { CarType.MiniVan, (70m, 15m) },
                { CarType.LuxurySUV, (120m, 25m) },
                { CarType.TwelvePass, (160m, 30m) },
                { CarType.FifteenPass, (200m, 35m) },
                { CarType.MercSprinter, (240m, 40m) }
            },

            // BK-PHILLY
            ["BROOKLYN-PHILLY"] = new()
            {
                { CarType.Car, (250m, 40m) },
                { CarType.SUV, (250m, 40m) },
                { CarType.MiniVan, (275m, 40m) },
                { CarType.LuxurySUV, (365m, 40m) },
                { CarType.TwelvePass, (475m, 55m) },
                { CarType.FifteenPass, (595m, 65m) },
                { CarType.MercSprinter, (710m, 75m) }
            },
            ["PHILLY-BROOKLYN"] = new()
            {
                { CarType.Car, (250m, 40m) },
                { CarType.SUV, (250m, 40m) },
                { CarType.MiniVan, (275m, 40m) },
                { CarType.LuxurySUV, (365m, 40m) },
                { CarType.TwelvePass, (475m, 55m) },
                { CarType.FifteenPass, (595m, 65m) },
                { CarType.MercSprinter, (710m, 75m) }
            },

            // BK-PASSAIC
            ["BROOKLYN-PASSAIC"] = new()
            {
                { CarType.Car, (135m, 20m) },
                { CarType.SUV, (135m, 20m) },
                { CarType.MiniVan, (145m, 20m) },
                { CarType.LuxurySUV, (200m, 40m) },
                { CarType.TwelvePass, (285m, 45m) },
                { CarType.FifteenPass, (355m, 50m) },
                { CarType.MercSprinter, (425m, 55m) }
            },
            ["PASSAIC-BROOKLYN"] = new()
            {
                { CarType.Car, (135m, 20m) },
                { CarType.SUV, (135m, 20m) },
                { CarType.MiniVan, (145m, 20m) },
                { CarType.LuxurySUV, (200m, 40m) },
                { CarType.TwelvePass, (285m, 45m) },
                { CarType.FifteenPass, (355m, 50m) },
                { CarType.MercSprinter, (425m, 55m) }
            },

            // BK-MONSEY
            ["BROOKLYN-MONSEY"] = new()
            {
                { CarType.Car, (155m, 30m) },
                { CarType.SUV, (155m, 30m) },
                { CarType.MiniVan, (170m, 30m) },
                { CarType.LuxurySUV, (230m, 40m) },
                { CarType.TwelvePass, (330m, 50m) },
                { CarType.FifteenPass, (410m, 55m) },
                { CarType.MercSprinter, (495m, 60m) }
            },
            ["MONSEY-BROOKLYN"] = new()
            {
                { CarType.Car, (155m, 30m) },
                { CarType.SUV, (155m, 30m) },
                { CarType.MiniVan, (170m, 30m) },
                { CarType.LuxurySUV, (230m, 40m) },
                { CarType.TwelvePass, (330m, 50m) },
                { CarType.FifteenPass, (410m, 55m) },
                { CarType.MercSprinter, (495m, 60m) }
            },

            // BK-MONROE
            ["BROOKLYN-MONROE"] = new()
            {
                { CarType.Car, (170m, 30m) },
                { CarType.SUV, (170m, 30m) },
                { CarType.MiniVan, (185m, 30m) },
                { CarType.LuxurySUV, (280m, 28m) },
                { CarType.TwelvePass, (365m, 40m) },
                { CarType.FifteenPass, (455m, 45m) },
                { CarType.MercSprinter, (450m, 45m) }
            },
            ["MONROE-BROOKLYN"] = new()
            {
                { CarType.Car, (170m, 30m) },
                { CarType.SUV, (170m, 30m) },
                { CarType.MiniVan, (185m, 30m) },
                { CarType.LuxurySUV, (280m, 28m) },
                { CarType.TwelvePass, (365m, 40m) },
                { CarType.FifteenPass, (455m, 45m) },
                { CarType.MercSprinter, (450m, 45m) }
            },

            // BK-STATEN ISLAND
            ["BROOKLYN-STATEN_ISLAND"] = new()
            {
                { CarType.Car, (80m, 20m) },
                { CarType.SUV, (80m, 20m) },
                { CarType.MiniVan, (90m, 20m) },
                { CarType.LuxurySUV, (150m, 15m) },
                { CarType.TwelvePass, (195m, 20m) },
                { CarType.FifteenPass, (245m, 25m) },
                { CarType.MercSprinter, (290m, 30m) }
            },
            ["STATEN_ISLAND-BROOKLYN"] = new()
            {
                { CarType.Car, (80m, 20m) },
                { CarType.SUV, (80m, 20m) },
                { CarType.MiniVan, (90m, 20m) },
                { CarType.LuxurySUV, (150m, 15m) },
                { CarType.TwelvePass, (195m, 20m) },
                { CarType.FifteenPass, (245m, 25m) },
                { CarType.MercSprinter, (290m, 30m) }
            },

            // BK-UPSTATE 
            ["BROOKLYN-UPSTATE"] = new()
            {
                { CarType.Car, (255m, 45m) },
                { CarType.SUV, (255m, 45m) },
                { CarType.MiniVan, (275m, 45m) },
                { CarType.LuxurySUV, (400m, 40m) },
                { CarType.TwelvePass, (570m, 60m) },
                { CarType.FifteenPass, (700m, 70m) },
                { CarType.MercSprinter, (800m, 80m) }
            },
            ["UPSTATE-BROOKLYN"] = new()
            {
                { CarType.Car, (255m, 45m) },
                { CarType.SUV, (255m, 45m) },
                { CarType.MiniVan, (275m, 45m) },
                { CarType.LuxurySUV, (400m, 40m) },
                { CarType.TwelvePass, (570m, 60m) },
                { CarType.FifteenPass, (700m, 70m) },
                { CarType.MercSprinter, (800m, 80m) }
            },

            // BK-LINDEN
            ["BROOKLYN-LINDEN"] = new()
            {
                { CarType.Car, (110m, 30m) },
                { CarType.SUV, (110m, 30m) },
                { CarType.MiniVan, (120m, 30m) },
                { CarType.LuxurySUV, (175m, 25m) },
                { CarType.TwelvePass, (245m, 35m) },
                { CarType.FifteenPass, (305m, 40m) },
                { CarType.MercSprinter, (365m, 45m) }
            },
            ["LINDEN-BROOKLYN"] = new()
            {
                { CarType.Car, (110m, 30m) },
                { CarType.SUV, (110m, 30m) },
                { CarType.MiniVan, (120m, 30m) },
                { CarType.LuxurySUV, (175m, 25m) },
                { CarType.TwelvePass, (245m, 35m) },
                { CarType.FifteenPass, (305m, 40m) },
                { CarType.MercSprinter, (365m, 45m) }
            },

            // ==================== UPSTATE ROUTES ====================

            // UPSTATE-EWR
            ["UPSTATE-EWR"] = new()
            {
                { CarType.Car, (165m, 35m) },
                { CarType.SUV, (165m, 35m) },
                { CarType.MiniVan, (175m, 35m) },
                { CarType.LuxurySUV, (350m, 50m) },
                { CarType.TwelvePass, (450m, 60m) },
                { CarType.FifteenPass, (560m, 70m) },
                { CarType.MercSprinter, (550m, 65m) }
            },
            ["EWR-UPSTATE"] = new()
            {
                { CarType.Car, (165m, 35m) },
                { CarType.SUV, (165m, 35m) },
                { CarType.MiniVan, (175m, 35m) },
                { CarType.LuxurySUV, (350m, 50m) },
                { CarType.TwelvePass, (450m, 60m) },
                { CarType.FifteenPass, (560m, 70m) },
                { CarType.MercSprinter, (550m, 65m) }
            },

            // UPSTATE-JFK
            ["UPSTATE-JFK"] = new()
            {
                { CarType.Car, (255m, 45m) },
                { CarType.SUV, (255m, 45m) },
                { CarType.MiniVan, (275m, 45m) },
                { CarType.LuxurySUV, (400m, 50m) },
                { CarType.TwelvePass, (570m, 70m) },
                { CarType.FifteenPass, (710m, 80m) },
                { CarType.MercSprinter, (700m, 80m) }
            },
            ["JFK-UPSTATE"] = new()
            {
                { CarType.Car, (255m, 45m) },
                { CarType.SUV, (255m, 45m) },
                { CarType.MiniVan, (275m, 45m) },
                { CarType.LuxurySUV, (400m, 50m) },
                { CarType.TwelvePass, (570m, 70m) },
                { CarType.FifteenPass, (710m, 80m) },
                { CarType.MercSprinter, (700m, 80m) }
            },

            // UPSTATE-MONSEY
            ["UPSTATE-MONSEY"] = new()
            {
                { CarType.Car, (125m, 30m) },
                { CarType.SUV, (125m, 30m) },
                { CarType.MiniVan, (135m, 30m) },
                { CarType.LuxurySUV, (200m, 35m) },
                { CarType.TwelvePass, (325m, 45m) },
                { CarType.FifteenPass, (405m, 50m) },
                { CarType.MercSprinter, (400m, 50m) }
            },
            ["MONSEY-UPSTATE"] = new()
            {
                { CarType.Car, (125m, 30m) },
                { CarType.SUV, (125m, 30m) },
                { CarType.MiniVan, (135m, 30m) },
                { CarType.LuxurySUV, (200m, 35m) },
                { CarType.TwelvePass, (325m, 45m) },
                { CarType.FifteenPass, (405m, 50m) },
                { CarType.MercSprinter, (400m, 50m) }
            },

            // UPSTATE-MONROE
            ["UPSTATE-MONROE"] = new()
            {
                { CarType.Car, (95m, 25m) },
                { CarType.SUV, (95m, 25m) },
                { CarType.MiniVan, (110m, 25m) },
                { CarType.LuxurySUV, (190m, 35m) },
                { CarType.TwelvePass, (285m, 40m) },
                { CarType.FifteenPass, (355m, 45m) },
                { CarType.MercSprinter, (350m, 45m) }
            },
            ["MONROE-UPSTATE"] = new()
            {
                { CarType.Car, (95m, 25m) },
                { CarType.SUV, (95m, 25m) },
                { CarType.MiniVan, (110m, 25m) },
                { CarType.LuxurySUV, (190m, 35m) },
                { CarType.TwelvePass, (285m, 40m) },
                { CarType.FifteenPass, (355m, 45m) },
                { CarType.MercSprinter, (350m, 45m) }
            }
        };
        #endregion

        #region Rush Hour Routes (routes that have rush hour surcharges)
        /// <summary>
        /// Routes with rush hour surcharges (2:00 PM - 5:30 PM pickup).
        /// Key = route key, Value = surcharge amount per car type
        /// </summary>
        private static readonly Dictionary<string, Dictionary<CarType, decimal>> RushHourSurcharges = new()
        {
            // LKWD-JFK: Car/SUV/MiniVan add $25
            ["LAKEWOOD-JFK"] = new()
            {
                { CarType.Car, 25m },
                { CarType.SUV, 25m },
                { CarType.MiniVan, 25m }
            },
            ["JFK-LAKEWOOD"] = new()
            {
                { CarType.Car, 25m },
                { CarType.SUV, 25m },
                { CarType.MiniVan, 25m }
            },
            // LKWD-BK: Car/SUV/MiniVan add $15
            ["LAKEWOOD-BROOKLYN"] = new()
            {
                { CarType.Car, 15m },
                { CarType.SUV, 15m },
                { CarType.MiniVan, 15m }
            },
            ["BROOKLYN-LAKEWOOD"] = new()
            {
                { CarType.Car, 15m },
                { CarType.SUV, 15m },
                { CarType.MiniVan, 15m }
            },
            ["BROOKLYN-JFK"] = new()
            {
                { CarType.Car, 25m },
                { CarType.SUV, 25m },
                { CarType.MiniVan, 25m },
                { CarType.LuxurySUV, 25m }
            },
            ["JFK-BROOKLYN"] = new()
            {
                { CarType.Car, 25m },
                { CarType.SUV, 25m },
                { CarType.MiniVan, 25m },
                { CarType.LuxurySUV, 25m }
            },
            ["BROOKLYN-UPSTATE"] = new()
            {
                { CarType.Car, 15m },
                { CarType.SUV, 15m },
                { CarType.MiniVan, 15m }
            },
            ["UPSTATE-BROOKLYN"] = new()
            {
                { CarType.Car, 15m },
                { CarType.SUV, 15m },
                { CarType.MiniVan, 15m }
            }
        };
        #endregion

        #region Toll Estimates (for formula-based pricing)
        /// <summary>
        /// Estimated toll amounts for routes (used when no set price exists)
        /// </summary>
        private static readonly Dictionary<(string, string), decimal> TollEstimates = new()
        {
            { ("BROOKLYN", "LAKEWOOD"), 40m },
            { ("LAKEWOOD", "BROOKLYN"), 40m },
            { ("LAKEWOOD", "MONSEY"), 15m },
            { ("MONSEY", "LAKEWOOD"), 15m },
            { ("BROOKLYN", "MONSEY"), 30m },
            { ("MONSEY", "BROOKLYN"), 30m },
            { ("LAKEWOOD", "UPSTATE"), 15m },
            { ("UPSTATE", "LAKEWOOD"), 15m },
            { ("BROOKLYN", "UPSTATE"), 30m },
            { ("UPSTATE", "BROOKLYN"), 30m },
            // Default for other routes
            { ("DEFAULT", "DEFAULT"), 20m }
        };
        #endregion

        public PricingService(GoogleMapsService googleMapsService)
        {
            _googleMapsService = googleMapsService;
        }

        /// <summary>
        /// Calculate the price for a ride
        /// </summary>
        public async Task<PricingResult> CalculatePriceAsync(
            string pickup,
            string dropOff,
            List<string> stops,
            CarType carType,
            bool isRoundTrip,
            DateTime? scheduledTime)
        {
            var result = new PricingResult();

            try
            {
                // 1. Detect locations
                string originLocation = DetectLocation(pickup);
                string destinationLocation = DetectLocation(dropOff);

                result.OriginArea = originLocation;
                result.DestinationArea = destinationLocation;

                // 2. Check for set price
                var setPrice = GetSetPrice(originLocation, destinationLocation, carType);

                if (setPrice.HasValue)
                {
                    result.PricingMethod = "SET_PRICE";
                    result.BasePrice = setPrice.Value.Price;
                    result.OwnerCut = setPrice.Value.OwnerCut;

                    // Handle round trip for set prices (add return leg)
                    if (isRoundTrip)
                    {
                        var returnPrice = GetSetPrice(destinationLocation, originLocation, carType);
                        if (returnPrice.HasValue)
                        {
                            result.BasePrice += returnPrice.Value.Price;
                            result.OwnerCut += returnPrice.Value.OwnerCut;
                        }
                        else
                        {
                            // If no return set price, double the outbound
                            result.BasePrice *= 2;
                            result.OwnerCut *= 2;
                        }
                    }

                    // Add rush hour surcharge if applicable
                    if (IsRushHour(scheduledTime))
                    {
                        var surcharge = GetRushHourSurcharge(originLocation, destinationLocation, carType);
                        if (surcharge > 0)
                        {
                            result.RushHourSurcharge = surcharge;
                            if (isRoundTrip)
                            {
                                // Add surcharge for return leg too
                                var returnSurcharge = GetRushHourSurcharge(destinationLocation, originLocation, carType);
                                result.RushHourSurcharge += returnSurcharge;
                            }
                        }
                    }
                }
                else
                {
                    // 3. Use formula-based pricing
                    result.PricingMethod = "FORMULA";

                    // Get trip duration from Google Maps
                    int durationMinutes = await GetTripDurationAsync(pickup, dropOff, stops, isRoundTrip);
                    result.EstimatedDurationMinutes = durationMinutes;

                    // Check if this is a local ride (same area)
                    bool isLocalRide = originLocation == destinationLocation && originLocation != "UNKNOWN";

                    // Check if this qualifies for minimum fare: local ride OR under 1 hour (one-way)
                    bool qualifiesForMinimumFare = !isRoundTrip && (isLocalRide || (durationMinutes > 0 && durationMinutes < 60));

                    if (durationMinutes <= 0)
                    {
                        // Google Maps failed - apply minimum fare
                        result.BasePrice = 65m;
                        result.OwnerCut = 20m; // Standard owner's cut for minimum fare rides
                        result.MinimumFareApplied = true;
                        result.PricingMethod = "MINIMUM_FARE";
                        Console.WriteLine($"Pricing: Google Maps failed, applying minimum fare $65 for {originLocation} -> {destinationLocation}");
                    }
                    else if (qualifiesForMinimumFare)
                    {
                        // Local ride or under 1 hour - apply flat $65 minimum fare
                        result.BasePrice = 65m;
                        result.OwnerCut = 20m; // Standard owner's cut for minimum fare rides
                        result.MinimumFareApplied = true;
                        result.PricingMethod = "MINIMUM_FARE";
                        Console.WriteLine($"Pricing: Local/short ride ({durationMinutes} min), applying minimum fare $65");
                    }
                    else
                    {
                        // Calculate using formula: Base + (hourly × hours) + tolls + owner's cut
                        decimal hours = Math.Ceiling(durationMinutes / 60.0m);
                        decimal baseAmount = 15m;
                        decimal hourlyRate = HourlyRates[carType];
                        decimal hourlyTotal = hourlyRate * hours;

                        // Get toll estimate
                        decimal tolls = GetTollEstimate(originLocation, destinationLocation);

                        // Calculate owner's cut based on trip duration
                        decimal ownersCut = CalculateOwnersCut(durationMinutes, isRoundTrip);

                        result.BasePrice = baseAmount + hourlyTotal + tolls + ownersCut;
                        result.OwnerCut = ownersCut;
                        result.TollEstimate = tolls;
                        result.HourlyRate = hourlyRate;
                        result.TripHours = hours;
                    }
                }

                // 4. Apply minimum fare ($65 for local rides or trips < 1 hour one-way) if not already applied
                if (!result.MinimumFareApplied)
                {
                    int effectiveDuration = result.EstimatedDurationMinutes > 0
                        ? result.EstimatedDurationMinutes
                        : 30; // Assume 30 min if unknown for minimum check

                    // Check if local ride (same origin and destination area)
                    bool isLocalRide = originLocation == destinationLocation && originLocation != "UNKNOWN";

                    // Apply minimum fare if: local ride OR under 1 hour (one-way) AND calculated price is less than minimum
                    bool shouldApplyMinimum = !isRoundTrip && (isLocalRide || effectiveDuration < 60);

                    if (shouldApplyMinimum && result.BasePrice < 65)
                    {
                        result.MinimumFareApplied = true;
                        result.OriginalPrice = result.BasePrice;
                        result.BasePrice = 65m;
                        // Adjust owner's cut for minimum fare rides
                        if (result.OwnerCut == 0 || result.OwnerCut > result.BasePrice)
                        {
                            result.OwnerCut = 20m; // Standard cut for minimum fare rides
                        }
                        Console.WriteLine($"Pricing: Applied minimum fare $65 (was ${result.OriginalPrice}) - Local: {isLocalRide}, Duration: {effectiveDuration}min");
                    }
                }                // 5. Calculate final totals
                result.TotalPrice = result.BasePrice + result.RushHourSurcharge;
                result.DriversCompensation = result.TotalPrice - result.OwnerCut;

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                result.Success = false;
            }

            return result;
        }

        #region Helper Methods

        /// <summary>
        /// Detect which known location an address belongs to
        /// </summary>
        private string DetectLocation(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return "UNKNOWN";

            // Check specific locations first (more specific before general)
            // Check airports first
            if (IsJFK(address)) return "JFK";
            if (IsLGA(address)) return "LGA";
            if (IsEWR(address)) return "EWR";

            // Check specific Brooklyn neighborhoods before general Brooklyn
            if (IsWilliamsburg(address)) return "WILLIAMSBURG";
            if (IsFlatbush(address)) return "FLATBUSH";
            if (IsBoroughPark(address)) return "BP";

            // Check other specific locations
            if (IsLakewood(address)) return "LAKEWOOD";
            if (IsMonsey(address)) return "MONSEY";
            if (IsMonroe(address)) return "MONROE";
            if (IsUpstate(address)) return "UPSTATE";
            if (IsStatenIsland(address)) return "STATEN_ISLAND";
            if (IsPhilly(address)) return "PHILLY";
            if (IsPassaic(address)) return "PASSAIC";
            if (IsLinden(address)) return "LINDEN";
            if (IsManhattan(address)) return "MANHATTAN";

            // General Brooklyn last
            if (IsBrooklyn(address)) return "BROOKLYN";

            return "UNKNOWN";
        }

        /// <summary>
        /// Get set price for a route if it exists
        /// </summary>
        private (decimal Price, decimal OwnerCut)? GetSetPrice(string origin, string destination, CarType carType)
        {
            // Normalize Brooklyn neighborhoods to BROOKLYN for pricing lookup
            string normalizedOrigin = NormalizeLocation(origin);
            string normalizedDestination = NormalizeLocation(destination);

            string routeKey = $"{normalizedOrigin}-{normalizedDestination}";

            if (SetPrices.TryGetValue(routeKey, out var prices))
            {
                if (prices.TryGetValue(carType, out var price))
                {
                    return price;
                }
            }

            return null;
        }

        /// <summary>
        /// Normalize specific Brooklyn neighborhoods to general BROOKLYN for pricing
        /// </summary>
        private string NormalizeLocation(string location)
        {
            return location switch
            {
                "FLATBUSH" => "BROOKLYN",
                "BP" => "BROOKLYN",
                "WILLIAMSBURG" => "BROOKLYN",  // Unless it's a Williamsburg-specific route
                _ => location
            };
        }

        /// <summary>
        /// Check if scheduled time is during rush hour (2:00 PM - 5:30 PM)
        /// </summary>
        private bool IsRushHour(DateTime? scheduledTime)
        {
            if (!scheduledTime.HasValue)
                return false;

            var time = scheduledTime.Value.TimeOfDay;
            var rushStart = new TimeSpan(14, 0, 0);  // 2:00 PM
            var rushEnd = new TimeSpan(17, 30, 0);   // 5:30 PM

            return time >= rushStart && time <= rushEnd;
        }

        /// <summary>
        /// Get rush hour surcharge for a route
        /// </summary>
        private decimal GetRushHourSurcharge(string origin, string destination, CarType carType)
        {
            string normalizedOrigin = NormalizeLocation(origin);
            string normalizedDestination = NormalizeLocation(destination);
            string routeKey = $"{normalizedOrigin}-{normalizedDestination}";

            if (RushHourSurcharges.TryGetValue(routeKey, out var surcharges))
            {
                if (surcharges.TryGetValue(carType, out var surcharge))
                {
                    return surcharge;
                }
            }

            return 0;
        }

        /// <summary>
        /// Get trip duration using Google Maps
        /// </summary>
        private async Task<int> GetTripDurationAsync(string pickup, string dropOff, List<string> stops, bool isRoundTrip)
        {
            int totalDuration = 0;

            // Build the full route
            var waypoints = new List<string> { pickup };
            if (stops != null && stops.Count > 0)
            {
                waypoints.AddRange(stops.Where(s => !string.IsNullOrWhiteSpace(s)));
            }
            waypoints.Add(dropOff);

            // Calculate duration for each leg
            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                int legDuration = await _googleMapsService.GetTravelTimeMinutesAsync(waypoints[i], waypoints[i + 1]);
                if (legDuration > 0)
                {
                    totalDuration += legDuration;
                }
            }

            // Add return leg if round trip
            if (isRoundTrip)
            {
                int returnDuration = await _googleMapsService.GetTravelTimeMinutesAsync(dropOff, pickup);
                if (returnDuration > 0)
                {
                    totalDuration += returnDuration;
                }
            }

            return totalDuration;
        }

        /// <summary>
        /// Get toll estimate for a route
        /// </summary>
        private decimal GetTollEstimate(string origin, string destination)
        {
            string normalizedOrigin = NormalizeLocation(origin);
            string normalizedDestination = NormalizeLocation(destination);

            if (TollEstimates.TryGetValue((normalizedOrigin, normalizedDestination), out var toll))
            {
                return toll;
            }

            // Return default toll estimate
            return TollEstimates[("DEFAULT", "DEFAULT")];
        }

        /// <summary>
        /// Calculate owner's cut based on trip duration
        /// $30 for first hour, +$15 for each additional hour
        /// </summary>
        private decimal CalculateOwnersCut(int durationMinutes, bool isRoundTrip)
        {
            // For round trips, we double the duration for owner's cut calculation
            int effectiveDuration = isRoundTrip ? durationMinutes * 2 : durationMinutes;

            int hours = (int)Math.Ceiling(effectiveDuration / 60.0);
            if (hours <= 0) hours = 1;

            // $30 base + $15 for each hour after the first
            decimal ownersCut = 30m + ((hours - 1) * 15m);

            return ownersCut;
        }

        #endregion
    }

    #region Pricing Result DTO

    public class PricingResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }

        // Detected areas
        public string OriginArea { get; set; }
        public string DestinationArea { get; set; }

        // Pricing method used
        public string PricingMethod { get; set; }  // "SET_PRICE" or "FORMULA"

        // Price components
        public decimal BasePrice { get; set; }
        public decimal RushHourSurcharge { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal OwnerCut { get; set; }
        public decimal DriversCompensation { get; set; }

        // Formula-specific details
        public decimal HourlyRate { get; set; }
        public decimal TripHours { get; set; }
        public decimal TollEstimate { get; set; }
        public int EstimatedDurationMinutes { get; set; }

        // Minimum fare
        public bool MinimumFareApplied { get; set; }
        public decimal OriginalPrice { get; set; }
    }

    #endregion

    #region Calculate Price Request DTO

    public class CalculatePriceRequest
    {
        public string Pickup { get; set; }
        public string DropOff { get; set; }
        public List<string> Stops { get; set; }
        public int CarType { get; set; }
        public bool IsRoundTrip { get; set; }
        public DateTime? ScheduledTime { get; set; }
    }

    #endregion
}
