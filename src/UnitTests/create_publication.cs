using static Blumchen.Subscriptions.Management.PublicationManagement;

namespace UnitTests
{
    public class create_publication
    {
        [Fact]
        public void with_no_publication_filter()
        {
            var publicationName = "publicationName";
            var tableName = "tableName";
            var sql = $"CREATE PUBLICATION \"{publicationName}\" FOR TABLE {tableName}  WITH (publish = 'insert');";
            Assert.Equal(sql, CreatePublication(publicationName, tableName, new HashSet<string>()));
        }

        [Fact]
        public void with_single_publication_filter()
        {
            const string publicationName = "publicationName";
            const string tableName = "tableName";
            const string messageType = "messageType";
            var sql = $"CREATE PUBLICATION \"{publicationName}\" FOR TABLE {tableName} WHERE (message_type = '{messageType}') WITH (publish = 'insert');";
            Assert.Equal(sql, CreatePublication(publicationName, tableName, new HashSet<string> { messageType }));
        }

        [Fact]
        public void with_multiple_publication_filters()
        {
            const string publicationName = "publicationName";
            const string tableName = "tableName";
            const string messageType1 = "messageType1";
            const string messageType2 = "messageType2";
            var sql = $"CREATE PUBLICATION \"{publicationName}\" FOR TABLE {tableName} WHERE (message_type = '{messageType1}' OR message_type = '{messageType2}') WITH (publish = 'insert');";
            Assert.Equal(sql, CreatePublication(publicationName, tableName, new HashSet<string> { messageType1, messageType2 }));
        }
    }
}
