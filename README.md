# Postgres Outbox Pattern with CDC and .NET
PoC of doing Outbox Pattern with CDC and .NET

Uses [Postgres logical replication](https://www.postgresql.org/docs/current/logical-replication.html) with [Npgsql integration](https://www.npgsql.org/doc/replication.html).

Main logic is placed in [EventsSubscription](./PostgresOutbox/Subscriptions/EventsSubscription.cs). Check [LogicalReplicationTest](./PostgresOutboxPatternWithCDC.NET.Tests/LogicalReplicationTest.cs) for sample usage.

Read more details in:
- [Push-based Outbox Pattern with Postgres Logical Replication](https://event-driven.io/en/push_based_outbox_pattern_with_postgres_logical_replication/?utm_source=github_outbox_cdc).
- [How to get all messages through Postgres logical replication](https://event-driven.io/en/how_to_get_all_messages_through_postgres_logical_replication/?utm_source=github_outbox_cdc).

## Running

1. Start Postgres with WAL enabled from Docker image.
```shell
docker-compose up
```
2. Run tests
```shell
dotnet test
```

## Links

### WAL
- [Devrim Gündüz -WAL: Everything you want to know](https://www.youtube.com/watch?v=feTihjJJs3g)
- [Postgres Documentation - Write Ahead Log](https://www.postgresql.org/docs/13/runtime-config-wal.html)
- [The Internals of PostgreSQL - Write Ahead Logging — WAL](https://www.interdb.jp/pg/pgsql09.html)
- [Hevo - Working With Postgres WAL Made Easy](https://hevodata.com/learn/working-with-postgres-wal/)

### Logical Replication
- [Gunnar Morling - Open-source Change Data Capture With Debezium - video](https://www.youtube.com/watch?v=G7TvRzPQH-U)
- [Gunnar Morling - Open-source Change Data Capture With Debezium - slides](https://speakerdeck.com/gunnarmorling/open-source-change-data-capture-with-debezium?slide=21)
- [Several9s - Using PostgreSQL Logical Replication to Maintain an Always Up-to-Date Read/Write TEST Server](https://severalnines.com/blog/using-postgresql-logical-replication-maintain-always-date-readwrite-test-server/)
- [Matt Tanner - PostgreSQL CDC: A Comprehensive Guide](https://www.arcion.io/learn/postgresql-cdc)

### General Introduction
- [Dmitry Narizhnykh - PostgreSQL Change Data Capture and Golang Sample Code](https://hackernoon.com/postgresql-change-data-capture-and-golang-sample-code)
- [Fujistsu - How PostgreSQL 15 improved communication in logical replication](https://www.postgresql.fastware.com/blog/how-postgresql-15-improved-communication-in-logical-replication)
- [Kinsta - PostgreSQL Replication: A Comprehensive Guide](https://kinsta.com/blog/postgresql-replication/)
- [Amit Kapila  - Replication Improvements In PostgreSQL-14](https://amitkapila16.blogspot.com/2021/09/logical-replication-improvements-in.html)
- [Postgresql Wiki - Logical Decoding Plugins](https://wiki.postgresql.org/wiki/Logical_Decoding_Plugins)
- [Npgsql - Logical Replication](https://www.npgsql.org/doc/replication.html)
- [Konstantin Evteev - Recovery use cases for Logical Replication in PostgreSQL 10](https://medium.com/avitotech/recovery-use-cases-for-logical-replication-in-postgresql-10-a1e6bab03072)
- [EDB - PostgreSQL Write-Ahead Logging (WAL) Trade-offs: Bounded vs. Archived vs. Replication Slots](https://www.enterprisedb.com/blog/postgresql-wal-write-ahead-logging-management-strategy-tradeoffs)
- [Percona - The 1-2-3 for PostgreSQL Logical Replication Using an RDS Snapshot](https://www.percona.com/blog/postgresql-logical-replication-using-an-rds-snapshot/)
- [2nd Quadrant - Basics of Tuning Checkpoints](https://www.2ndquadrant.com/en/blog/basics-of-tuning-checkpoints/)
- [Thiago - How to use Change Data Capture (CDC) with Postgres](https://dev.to/thiagosilvaf/how-to-use-change-database-capture-cdc-in-postgres-37b8)
- [Ramesh naik E - Change Data Capture(CDC) in PostgreSQL](https://medium.com/@ramesh.esl/change-data-capture-cdc-in-postgresql-7dee2d467d1b)
- [Wal2Json - JSON output plugin for changeset extraction](https://github.com/eulerto/wal2json)
- [AWS Database Blog - Stream changes from Amazon RDS for PostgreSQL using Amazon Kinesis Data Streams and AWS Lambda](https://aws.amazon.com/blogs/database/stream-changes-from-amazon-rds-for-postgresql-using-amazon-kinesis-data-streams-and-aws-lambda/)
- [PGDeltaStream - Streaming Postgres logical replication changes atleast-once over websockets ](https://github.com/hasura/pgdeltastream)
- [Hrvoje Milković - Replicate PostreSQL data to Elasticsearch via Logical replication slots](http://staging.kraken.hr/blog/2018/postgresql-replication-elasticsearch)
- [Azure Database for PostgreSQL—Logical decoding and wal2json for change data capture](https://azure.microsoft.com/en-us/updates/azure-database-for-postgresql-logical-decoding-and-wal2json-for-change-data-capture/)
- [Postgresql To Kinesis For Java - Disney Streaming](https://github.com/disneystreaming/pg2k4j)

#### Performance
- [2ndQuadrant - Performance limits of logical replication solutions](https://www.2ndquadrant.com/en/blog/performance-limits-of-logical-replication-solutions/)

#### Snapshots
- [Christos Christoudias - Creating a Logical Replica from a Snapshot in RDS Postgres](https://tech.instacart.com/creating-a-logical-replica-from-a-snapshot-in-rds-postgres-886d9d2c7343)

#### Ordering
- [Virender Singla - Postgres — Logical Replication and long running transactions](https://virender-cse.medium.com/postgres-logical-replication-and-long-running-transactions-81a69b7ac470)

### Locks
- [Depesz - Picking task from queue – revisit](https://www.depesz.com/2016/05/04/picking-task-from-queue-revisit/)
- [Alvaro Herrera - Waiting for 9.5 – Implement SKIP LOCKED for row-level locks](https://www.depesz.com/2014/10/10/waiting-for-9-5-implement-skip-locked-for-row-level-locks/)
- [Chris Hanks - Turning PostgreSQL into a queue serving 10,000 jobs per second](https://gist.github.com/chanks/7585810)
- [Vlad Mihalcea - How do PostgreSQL advisory locks work](https://vladmihalcea.com/how-do-postgresql-advisory-locks-work/)
- [Marco Slot - When Postgres blocks: 7 tips for dealing with locks](https://www.citusdata.com/blog/2018/02/22/seven-tips-for-dealing-with-postgres-locks/)
- [Marco Slot - PostgreSQL rocks, except when it blocks: Understanding locks](https://www.citusdata.com/blog/2018/02/15/when-postgresql-blocks/)
- [Nickolay Ihalainen - PostgreSQL locking, Part 1: Row Locks](https://www.percona.com/blog/2018/10/16/postgresql-locking-part-1-row-locks/)
- [Nickolay Ihalainen - PostgreSQL locking, part 2: heavyweight locks](https://www.percona.com/blog/2018/10/24/postgresql-locking-part-2-heavyweight-locks/)
- [Nickolay Ihalainen - PostgreSQL locking, part 3: lightweight locks](https://www.percona.com/blog/2018/10/30/postgresql-locking-part-3-lightweight-locks/)

### Outbox implementations
- [Robert Kawecki - esdf2-eventstore-pg](https://github.com/rkaw92/esdf2-eventstore-pg/blob/a49f88cd1f10d4f06a12ef0982293a8c7abb4ff9/src/PostgresEventStore.ts#L116)
