using Newtonsoft.Json;

var ibReportsPath = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.csv");

var lines = ibReportsPath.SelectMany(x => File.ReadAllLines(x));

var lines2024 = File.ReadAllLines(ibReportsPath.Where(x => x.Contains("2024")).Single());
var openPositionsLines = lines2024.Where(x => x.StartsWith("Open Positions,Data,Summary"));


var buyLines = lines.Where(x => x.StartsWith("Trades,Data,Order,Stocks") && (x.Contains("O;") || x.EndsWith(",O")));
var soldLines = lines.Where(x => x.StartsWith("Trades,Data,Order,Stocks") && !x.Contains("O;") && !x.EndsWith(",O"));
var lastSoldLines = soldLines.Where(x => x.Contains("2024-10-31"));

var eurByuLines = lines.Where(x => x.Contains("Trades,Data,Order,Forex,USD,EUR.USD,\"2024-10-31"));



foreach (var line in soldLines)
{
    //Console.WriteLine(line);
}

foreach (var line in lastSoldLines)
{
    //Console.WriteLine(line);
}

foreach (var line in buyLines.OrderBy(x => x))
{
    //Console.WriteLine(line);
}

foreach (var line in eurByuLines)
{
    //Console.WriteLine(line);
}




var httpClient = new HttpClient();

var openPositions = new Dictionary<string, decimal> { };
var sellCount = new Dictionary<string, decimal> { };
var canUseLdvDic = new Dictionary<string, bool> { };
decimal totalSell = 0;

var incomeDateString2 = "2024-10-31";
var incomeDateResponse2 = await httpClient.GetStringAsync($"https://lkfl2.nalog.ru/taps/api/v1/dictionary/currency-rates?code={840}&date={incomeDateString2}");
var incomeDateCurrencyRate2 = JsonConvert.DeserializeObject<CurrencyRate>(incomeDateResponse2);
var sellDayRate = (decimal)incomeDateCurrencyRate2.Rate;

Console.WriteLine(sellDayRate);


var incomeDateResponseEur = await httpClient.GetStringAsync($"https://lkfl2.nalog.ru/taps/api/v1/dictionary/currency-rates?code={978}&date={incomeDateString2}");
var incomeDateCurrencyRateEur = JsonConvert.DeserializeObject<CurrencyRate>(incomeDateResponseEur);
var buyEurRate = (decimal)incomeDateCurrencyRateEur.Rate;
Console.WriteLine(buyEurRate);

var totalBuyEur = 0.0M;
var totalBuyRub = 0.0M;
foreach (var line in eurByuLines)
{
    //break;
    var lineItems = line.Split(',');

    var currency = lineItems[4];
    var dateLine = lineItems[6].Replace("\"", "");
    var date = DateOnly.Parse(dateLine);

    var eurCount = decimal.Parse(lineItems[8]);
    //var sellValueUsd = decimal.Parse(lineItems[11]);
    var commission = decimal.Parse(lineItems[12]);
    var ticker = lineItems[5];


    var buyValueRub = buyEurRate * (eurCount - commission);
    var profitUsd = eurCount + commission;
    //var count = decimal.Parse(lineItems[8]);

    totalBuyEur += eurCount;
    totalBuyRub += buyValueRub;

    Console.WriteLine($"{ticker}\t{eurCount}\t{buyValueRub}");
}

var eurInRussia = totalBuyEur - 155 - 10;

Console.WriteLine(totalBuyEur);
Console.WriteLine(eurInRussia);
Console.WriteLine(totalBuyRub);


var totalSellEurRub = eurInRussia * 103;
Console.WriteLine(totalSellEurRub);

var loss = totalBuyRub - totalSellEurRub;
var taxLoss = loss * 0.15M;

Console.WriteLine(loss);
Console.WriteLine(taxLoss);


