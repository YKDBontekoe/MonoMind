using System.Net;
using System.Text;
using System.Text.Json;

namespace Autonocraft.Core.Agent;

internal static class AgentHttpHelpers
{
    public static void SendResponse(HttpListenerResponse response, HttpStatusCode statusCode, string content, string contentType)
    {
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(content);
            response.ContentType = contentType;
            response.ContentLength64 = bytes.Length;
            response.StatusCode = (int)statusCode;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.OutputStream.Close();
        }
        catch
        {
            // Response may already be closed by the client.
        }
    }

    public static void SendJsonResponse(HttpListenerResponse response, HttpStatusCode statusCode, object payload)
    {
        SendResponse(response, statusCode, JsonSerializer.Serialize(payload), "application/json");
    }

    public static string? ExtractJsonString(string json, string key)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        string pattern = $"\"{key}\"";
        int idx = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return null;
        }

        int start = json.IndexOf('"', idx + pattern.Length);
        if (start < 0)
        {
            return null;
        }

        start++;
        int end = json.IndexOf('"', start);
        return end > start ? json.Substring(start, end - start) : null;
    }
}
