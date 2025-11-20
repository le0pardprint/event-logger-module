using System.Diagnostics;

namespace TestProject1
{
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {
            var logger = new Logger();
            var fileStorage = new FileStorage("tc001_test.log");
            var consoleStorage = new ConsoleStorage();

            logger.AddStorage(fileStorage);
            logger.AddStorage(consoleStorage);

            logger.LogInfo(EventType.RecordCreated, "Иванов А.П.", "Заявка-001", "Создана новая заявка");

            // проверка записи в файл
            Assert.True(File.Exists("tc001_test.log"));
            var fileContent = File.ReadAllText("tc001_test.log");
            Assert.Contains("Иванов А.П.", fileContent);
            Assert.Contains("Заявка-001", fileContent);
            Assert.Contains("RecordCreated", fileContent);

            // проверка наличия в памяти
            var events = logger.GetEventsByPeriod(DateTime.Today, DateTime.Now);
            Assert.Single(events);
            var logEvent = events[0];

            Assert.NotEqual(Guid.Empty, logEvent.Id);
            Assert.Equal(LogLevel.Info, logEvent.Level);
            Assert.Equal(EventType.RecordCreated, logEvent.EventType);
            Assert.Equal("Иванов А.П.", logEvent.User);
            Assert.Equal("Заявка-001", logEvent.ObjectId);
            Assert.Equal("Создана новая заявка", logEvent.Description);

            File.Delete("tc001_test.log");
        }

        [Fact]
        public void TC002_CreateErrorEvent()
        {
            var logger = new Logger();
            var fileStorage = new FileStorage("tc002_test.log");
            var consoleStorage = new ConsoleStorage();

            logger.AddStorage(fileStorage);
            logger.AddStorage(consoleStorage);

            logger.LogError(EventType.SystemError, "System", "Оборудование-023", "Ошибка выдачи оборудования");

            // проверка данных события
            var events = logger.GetEventsByPeriod(DateTime.Today, DateTime.Now);
            Assert.Single(events);
            var logEvent = events[0];

            Assert.Equal(LogLevel.Error, logEvent.Level);
            Assert.Equal(EventType.SystemError, logEvent.EventType);
            Assert.Equal("System", logEvent.User);
            Assert.Equal("Оборудование-023", logEvent.ObjectId);
            Assert.Equal("Ошибка выдачи оборудования", logEvent.Description);

            // проверка сохранения в файл
            Assert.True(File.Exists("tc002_test.log"));
            var fileContent = File.ReadAllText("tc002_test.log");
            Assert.Contains("Error", fileContent);
            Assert.Contains("SystemError", fileContent);

            File.Delete("tc002_test.log");
        }

        [Fact]
        public void TC003_FilterEventsByPeriod()
        {
            var logger = new Logger();
            logger.AddStorage(new ConsoleStorage());

            // создание событий с разными временными метками
            logger.LogInfo(EventType.RecordCreated, "User1", "Object1", "Old event");

            // имитация задержки для создания события в другом временном промежутке
            System.Threading.Thread.Sleep(100);
            var startTime = DateTime.Now;

            logger.LogInfo(EventType.StatusChanged, "User2", "Object2", "Recent event 1");
            logger.LogInfo(EventType.RecordUpdated, "User3", "Object3", "Recent event 2");

            var endTime = DateTime.Now;

            var recentEvents = logger.GetEventsByPeriod(startTime, endTime);

            Assert.Equal(2, recentEvents.Count);
            Assert.All(recentEvents, e =>
            {
                Assert.True(e.Timestamp >= startTime && e.Timestamp <= endTime);
            });

            // проверка сортировки по времени
            for (int i = 0; i < recentEvents.Count - 1; i++)
            {
                Assert.True(recentEvents[i].Timestamp <= recentEvents[i + 1].Timestamp);
            }
        }

