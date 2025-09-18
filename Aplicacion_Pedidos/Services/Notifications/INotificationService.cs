using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Aplicacion_Pedidos.Services.Notifications
{
    public interface INotificationService
    {
        void AddNotification(ITempDataDictionary tempData, string message, NotificationType type);
        List<Notification> GetNotifications(ITempDataDictionary tempData);
        void Success(ITempDataDictionary tempData, string message);
        void Info(ITempDataDictionary tempData, string message);
        void Warning(ITempDataDictionary tempData, string message);
        void Error(ITempDataDictionary tempData, string message);
    }
}