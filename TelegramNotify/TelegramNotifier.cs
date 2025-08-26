using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TelegramNotify
{
    /// <summary>
    /// 提供發送 Telegram 通知的功能。
    /// </summary>
    public class TelegramNotifier
    {
        private readonly string _botToken;
        private readonly HttpClient _httpClient;

        /// <summary>
        /// 建構函式，允許注入 <see cref="HttpClient"/> 以利測試與重複使用。
        /// </summary>
        /// <param name="botToken">Telegram Bot 的存取 Token。</param>
        /// <param name="httpClient">可選用的 <see cref="HttpClient"/>，若為 <c>null</c> 則會建立新的實例。</param>
        public TelegramNotifier(string botToken, HttpClient? httpClient = null)
        {
            _botToken = botToken ?? throw new ArgumentNullException(nameof(botToken));
            _httpClient = httpClient ?? new HttpClient();
        }

        /// <summary>
        /// 發送訊息到指定的聊天室。
        /// </summary>
        /// <param name="chatId">聊天室 ID。</param>
        /// <param name="subject">訊息主旨。</param>
        /// <param name="body">訊息內容。</param>
        /// <param name="cancellationToken">取消操作的 <see cref="CancellationToken"/>。</param>
        public async Task SendMessageAsync(long chatId, string subject, string body, CancellationToken cancellationToken = default)
        {
            // 把主旨與內容組成 MarkdownV2 格式的字串
            var text = $"*{EscapeMarkdown(subject)}*\n\n{EscapeMarkdown(body)}";
            var url = $"https://api.telegram.org/bot{_botToken}/sendMessage";

            using var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("chat_id", chatId.ToString()),
                new KeyValuePair<string, string>("text", text),
                new KeyValuePair<string, string>("parse_mode", "MarkdownV2")
            });

            var response = await _httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                throw new HttpRequestException($"Telegram API Error: {responseBody}");
            }
        }

        private static string EscapeMarkdown(string input)
        {
            const string specials = "_*[]()~`>#+-=|{}.!";
            var builder = new StringBuilder(input.Length * 2);
            foreach (var c in input)
            {
                if (specials.IndexOf(c) >= 0)
                {
                    builder.Append('\\');
                }
                builder.Append(c);
            }
            return builder.ToString();
        }
    }
}
