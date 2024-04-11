using CreateDoctorsCollection.Models;

namespace CreateDoctorsCollection.Repository
{
    public interface IDoctorsRepository
    {
        Task CreateDoctorsCollectionAsync(string collectionName);
        Task DropDoctorsCollectionAsync(string collectionName);
        Task AddDoctorsToCollectionAsync(List<StorageDoctorsModel> doctors);
    }
}
