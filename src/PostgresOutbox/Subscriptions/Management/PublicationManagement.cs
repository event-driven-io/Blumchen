using Npgsql;
using PostgresOutbox.Database;
#pragma warning disable CA2208

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

        return createStyle switch
        {
            CreateStyle.Never => new None(),
            CreateStyle.AlwaysRecreate => await ReCreate(dataSource, publicationName, tableName, ct),
            CreateStyle.WhenNotExists when await dataSource.PublicationExists(publicationName, ct) => await Refresh(dataSource, publicationName, tableName, shouldReAddTablesIfWereRecreated, ct),
            CreateStyle.WhenNotExists => await Create(dataSource, publicationName, tableName, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(options.CreateStyle))
        };

        static async Task<SetupPublicationResult> ReCreate(
            NpgsqlDataSource dataSource,
            string publicationName,
            string tableName, CancellationToken ct)
        {
            await dataSource.DropPublication(publicationName, ct);
            return await Create(dataSource, publicationName, tableName, ct);
        }

        static async Task<SetupPublicationResult> Create(
            NpgsqlDataSource dataSource,
            string publicationName,
            string tableName, CancellationToken ct)
        {
            await dataSource.CreatePublication(publicationName, tableName, ct);
            return new Created();
        }

        static async Task<SetupPublicationResult> Refresh(NpgsqlDataSource dataSource,
            string publicationName,
            string tableName,
            bool shouldReAddTablesIfWereRecreated,
            CancellationToken ct)
        {
            if(shouldReAddTablesIfWereRecreated)
                await dataSource.RefreshPublicationTables(publicationName, tableName, ct);
            return new AlreadyExists();
        }
    }

    private static Task CreatePublication(
        this NpgsqlDataSource dataSource,
        string publicationName,
        string tableName,
        CancellationToken ct
    ) =>
        dataSource.Execute($"CREATE PUBLICATION {publicationName} FOR TABLE {tableName} WITH (publish = 'insert');", ct);

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
                     EXECUTE format('ALTER PUBLICATION %I DROP TABLE %I', '{publicationName}', '{tableName}');
                 END IF;
             END $$;
             ALTER PUBLICATION {publicationName} ADD TABLE {tableName};
             """, ct);

    private static Task<bool> PublicationExists(
        this NpgsqlDataSource dataSource,
        string publicationName,
        CancellationToken ct
    ) =>
        dataSource.Exists("pg_publication", "pubname = $1", [publicationName], ct);

    public abstract record SetupPublicationResult
    {
        public record None: SetupPublicationResult;

        public record AlreadyExists: SetupPublicationResult;

        public record Created: SetupPublicationResult;
    }

    public sealed record PublicationSetupOptions(
        string PublicationName = "pub",
        string TableName = PublicationSetupOptions.DefaultTableName,
        CreateStyle CreateStyle = CreateStyle.WhenNotExists,
        bool ShouldReAddTablesIfWereRecreated = false
    )
    {
        internal const string DefaultTableName = "outbox";
    }
}
