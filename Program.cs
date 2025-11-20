using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public enum LogLevel
{
    Info,      
    Warning,    
    Error      
}

// типы событий в системе
public enum EventType
{
    ApplicationStart,
    ApplicationShutdown,
    RecordCreated,
    RecordUpdated,
    RecordDeleted,
    StatusChanged,
    DeadlineChanged,
    TechnicalAction,
    SystemError
}

public class LogEvent
{
    public Guid Id { get; set; }
    public LogLevel Level { get; set; }
    public EventType EventType { get; set; }
    public DateTime Timestamp { get; set; }
    public string User { get; set; }
    public string ObjectId { get; set; }
    public string Description { get; set; }

    public LogEvent(LogLevel level, EventType eventType, string user, string objectId, string description)
    {
        Id = Guid.NewGuid();
        Level = level;
        EventType = eventType;
        Timestamp = DateTime.Now;
        User = user ?? "System";
        ObjectId = objectId ?? string.Empty;
        Description = description ?? string.Empty;
    }

    // конструктор для создания структуры данных из строки
    public LogEvent(string dataLine)
    {
        var parts = dataLine.Split('|');
        if (parts.Length >= 7)
        {
            Id = Guid.Parse(parts[0]);
            Level = (LogLevel)Enum.Parse(typeof(LogLevel), parts[1]);
            EventType = (EventType)Enum.Parse(typeof(EventType), parts[2]);
            Timestamp = DateTime.Parse(parts[3]);
            User = parts[4];
            ObjectId = parts[5];
            Description = parts[6];
        }
    }

    public override string ToString()
    {
        return $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level}] {EventType} - User: {User}, Object: {ObjectId}, Description: {Description}";
    }

    // метод для сериализации в строку
    public string ToDataString()
    {
        return $"{Id}|{Level}|{EventType}|{Timestamp:O}|{User}|{ObjectId}|{Description}";
    }
}

public interface ILogStorage
{
    void SaveEvent(LogEvent logEvent);
    List<LogEvent> LoadEvents();
    List<LogEvent> GetEventsByPeriod(DateTime startDate, DateTime endDate);
}

public class FileStorage : ILogStorage
{
    private readonly string _filePath;

    public FileStorage(string filePath = "event_log.txt")
    {
        _filePath = filePath;
    }

