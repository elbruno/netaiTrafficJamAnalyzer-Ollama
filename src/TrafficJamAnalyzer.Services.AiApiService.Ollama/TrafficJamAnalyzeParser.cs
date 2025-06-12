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
        try
        {
            var result = JsonConvert.DeserializeObject<TrafficJamAnalyze>(content);
            if (result != null && !string.IsNullOrEmpty(result.Title) && !string.IsNullOrEmpty(result.Date))
            {
                return result;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse as TrafficJamAnalyze object");
        }

        // Try to parse as a JSON array with a 'data' property (supporting both object and array)
        try
        {
            var arr = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(content);
            if (arr != null && arr.Count > 0 && arr[0].ContainsKey("data"))
            {
                var dataString = arr[0]["data"];

                // Try to parse dataString as a JSON object (may be single-quoted or double-quoted)
                // Normalize single quotes to double quotes for JSON parsing
                var normalizedDataString = dataString
                    .Trim()
                    .Replace("'", "\"");

                try
                {
                    var innerObj = JsonConvert.DeserializeObject<TrafficJamAnalyze>(normalizedDataString);
                    if (innerObj != null && !string.IsNullOrEmpty(innerObj.Title) && !string.IsNullOrEmpty(innerObj.Date))
                    {
                        return innerObj;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to parse inner 'data' as TrafficJamAnalyze object");
                }

                // Fallback: Try to extract Title, Date, and Traffic from a flat string
                var match = Regex.Match(dataString, @"^(.*?)\s*[-\.]\s*(\d{2}/\d{2}/\d{4} \d{2}:\d{2})\s*[-\.]\s*(\d+)");
                if (match.Success)
                {
                    return new TrafficJamAnalyze
                    {
                        Title = match.Groups[1].Value.Trim(),
                        Date = match.Groups[2].Value.Trim(),
                        Traffic = int.TryParse(match.Groups[3].Value, out var traffic) ? traffic : 0
                    };
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse as array with 'data' property");
        }

        // Try to parse as a single object with a 'data' property
        try
        {
            var obj = JsonConvert.DeserializeObject<Dictionary<string, string>>(content);
            if (obj != null && obj.ContainsKey("data"))
            {
                var dataString = obj["data"];
                var normalizedDataString = dataString
                    .Trim()
                    .Replace("'", "\"");

                try
                {
                    var innerObj = JsonConvert.DeserializeObject<TrafficJamAnalyze>(normalizedDataString);
                    if (innerObj != null && !string.IsNullOrEmpty(innerObj.Title) && !string.IsNullOrEmpty(innerObj.Date))
                    {
                        return innerObj;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to parse inner 'data' as TrafficJamAnalyze object");
                }

                var match = Regex.Match(dataString, @"^(.*?)\s*[-\.]\s*(\d{2}/\d{2}/\d{4} \d{2}:\d{2})\s*[-\.]\s*(\d+)");
                if (match.Success)
                {
                    return new TrafficJamAnalyze
                    {
                        Title = match.Groups[1].Value.Trim(),
                        Date = match.Groups[2].Value.Trim(),
                        Traffic = int.TryParse(match.Groups[3].Value, out var traffic) ? traffic : 0
                    };
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse as object with 'data' property");
        }

        // if not parsing was successful, let's call the client to get the response again with 3 different prompts to get the title, date and traffic
        try
        {
            var titlePrompt = @"You are analyzing a CCTV traffic camera image. Your task is to extract and return ONLY the text visible in the top left corner of the image as a plain string. Do NOT return JSON, Markdown, HTML, or any explanation. Only the text itself.
Sample output: '3M-TVM-21 (Túnel 3 de Mayo)'";
            var title = await AnalyzeImage(titlePrompt, logger, client, imageChatMessage);

            var datePrompt = @"You are analyzing a CCTV traffic camera image. Your task is to extract and return ONLY the text visible in the bottom right corner of the image as a plain string. The text represents a Date. Do NOT return JSON, Markdown, HTML, or any explanation. Only the date string.
Sample output: '12/06/2025 18:47'";
            var date = await AnalyzeImage(datePrompt, logger, client, imageChatMessage);

            var trafficPrompt = @"You are analyzing a CCTV traffic camera image. Your task is to analyze the visible road area and return ONLY the estimated current traffic level as an integer from 0 (no traffic) to 100 (maximum congestion). Do NOT return JSON, Markdown, HTML, or any explanation. Only the integer value.
Sample output: '0'
Sample output: '77'";            
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
        catch (Exception)
        {

            throw;
        }



        logger.LogWarning("Content could not be parsed as valid TrafficJamAnalyze");
        return null;
    }

    public static async Task<string> AnalyzeImage(string prompt, ILogger logger, OllamaSharp.OllamaApiClient client, Microsoft.Extensions.AI.ChatMessage imageChatMessage) {

        var messages = new List<ChatMessage>
        {
            imageChatMessage,
            new ChatMessage(Microsoft.Extensions.AI.ChatRole.User, prompt)
        };
        var result = await client.GetResponseAsync<string>(messages: messages);
        var response = result.Text;


        logger.LogInformation("Response received from AnalyzeImage: {Response}", response);
        return response;
    }
}