using Npgsql;
using PostgresOutbox.Database;

namespace PostgresOutbox.Subscriptions.Management;

using static PublicationManagement.SetupPublicationResult;

public static class PublicationManagement
{
    public static async Task<SetupPublicationResult> SetupPublication(
        this NpgsqlDataSource dataSource,
        PublicationSetupOptions options,
        CancellationToken ct
    )
    {
        var (publicationName, tableName, createStyle, shouldReAddTablesIfWereRecreated) = options;

        if (createStyle == CreateStyle.Never)
            return new None();

        if (createStyle == CreateStyle.AlwaysRecreate)
            await dataSource.DropPublication(publicationName, ct);

        if (createStyle == CreateStyle.WhenNotExists
            && await dataSource.PublicationExists(publicationName, ct))
        {
            if (shouldReAddTablesIfWereRecreated)
                await dataSource.RefreshPublicationTables(publicationName, tableName, ct);

            return new AlreadyExists();
        }

        await dataSource.CreatePublication(publicationName, tableName, ct);

        return new Created();
    }

    private static Task CreatePublication(
        this NpgsqlDataSource dataSource,
        string publicationName,
        string tableName,
        CancellationToken ct
    ) =>
        dataSource.Execute($"CREATE PUBLICATION {publicationName} FOR TABLE {tableName};", ct);

    private static Task DropPublication(
        this NpgsqlDataSource dataSource,
        string publicationName,
        CancellationToken ct
    ) =>
        dataSource.Execute($"DROP PUBLICATION IF EXISTS {publicationName};", ct);

    private static Task RefreshPublicationTables(
        this NpgsqlDataSource dataSource,
        string publicationName,
        string tableName,
        CancellationToken ct
    ) =>
        dataSource.Execute(
            $"""
             DO $$
             DECLARE
                 v_count int;
             BEGIN
                 SELECT COUNT(*)
                 INTO v_count
                 FROM pg_publication_tables
                 WHERE pubname = '{publicationName}'
                 AND tablename = '{tableName}';

                 IF v_count > 0 THEN
                     EXECUTE format('ALTER PUBLICATION %I DROP TABLE %I', 'your_publication_name', 'your_table_name');
                 END IF;
             END $$;
             ALTER PUBLICATION {publicationName} ADD TABLE {tableName};
             """, ct);

    private static Task<bool> PublicationExists(
        this NpgsqlDataSource dataSource,
        string publicationName,
        CancellationToken ct
    ) =>
        dataSource.Exists("pg_publication", "pubname = $1", new object[] { publicationName }, ct);

    public abstract record SetupPublicationResult
    {
        public record None: SetupPublicationResult;

        public record AlreadyExists: SetupPublicationResult;

        public record Created: SetupPublicationResult;
    }

    public record PublicationSetupOptions(
        string PublicationName,
        string TableName,
        CreateStyle CreateStyle = CreateStyle.WhenNotExists,
        bool ShouldReAddTablesIfWereRecreated = false
    );
}
