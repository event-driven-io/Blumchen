using Testcontainers.PostgreSql;

namespace Testing.PostgreSql
{
    public class Container
    {
        private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
            .WithCommand("-c", "wal_level=logical").Build();

        public Task StartAsync()
        {
            return _container.StartAsync();
        }

        public Task StopAsync()
        {
            return _container.StopAsync();
        }

    }
}
