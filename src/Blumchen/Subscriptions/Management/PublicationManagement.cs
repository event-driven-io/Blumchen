using Blumchen.Database;
using Blumchen.Serialization;
using Npgsql;

#pragma warning disable CA2208
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Blumchen.Subscriptions.Management;

using static PublicationManagement.SetupPublicationResult;

public static class PublicationManagement
{
    public static async Task<SetupPublicationResult> SetupPublication(
        this NpgsqlDataSource dataSource,
        PublicationSetupOptions setupOptions,
        CancellationToken ct
    )
    {
        var (publicationName, tableName, createStyle, shouldReAddTablesIfWereRecreated, typeResolver) = setupOptions;

        return createStyle switch
        {
            CreateStyle.Never => new None(),
            CreateStyle.AlwaysRecreate => await ReCreate(dataSource, publicationName, tableName, typeResolver, ct).ConfigureAwait(false),
            CreateStyle.WhenNotExists when await dataSource.PublicationExists(publicationName, ct).ConfigureAwait(false) => await Refresh(dataSource, publicationName, tableName, shouldReAddTablesIfWereRecreated, ct).ConfigureAwait(false),
            CreateStyle.WhenNotExists => await Create(dataSource, publicationName, tableName, typeResolver, ct).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(setupOptions.CreateStyle))
        };

        static async Task<SetupPublicationResult> ReCreate(
            NpgsqlDataSource dataSource,
            string publicationName,
            string tableName,
            ITypeResolver? typeResolver,
            CancellationToken ct
        ) {
            await dataSource.DropPublication(publicationName, ct).ConfigureAwait(false);
            return await Create(dataSource, publicationName, tableName, typeResolver, ct).ConfigureAwait(false);
        }

        static async Task<SetupPublicationResult> Create(NpgsqlDataSource dataSource,
            string publicationName,
            string tableName,
            ITypeResolver? typeResolver,
            CancellationToken ct
        ) {
            await dataSource.CreatePublication(publicationName, tableName,
                typeResolver?.RegisteredTypes ?? Enumerable.Empty<string>().ToHashSet(), ct).ConfigureAwait(false);
               
            return new Created();
        }

        static async Task<SetupPublicationResult> Refresh(NpgsqlDataSource dataSource,
            string publicationName,
            string tableName,
            bool shouldReAddTablesIfWereRecreated,
            CancellationToken ct
        ) {
            if(shouldReAddTablesIfWereRecreated)
                await dataSource.RefreshPublicationTables(publicationName, tableName, ct).ConfigureAwait(false);
            return new AlreadyExists();
        }
    }

    private static Task CreatePublication(
        this NpgsqlDataSource dataSource,
        string publicationName,
        string tableName,
        ISet<string> eventTypes,
        CancellationToken ct
    ) {
        return eventTypes.Count switch
        {
            0 => Execute(dataSource, $"CREATE PUBLICATION {publicationName} FOR TABLE {tableName} WITH (publish = 'insert');",
                ct
            ),
            _ => Execute(dataSource, $"CREATE PUBLICATION {publicationName} FOR TABLE {tableName} WHERE ({PublicationFilter(eventTypes)}) WITH (publish = 'insert');",
                ct
            )
        };
        static string PublicationFilter(ICollection<string> input) => string.Join(" OR ", input.Select(s => $"message_type = '{s}'"));
    }

    private static async Task Execute(
        this NpgsqlDataSource dataSource,
        string sql,
        CancellationToken ct
    )
    {
        var command = dataSource.CreateCommand(sql);
        await using (command.ConfigureAwait(false))
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

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
        string PublicationName = PublicationSetupOptions.DefaultPublicationName,
        string TableName = PublicationSetupOptions.DefaultTableName,
        CreateStyle CreateStyle = CreateStyle.WhenNotExists,
        bool ShouldReAddTablesIfWereRecreated = false
    )
    {
        internal const string DefaultTableName = "outbox";
        internal const string DefaultPublicationName = "pub";
        public ITypeResolver? TypeResolver { get; internal init; } = default;

        public void Deconstruct(
            out string publicationName,
            out string tableName,
            out CreateStyle createStyle,
            out bool reAddTablesIfWereRecreated,
            out ITypeResolver? typeResolver)
        {
            publicationName = PublicationName;
            tableName = TableName;
            createStyle = CreateStyle.WhenNotExists;
            reAddTablesIfWereRecreated = ShouldReAddTablesIfWereRecreated;
            typeResolver = TypeResolver;
        }
    }
}
