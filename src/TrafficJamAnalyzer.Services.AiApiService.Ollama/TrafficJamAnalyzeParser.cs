using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using TrafficJamAnalyzer.Shared.Models;

public static class TrafficJamAnalyzeParser
{
    public static async Task<TrafficJamAnalyze?> ParseAsync(string content, ILogger logger, OllamaSharp.OllamaApiClient client, Microsoft.Extensions.AI.ChatMessage imageChatMessage)
    {
        // Try to parse as the expected JSON object
        if (TryParseTrafficJamAnalyze(content, out var result))
            return result;

        // Try to parse as a JSON array with a 'data' property
        if (TryParseDataPropertyFromArray(content, logger, out result))
            return result;

        // Try to parse as a single object with a 'data' property
        if (TryParseDataPropertyFromObject(content, logger, out result))
            return result;

        // Fallback: Use AI to extract fields
        try
        {
            var titlePrompt = "You are analyzing a CCTV traffic camera image. Your task is to extract and return ONLY the text visible in the top left corner of the image as a plain string. Do NOT return JSON, Markdown, HTML, or any explanation. Only the text itself.\nSample output: '3M-TVM-21 (Túnel 3 de Mayo)'";
            var datePrompt = "You are analyzing a CCTV traffic camera image. Your task is to extract and return ONLY the text visible in the bottom right corner of the image as a plain string. The text represents a Date. Do NOT return JSON, Markdown, HTML, or any explanation. Only the date string.\nSample output: '12/06/2025 18:47'";
            var trafficPrompt = "You are analyzing a CCTV traffic camera image. Your task is to analyze the visible road area and return ONLY the estimated current traffic level as an integer from 0 (no traffic) to 100 (maximum congestion). Do NOT return JSON, Markdown, HTML, or any explanation. Only the integer value.\nSample output: '0'\nSample output: '77'";

            var title = await AnalyzeImage(titlePrompt, logger, client, imageChatMessage);
            var date = await AnalyzeImage(datePrompt, logger, client, imageChatMessage);
            var traffic = await AnalyzeImage(trafficPrompt, logger, client, imageChatMessage);

            if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(date) && !string.IsNullOrEmpty(traffic))
            {
                return new TrafficJamAnalyze
                {
                    Title = title.Trim(),
                    Date = date.Trim(),
                    Traffic = int.TryParse(traffic.Trim(), out var trafficValue) ? trafficValue : 0
                };
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI fallback failed");
            throw;
        }

        logger.LogWarning("Content could not be parsed as valid TrafficJamAnalyze");
        return null;
    }

    private static bool TryParseTrafficJamAnalyze(string content, out TrafficJamAnalyze? result)
    {
        result = null;
        try
        {
            var obj = JsonConvert.DeserializeObject<TrafficJamAnalyze>(content);
            if (obj != null && !string.IsNullOrEmpty(obj.Title) && !string.IsNullOrEmpty(obj.Date))
            {
                result = obj;
                return true;
            }
        }
        catch { }
        return false;
    }

    private static bool TryParseDataPropertyFromArray(string content, ILogger logger, out TrafficJamAnalyze? result)
    {
        result = null;
        try
        {
            var arr = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(content);
            if (arr != null && arr.Count > 0 && arr[0].ContainsKey("data"))
            {
                return TryParseDataString(arr[0]["data"], logger, out result);
            }
        }
        catch (Exception ex)
        {
            //logger.LogWarning(ex, "Failed to parse as array with 'data' property");
        }
        return false;
    }

    private static bool TryParseDataPropertyFromObject(string content, ILogger logger, out TrafficJamAnalyze? result)
    {
        result = null;
        try
        {
            var obj = JsonConvert.DeserializeObject<Dictionary<string, string>>(content);
            if (obj != null && obj.ContainsKey("data"))
            {
                return TryParseDataString(obj["data"], logger, out result);
            }
        }
        catch (Exception ex)
        {
            //logger.LogWarning(ex, "Failed to parse as object with 'data' property");
        }
        return false;
    }

    private static bool TryParseDataString(string dataString, ILogger logger, out TrafficJamAnalyze? result)
    {
        result = null;
        var normalizedDataString = dataString.Trim().Replace("'", "\"");
        try
        {
            var innerObj = JsonConvert.DeserializeObject<TrafficJamAnalyze>(normalizedDataString);
            if (innerObj != null && !string.IsNullOrEmpty(innerObj.Title) && !string.IsNullOrEmpty(innerObj.Date))
            {
                result = innerObj;
                return true;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse inner 'data' as TrafficJamAnalyze object");
        }
        // Fallback: Try regex
        var match = Regex.Match(dataString, @"^(.*?)\s*[-\.]\s*(\d{2}/\d{2}/\d{4} \d{2}:\d{2})\s*[-\.]\s*(\d+)");
        if (match.Success)
        {
            result = new TrafficJamAnalyze
            {
                Title = match.Groups[1].Value.Trim(),
                Date = match.Groups[2].Value.Trim(),
                Traffic = int.TryParse(match.Groups[3].Value, out var traffic) ? traffic : 0
            };
            return true;
        }
        return false;
    }

    public static async Task<string> AnalyzeImage(string prompt, ILogger logger, OllamaSharp.OllamaApiClient client, Microsoft.Extensions.AI.ChatMessage imageChatMessage)
    {
        var messages = new List<ChatMessage>
        {
            imageChatMessage,
            new ChatMessage(Microsoft.Extensions.AI.ChatRole.User, prompt)
        };
        var result = await client.GetResponseAsync<string>(messages: messages);
        var response = result.Text;
        logger.LogInformation("Response received from AnalyzeImage: {Response}", response);

        // if the information is in this format '{ "data": "3M-TVM-21 (Túnel 3 de Mayo)" }', remove the { "data": " and the end " } to return only the value
        if (response.StartsWith("{ \"data\": \"") && response.EndsWith("\" }"))
        {
            response = response[11..^3]; // Remove '{ "data": "' and the ending '" }'
        }

        return response;
    }
}