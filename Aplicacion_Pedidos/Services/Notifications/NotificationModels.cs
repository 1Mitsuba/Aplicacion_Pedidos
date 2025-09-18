namespace Aplicacion_Pedidos.Services.Notifications
{
    public enum NotificationType
    {
        Success,
        Info,
        Warning,
        Error
    }

    public class Notification
    {
        public string Message { get; set; } = string.Empty;
        public NotificationType Type { get; set; }
        public bool AutoHide { get; set; } = true;
        public int DurationSeconds { get; set; } = 5;
    }
}