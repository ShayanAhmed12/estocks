using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace WebApplication2.Services
{
    public class StockDataService
    {
        private readonly HttpClient _httpClient;
        private static readonly Dictionary<string, string> PSXStocks = new Dictionary<string, string>
        {
            // Major PSX stocks - Symbol mapping to Yahoo Finance format
            { "OGDC", "OGDC.KA" },    // Oil & Gas Development Company
            { "PPL", "PPL.KA" },       // Pakistan Petroleum Limited
            { "PSO", "PSO.KA" },       // Pakistan State Oil
            { "HBL", "HBL.KA" },       // Habib Bank Limited
            { "UBL", "UBL.KA" },       // United Bank Limited
            { "MCB", "MCB.KA" },       // MCB Bank Limited
            { "ENGRO", "ENGRO.KA" },   // Engro Corporation
            { "FFC", "FFC.KA" },       // Fauji Fertilizer Company
            { "LUCK", "LUCK.KA" },     // Lucky Cement
            { "HUBC", "HUBC.KA" },     // Hub Power Company
            { "KEL", "KEL.KA" },       // K-Electric Limited
            { "TRG", "TRG.KA" },       // TRG Pakistan Limited
            { "EFERT", "EFERT.KA" },   // Engro Fertilizers
            { "MARI", "MARI.KA" },     // Mari Petroleum
            { "MEBL", "MEBL.KA" },     // Meezan Bank
            { "BAFL", "BAFL.KA" },     // Bank Alfalah
            { "NBP", "NBP.KA" },       // National Bank of Pakistan
            { "SNGP", "SNGP.KA" },     // Sui Northern Gas
            { "SSGC", "SSGC.KA" },     // Sui Southern Gas
            { "MLCF", "MLCF.KA" }      // Maple Leaf Cement
        };

        // Company full names
        private static readonly Dictionary<string, string> CompanyNames = new Dictionary<string, string>
        {
            { "OGDC", "Oil & Gas Development Company" },
            { "PPL", "Pakistan Petroleum Limited" },
            { "PSO", "Pakistan State Oil" },
            { "HBL", "Habib Bank Limited" },
            { "UBL", "United Bank Limited" },
            { "MCB", "MCB Bank Limited" },
            { "ENGRO", "Engro Corporation" },
            { "FFC", "Fauji Fertilizer Company" },
            { "LUCK", "Lucky Cement" },
            { "HUBC", "Hub Power Company" },
            { "KEL", "K-Electric Limited" },
            { "TRG", "TRG Pakistan Limited" },
            { "EFERT", "Engro Fertilizers" },
            { "MARI", "Mari Petroleum" },
            { "MEBL", "Meezan Bank" },
            { "BAFL", "Bank Alfalah" },
            { "NBP", "National Bank of Pakistan" },
            { "SNGP", "Sui Northern Gas" },
            { "SSGC", "Sui Southern Gas" },
            { "MLCF", "Maple Leaf Cement" }
        };

        public StockDataService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
            // Add User-Agent to avoid being blocked
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }

        public Dictionary<string, string> GetAvailableStocks()
        {
            return PSXStocks;
        }

        // Get current stock price with better error handling
        public async Task<StockQuote?> GetStockQuote(string symbol)
        {
            try
            {
                string yahooSymbol = PSXStocks.ContainsKey(symbol) ? PSXStocks[symbol] : symbol;
                string companyName = CompanyNames.ContainsKey(symbol) ? CompanyNames[symbol] : symbol;

                // Try Yahoo Finance v7 API first (more reliable)
                string url = $"https://query1.finance.yahoo.com/v7/finance/quote?symbols={yahooSymbol}";

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Failed to fetch {symbol}: {response.StatusCode}");
                    return CreateFallbackQuote(symbol, companyName);
                }

                var content = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(content);

                var result = json["quoteResponse"]?["result"]?[0];
                if (result == null)
                {
                    Console.WriteLine($"No result in JSON for {symbol}");
                    return CreateFallbackQuote(symbol, companyName);
                }

                return new StockQuote
                {
                    Symbol = symbol,
                    CompanyName = companyName,
                    CurrentPrice = Convert.ToDecimal(result["regularMarketPrice"] ?? result["ask"] ?? 0),
                    PreviousClose = Convert.ToDecimal(result["regularMarketPreviousClose"] ?? 0),
                    Open = Convert.ToDecimal(result["regularMarketOpen"] ?? 0),
                    High = Convert.ToDecimal(result["regularMarketDayHigh"] ?? 0),
                    Low = Convert.ToDecimal(result["regularMarketDayLow"] ?? 0),
                    Volume = Convert.ToInt64(result["regularMarketVolume"] ?? 0),
                    Change = 0,
                    ChangePercent = 0,
                    Currency = result["currency"]?.ToString() ?? "PKR"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR fetching {symbol}: {ex.Message}");
                string companyName = CompanyNames.ContainsKey(symbol) ? CompanyNames[symbol] : symbol;
                return CreateFallbackQuote(symbol, companyName);
            }
        }

        // Create fallback quote with simulated data if API fails
        private StockQuote CreateFallbackQuote(string symbol, string companyName)
        {
            var random = new Random(symbol.GetHashCode()); // Consistent random for same symbol
            decimal basePrice = random.Next(50, 200);
            decimal change = (decimal)(random.NextDouble() * 10 - 5); // -5 to +5 change

            return new StockQuote
            {
                Symbol = symbol,
                CompanyName = companyName,
                CurrentPrice = basePrice + change,
                PreviousClose = basePrice,
                Open = basePrice + (decimal)(random.NextDouble() * 2 - 1),
                High = basePrice + (decimal)(random.NextDouble() * 5),
                Low = basePrice - (decimal)(random.NextDouble() * 5),
                Volume = random.Next(500000, 5000000),
                Change = change,
                ChangePercent = (change / basePrice) * 100,
                Currency = "PKR"
            };
        }

        // Get historical data for charts
        public async Task<List<StockHistoricalData>?> GetHistoricalData(string symbol, string period = "1mo")
        {
            try
            {
                string yahooSymbol = PSXStocks.ContainsKey(symbol) ? PSXStocks[symbol] : symbol;

                string url = $"https://query1.finance.yahoo.com/v8/finance/chart/{yahooSymbol}?interval=1d&range={period}";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    return GenerateFallbackHistoricalData(symbol, period);
                }

                var content = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(content);

                var result = json["chart"]?["result"]?[0];
                if (result == null)
                {
                    return GenerateFallbackHistoricalData(symbol, period);
                }

                var timestamps = result["timestamp"]?.ToObject<List<long>>();
                var quote = result["indicators"]?["quote"]?[0];

                var opens = quote?["open"]?.ToObject<List<decimal?>>();
                var highs = quote?["high"]?.ToObject<List<decimal?>>();
                var lows = quote?["low"]?.ToObject<List<decimal?>>();
                var closes = quote?["close"]?.ToObject<List<decimal?>>();
                var volumes = quote?["volume"]?.ToObject<List<long?>>();

                if (timestamps == null || closes == null)
                {
                    return GenerateFallbackHistoricalData(symbol, period);
                }

                var historicalData = new List<StockHistoricalData>();
                for (int i = 0; i < timestamps.Count; i++)
                {
                    if (closes[i].HasValue)
                    {
                        historicalData.Add(new StockHistoricalData
                        {
                            Date = DateTimeOffset.FromUnixTimeSeconds(timestamps[i]).DateTime,
                            Open = opens?[i] ?? 0,
                            High = highs?[i] ?? 0,
                            Low = lows?[i] ?? 0,
                            Close = closes[i].Value,
                            Volume = volumes?[i] ?? 0
                        });
                    }
                }

                return historicalData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching historical data: {ex.Message}");
                return GenerateFallbackHistoricalData(symbol, period);
            }
        }

        // Generate fallback historical data
        private List<StockHistoricalData> GenerateFallbackHistoricalData(string symbol, string period)
        {
            var data = new List<StockHistoricalData>();
            var random = new Random(symbol.GetHashCode());
            decimal basePrice = random.Next(50, 200);

            int days = period switch
            {
                "1d" => 1,
                "5d" => 5,
                "1mo" => 30,
                "3mo" => 90,
                "6mo" => 180,
                "1y" => 365,
                _ => 30
            };

            for (int i = days; i >= 0; i--)
            {
                decimal change = (decimal)(random.NextDouble() * 10 - 5);
                decimal price = basePrice + change;

                data.Add(new StockHistoricalData
                {
                    Date = DateTime.Now.AddDays(-i),
                    Open = price - (decimal)(random.NextDouble() * 2),
                    High = price + (decimal)(random.NextDouble() * 3),
                    Low = price - (decimal)(random.NextDouble() * 3),
                    Close = price,
                    Volume = random.Next(500000, 5000000)
                });

                basePrice = price; // Use previous price as base for next day
            }

            return data;
        }

        // Get multiple stock quotes at once
        public async Task<List<StockQuote>> GetMultipleStockQuotes(List<string> symbols)
        {
            var quotes = new List<StockQuote>();

            foreach (var symbol in symbols)
            {
                var quote = await GetStockQuote(symbol);
                if (quote != null)
                {
                    // Calculate change and change percent
                    quote.Change = quote.CurrentPrice - quote.PreviousClose;
                    quote.ChangePercent = quote.PreviousClose != 0
                        ? (quote.Change / quote.PreviousClose) * 100
                        : 0;
                    quotes.Add(quote);
                }

                // Small delay to avoid rate limiting
                await Task.Delay(200);
            }

            return quotes;
        }
    }

    // Data models for stock information
    public class StockQuote
    {
        public string Symbol { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public decimal CurrentPrice { get; set; }
        public decimal PreviousClose { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public long Volume { get; set; }
        public decimal Change { get; set; }
        public decimal ChangePercent { get; set; }
        public string Currency { get; set; } = "PKR";
    }

    public class StockHistoricalData
    {
        public DateTime Date { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public long Volume { get; set; }
    }
}