foreach (var line in lastSoldLines)
{
    //break;
    var lineItems = line.Split(',');

    var currency = lineItems[4];
    var dateLine = lineItems[6].Replace("\"", "");
    var date = DateOnly.Parse(dateLine);

    var buyValueUsd = decimal.Parse(lineItems[13]);
    var sellValueUsd = decimal.Parse(lineItems[11]);
    var commission = decimal.Parse(lineItems[12]);
    var ticker = lineItems[5];

    if (currency != "USD")
        throw new Exception($"Unknown currency '{currency}'");

    var sellValueRub = sellDayRate * sellValueUsd;
    var profitUsd = sellValueUsd + commission;
    var count = decimal.Parse(lineItems[8]);
    openPositions.Add(ticker, profitUsd);
    sellCount.Add(ticker, count);

    totalSell += -buyValueUsd;

    var ldvResponse = await httpClient.GetStringAsync($"https://spbexchange.ru/api/listing/v1/instruments/eng/list?page=0&size=10&sortBy=instrumentKind&sortByDirection=desc&searchText={ticker}");
    var canUseLdv = !ldvResponse.Contains("\"content\":[]") && ldvResponse.Contains($"\"{ticker}\"");

    canUseLdvDic.Add(ticker, canUseLdv);

    //Console.WriteLine(ticker + '\t' + -buyValueUsd);
}


Dictionary<string, decimal> buyRub = new Dictionary<string, decimal>();
Dictionary<string, decimal> buyUsd = new Dictionary<string, decimal>();


var prevTicker = "AZN";
Console.WriteLine("Тикер\tОперация\tДата\tСтоимость в долларах\tКурс ЦБ\tСтоимость в рублях\tКоличество");
foreach (var taxLine in buyLines.OrderBy(x => x))
{
    var lineItems = taxLine.Split(',');


    var currency = lineItems[4];
    var dateLine = lineItems[6].Replace("\"", "");
    var date = DateOnly.Parse(dateLine);

    var buyValueWithCommissionUsd = decimal.Parse(lineItems[13]);
    var ticker = lineItems[5];

    if (currency != "USD")
        throw new Exception($"Unknown currency '{currency}'");

    var dateFormat = "yyyy-MM-dd";
    var incomeDate = date;
    var incomeDateString1 = incomeDate.ToString(dateFormat);

    var incomeDateResponse = await httpClient.GetStringAsync($"https://lkfl2.nalog.ru/taps/api/v1/dictionary/currency-rates?code={840}&date={incomeDateString1}");
    var incomeDateCurrencyRate = JsonConvert.DeserializeObject<CurrencyRate>(incomeDateResponse);
    var incomeDateRate = (decimal)incomeDateCurrencyRate.Rate;

    var byuValueWithCommissionRub = incomeDateRate * buyValueWithCommissionUsd;

    var count = decimal.Parse(lineItems[8]);
    var commission = decimal.Parse(lineItems[12]);



    if (prevTicker != ticker && openPositions.ContainsKey(prevTicker))
    {
        var sellValueUsd = openPositions[prevTicker];

        var sellValueRub = sellValueUsd * sellDayRate;
        var sellCount1 = sellCount[prevTicker];

        Console.WriteLine($"{prevTicker}\tПРОДАЖА\t{incomeDateString2}\t{sellValueUsd}\t{sellDayRate}\t{sellValueRub}\t{-sellCount1}");
        var byuRub = buyRub[prevTicker];
        var byuUsd = buyUsd[prevTicker];
        var profit = sellValueRub - byuRub;
        var tax15 = profit * 0.15M;
        var tax13 = profit * 0.13M;
        //Console.WriteLine($"{byuUsd}\t{sellValueUsd}\t{sellValueUsd - byuUsd}");
        //Console.WriteLine($"{byuRub}\t{sellValueRub}\t{profit}t{tax13}\t{tax15}");
        Console.WriteLine();
        Console.WriteLine($"Налогооблагаемая база\t{profit}\tНалог 13%\t{tax13}\tНалог 15%\t{tax15}");
        Console.WriteLine();
        Console.WriteLine();
    }
    if (openPositions.ContainsKey(ticker))
    {
        Console.WriteLine($"{ticker}\tПОКУПКА\t{incomeDateString1}\t{buyValueWithCommissionUsd}\t{incomeDateRate}\t{byuValueWithCommissionRub}\t{count}");
    }

    prevTicker = ticker;

    var canUseLdv = canUseLdvDic.ContainsKey(ticker) && canUseLdvDic[ticker];

    Console.WriteLine(canUseLdv);
    // лдв
    if (incomeDate >= new DateOnly(2021, 10, 31) || !canUseLdv)
    {
        if (!buyRub.ContainsKey(ticker))
        {
            buyRub.Add(ticker, byuValueWithCommissionRub);
            buyUsd.Add(ticker, buyValueWithCommissionUsd);
        }
        else
        {
            buyRub[ticker] += byuValueWithCommissionRub;
            buyUsd[ticker] += buyValueWithCommissionUsd;
        }
    }
    else if (openPositions.ContainsKey(ticker) && canUseLdv)
    {
        var sellValue = openPositions[ticker];

        var sellCount1 = -sellCount[ticker];
        var price = sellValue / sellCount1;

        var newSellValueUsd = count * price;
        var newSellValueRub = newSellValueUsd * sellDayRate;

        if (!buyRub.ContainsKey(ticker))
        {
            buyRub.Add(ticker, newSellValueRub);
            buyUsd.Add(ticker, newSellValueUsd);
        }
        else
        {
            buyRub[ticker] += newSellValueRub;
            buyUsd[ticker] += newSellValueUsd;
        }
    }

    if (ticker == "XOM")
    {
        var sellValueUsd = openPositions[prevTicker];

        var sellValueRub = sellValueUsd * sellDayRate;
        var sellCount1 = sellCount[prevTicker];

        Console.WriteLine($"{prevTicker}\tПРОДАЖА\t{incomeDateString2}\t{sellValueUsd}\t{sellDayRate}\t{sellValueRub}\t{-sellCount1}");
        var byuRub = buyRub[prevTicker];
        var byuUsd = buyUsd[prevTicker];
        var profit = sellValueRub - byuRub;
        var tax15 = profit * 0.15M;
        var tax13 = profit * 0.13M;
        //Console.WriteLine($"{byuUsd}\t{sellValueUsd}\t{sellValueUsd - byuUsd}");
        //Console.WriteLine($"{byuRub}\t{sellValueRub}\t{profit}t{tax13}\t{tax15}");
        Console.WriteLine();
        Console.WriteLine($"Налогооблагаемая база\t{profit}\tНалог 13%\t{tax13}\tНалог 15%\t{tax15}");
        Console.WriteLine();
        Console.WriteLine();
    }
}

