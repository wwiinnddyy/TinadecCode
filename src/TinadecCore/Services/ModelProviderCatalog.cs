using Tinadec.Contracts.Models;

namespace Tinadec.AgentCore.Services;

public static class ModelProviderCatalog
{
    public static IReadOnlyList<ModelProviderTemplateDto> ListTemplates()
    {
        return
        [
            new("openai-compatible", "OpenAI Compatible", "api-key", "Generic Chat Completions API.", "https://api.openai.com/v1", "gpt-5.4-mini", ["chat", "streaming", "tool-calls"]),
            new("deepseek", "DeepSeek", "api-key", "DeepSeek OpenAI-compatible endpoint.", "https://api.deepseek.com/v1", "deepseek-chat", ["chat", "streaming", "reasoning", "tool-calls"]),
            new("openrouter", "OpenRouter", "api-key", "OpenRouter model gateway.", "https://openrouter.ai/api/v1", "openai/gpt-5", ["chat", "streaming", "routing", "tool-calls"]),
            new("groq", "Groq", "api-key", "Groq OpenAI-compatible endpoint.", "https://api.groq.com/openai/v1", "llama-3.3-70b-versatile", ["chat", "streaming", "fast"]),
            new("togetherai", "Together AI", "api-key", "Together OpenAI-compatible endpoint.", "https://api.together.xyz/v1", "meta-llama/Llama-3.3-70B-Instruct-Turbo", ["chat", "streaming", "tool-calls"]),
            new("fireworks", "Fireworks AI", "api-key", "Fireworks OpenAI-compatible endpoint.", "https://api.fireworks.ai/inference/v1", "accounts/fireworks/models/deepseek-v3", ["chat", "streaming"]),
            new("ollama", "Ollama", "local-server", "Local OpenAI-compatible Ollama server.", "http://localhost:11434/v1", "llama3.2", ["chat", "local", "no-api-key"]),
            new("vllm", "vLLM", "local-server", "Local or remote vLLM OpenAI-compatible server.", "http://localhost:8000/v1", "default", ["chat", "local", "no-api-key"]),
            new("sglang", "SGLang", "local-server", "Local or remote SGLang OpenAI-compatible server.", "http://localhost:30000/v1", "default", ["chat", "local", "no-api-key"]),
            new("codex-cli", "Codex CLI", "cli", "Codex CLI runtime instance.", null, "gpt-5.4", ["agent", "cli", "workspace"]),
            new("claude-cli", "Claude CLI", "cli", "Claude CLI runtime instance.", null, "claude-sonnet-4-6", ["agent", "cli", "workspace"]),
            new("cursor-acp", "Cursor ACP", "cli", "Cursor agent ACP runtime.", null, "auto", ["agent", "cli", "acp"]),
            new("opencode", "OpenCode", "cli", "OpenCode CLI or server runtime.", "http://127.0.0.1:4096", "openai/gpt-5", ["agent", "cli", "server"])
        ];
    }

    public static ModelProviderTemplateDto? FindTemplate(string driver)
    {
        return ListTemplates().FirstOrDefault(template => template.Driver.Equals(driver, StringComparison.OrdinalIgnoreCase));
    }
}
