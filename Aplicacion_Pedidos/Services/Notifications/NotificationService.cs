using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System.Text.Json;

namespace Aplicacion_Pedidos.Services.Notifications
{
    public class NotificationService : INotificationService
    {
        private const string TempDataKey = "Notifications";

        public List<Notification> GetNotifications(ITempDataDictionary tempData)
        {
            if (!tempData.ContainsKey(TempDataKey))
                return new List<Notification>();

            var data = tempData[TempDataKey]?.ToString();
            if (string.IsNullOrEmpty(data))
                return new List<Notification>();

            return JsonSerializer.Deserialize<List<Notification>>(data) ?? new List<Notification>();
        }

        public void AddNotification(ITempDataDictionary tempData, string message, NotificationType type)
        {
            var notifications = GetNotifications(tempData);
            notifications.Add(new Notification 
            { 
                Message = message, 
                Type = type 
            });
            
            tempData[TempDataKey] = JsonSerializer.Serialize(notifications);
        }

        public void Success(ITempDataDictionary tempData, string message)
            => AddNotification(tempData, message, NotificationType.Success);

        public void Info(ITempDataDictionary tempData, string message)
            => AddNotification(tempData, message, NotificationType.Info);

        public void Warning(ITempDataDictionary tempData, string message)
            => AddNotification(tempData, message, NotificationType.Warning);

        public void Error(ITempDataDictionary tempData, string message)
            => AddNotification(tempData, message, NotificationType.Error);
    }
}