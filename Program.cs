using Newtonsoft.Json;

var ibReportsPath = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.csv");

var lines = ibReportsPath.SelectMany(x => File.ReadAllLines(x));

var lines2024 = File.ReadAllLines(ibReportsPath.Where(x => x.Contains("2024")).Single());
var openPositionsLines = lines2024.Where(x => x.StartsWith("Open Positions,Data,Summary"));


var buyLines = lines.Where(x => x.StartsWith("Trades,Data,Order,Stocks") && (x.Contains("O;") || x.EndsWith(",O")));
var soldLines = lines.Where(x => x.StartsWith("Trades,Data,Order,Stocks") && !x.Contains("O;") && !x.EndsWith(",O"));
foreach (var line in soldLines)
{
    Console.WriteLine(line);
}

foreach (var line in buyLines.OrderBy(x => x))
{
    //Console.WriteLine(line);
}


var httpClient = new HttpClient();


Dictionary<string, decimal> buyRub = new Dictionary<string, decimal>();
Dictionary<string, decimal> buyUsd = new Dictionary<string, decimal>();

foreach (var taxLine in buyLines)
{
    var lineItems = taxLine.Split(',');


    var currency = lineItems[4];
    var dateLine = lineItems[6].Replace("\"", "");
    var date = DateOnly.Parse(dateLine);

    var buyValueUsd = decimal.Parse(lineItems[13]);
    var ticker = lineItems[5];

    if (currency != "USD")
        throw new Exception($"Unknown currency '{currency}'");

    var dateFormat = "yyyy-MM-dd";
    var incomeDate = date;
    var incomeDateString1 = incomeDate.ToString(dateFormat);

    var incomeDateResponse = await httpClient.GetStringAsync($"https://lkfl2.nalog.ru/taps/api/v1/dictionary/currency-rates?code={840}&date={incomeDateString1}");
    var incomeDateCurrencyRate = JsonConvert.DeserializeObject<CurrencyRate>(incomeDateResponse);

    var byuValueRub = (decimal)incomeDateCurrencyRate.Rate * buyValueUsd;


    if (!buyRub.ContainsKey(ticker))
    {
        buyRub.Add(ticker, byuValueRub);
        buyUsd.Add(ticker, buyValueUsd);
    }
    else
    {
        buyRub[ticker] += byuValueRub;
        buyUsd[ticker] += buyValueUsd;
    }
}

var openPositions = new Dictionary<string, decimal> { };

foreach (var openPositionLine in openPositionsLines)
{
    var lineItems = openPositionLine.Split(',');
    var ticker = lineItems[5];
    var curPrice = decimal.Parse(lineItems[11]);
    openPositions.Add(ticker, curPrice);
}


var taxTotal = 0M;
var taxTotal2 = 0M;
foreach (var d in buyRub.OrderByDescending(x => x.Value))
{
    if (!openPositions.ContainsKey(d.Key))
        continue;

    var paydUsd = buyUsd[d.Key];

    var currentPrice = openPositions.ContainsKey(d.Key) ? openPositions[d.Key] : 0M;
    var curPriceRub = currentPrice * 90;
    var profit = curPriceRub - d.Value;
    var tax = profit * 0.15M;
    taxTotal += tax;
    taxTotal2 += profit * 0.13M;
    Console.WriteLine($"{d.Key}\t{paydUsd:0}\t{d.Value:0}\t{currentPrice:0}\t{curPriceRub:0}\t{profit:0}\t{tax:0}");
}

Console.WriteLine(taxTotal);
Console.WriteLine(taxTotal2);

public partial class CurrencyRate
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("count")]
    public long Count { get; set; }

    [JsonProperty("rate")]
    public double Rate { get; set; }
}
