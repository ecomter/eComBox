using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using eComBox.Models;
using Newtonsoft.Json;
using Windows.Storage;

namespace eComBox.Services
{
    public static class CountdownStorageService
    {
        private static readonly string CardFilePath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "data.json");
        private static readonly string DateHistoryPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "date_history.json");
        private static readonly SemaphoreSlim FileSemaphore = new SemaphoreSlim(1, 1);

        public static async Task SaveCardsAsync(List<CountdownCardModel> cards)
        {
            var safeCards = cards ?? new List<CountdownCardModel>();
            var settings = ApplicationData.Current.LocalSettings;

            foreach (var card in safeCards)
            {
                if (settings.Values.TryGetValue($"Card_{card.Title}_Notification", out object value) && value is bool enabled)
                {
                    card.EnableDateNotification = enabled;
                }
            }

            await FileSemaphore.WaitAsync();
            try
            {
                var json = JsonConvert.SerializeObject(safeCards);
                var tempFilePath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "temp_data.json");
                await File.WriteAllTextAsync(tempFilePath, json);
                File.Copy(tempFilePath, CardFilePath, true);
                File.Delete(tempFilePath);
            }
            finally
            {
                FileSemaphore.Release();
            }
        }

        public static async Task<List<CountdownCardModel>> LoadCardsAsync()
        {
            if (!File.Exists(CardFilePath))
            {
                return new List<CountdownCardModel>();
            }

            await FileSemaphore.WaitAsync();
            try
            {
                var json = await File.ReadAllTextAsync(CardFilePath);
                return JsonConvert.DeserializeObject<List<CountdownCardModel>>(json) ?? new List<CountdownCardModel>();
            }
            finally
            {
                FileSemaphore.Release();
            }
        }

        public static async Task SaveDateHistoryAsync(List<UserDateSelection> history)
        {
            var json = JsonConvert.SerializeObject(history ?? new List<UserDateSelection>());
            await File.WriteAllTextAsync(DateHistoryPath, json);
        }

        public static async Task<List<UserDateSelection>> LoadDateHistoryAsync()
        {
            if (!File.Exists(DateHistoryPath))
            {
                return new List<UserDateSelection>();
            }

            var json = await File.ReadAllTextAsync(DateHistoryPath);
            return JsonConvert.DeserializeObject<List<UserDateSelection>>(json) ?? new List<UserDateSelection>();
        }
    }
}
