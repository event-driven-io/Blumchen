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
        var (publicationName, createStyle, shouldReAddTablesIfWereRecreated, typeResolver, tableDescription) = setupOptions;

        return createStyle switch
        {
            Subscription.CreateStyle.Never => new None(),
            Subscription.CreateStyle.AlwaysRecreate => await ReCreate(dataSource, publicationName, tableDescription.Name, typeResolver, ct).ConfigureAwait(false),
            Subscription.CreateStyle.WhenNotExists when await dataSource.PublicationExists(publicationName, ct).ConfigureAwait(false) => await Refresh(dataSource, publicationName, tableDescription.Name, shouldReAddTablesIfWereRecreated, ct).ConfigureAwait(false),
            Subscription.CreateStyle.WhenNotExists => await Create(dataSource, publicationName, tableDescription.Name, typeResolver, ct).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(setupOptions.CreateStyle))
        };

        static async Task<SetupPublicationResult> ReCreate(
            NpgsqlDataSource dataSource,
            string publicationName,
            string tableName,
            JsonTypeResolver? typeResolver,
            CancellationToken ct
        ) {
            await dataSource.DropPublication(publicationName, ct).ConfigureAwait(false);
            return await Create(dataSource, publicationName, tableName, typeResolver, ct).ConfigureAwait(false);
        }

        static async Task<SetupPublicationResult> Create(NpgsqlDataSource dataSource,
            string publicationName,
            string tableName,
            JsonTypeResolver? typeResolver,
            CancellationToken ct
        ) {
            await dataSource.CreatePublication(publicationName, tableName,
                typeResolver.Keys().ToHashSet(), ct).ConfigureAwait(false);
               
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

    internal static Task CreatePublication(
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
        Subscription.CreateStyle CreateStyle = Subscription.CreateStyle.WhenNotExists,
        bool ShouldReAddTablesIfWereRecreated = false
    )
    {
        internal const string DefaultPublicationName = "pub";
        internal JsonTypeResolver? TypeResolver { get; init; } = default;

        internal TableDescriptorBuilder.MessageTable TableDescriptor { get; init; } = new TableDescriptorBuilder().Build();

        internal void Deconstruct(
            out string publicationName,
            out Subscription.CreateStyle createStyle,
            out bool reAddTablesIfWereRecreated,
            out JsonTypeResolver? typeResolver,
            out TableDescriptorBuilder.MessageTable tableDescription)
        {
            publicationName = PublicationName;
            createStyle = Subscription.CreateStyle.WhenNotExists;
            reAddTablesIfWereRecreated = ShouldReAddTablesIfWereRecreated;
            typeResolver = TypeResolver;
            tableDescription = TableDescriptor;
        }

    }
}