        [Fact]
        public void TC004_SearchEventsByUser()
        {
            var logger = new Logger();
            logger.AddStorage(new ConsoleStorage());

            // создание событий от разных пользователей
            logger.LogInfo(EventType.RecordCreated, "Иванов А.П.", "Заявка-001", "Заявка 1");
            logger.LogInfo(EventType.StatusChanged, "Петрова С.И.", "Заявка-002", "Заявка 2");
            logger.LogInfo(EventType.RecordCreated, "Иванов А.П.", "Заявка-003", "Заявка 3");
            logger.LogInfo(EventType.RecordDeleted, "Сидоров В.К.", "Заявка-004", "Заявка 4");

            // поиск с разным регистром
            var ivanovEvents1 = logger.GetEventsByUser("Иванов А.П.");
            var ivanovEvents2 = logger.GetEventsByUser("иванов а.п."); // нижний регистр

            Assert.Equal(2, ivanovEvents1.Count);
            Assert.Equal(2, ivanovEvents2.Count); // регистр не должен влиять

            Assert.All(ivanovEvents1, e => Assert.Equal("Иванов А.П.", e.User));
            Assert.All(ivanovEvents2, e => Assert.Equal("Иванов А.П.", e.User));

            // проверка что найдены правильные события
            var objectIds = ivanovEvents1.Select(e => e.ObjectId).ToList();
            Assert.Contains("Заявка-001", objectIds);
            Assert.Contains("Заявка-003", objectIds);
            Assert.DoesNotContain("Заявка-002", objectIds);
            Assert.DoesNotContain("Заявка-004", objectIds);
        }

        [Fact]
        public void TC005_FilterEventsByType()
        {
            var logger = new Logger();
            logger.AddStorage(new ConsoleStorage());

            // создание событий различных типов
            logger.LogInfo(EventType.RecordCreated, "User1", "Object1", "Создание 1");
            logger.LogInfo(EventType.StatusChanged, "User1", "Object1", "Изменение статуса 1");
            logger.LogInfo(EventType.RecordCreated, "User2", "Object2", "Создание 2");
            logger.LogInfo(EventType.StatusChanged, "User2", "Object2", "Изменение статуса 2");
            logger.LogInfo(EventType.RecordDeleted, "User3", "Object3", "Удаление");

            var statusChangedEvents = logger.GetEventsByType(EventType.StatusChanged);

            Assert.Equal(2, statusChangedEvents.Count);
            Assert.All(statusChangedEvents, e => Assert.Equal(EventType.StatusChanged, e.EventType));

            // проверка конкретных событий
            var descriptions = statusChangedEvents.Select(e => e.Description).ToList();
            Assert.Contains("Изменение статуса 1", descriptions);
            Assert.Contains("Изменение статуса 2", descriptions);
            Assert.DoesNotContain("Создание 1", descriptions);
            Assert.DoesNotContain("Удаление", descriptions);
        }

        [Fact]
        public void TC006_HandleNullAndEmptyValues()
        {
            var logger = new Logger();
            logger.AddStorage(new ConsoleStorage());

            // вызов с null и пустыми значениями
            logger.Log(LogLevel.Info, EventType.RecordCreated, null, "", null);
            logger.Log(LogLevel.Warning, EventType.StatusChanged, "", null, "");

            var events = logger.GetEventsByPeriod(DateTime.Today, DateTime.Now);
            Assert.Equal(2, events.Count);

            // проверка значений по умолчанию для первого события
            var firstEvent = events[0];
            Assert.Equal("System", firstEvent.User);
            Assert.Equal("", firstEvent.ObjectId);
            Assert.Equal("", firstEvent.Description);

            // проверка значений по умолчанию для второго события
            var secondEvent = events[1];
            Assert.Equal("", secondEvent.User);
            Assert.Equal("", secondEvent.ObjectId);
            Assert.Equal("", secondEvent.Description);

            // проверка что исключений не возникло
            Assert.All(events, e =>
            {
                Assert.NotNull(e.User);
                Assert.NotNull(e.ObjectId);
                Assert.NotNull(e.Description);
            });
        }

