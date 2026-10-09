// See https://aka.ms/new-console-template for more information
using System.Diagnostics;
using System.Text;
using System.Text.Json;

Console.WriteLine("Hello, World!");


// 1. Get input - video URL + prompt
var videoUrl = string.Empty;
var userPrompt = string.Empty;

Console.WriteLine("Enter video URL:");
videoUrl = Console.ReadLine();

Console.WriteLine("Enter your prompt:");
userPrompt = Console.ReadLine();

// 2. Download audio from video with yt-dlp
Console.WriteLine($"Downloading audio from video: {videoUrl}");
var outputAudioPath = $"{Guid.NewGuid()}.wav";
var ytdlpArguments = $"-f \"bestaudio/best\" -x --audio-format wav --postprocessor-args \"-ar 16000 -ac 1\" -o \"{outputAudioPath}\" {videoUrl}";
await RunCommandAsync("yt-dlp", ytdlpArguments);
Console.WriteLine($"Downloaded audio into file: {outputAudioPath}");

// 3. Transribe audio using nemotron diarization
Console.WriteLine($"Transcribing audio");
var outputTransciptPath = $"{Guid.NewGuid()}.srt";
var nemospeechArguments = $"transcribe {outputAudioPath} --diarize -f text -o {outputTransciptPath}";
await RunCommandAsync("nemo-speech", nemospeechArguments);
Console.WriteLine($"Trascribed audio into file: {outputTransciptPath}");

// 4. Send transcript with user prompt to LLM
Console.WriteLine($"");
var transcript = await File.ReadAllTextAsync(outputTransciptPath);
var llmResponse = CallLlmModelAsync(userPrompt, transcript);

// 5. Print LLM output
Console.WriteLine($"LLM responded with:\n{llmResponse}");



// ----------------------

static async Task<string> RunCommandAsync(string command, string arguments)
{
    Console.WriteLine($"[YT] Running command: {command} {arguments}");

    var psi = new ProcessStartInfo
    {
        FileName = command,
        Arguments = arguments,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    using var process = Process.Start(psi)!;

    string stdout = await process.StandardOutput.ReadToEndAsync();
    string stderr = await process.StandardError.ReadToEndAsync();

    await process.WaitForExitAsync();

    if (process.ExitCode != 0)
        throw new Exception(stderr);

    return stdout;
}

static async Task CallLlmModelAsync(string userPrompt, string videoTranscript)
{
    using var client = new HttpClient();
    client.DefaultRequestHeaders.Add("Authorization", "Bearer YOUR_OPENROUTER_API_KEY");

    var prompt = $"{userPrompt}\nHere is the diarized video transcipt: {videoTranscript}";
    var body = new
    {
        model = "openai/gpt-4o-mini",
        messages = new[] { new { role = "user", content = prompt } }
    };

    var response = await client.PostAsync(
        "https://openrouter.ai/api/v1/chat/completions",
        new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
    );

    var json = await response.Content.ReadAsStringAsync();
    using var doc = JsonDocument.Parse(json);

    string reply = doc.RootElement
        .GetProperty("choices")[0]
        .GetProperty("message")
        .GetProperty("content")
        .GetString();

    Console.WriteLine(reply);
}