    public void SaveEvent(LogEvent logEvent)
    {
        try
        {
            using (var writer = new StreamWriter(_filePath, true, Encoding.UTF8))
            {
                writer.WriteLine(logEvent.ToDataString());
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при сохранении события: {ex.Message}");
        }
    }

    public List<LogEvent> LoadEvents()
    {
        var events = new List<LogEvent>();

        if (!File.Exists(_filePath))
            return events;

        try
        {
            using (var reader = new StreamReader(_filePath, Encoding.UTF8))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        try
                        {
                            var logEvent = new LogEvent(line);
                            events.Add(logEvent);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Ошибка при чтении строки: {line}. {ex.Message}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при загрузке событий: {ex.Message}");
        }

        return events;
    }

    public List<LogEvent> GetEventsByPeriod(DateTime startDate, DateTime endDate)
    {
        var events = LoadEvents();
        return events.Where(e => e.Timestamp >= startDate && e.Timestamp <= endDate)
                    .OrderBy(e => e.Timestamp)
                    .ToList();
    }
}

public class ConsoleStorage : ILogStorage
{
    private readonly List<LogEvent> _events = new List<LogEvent>();

    public void SaveEvent(LogEvent logEvent)
    {
        _events.Add(logEvent);
        Console.WriteLine(logEvent.ToString());
    }

    public List<LogEvent> LoadEvents()
    {
        return new List<LogEvent>(_events);
    }

    public List<LogEvent> GetEventsByPeriod(DateTime startDate, DateTime endDate)
    {
        return _events.Where(e => e.Timestamp >= startDate && e.Timestamp <= endDate)
                     .OrderBy(e => e.Timestamp)
                     .ToList();
    }
}

public class Logger
{
    private readonly List<ILogStorage> _storages;
    private readonly List<LogEvent> _events;

    public Logger()
    {
        _storages = new List<ILogStorage>();
        _events = new List<LogEvent>();
    }

    // добавление способа хранения
    public void AddStorage(ILogStorage storage)
    {
        _storages.Add(storage);
    }

    // основной метод для логирования событий
    public void Log(LogLevel level, EventType eventType, string user, string objectId, string description)
    {
        var logEvent = new LogEvent(level, eventType, user, objectId, description);

        // сохранение события во всех хранилищах
        foreach (var storage in _storages)
        {
            storage.SaveEvent(logEvent);
        }

        _events.Add(logEvent);
    }

    // перегруженные методы для удобства
    public void LogInfo(EventType eventType, string user, string objectId, string description)
    {
        Log(LogLevel.Info, eventType, user, objectId, description);
    }

    public void LogWarning(EventType eventType, string user, string objectId, string description)
    {
        Log(LogLevel.Warning, eventType, user, objectId, description);
    }

    public void LogError(EventType eventType, string user, string objectId, string description)
    {
        Log(LogLevel.Error, eventType, user, objectId, description);
    }

    // получение событий за период
    public List<LogEvent> GetEventsByPeriod(DateTime startDate, DateTime endDate)
    {
        return _events.Where(e => e.Timestamp >= startDate && e.Timestamp <= endDate)
                     .OrderBy(e => e.Timestamp)
                     .ToList();
    }

    // загрузка событий из файлового хранилища
    public void LoadEventsFromFileStorage()
    {
        var fileStorage = _storages.OfType<FileStorage>().FirstOrDefault();
        if (fileStorage != null)
        {
            var loadedEvents = fileStorage.LoadEvents();
            _events.Clear();
            _events.AddRange(loadedEvents);
        }
    }

    // метод для поиска событий по пользователю
    public List<LogEvent> GetEventsByUser(string user)
    {
        return _events.Where(e => e.User.Equals(user, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(e => e.Timestamp)
                     .ToList();
    }

    // метод для поиска событий по типу
    public List<LogEvent> GetEventsByType(EventType eventType)
    {
        return _events.Where(e => e.EventType == eventType)
                     .OrderBy(e => e.Timestamp)
                     .ToList();
    }
}

class Program
{
    static void Main(string[] args)
    {
        // создание логгера с двумя способами хранения
        var logger = new Logger();
        logger.AddStorage(new FileStorage("system_events.txt"));
        logger.AddStorage(new ConsoleStorage());

        // загрузка предыдущих событий при запуске
        logger.LoadEventsFromFileStorage();

        Console.WriteLine("=== Система логирования событий ===\n");

        // логирование событий из разных систем организации -

        // система учёта заявок
        logger.LogInfo(EventType.RecordCreated, "Иванов А.П.", "Заявка-001",
            "Создана новая заявка на оборудование");

        logger.LogInfo(EventType.StatusChanged, "Петрова С.И.", "Заявка-001",
            "Статус изменён на 'В обработке'");

        // система бронирования помещений
        logger.LogInfo(EventType.RecordCreated, "Сидоров В.К.", "Бронирование-015",
            "Забронирован конференц-зал на 15:00");

        logger.LogWarning(EventType.DeadlineChanged, "Сидоров В.К.", "Бронирование-015",
            "Перенос встречи с 14:00 на 15:00");

        // система выдачи оборудования
        logger.LogError(EventType.SystemError, "System", "Оборудование-023",
            "Ошибка при попытке выдачи оборудования: устройство не найдено");

        // технические события
        logger.LogInfo(EventType.TechnicalAction, "System", "Database",
            "Выполнено резервное копирование данных");

        // дополнительные события для демонстрации
        logger.LogInfo(EventType.RecordUpdated, "Иванов А.П.", "Заявка-001",
            "Добавлено дополнительное оборудование в заявку");

        logger.LogInfo(EventType.RecordDeleted, "Петрова С.И.", "Заявка-005",
            "Заявка удалена по просьбе пользователя");

        // просмотр событий за сегодня
        Console.WriteLine("\n=== События за сегодня ===");
        var todayEvents = logger.GetEventsByPeriod(DateTime.Today, DateTime.Now);
        foreach (var eventItem in todayEvents)
        {
            Console.WriteLine(eventItem);
        }

        // просмотр событий по пользователю
        Console.WriteLine("\n=== События пользователя Иванов А.П. ===");
        var userEvents = logger.GetEventsByUser("Иванов А.П.");
        foreach (var eventItem in userEvents)
        {
            Console.WriteLine(eventItem);
        }

        // просмотр ошибок
        Console.WriteLine("\n=== Все ошибки в системе ===");
        var errorEvents = logger.GetEventsByType(EventType.SystemError);
        foreach (var eventItem in errorEvents)
        {
            Console.WriteLine(eventItem);
        }

        Console.WriteLine($"\nВсего записано событий: {todayEvents.Count}");
        Console.WriteLine("\nНажмите любую клавишу для выхода...");
        Console.ReadKey();
    }
}