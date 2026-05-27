using DiplomMurtazin.Core;
using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace DiplomMurtazin.ViewModel
{
    public class NotificationItem
    {
        public DateTime EventDate { get; set; }
        public string UserLogin { get; set; }
        public string ActionType { get; set; }
        public string EntityType { get; set; }
        public string Details { get; set; }
    }

    public class NotificationsViewModel : BaseViewModel
    {
        private const int PollingSeconds = 5;
        private readonly DispatcherTimer _pollTimer;
        private int _lastSeenAuditId;
        private string _statusMessage = "Ожидание событий...";
        private string _statusColor = "#3498db";

        public ObservableCollection<NotificationItem> Items { get; } = new ObservableCollection<NotificationItem>();

        public string StatusMessage
        {
            get => _statusMessage;
            set => Set(ref _statusMessage, value);
        }

        public string StatusColor
        {
            get => _statusColor;
            set => Set(ref _statusColor, value);
        }

        public ICommand StartCommand { get; }
        public ICommand RefreshCommand { get; }

        public NotificationsViewModel()
        {
            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(PollingSeconds) };
            _pollTimer.Tick += (_, __) => PollForExternalChanges();

            StartCommand = new RelayCommand(_ => StartPolling());
            RefreshCommand = new RelayCommand(_ => PollForExternalChanges());
        }

        private void StartPolling()
        {
            InitializeCursor();
            PollForExternalChanges();
            _pollTimer.Start();
        }

        private void InitializeCursor()
        {
            try
            {
                using (var context = new KPMurtazinEntities())
                {
                    _lastSeenAuditId = context.Database.SqlQuery<int>("SELECT ISNULL(MAX(AuditID), 0) FROM dbo.AuditLog").FirstOrDefault();
                }
            }
            catch
            {
                _lastSeenAuditId = 0;
            }
        }

        private void PollForExternalChanges()
        {
            try
            {
                using (var context = new KPMurtazinEntities())
                {
                    const string sql = @"
SELECT TOP 100 AuditID, EventDate, UserLogin, ActionType, EntityType, Details
FROM dbo.AuditLog
WHERE AuditID > @lastAuditId
  AND (UserLogin IS NULL OR UserLogin <> @currentUser)
ORDER BY AuditID DESC";

                    var currentLogin = App.CurrentUser?.Login ?? string.Empty;
                    var newRows = context.Database.SqlQuery<AuditRow>(
                        sql,
                        new SqlParameter("@lastAuditId", _lastSeenAuditId),
                        new SqlParameter("@currentUser", currentLogin)
                    ).ToList();

                    var maxId = context.Database.SqlQuery<int>("SELECT ISNULL(MAX(AuditID), 0) FROM dbo.AuditLog").FirstOrDefault();
                    _lastSeenAuditId = Math.Max(_lastSeenAuditId, maxId);

                    if (newRows.Any())
                    {
                        foreach (var row in newRows.OrderByDescending(x => x.AuditID))
                        {
                            Items.Insert(0, new NotificationItem
                            {
                                EventDate = row.EventDate,
                                UserLogin = row.UserLogin,
                                ActionType = row.ActionType,
                                EntityType = row.EntityType,
                                Details = row.Details
                            });
                        }

                        while (Items.Count > 300)
                        {
                            Items.RemoveAt(Items.Count - 1);
                        }

                        DataRefreshBus.PublishExternalChanges(newRows.Count);
                        StatusMessage = $"Новых внешних событий: {newRows.Count}";
                        StatusColor = "#27ae60";
                    }
                    else
                    {
                        StatusMessage = $"Новых событий нет. Проверка каждые {PollingSeconds} сек.";
                        StatusColor = "#3498db";
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка чтения уведомлений: {ex.Message}";
                StatusColor = "#e74c3c";
            }
        }

        private class AuditRow
        {
            public int AuditID { get; set; }
            public DateTime EventDate { get; set; }
            public string UserLogin { get; set; }
            public string ActionType { get; set; }
            public string EntityType { get; set; }
            public string Details { get; set; }
        }
    }
}
