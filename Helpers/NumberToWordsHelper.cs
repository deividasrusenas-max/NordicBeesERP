using System;
using System.Globalization;

namespace NordicBeesERP.Helpers;

/// <summary>
/// Shared number-to-words conversion for report documents (amount in words).
/// The Lithuanian logic mirrors PdfGeneratorService.ConvertToLithuanianWords
/// (which is private there) so it can be reused; the English logic is an
/// inline implementation — no external NuGet package is introduced.
/// </summary>
public static class NumberToWordsHelper
{
    public static string ConvertToLithuanianWords(decimal amount)
    {
        if (amount < 0 || amount > 999999)
            return amount.ToString("N2", CultureInfo.InvariantCulture) + " €";

        var eur = (int)Math.Floor(amount);
        var ct = (int)Math.Round((amount - eur) * 100);

        var eurWords = LithuanianNumberToWords(eur) + " eurų";
        var ctWords = ct > 0 ? $"{ct:D2} ct" : "";

        return ct > 0 ? $"{eurWords} {ctWords}" : eurWords;
    }

    public static string ConvertToEnglishWords(decimal amount)
    {
        if (amount < 0 || amount > 999999)
            return amount.ToString("N2", CultureInfo.InvariantCulture) + " EUR";

        var eur = (int)Math.Floor(amount);
        var ct = (int)Math.Round((amount - eur) * 100);

        var eurWords = EnglishNumberToWords(eur) + " euro";
        var ctWords = ct > 0 ? $"{ct:D2} cents" : "";

        return ct > 0 ? $"{eurWords} {ctWords}" : eurWords;
    }

    private static string LithuanianNumberToWords(int number)
    {
        if (number == 0) return "nulis";

        var units = new[] { "", "vienas", "du", "trys", "keturi", "penki", "šeši", "septyni", "aštuoni", "devyni" };
        var teens = new[] { "dešimt", "vienuolika", "dvylika", "trylika", "keturiolika", "penkiolika", "šešiolika", "septyniolika", "aštuoniolika", "devyniolika" };
        var tens = new[] { "", "", "dvidešimt", "trisdešimt", "keturiasdešimt", "penkiasdešimt", "šešiasdešimt", "septyniasdešimt", "aštuoniasdešimt", "devyniasdešimt" };
        var hundreds = new[] { "", "šimtas", "du šimtai", "trys šimtai", "keturi šimtai", "penki šimtai", "šeši šimtai", "septyni šimtai", "aštuoni šimtai", "devyni šimtai" };

        var result = "";

        var thousands = number / 1000;
        var remainder = number % 1000;

        if (thousands > 0)
        {
            if (thousands == 1)
                result += "tūkstantis";
            else if (thousands < 10)
                result += units[thousands] + " tūkstančių";
            else if (thousands < 20)
                result += teens[thousands - 10] + " tūkstančių";
            else
                result += LithuanianNumberToWords(thousands) + " tūkstančių";

            if (remainder > 0)
                result += " ";
        }

        var hundredsPart = remainder / 100;
        remainder = remainder % 100;

        if (hundredsPart > 0)
        {
            result += hundreds[hundredsPart] + " ";
        }

        if (remainder >= 10 && remainder < 20)
        {
            result += teens[remainder - 10];
        }
        else
        {
            var tensPart = remainder / 10;
            var unitsPart = remainder % 10;

            if (tensPart > 0)
                result += tens[tensPart] + " ";

            if (unitsPart > 0)
                result += units[unitsPart];
        }

        return result.Trim();
    }

    private static string EnglishNumberToWords(int number)
    {
        if (number == 0) return "zero";

        var units = new[] { "", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine" };
        var teens = new[] { "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen" };
        var tens = new[] { "", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety" };

        var result = "";

        var thousands = number / 1000;
        var remainder = number % 1000;

        if (thousands > 0)
        {
            result += EnglishNumberToWords(thousands) + " thousand";
            if (remainder > 0)
                result += " ";
        }

        var hundredsPart = remainder / 100;
        remainder = remainder % 100;

        if (hundredsPart > 0)
        {
            result += units[hundredsPart] + " hundred";
            if (remainder > 0)
                result += " ";
        }

        if (remainder >= 10 && remainder < 20)
        {
            result += teens[remainder - 10];
        }
        else
        {
            var tensPart = remainder / 10;
            var unitsPart = remainder % 10;

            if (tensPart > 0)
                result += tens[tensPart];

            if (unitsPart > 0)
                result += (tensPart > 0 ? " " : "") + units[unitsPart];
        }

        return result.Trim();
    }
}