        [Fact]
        public void TC007_HandleCorruptedLogFile()
        {
            var filePath = "tc007_corrupted.log";

            // создание файла с поврежденными данными
            File.WriteAllLines(filePath, new[] {
            "invalid_data",
            "123|broken|record",
            "a1b2c3d4|Info|RecordCreated|invalid_date|User|Object|Description",
            "|||2024-01-15T10:30:00|||",
            "correct|Info|RecordCreated|2024-01-15T10:30:00.0000000+03:00|User|Object|Description" // корректная запись
        });

            var fileStorage = new FileStorage(filePath);

            var originalOut = Console.Out;
            using var stringWriter = new StringWriter();
            Console.SetOut(stringWriter);

            var events = fileStorage.LoadEvents();

            Console.SetOut(originalOut);
            var consoleOutput = stringWriter.ToString();

            Assert.Single(events); // только одна корректная запись должна загрузиться
            Assert.Equal("correct", events[0].Id.ToString().Substring(0, 7));

            // проверка что ошибки были залогированы в консоль
            Assert.Contains("Ошибка", consoleOutput);
            Assert.Contains("некорректный формат", consoleOutput.ToLower());

            File.Delete(filePath);
        }

        [Fact]
        public void TC008_SimulateRequestManagementSystem()
        {
            var logger = new Logger();
            logger.AddStorage(new ConsoleStorage());

            // имитация работы системы учета заявок
            // 1. создание заявки
            logger.LogInfo(EventType.RecordCreated, "Иванов А.П.", "Заявка-001",
                "Создана заявка на ноутбук Dell Latitude");

            // 2. изменение статуса заявки
            logger.LogInfo(EventType.StatusChanged, "Петрова С.И.", "Заявка-001",
                "Статус изменен на 'В обработке'");

            // 3. перенос срока выполнения
            logger.LogWarning(EventType.DeadlineChanged, "Иванов А.П.", "Заявка-001",
                "Срок выполнения перенесен на 2 дня");

            // 4. удаление заявки
            logger.LogInfo(EventType.RecordDeleted, "Сидоров В.К.", "Заявка-001",
                "Заявка удалена по причине дублирования");

            var events = logger.GetEventsByPeriod(DateTime.Today, DateTime.Now);
            Assert.Equal(4, events.Count);

            // проверка типов событий
            var eventTypes = events.Select(e => e.EventType).ToList();
            Assert.Contains(EventType.RecordCreated, eventTypes);
            Assert.Contains(EventType.StatusChanged, eventTypes);
            Assert.Contains(EventType.DeadlineChanged, eventTypes);
            Assert.Contains(EventType.RecordDeleted, eventTypes);

            // проверка что все события относятся к одной заявке
            Assert.All(events, e => Assert.Equal("Заявка-001", e.ObjectId));

            // проверка пользователей
            var users = events.Select(e => e.User).ToList();
            Assert.Contains("Иванов А.П.", users);
            Assert.Contains("Петрова С.И.", users);
            Assert.Contains("Сидоров В.К.", users);
        }

        [Fact]
        public void TC009_SimulateRoomBookingSystem()
        {
            var logger = new Logger();
            logger.AddStorage(new ConsoleStorage());

            // имитация работы системы бронирования помещений
            // 1. бронирование помещения
            logger.LogInfo(EventType.RecordCreated, "Сидоров В.К.", "Бронирование-015",
                "Забронирован конференц-зал А с 10:00 до 12:00");

            // 2. изменение времени бронирования
            logger.LogWarning(EventType.DeadlineChanged, "Сидоров В.К.", "Бронирование-015",
                "Время бронирования изменено: с 10:00-12:00 на 14:00-16:00");

            // 3. отмена бронирования
            logger.LogInfo(EventType.RecordDeleted, "Сидоров В.К.", "Бронирование-015",
                "Бронирование отменено по просьбе пользователя");

            var bookingEvents = logger.GetEventsByType(EventType.RecordCreated)
                                    .Concat(logger.GetEventsByType(EventType.DeadlineChanged))
                                    .Concat(logger.GetEventsByType(EventType.RecordDeleted))
                                    .Where(e => e.ObjectId == "Бронирование-015")
                                    .ToList();

            Assert.Equal(3, bookingEvents.Count);

            // проверка описаний событий
            var descriptions = bookingEvents.Select(e => e.Description).ToList();
            Assert.Contains("Забронирован конференц-зал А", descriptions);
            Assert.Contains("Время бронирования изменено", descriptions);
            Assert.Contains("Бронирование отменено", descriptions);

            // проверка что все события от одного пользователя
            Assert.All(bookingEvents, e => Assert.Equal("Сидоров В.К.", e.User));
        }

