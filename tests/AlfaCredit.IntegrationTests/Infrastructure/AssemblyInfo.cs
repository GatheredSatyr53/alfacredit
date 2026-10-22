// Стенды поднимают хосты API в одном процессе. Serilog держит bootstrap-логгер в статическом Log.Logger,
// поэтому параллельная сборка двух хостов заканчивается ошибкой «The logger is already frozen».
// Коллекции выполняются последовательно: сначала основной стенд, затем стенд с внешним скорингом.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