Console.WriteLine();




foreach (var openPositionLine in openPositionsLines)
{
    var lineItems = openPositionLine.Split(',');
    var ticker = lineItems[5];
    var curPrice = decimal.Parse(lineItems[11]);
    openPositions.Add(ticker, curPrice);
}


var taxTotal13 = 0M;
var taxTotal15 = 0M;


var curPriceUsdTotal = 0M;
var curPriceRubTotal = 0M;
var usdProfitTotal = 0M;
var rubProfitTotal = 0M;



foreach (var d in buyRub.OrderByDescending(x => x.Value))
{
    if (!openPositions.ContainsKey(d.Key))
        continue;

    var paydUsd = buyUsd[d.Key];

    var currentPrice = openPositions.ContainsKey(d.Key) ? openPositions[d.Key] : 0M;
    curPriceUsdTotal += currentPrice;

    var curPriceRub = currentPrice * sellDayRate;
    curPriceRubTotal += curPriceRub;

    usdProfitTotal += currentPrice - paydUsd;

    var profit = curPriceRub - d.Value;


    rubProfitTotal += profit;

    var tax13 = profit * 0.13M;
    var tax15 = profit * 0.15M;
    taxTotal13 += tax13;
    taxTotal15 += tax15;
    Console.WriteLine($"{d.Key}\t{paydUsd:0}\t{d.Value:0}\t{currentPrice:0}\t{curPriceRub:0}\t{profit:0}\t{tax13:0}\t{tax15:0}");
}

Console.WriteLine(rubProfitTotal);
Console.WriteLine(taxTotal13);
Console.WriteLine(taxTotal15);
Console.WriteLine(curPriceRubTotal / 1000000);
Console.WriteLine((curPriceRubTotal - taxTotal13) / 1000000);
Console.WriteLine(curPriceUsdTotal);
Console.WriteLine(usdProfitTotal);


Console.WriteLine("ИТОГО");
Console.WriteLine($"Налогооблагаемая база\t{rubProfitTotal}\tНалог 13%\t{taxTotal13}\tНалог 15%\t{taxTotal15}");

public partial class CurrencyRate
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("count")]
    public long Count { get; set; }

    [JsonProperty("rate")]
    public double Rate { get; set; }
}
