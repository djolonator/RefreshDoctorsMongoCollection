using MongoDB.Driver;

namespace CreateDoctorsCollection
{
    public class Context
    {
        private readonly IMongoDatabase _database;

        public Context(string connectionString, string databaseName)
        {
            var client = new MongoClient(connectionString);
            _database = client.GetDatabase(databaseName);
        }

        public IMongoDatabase GetDatabase()
        {
            return _database;
        }

        public IMongoCollection<T> GetCollection<T>(string name)
        {
            return _database.GetCollection<T>(name);
        }
    }
}
