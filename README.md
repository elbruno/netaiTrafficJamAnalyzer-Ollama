# TrafficJamAnalyzer-Ollama

This project is a modification of the original [TrafficJamAnalyzer](https://aka.ms/netaitrafficjamanalyzer), adapted to use Ollama for local image analysis. It allows users to leverage powerful open-source language models running on their own hardware to analyze traffic camera images, determine traffic density, and extract relevant information like location and date.

The original `netaiTrafficJamAnalyzer` used Semantic Kernel and OpenAI. This version retains the core functionality but swaps the AI backend for Ollama.

- [Link to the original netaiTrafficJamAnalyzer project](https://aka.ms/netaitrafficjamanalyzer)
- [Video Overview of the original project](https://youtu.be/2Is_372Nvhc)

## Goal

The primary goal of this repository is to provide a self-hostable solution for traffic image analysis. By using Ollama with vision-capable models, the application can:

- Analyze images from traffic cameras.
- Determine traffic density (rated from 0 to 100).
- Extract textual information from the images, such as location names and dates.
- Offer this analysis via a simple API.

## Installation

### Prerequisites

1.  **.NET SDK:** .NET 8.0 or higher (to match the project's target framework). You can download it from [here](https://dotnet.microsoft.com/download).
2.  **Ollama:** Ollama must be installed and running. Visit the [Ollama website](https://ollama.com/) for installation instructions for your operating system.
3.  **Vision-Capable Ollama Model:** You need to have a vision-capable model pulled into your Ollama instance. Examples include:
    *   `llava` (e.g., `ollama pull llava`)
    *   `llama3-llava-next`
    *   Other multimodal models available on [Ollama Hub](https://ollama.com/library?q=&f=multimodal)
    You can pull a model using the command:
    ```bash
    ollama pull <model_name>
    ```
    For example:
    ```bash
    ollama pull llava
    ```

### Configuration

The application's AI service (`TrafficJamAnalyzer.Services.AiApiService`) requires configuration for the Ollama API endpoint and the model name. This is done in the `src/TrafficJamAnalyzer.Services.AiApiService/appsettings.json` file.

Create or update your `appsettings.json` with the following structure:

```json
{
  "ConnectionStrings": {
    "ollamaVision": "http://localhost:11434"
  },
  "OllamaVisionModel": "llava",
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

**Explanation:**

*   `ConnectionStrings:ollamaVision`: This is the base URL of your running Ollama instance. The default is usually `http://localhost:11434`.
*   `OllamaVisionModel`: Specify the name of the vision-capable model you have pulled and want to use (e.g., "llava", "llama3-llava-next"). This must match the model name in your Ollama instance.

### Running the Application

1.  **Clone the repository:**
    ```bash
    git clone <repository-url>
    cd TrafficJamAnalyzer-Ollama
    ```
2.  **Navigate to the AppHost project:**
    The solution is typically run using the .NET Aspire AppHost.
    ```bash
    cd src/TrafficJamAnalyzer.AppHost
    ```
3.  **Run the application:**
    ```bash
    dotnet run
    ```
    This will start the AiApiService and any other services defined in the AppHost. The API service will then be available at the specified port (usually found in the launchSettings.json of the AiApiService or output by .NET Aspire).

    Alternatively, you can open the solution (`.sln` file) in an IDE like Visual Studio or JetBrains Rider and run the `TrafficJamAnalyzer.AppHost` project.

## Usage

The core AI analysis functionality is exposed via an API endpoint hosted by the `TrafficJamAnalyzer.Services.AiApiService`.

### API Endpoint

-   **URL:** `GET /analyze/{identifier}`
-   **Method:** `GET`
-   **Host:** The service `TrafficJamAnalyzer.Services.AiApiService` (the exact base URL and port will depend on your .NET Aspire configuration, typically something like `http://localhost:5XXX`).

### Input

-   `{identifier}`: This is a string that the service uses to construct the full image URL. The current implementation expects identifiers that correspond to images from `http://cic.tenerife.es/e-Traffic3/data/{identifier}.jpg`.
    -   Example identifier: `camara-2701001-26`

### Output

The endpoint returns a JSON object with the analysis results.

**Example Response:**

```json
{
  "createdAt": "2024-07-30T10:30:00.123Z",
  "sourceUrl": "http://cic.tenerife.es/e-Traffic3/data/camara-2701001-26.jpg",
  "result": {
    "Title": "TF-5 P.K. 27+000 DECRECIENTE", // Extracted title from the image
    "Traffic": 65,                          // Integer from 0 to 100, representing traffic density
    "Date": "30/07/2024 12:30:05"           // Extracted date and time from the image
  }
}
```

**Fields:**

-   `createdAt`: Timestamp (UTC) of when the analysis was performed.
-   `sourceUrl`: The URL of the image that was analyzed.
-   `result`: An object containing the core analysis:
    -   `Title`: Textual description of the location, extracted from the image.
    -   `Traffic`: An integer between 0 (no traffic) and 100 (heavy traffic).
    -   `Date`: Date and time information extracted from the image.
```

## Using Other Ollama Models

This project is designed to be flexible and can be adapted to use different vision-capable models available through Ollama. The choice of model can impact performance, accuracy, and resource consumption.

### How to Change the Model

1.  **Update Configuration:** The primary way to change the model is by updating the `OllamaVisionModel` value in the `src/TrafficJamAnalyzer.Services.AiApiService/appsettings.json` file.
    ```json
    {
      // ... other settings
      "OllamaVisionModel": "your-chosen-model-name"
      // ... other settings
    }
    ```
2.  **Pull the Model:** Ensure that the chosen model is downloaded to your local Ollama instance. You can do this using the command:
    ```bash
    ollama pull <your-chosen-model-name>
    ```
    For example, to use `bakllava`:
    ```bash
    ollama pull bakllava
    ```

### Considerations When Choosing a Model

-   **Different Architectures/Sizes:** Ollama offers a variety of models like `llava` (the default for this project if not specified), `bakllava`, `moondream`, `llama3-llava-next`, and other LLaVA (Large Language and Vision Assistant) variants. These models often come in different sizes (e.g., based on 7 billion, 13 billion, or more parameters).
    -   **Larger models** (e.g., 13b+) might provide higher accuracy or more detailed and nuanced descriptions but will require more computational resources (RAM, VRAM) and may have higher latency (slower response times).
    -   **Smaller models** are generally faster, require fewer resources, and can be suitable for scenarios where speed is critical or hardware is limited, potentially at the cost of some accuracy.

-   **Specialized Models:** While the current image analysis prompt in `Program.cs` is quite general, some models might be fine-tuned for specific visual tasks. For example, if you find a model particularly good at precise text extraction from noisy images or detailed object recognition (like counting specific types of vehicles), you could switch to that model. This might also involve tailoring the prompt to leverage the model's specific strengths.

-   **Prompt Adjustments:** The effectiveness of a language model is significantly influenced by the prompt provided. The default prompt in this project (`userPrompt` in `src/TrafficJamAnalyzer.Services.AiApiService/Program.cs`) is designed for general traffic analysis. If you switch to a different model, you may find that adjusting the prompt (e.g., changing the wording, asking for information in a different format, or providing different examples if the model supports few-shot prompting) can lead to better or more consistent results.

-   **Multilingual Capabilities:** If the traffic camera images you are analyzing contain text in multiple languages, some models might offer superior multilingual understanding and transcription capabilities compared to others.

**Experimentation is encouraged!** Visit the [Ollama Hub](https://ollama.com/library?f=multimodal) to explore the range of available multimodal models. Test different models and prompts to find the combination that best suits your specific needs and hardware capabilities.
