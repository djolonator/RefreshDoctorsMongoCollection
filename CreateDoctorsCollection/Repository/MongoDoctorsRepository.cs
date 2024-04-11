
using CreateDoctorsCollection.Models;



namespace CreateDoctorsCollection.Repository
{
    public class MongoDoctorsRepository: IDoctorsRepository
    {
        private readonly Context _context;
        private readonly string _connString = "";
        private readonly string _databaseName = "RfzoData";
        public MongoDoctorsRepository()
        {
            _context = new Context(_connString, _databaseName);
        }

        public async Task CreateDoctorsCollectionAsync(string collectionName)
        {
            var database = _context.GetDatabase();
            await database.CreateCollectionAsync(collectionName);
        }

        public async Task DropDoctorsCollectionAsync(string collectionName)
        {
            var database = _context.GetDatabase();
            await database.DropCollectionAsync(collectionName);
        }

        public async Task AddDoctorsToCollectionAsync(List<StorageDoctorsModel> doctors)
        {
            var doctorsCollection = _context.GetCollection<StorageDoctorsModel>("Doctors");
            await doctorsCollection.InsertManyAsync(doctors);
        }
    }
}
