using Newtonsoft.Json;
using System.Text;

namespace ScrapRatWebApi.Discord
{
    public class DiscordWebhook(string webhookUrl)
    {
        private readonly string _webhookUrl = webhookUrl;
        private readonly HttpClient _httpClient = new();

        public void SendMessage(string message)
        {
            var payload = new
            {
                content = message,
                flags = 2
            };

            var json = JsonConvert.SerializeObject(payload);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

            this._httpClient.PostAsync(this._webhookUrl, httpContent);
        }
    }
}