        [Fact]
        public void TC010_SimulateEquipmentIssuanceSystem()
        {
            var logger = new Logger();
            logger.AddStorage(new ConsoleStorage());

            // имитация работы системы выдачи оборудования
            // 1. выдача оборудования
            logger.LogInfo(EventType.RecordCreated, "Петрова С.И.", "Оборудование-023",
                "Выдан ноутбук Lenovo ThinkPad сотруднику Иванову А.П.");

            // 2. возврат оборудования
            logger.LogInfo(EventType.StatusChanged, "Иванов А.П.", "Оборудование-023",
                "Оборудование возвращено, состояние удовлетворительное");

            // 3. ошибка выдачи
            logger.LogError(EventType.SystemError, "System", "Оборудование-023",
                "Ошибка при попытке выдачи: оборудование находится в ремонте");

            var equipmentEvents = logger.GetEventsByPeriod(DateTime.Today, DateTime.Now)
                                      .Where(e => e.ObjectId == "Оборудование-023")
                                      .ToList();

            Assert.Equal(3, equipmentEvents.Count);

            // проверка уровней важности
            var levels = equipmentEvents.Select(e => e.Level).ToList();
            Assert.Equal(2, levels.Count(l => l == LogLevel.Info));
            Assert.Equal(1, levels.Count(l => l == LogLevel.Error));

            // проверка типов событий
            var eventTypes = equipmentEvents.Select(e => e.EventType).ToList();
            Assert.Contains(EventType.RecordCreated, eventTypes);
            Assert.Contains(EventType.StatusChanged, eventTypes);
            Assert.Contains(EventType.SystemError, eventTypes);

            // проверка содержания описаний
            var errorEvent = equipmentEvents.First(e => e.Level == LogLevel.Error);
            Assert.Contains("ремонте", errorEvent.Description);
        }

        [Fact]
        public void TC011_PerformanceMassEventCreation()
        {
            var logger = new Logger();
            var fileStorage = new FileStorage("tc011_performance.log");
            logger.AddStorage(fileStorage);

            var stopwatch = new Stopwatch();
            const int eventCount = 1000;

            stopwatch.Start();

            for (int i = 0; i < eventCount; i++)
            {
                var level = i % 3 == 0 ? LogLevel.Info :
                           i % 3 == 1 ? LogLevel.Warning : LogLevel.Error;

                var eventType = i % 4 == 0 ? EventType.RecordCreated :
                              i % 4 == 1 ? EventType.StatusChanged :
                              i % 4 == 2 ? EventType.DeadlineChanged : EventType.RecordDeleted;

                logger.Log(level, eventType, $"User{i % 10}", $"Object{i}", $"Test event {i}");
            }

            stopwatch.Stop();

            var totalTime = stopwatch.ElapsedMilliseconds;
            Assert.True(totalTime < 5000, $"Время выполнения: {totalTime} мс, ожидалось < 5000 мс");

            // проверка, что все события сохранены
            var events = logger.GetEventsByPeriod(DateTime.Today, DateTime.Now);
            Assert.Equal(eventCount, events.Count);

            // дополнительная проверка - загрузка из файла
            var loadedEvents = fileStorage.LoadEvents();
            Assert.Equal(eventCount, loadedEvents.Count);

            File.Delete("tc011_performance.log");
        }
    }
}